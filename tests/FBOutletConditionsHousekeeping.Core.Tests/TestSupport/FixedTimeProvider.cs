namespace FBOutletConditionsHousekeeping.Core.Tests.TestSupport;

/// <summary>Returns a fixed instant for every call, so age-eligibility tests are deterministic.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
