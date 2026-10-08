using FBOutletConditionsHousekeeping.Core.Configuration;
using MimeKit;

namespace FBOutletConditionsHousekeeping.Core.Services;

/// <summary>
/// Abstracts the actual SMTP connect/authenticate/send sequence so
/// <see cref="MailKitEmailNotificationService"/>'s EmailEnabled branching
/// and content formatting can be unit-tested without a live SMTP server
/// (see docs/SPECIFICATION.md AC-007/AC-008/AC-009).
/// </summary>
public interface ISmtpTransport
{
    Task SendAsync(MimeMessage message, EmailOptions options, CancellationToken cancellationToken);
}
