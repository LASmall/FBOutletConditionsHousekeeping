using FBOutletConditionsHousekeeping.Core.Models;

namespace FBOutletConditionsHousekeeping.Core.Services;

/// <summary>
/// Abstracts read/delete access to the SharePoint "Report Log" list via
/// Microsoft Graph, so <see cref="CleanupOrchestrator"/> can be unit-tested
/// without a live Graph connection.
/// </summary>
public interface ISharePointReportLogService
{
    /// <summary>
    /// Retrieves every Report Log list item, paged to the configured page
    /// size. Age-eligibility filtering is performed by the caller
    /// (<see cref="CleanupOrchestrator"/>), not by this method, per
    /// docs/ARCHITECTURE.md ADR-005.
    /// </summary>
    IAsyncEnumerable<ReportLogEntry> GetAllEntriesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the SharePoint list item with the given ID. Must only be
    /// called after all of the entry's Cloudinary images have been
    /// confirmed deleted (BR-001).
    /// </summary>
    Task DeleteEntryAsync(string entryId, CancellationToken cancellationToken);
}
