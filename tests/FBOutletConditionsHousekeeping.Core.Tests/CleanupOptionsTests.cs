using FBOutletConditionsHousekeeping.Core.Configuration;
using Xunit;

namespace FBOutletConditionsHousekeeping.Core.Tests;

/// <summary>
/// TEST-011: the timer schedule default is 02:30 daily (FR-007), and VAL-001
/// (EntryAgeDays must be a non-negative integer) is enforced.
/// </summary>
public sealed class CleanupOptionsTests
{
    [Fact]
    public void TimerSchedule_DefaultsTo_02_30_Daily()
    {
        var options = new CleanupOptions();

        Assert.Equal("0 30 2 * * *", options.TimerSchedule);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(90, true)]
    [InlineData(-1, false)]
    public void Validator_EntryAgeDays_EnforcesNonNegative(int entryAgeDays, bool expectedValid)
    {
        var validator = new CleanupOptionsValidator();
        var options = new CleanupOptions { EntryAgeDays = entryAgeDays };

        var result = validator.Validate(null, options);

        Assert.Equal(expectedValid, result.Succeeded);
    }
}
