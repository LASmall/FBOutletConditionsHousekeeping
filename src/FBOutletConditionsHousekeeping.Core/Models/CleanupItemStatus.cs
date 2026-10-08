namespace FBOutletConditionsHousekeeping.Core.Models;

/// <summary>
/// The outcome of processing a single Report Log entry during a cleanup run.
/// See docs/SCHEMA.md Section 9.
/// </summary>
public enum CleanupItemStatus
{
    /// <summary>Entry and all its Cloudinary images were successfully deleted.</summary>
    Deleted,

    /// <summary>DryRun was enabled; entry would have been deleted had DryRun been disabled.</summary>
    WouldDelete,

    /// <summary>Entry's age field was missing/invalid and could not be evaluated for eligibility.</summary>
    Skipped,

    /// <summary>
    /// One or more Cloudinary image deletions failed, or the SharePoint item
    /// delete itself failed; entry was not deleted.
    /// </summary>
    Failed,
}
