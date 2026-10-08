using FBOutletConditionsHousekeeping.Core.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FBOutletConditionsHousekeeping.Functions.Functions;

/// <summary>
/// Timer-triggered entry point for the daily Report Log cleanup run
/// (FR-007). This class is intentionally thin: all business logic lives in
/// <see cref="ICleanupOrchestrator"/> (FBOutletConditionsHousekeeping.Core),
/// including run-level failure handling (logging + failure email + rethrow)
/// so that behavior is covered by Core unit tests without requiring a live
/// Azure Functions host.
/// </summary>
public sealed class ReportLogCleanupFunction
{
    private readonly ICleanupOrchestrator _orchestrator;
    private readonly ILogger<ReportLogCleanupFunction> _logger;

    public ReportLogCleanupFunction(ICleanupOrchestrator orchestrator, ILogger<ReportLogCleanupFunction> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    // The schedule is read from the "Cleanup:TimerSchedule" configuration
    // value (default "0 30 2 * * *" = 02:30 daily) rather than being
    // hardcoded, so the schedule can be changed via app settings without a
    // code change.
    [Function(nameof(ReportLogCleanupFunction))]
    public async Task RunAsync(
        [TimerTrigger("%Cleanup:TimerSchedule%")] TimerInfo timerInfo,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Report Log cleanup timer fired. IsPastDue={IsPastDue}", timerInfo.IsPastDue);

        // Exceptions are intentionally allowed to propagate: CleanupOrchestrator
        // already logs and sends the failure notification email before
        // rethrowing, and letting the exception reach the Functions host
        // marks this invocation as failed in Application Insights/monitoring.
        await _orchestrator.RunAsync(cancellationToken).ConfigureAwait(false);
    }
}
