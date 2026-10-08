using FBOutletConditionsHousekeeping.Core.Models;

namespace FBOutletConditionsHousekeeping.Core.Services;

/// <summary>
/// Abstracts summary/failure email delivery so <see cref="CleanupOrchestrator"/>
/// and the Functions entry point can be unit-tested without sending real
/// email. Honors EmailOptions.EmailEnabled internally — callers may call
/// these methods unconditionally (AC-007/AC-008/AC-009).
/// </summary>
public interface IEmailNotificationService
{
    /// <summary>Sends the run summary email (FR-008/FR-013), if EmailEnabled.</summary>
    Task SendSummaryAsync(CleanupSummary summary, CancellationToken cancellationToken);

    /// <summary>Sends the run failure email (FR-009), if EmailEnabled.</summary>
    Task SendFailureAsync(Exception exception, DateTimeOffset runStartedAtUtc, CancellationToken cancellationToken);
}
