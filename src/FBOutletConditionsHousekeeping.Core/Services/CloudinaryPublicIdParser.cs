using System.Text.RegularExpressions;

namespace FBOutletConditionsHousekeeping.Core.Services;

/// <summary>
/// Extracts a Cloudinary asset's public_id from its stored delivery URL, so
/// that image deletion can be performed without SharePoint needing to store
/// the public_id separately (see docs/SCHEMA.md Section 17).
/// </summary>
public static partial class CloudinaryPublicIdParser
{
    // Matches a Cloudinary delivery URL path of the general form:
    // /<resource_type>/<delivery_type>/[<transformations>/][v<version>/]<public_id>.<extension>
    // The single optional transformation segment is only consumed when it is
    // immediately followed by a version segment (v<digits>/) — this
    // disambiguates it from a plain folder segment, which has no such
    // lookahead constraint. The public_id group is greedy so it naturally
    // extends up to the LAST dot in the remaining path (its own folder
    // segments may themselves contain dots).
    [GeneratedRegex(
        @"/upload/(?:(?<transform>[^/]+)/(?=v\d+/))?(?:v\d+/)?(?<publicId>.+)\.[A-Za-z0-9]+$",
        RegexOptions.IgnoreCase)]
    private static partial Regex UploadPathRegex();

    /// <summary>
    /// Attempts to parse a Cloudinary public_id from a delivery URL such as
    /// https://res.cloudinary.com/&lt;cloud_name&gt;/image/upload/v1690000000/reports/entry-123-1.jpg
    /// </summary>
    public static bool TryParse(string imageUrl, out string publicId)
    {
        publicId = string.Empty;

        if (string.IsNullOrWhiteSpace(imageUrl) || !Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var match = UploadPathRegex().Match(uri.AbsolutePath);
        if (!match.Success)
        {
            return false;
        }

        publicId = Uri.UnescapeDataString(match.Groups["publicId"].Value);
        return !string.IsNullOrWhiteSpace(publicId);
    }
}
