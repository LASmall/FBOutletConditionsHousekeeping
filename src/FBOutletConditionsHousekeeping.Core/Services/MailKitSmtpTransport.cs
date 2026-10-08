using FBOutletConditionsHousekeeping.Core.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace FBOutletConditionsHousekeeping.Core.Services;

/// <summary>
/// Default <see cref="ISmtpTransport"/> implementation using MailKit
/// (ADR-003).
/// </summary>
public sealed class MailKitSmtpTransport : ISmtpTransport
{
    public async Task SendAsync(MimeMessage message, EmailOptions options, CancellationToken cancellationToken)
    {
        using var client = new SmtpClient();

        // When SmtpUseSsl is true, connect with implicit SSL (typically port
        // 465); otherwise negotiate STARTTLS on the configured port
        // (typically 587) — the common SMTP AUTH configuration.
        var secureSocketOptions = options.SmtpUseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

        await client.ConnectAsync(options.SmtpHost, options.SmtpPort, secureSocketOptions, cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrEmpty(options.SmtpUsername))
        {
            await client.AuthenticateAsync(options.SmtpUsername, options.SmtpPassword, cancellationToken)
                .ConfigureAwait(false);
        }

        await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
        await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
    }
}
