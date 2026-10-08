namespace FBOutletConditionsHousekeeping.Core.Configuration;

/// <summary>
/// Connection and field-mapping configuration for the SharePoint Online
/// "Report Log" list accessed via Microsoft Graph. Field internal names are
/// configurable rather than hardcoded because the actual list schema is
/// tenant/site specific (see docs/SCHEMA.md).
/// </summary>
public sealed class SharePointOptions
{
    public const string SectionName = "SharePoint";

    /// <summary>Azure AD (Entra ID) tenant ID.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Azure AD App Registration (application) client ID.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Azure AD App Registration client secret.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Microsoft Graph site ID hosting the Report Log list.</summary>
    public string SiteId { get; set; } = string.Empty;

    /// <summary>Microsoft Graph list ID for the Report Log list.</summary>
    public string ListId { get; set; } = string.Empty;

    /// <summary>
    /// Internal (not display) name of the list column used to determine
    /// entry age eligibility.
    /// </summary>
    public string DateFieldInternalName { get; set; } = "Created";

    /// <summary>
    /// Comma-separated list of up to 5 internal column names, each holding
    /// either an empty value or a Cloudinary image URL referenced by the
    /// entry.
    /// </summary>
    public string ImageUrlFieldInternalNames { get; set; } = string.Empty;

    /// <summary>
    /// Parses <see cref="ImageUrlFieldInternalNames"/> into its individual
    /// field names, trimming whitespace and ignoring empty entries.
    /// </summary>
    public IReadOnlyList<string> GetImageUrlFieldNames() =>
        ImageUrlFieldInternalNames
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
}
