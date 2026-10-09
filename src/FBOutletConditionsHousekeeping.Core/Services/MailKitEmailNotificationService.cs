using System.Net;
using System.Text;
using FBOutletConditionsHousekeeping.Core.Configuration;
using FBOutletConditionsHousekeeping.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace FBOutletConditionsHousekeeping.Core.Services;

/// <summary>
/// Sends the HTML cleanup summary/failure notification emails via SMTP
/// (ADR-003), delegating the actual connect/send sequence to
/// <see cref="ISmtpTransport"/> so this class's EmailEnabled branching and
/// content formatting are unit-testable without a live SMTP server. Honors
/// <see cref="EmailOptions.EmailEnabled"/> internally so callers do not need
/// to guard every call site (AC-009).
/// </summary>
public sealed class MailKitEmailNotificationService : IEmailNotificationService
{
    private readonly EmailOptions _options;
    private readonly ISmtpTransport _transport;
    private readonly ILogger<MailKitEmailNotificationService> _logger;

    public MailKitEmailNotificationService(
        IOptions<EmailOptions> options, ISmtpTransport transport, ILogger<MailKitEmailNotificationService> logger)
    {
        _options = options.Value;
        _transport = transport;
        _logger = logger;
    }

    public Task SendSummaryAsync(CleanupSummary summary, CancellationToken cancellationToken)
    {
        var subject = summary.DryRun
            ? $"[Dry Run] F&B Outlet Conditions Cleanup Summary - {summary.RunStartedAtUtc:yyyy-MM-dd}"
            : $"F&B Outlet Conditions Cleanup Summary - {summary.RunStartedAtUtc:yyyy-MM-dd}";

        return SendAsync(subject, BuildSummaryHtml(summary), cancellationToken);
    }

    public Task SendFailureAsync(Exception exception, DateTimeOffset runStartedAtUtc, CancellationToken cancellationToken)
    {
        var subject = $"[FAILURE] F&B Outlet Conditions Cleanup - {runStartedAtUtc:yyyy-MM-dd}";
        return SendAsync(subject, BuildFailureHtml(exception, runStartedAtUtc), cancellationToken);
    }

    private async Task SendAsync(string subject, string htmlBody, CancellationToken cancellationToken)
    {
        if (!_options.EmailEnabled)
        {
            _logger.LogInformation(
                "Email notifications are disabled (EmailEnabled=false); not sending '{Subject}'.", subject);
            return;
        }

        var recipients = _options.GetRecipients();
        if (recipients.Count == 0)
        {
            _logger.LogWarning(
                "EmailEnabled is true but no valid EmailRecipients are configured; cannot send '{Subject}'.", subject);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_options.EmailFromAddress));
        foreach (var recipient in recipients)
        {
            message.To.Add(MailboxAddress.Parse(recipient));
        }

        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        try
        {
            await _transport.SendAsync(message, _options, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Sent email '{Subject}' to {RecipientCount} recipient(s).", subject, recipients.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Per ERR-004 (docs/SPECIFICATION.md): an email delivery failure
            // is logged but must not cause the cleanup run itself to be
            // reported as failed, since the cleanup outcome is independent
            // of notification delivery.
            _logger.LogError(ex, "Failed to send email '{Subject}'.", subject);
        }
    }

    private static string BuildSummaryHtml(CleanupSummary summary)
    {
        var sb = new StringBuilder();
        sb.Append("<html><body style=\"font-family: Segoe UI, Arial, sans-serif;\">");
        sb.Append("<h2>F&amp;B Outlet Conditions Cleanup ").Append(summary.DryRun ? "(DRY RUN) " : string.Empty).Append("Summary</h2>");
        sb.Append("<table cellpadding=\"6\" style=\"border-collapse: collapse;\">");
        AppendRow(sb, "Run started (UTC)", summary.RunStartedAtUtc.ToString("u"));
        AppendRow(sb, "Run ended (UTC)", summary.RunEndedAtUtc.ToString("u"));
        AppendRow(sb, "Duration", summary.Duration.ToString(@"hh\:mm\:ss"));
        AppendRow(sb, "Dry run", summary.DryRun ? "Yes" : "No");
        AppendRow(sb, "Age threshold (days)", summary.EntryAgeDays.ToString());
        AppendRow(sb, "Entries scanned", summary.EntriesScanned.ToString());
        AppendRow(sb, "Entries eligible", summary.EligibleCount.ToString());
        AppendRow(sb, summary.DryRun ? "Entries that would be deleted" : "Entries deleted", summary.DeletedCount.ToString());
        AppendRow(sb, "Entries skipped (unparseable age field)", summary.SkippedCount.ToString());
        AppendRow(sb, "Entries failed", summary.FailedCount.ToString());
        AppendRow(sb, summary.DryRun ? "Cloudinary images that would be deleted" : "Cloudinary images deleted", summary.TotalImagesDeleted.ToString());
        sb.Append("</table>");

        if (summary.FailedItems.Count > 0)
        {
            sb.Append("<h3>Failures</h3><ul>");
            foreach (var item in summary.FailedItems)
            {
                sb.Append("<li>Item ")
                    .Append(WebUtility.HtmlEncode(item.EntryId))
                    .Append(": ")
                    .Append(WebUtility.HtmlEncode(item.FailureReason))
                    .Append("</li>");
            }

            sb.Append("</ul>");
        }

        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static string BuildFailureHtml(Exception exception, DateTimeOffset runStartedAtUtc)
    {
        var sb = new StringBuilder();
        sb.Append("<html><body style=\"font-family: Segoe UI, Arial, sans-serif;\">");
        sb.Append("<h2>F&amp;B Outlet Conditions Cleanup &mdash; Run Failure</h2>");
        sb.Append("<table cellpadding=\"6\" style=\"border-collapse: collapse;\">");
        AppendRow(sb, "Run started (UTC)", runStartedAtUtc.ToString("u"));
        AppendRow(sb, "Exception type", exception.GetType().FullName ?? exception.GetType().Name);
        AppendRow(sb, "Message", exception.Message);
        sb.Append("</table>");
        sb.Append("<h3>Details</h3>");
        sb.Append("<pre>").Append(WebUtility.HtmlEncode(exception.ToString())).Append("</pre>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, string label, string? value)
    {
        sb.Append("<tr><td style=\"font-weight: bold; border: 1px solid #ccc;\">")
            .Append(WebUtility.HtmlEncode(label))
            .Append("</td><td style=\"border: 1px solid #ccc;\">")
            .Append(WebUtility.HtmlEncode(value))
            .Append("</td></tr>");
    }
}
