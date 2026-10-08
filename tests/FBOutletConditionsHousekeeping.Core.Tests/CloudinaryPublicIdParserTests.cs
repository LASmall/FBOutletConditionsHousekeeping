using FBOutletConditionsHousekeeping.Core.Services;
using Xunit;

namespace FBOutletConditionsHousekeeping.Core.Tests;

/// <summary>
/// TEST-010: Cloudinary public_id is correctly parsed from a representative
/// Cloudinary delivery URL (see docs/SCHEMA.md Section 17).
/// </summary>
public sealed class CloudinaryPublicIdParserTests
{
    [Theory]
    [InlineData(
        "https://res.cloudinary.com/demo/image/upload/v1690000000/reports/entry-123-1.jpg",
        "reports/entry-123-1")]
    [InlineData(
        "https://res.cloudinary.com/demo/image/upload/entry-456.png",
        "entry-456")]
    [InlineData(
        "https://res.cloudinary.com/demo/image/upload/c_fill,w_200/v1690000000/reports/entry-789-2.jpg",
        "reports/entry-789-2")]
    public void TryParse_ValidCloudinaryUrl_ExtractsPublicId(string url, string expectedPublicId)
    {
        var parsed = CloudinaryPublicIdParser.TryParse(url, out var publicId);

        Assert.True(parsed);
        Assert.Equal(expectedPublicId, publicId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("https://example.com/no-upload-segment/a.jpg")]
    public void TryParse_InvalidOrUnrecognizedUrl_ReturnsFalse(string url)
    {
        var parsed = CloudinaryPublicIdParser.TryParse(url, out var publicId);

        Assert.False(parsed);
        Assert.Equal(string.Empty, publicId);
    }
}
