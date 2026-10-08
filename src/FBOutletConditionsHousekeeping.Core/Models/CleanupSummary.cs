namespace FBOutletConditionsHousekeeping.Core.Models;

/// <summary>
/// The aggregate outcome of a single cleanup run, used both for logging and
/// to build the summary notification email (FR-013).
/// </summary>
/// <param name="RunStartedAtUtc">When the run began.</param>
/// <param name="DryRun">Whether this run was a dry run.</param>
/// <param name="EntriesScanned">Total Report Log entries evaluated.</param>
/// <param name="Items">Per-entry results for every entry that was eligible or skipped.</param>
public sealed record CleanupSummary(
    DateTimeOffset RunStartedAtUtc,
    bool DryRun,
    int EntriesScanned,
    IReadOnlyList<CleanupItemResult> Items)
{
    public int EligibleCount => Items.Count;

    public int DeletedCount => Items.Count(i => i.Status is CleanupItemStatus.Deleted or CleanupItemStatus.WouldDelete);

    public int FailedCount => Items.Count(i => i.Status == CleanupItemStatus.Failed);

    public int SkippedCount => Items.Count(i => i.Status == CleanupItemStatus.Skipped);

    public int TotalImagesDeleted => Items.Sum(i => i.ImagesDeleted);

    public IReadOnlyList<CleanupItemResult> FailedItems =>
        Items.Where(i => i.Status == CleanupItemStatus.Failed).ToList();
}
