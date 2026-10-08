using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Logging;

namespace FBOutletConditionsHousekeeping.Core.Services;

/// <summary>
/// Deletes Cloudinary images referenced by Report Log entries, using the
/// public_id parsed from the entry's stored delivery URL (see
/// docs/SCHEMA.md Section 17).
/// </summary>
public sealed class CloudinaryImageService : ICloudinaryImageService
{
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<CloudinaryImageService> _logger;

    public CloudinaryImageService(Cloudinary cloudinary, ILogger<CloudinaryImageService> logger)
    {
        _cloudinary = cloudinary;
        _logger = logger;
    }

    public async Task<bool> DeleteImageAsync(string imageUrl, CancellationToken cancellationToken)
    {
        if (!CloudinaryPublicIdParser.TryParse(imageUrl, out var publicId))
        {
            _logger.LogWarning("Could not parse a Cloudinary public_id from URL '{ImageUrl}'.", imageUrl);
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var deletionParams = new DeletionParams(publicId) { ResourceType = ResourceType.Image };
            var result = await _cloudinary.DestroyAsync(deletionParams).ConfigureAwait(false);

            // Cloudinary reports "not found" for an asset that no longer
            // exists. This is treated as a successful no-op (EDGE-004), not
            // a failure, since the desired end state — the asset being
            // absent — already holds.
            if (string.Equals(result.Result, "ok", StringComparison.OrdinalIgnoreCase)
                || string.Equals(result.Result, "not found", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "Deleted Cloudinary image {PublicId} (result: {Result}).", publicId, result.Result);
                return true;
            }

            _logger.LogError(
                "Cloudinary deletion of {PublicId} returned unexpected result '{Result}'.", publicId, result.Result);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Cloudinary deletion of {PublicId} threw an exception.", publicId);
            return false;
        }
    }
}
