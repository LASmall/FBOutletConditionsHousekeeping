namespace FBOutletConditionsHousekeeping.Core.Models;

/// <summary>
/// The result of processing a single Report Log entry.
/// </summary>
/// <param name="EntryId">SharePoint list item ID.</param>
/// <param name="Status">The outcome of processing this entry.</param>
/// <param name="ImagesDeleted">
/// Number of Cloudinary images successfully deleted (or that would have
/// been deleted, under DryRun) for this entry.
/// </param>
/// <param name="FailureReason">
/// Populated only when <see cref="Status"/> is <see cref="CleanupItemStatus.Failed"/>;
/// describes why the entry was not deleted.
/// </param>
public sealed record CleanupItemResult(
    string EntryId,
    CleanupItemStatus Status,
    int ImagesDeleted,
    string? FailureReason = null);
