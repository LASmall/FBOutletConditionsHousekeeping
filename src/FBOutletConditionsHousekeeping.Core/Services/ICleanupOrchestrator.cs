using FBOutletConditionsHousekeeping.Core.Models;

namespace FBOutletConditionsHousekeeping.Core.Services;

/// <summary>
/// Orchestrates a single cleanup run: identifies eligible Report Log
/// entries, enforces the Cloudinary-before-SharePoint deletion ordering
/// (BR-001), honors DryRun, isolates per-entry failures (BR-002), and
/// returns the aggregate <see cref="CleanupSummary"/>.
/// </summary>
public interface ICleanupOrchestrator
{
    Task<CleanupSummary> RunAsync(CancellationToken cancellationToken);
}
