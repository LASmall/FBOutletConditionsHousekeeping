using FBOutletConditionsHousekeeping.Core.Configuration;
using FBOutletConditionsHousekeeping.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FBOutletConditionsHousekeeping.Core.Services;

/// <summary>
/// Implements the full cleanup business workflow described in
/// docs/SPECIFICATION.md US-001: identifies eligible Report Log entries,
/// enforces the Cloudinary-before-SharePoint ordering rule (BR-001), honors
/// DryRun (BR-003), isolates per-entry failures (BR-002), and sends the
/// summary/failure notification email.
/// </summary>
public sealed class CleanupOrchestrator : ICleanupOrchestrator
{
    private readonly ISharePointReportLogService _sharePointService;
    private readonly ICloudinaryImageService _cloudinaryService;
    private readonly IEmailNotificationService _emailService;
    private readonly CleanupOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CleanupOrchestrator> _logger;

    public CleanupOrchestrator(
        ISharePointReportLogService sharePointService,
        ICloudinaryImageService cloudinaryService,
        IEmailNotificationService emailService,
        IOptions<CleanupOptions> options,
        TimeProvider timeProvider,
        ILogger<CleanupOrchestrator> logger)
    {
        _sharePointService = sharePointService;
        _cloudinaryService = cloudinaryService;
        _emailService = emailService;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<CleanupSummary> RunAsync(CancellationToken cancellationToken)
    {
        var runStartedAtUtc = _timeProvider.GetUtcNow();

        try
        {
            var summary = await ExecuteRunAsync(runStartedAtUtc, cancellationToken).ConfigureAwait(false);

            // AC-007/AC-009: the email service itself honors EmailEnabled,
            // so this call can be made unconditionally.
            await _emailService.SendSummaryAsync(summary, cancellationToken).ConfigureAwait(false);

            return summary;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // ERR-001: a run-level failure (e.g. SharePoint/Graph
            // authentication failure) aborts the whole run. Notify (AC-008)
            // and rethrow so the Azure Functions host marks the invocation
            // as failed and it is visible in monitoring.
            _logger.LogError(ex, "Report Log cleanup run failed.");
            await _emailService.SendFailureAsync(ex, runStartedAtUtc, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<CleanupSummary> ExecuteRunAsync(DateTimeOffset runStartedAtUtc, CancellationToken cancellationToken)
    {
        // BR-004: eligibility is driven entirely by the configured date
        // field compared against a day-count cutoff, not by item ID/order.
        var cutoff = runStartedAtUtc - TimeSpan.FromDays(_options.EntryAgeDays);

        var results = new List<CleanupItemResult>();
        var scannedCount = 0;

        await foreach (var entry in _sharePointService.GetAllEntriesAsync(cancellationToken))
        {
            scannedCount++;

            if (entry.AgeFieldValue is null)
            {
                _logger.LogWarning("Entry {EntryId} has no usable age field value; skipping.", entry.Id);
                results.Add(new CleanupItemResult(entry.Id, CleanupItemStatus.Skipped, 0));
                continue;
            }

            if (entry.AgeFieldValue.Value > cutoff)
            {
                // AC-002: not old enough yet; left untouched and not
                // reported in the summary at all.
                continue;
            }

            // BR-002: a failure on this entry is caught inside
            // ProcessEligibleEntryAsync and recorded, not thrown, so the
            // run continues to the next entry.
            results.Add(await ProcessEligibleEntryAsync(entry, cancellationToken).ConfigureAwait(false));
        }

        var runEndedAtUtc = _timeProvider.GetUtcNow();
        var summary = new CleanupSummary(
            runStartedAtUtc, runEndedAtUtc, _options.DryRun, _options.EntryAgeDays, scannedCount, results);

        _logger.LogInformation(
            "Cleanup run complete. DryRun={DryRun} Scanned={Scanned} Eligible={Eligible} Deleted={Deleted} Failed={Failed} Skipped={Skipped} ImagesDeleted={Images} Duration={Duration}",
            summary.DryRun,
            summary.EntriesScanned,
            summary.EligibleCount,
            summary.DeletedCount,
            summary.FailedCount,
            summary.SkippedCount,
            summary.TotalImagesDeleted,
            summary.Duration);

        return summary;
    }

    private async Task<CleanupItemResult> ProcessEligibleEntryAsync(ReportLogEntry entry, CancellationToken cancellationToken)
    {
        var imagesDeleted = 0;

        // BR-001: every referenced Cloudinary image must be deleted (or
        // confirmed absent/empty) BEFORE the SharePoint item delete is even
        // attempted. The loop exits at the first failure, leaving the
        // SharePoint item intact.
        foreach (var imageUrl in entry.ImageUrls)
        {
            if (_options.DryRun)
            {
                // BR-003: DryRun never issues a real delete call.
                _logger.LogInformation(
                    "[DryRun] Would delete Cloudinary image for entry {EntryId}: {ImageUrl}", entry.Id, imageUrl);
                imagesDeleted++;
                continue;
            }

            var deleted = await _cloudinaryService.DeleteImageAsync(imageUrl, cancellationToken).ConfigureAwait(false);
            if (!deleted)
            {
                return new CleanupItemResult(
                    entry.Id,
                    CleanupItemStatus.Failed,
                    imagesDeleted,
                    $"Failed to delete Cloudinary image: {imageUrl}");
            }

            imagesDeleted++;
        }

        if (_options.DryRun)
        {
            _logger.LogInformation("[DryRun] Would delete SharePoint item {EntryId}.", entry.Id);
            return new CleanupItemResult(entry.Id, CleanupItemStatus.WouldDelete, imagesDeleted);
        }

        try
        {
            // Only reached once every referenced Cloudinary image has been
            // successfully deleted (or there were none) — BR-001.
            await _sharePointService.DeleteEntryAsync(entry.Id, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // ERR-003: the already-deleted Cloudinary images are not rolled
            // back; this is a deliberate, documented trade-off.
            _logger.LogError(ex, "Failed to delete SharePoint item {EntryId} after its images were deleted.", entry.Id);
            return new CleanupItemResult(
                entry.Id, CleanupItemStatus.Failed, imagesDeleted, $"SharePoint item delete failed: {ex.Message}");
        }

        return new CleanupItemResult(entry.Id, CleanupItemStatus.Deleted, imagesDeleted);
    }
}
