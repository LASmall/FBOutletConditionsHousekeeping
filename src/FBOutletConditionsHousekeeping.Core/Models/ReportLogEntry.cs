namespace FBOutletConditionsHousekeeping.Core.Models;

/// <summary>
/// A SharePoint "Report Log" list item as read from Microsoft Graph, reduced
/// to only the fields this application needs (see docs/SCHEMA.md Section 5.1).
/// </summary>
/// <param name="Id">SharePoint list item ID (Graph ListItem.Id).</param>
/// <param name="AgeFieldValue">
/// The configured date field's value, used to determine eligibility. Null
/// when the field was missing/blank on the item (EDGE case: entry is
/// skipped, not treated as eligible — see docs/SPECIFICATION.md Section 20).
/// </param>
/// <param name="ImageUrls">
/// The raw values of the configured Cloudinary image URL fields, in
/// configured field order. Empty/blank field values are omitted so that an
/// entry with fewer than 5 populated fields only yields the populated ones
/// (AC-006).
/// </param>
public sealed record ReportLogEntry(
    string Id,
    DateTimeOffset? AgeFieldValue,
    IReadOnlyList<string> ImageUrls);
