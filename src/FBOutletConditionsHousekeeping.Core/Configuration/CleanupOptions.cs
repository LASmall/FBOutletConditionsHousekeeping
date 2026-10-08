namespace FBOutletConditionsHousekeeping.Core.Configuration;

/// <summary>
/// Controls the overall behavior of the Report Log cleanup run: eligibility
/// age, dry-run safety switch, timer schedule, and Graph paging size.
/// </summary>
public sealed class CleanupOptions
{
    /// <summary>
    /// Configuration section name this options class binds to.
    /// </summary>
    public const string SectionName = "Cleanup";

    /// <summary>
    /// How many days old a Report Log entry's configured date field must be
    /// before the entry becomes eligible for deletion.
    /// </summary>
    public int EntryAgeDays { get; set; } = 90;

    /// <summary>
    /// When true, the function evaluates and logs what it would delete but
    /// issues no delete calls against Cloudinary or SharePoint. Defaults to
    /// true so a fresh deployment never deletes data until explicitly
    /// configured to do so.
    /// </summary>
    public bool DryRun { get; set; } = true;

    /// <summary>
    /// CRON expression (NCRONTAB format) controlling the timer trigger
    /// schedule. Defaults to 02:30 daily, per the functional requirement.
    /// This value is documentation/reference only for app settings — the
    /// Functions host binds the schedule from the "Cleanup:TimerSchedule"
    /// app setting referenced by the %Cleanup:TimerSchedule% binding
    /// expression on the TimerTrigger attribute.
    /// </summary>
    public string TimerSchedule { get; set; } = "0 30 2 * * *";

    /// <summary>
    /// Page size used when paging through Microsoft Graph list-item
    /// results, to avoid loading an unbounded result set into memory.
    /// </summary>
    public int MaxPageSize { get; set; } = 200;
}
