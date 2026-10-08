using Microsoft.Extensions.Options;

namespace FBOutletConditionsHousekeeping.Core.Configuration;

/// <summary>
/// Enforces VAL-001 (docs/SPECIFICATION.md): EntryAgeDays must be a
/// non-negative integer. Validation runs at startup so misconfiguration
/// fails fast rather than surfacing deep inside a run.
/// </summary>
public sealed class CleanupOptionsValidator : IValidateOptions<CleanupOptions>
{
    public ValidateOptionsResult Validate(string? name, CleanupOptions options)
    {
        // EntryAgeDays = 0 is a valid edge case (EDGE-002: all entries are
        // immediately eligible), so only negative values are rejected.
        if (options.EntryAgeDays < 0)
        {
            return ValidateOptionsResult.Fail("EntryAgeDays must be a non-negative integer.");
        }

        if (options.MaxPageSize <= 0)
        {
            return ValidateOptionsResult.Fail("MaxPageSize must be a positive integer.");
        }

        return ValidateOptionsResult.Success;
    }
}
