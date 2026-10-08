using System.Globalization;
using System.Runtime.CompilerServices;
using FBOutletConditionsHousekeeping.Core.Configuration;
using FBOutletConditionsHousekeeping.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace FBOutletConditionsHousekeeping.Core.Services;

/// <summary>
/// Reads and deletes SharePoint "Report Log" list items via the Microsoft
/// Graph SDK. Field names are taken from <see cref="SharePointOptions"/>
/// rather than hardcoded, because the real list schema is tenant/site
/// specific (see docs/SCHEMA.md).
/// </summary>
public sealed class GraphSharePointReportLogService : ISharePointReportLogService
{
    private readonly GraphServiceClient _graphClient;
    private readonly SharePointOptions _sharePointOptions;
    private readonly CleanupOptions _cleanupOptions;
    private readonly ILogger<GraphSharePointReportLogService> _logger;

    public GraphSharePointReportLogService(
        GraphServiceClient graphClient,
        IOptions<SharePointOptions> sharePointOptions,
        IOptions<CleanupOptions> cleanupOptions,
        ILogger<GraphSharePointReportLogService> logger)
    {
        _graphClient = graphClient;
        _sharePointOptions = sharePointOptions.Value;
        _cleanupOptions = cleanupOptions.Value;
        _logger = logger;
    }

    public async IAsyncEnumerable<ReportLogEntry> GetAllEntriesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var imageFieldNames = _sharePointOptions.GetImageUrlFieldNames();
        var dateFieldName = _sharePointOptions.DateFieldInternalName;

        var response = await _graphClient.Sites[_sharePointOptions.SiteId]
            .Lists[_sharePointOptions.ListId]
            .Items
            .GetAsync(requestConfiguration =>
            {
                requestConfiguration.QueryParameters.Expand = new[] { "fields" };
                requestConfiguration.QueryParameters.Top = _cleanupOptions.MaxPageSize;
            }, cancellationToken)
            .ConfigureAwait(false);

        // Pages are walked manually via the Graph SDK's OdataNextLink rather
        // than PageIterator, so results can be yielded lazily as an
        // IAsyncEnumerable without buffering the entire list in memory.
        while (response?.Value is not null)
        {
            foreach (var item in response.Value)
            {
                yield return MapToEntry(item, dateFieldName, imageFieldNames);
            }

            if (string.IsNullOrEmpty(response.OdataNextLink))
            {
                yield break;
            }

            response = await _graphClient.Sites[_sharePointOptions.SiteId]
                .Lists[_sharePointOptions.ListId]
                .Items
                .WithUrl(response.OdataNextLink)
                .GetAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task DeleteEntryAsync(string entryId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting SharePoint Report Log item {ItemId}.", entryId);

        await _graphClient.Sites[_sharePointOptions.SiteId]
            .Lists[_sharePointOptions.ListId]
            .Items[entryId]
            .DeleteAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private ReportLogEntry MapToEntry(ListItem item, string dateFieldName, IReadOnlyList<string> imageFieldNames)
    {
        var rawFields = item.Fields?.AdditionalData;

        DateTimeOffset? ageFieldValue = null;
        if (rawFields is not null && rawFields.TryGetValue(dateFieldName, out var rawDate))
        {
            ageFieldValue = TryParseDate(rawDate);
            if (ageFieldValue is null)
            {
                _logger.LogWarning(
                    "Report Log item {ItemId} has an unparseable value in date field '{DateField}'; it will be skipped.",
                    item.Id,
                    dateFieldName);
            }
        }

        var imageUrls = new List<string>(imageFieldNames.Count);
        if (rawFields is not null)
        {
            foreach (var fieldName in imageFieldNames)
            {
                if (rawFields.TryGetValue(fieldName, out var rawUrl)
                    && rawUrl is string url
                    && !string.IsNullOrWhiteSpace(url))
                {
                    imageUrls.Add(url);
                }
            }
        }

        return new ReportLogEntry(item.Id ?? string.Empty, ageFieldValue, imageUrls);
    }

    private static DateTimeOffset? TryParseDate(object? rawValue) => rawValue switch
    {
        null => null,
        DateTimeOffset dto => dto,
        DateTime dt => new DateTimeOffset(dt.ToUniversalTime(), TimeSpan.Zero),
        string s when DateTimeOffset.TryParse(
            s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) => parsed,
        _ => null,
    };
}
