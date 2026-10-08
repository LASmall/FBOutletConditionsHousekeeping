namespace FBOutletConditionsHousekeeping.Core.Services;

/// <summary>
/// Abstracts Cloudinary image deletion so <see cref="CleanupOrchestrator"/>
/// can be unit-tested without a live Cloudinary connection.
/// </summary>
public interface ICloudinaryImageService
{
    /// <summary>
    /// Deletes the Cloudinary image referenced by the given delivery URL.
    /// Returns true if the image was deleted (or was already absent — a
    /// "not found" result is treated as a successful no-op per
    /// docs/SPECIFICATION.md EDGE-004), or false if the URL could not be
    /// parsed into a public_id or the delete call failed.
    /// </summary>
    Task<bool> DeleteImageAsync(string imageUrl, CancellationToken cancellationToken);
}
