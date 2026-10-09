using FBOutletConditionsHousekeeping.Core.Configuration;
using FBOutletConditionsHousekeeping.Core.Models;
using FBOutletConditionsHousekeeping.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using Moq;
using Xunit;

namespace FBOutletConditionsHousekeeping.Core.Tests;

/// <summary>
/// Validates the EmailEnabled branching (AC-007/AC-008/AC-009) implemented
/// in <see cref="MailKitEmailNotificationService"/>, using a mocked
/// <see cref="ISmtpTransport"/> so no live SMTP server is required.
/// </summary>
public sealed class MailKitEmailNotificationServiceTests
{
    private readonly Mock<ISmtpTransport> _transportMock = new();

    private MailKitEmailNotificationService CreateService(EmailOptions options) =>
        new(Options.Create(options), _transportMock.Object, NullLogger<MailKitEmailNotificationService>.Instance);

    private static EmailOptions ValidEnabledOptions() => new()
    {
        EmailEnabled = true,
        EmailFromAddress = "noreply@example.com",
        EmailRecipients = "admin@example.com",
        SmtpHost = "smtp.example.com",
    };

    private static CleanupSummary SampleSummary()
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        return new(
            startedAtUtc,
            RunEndedAtUtc: startedAtUtc.AddSeconds(5),
            DryRun: false,
            EntryAgeDays: 90,
            EntriesScanned: 5,
            Items:
            [
                new CleanupItemResult("item-1", CleanupItemStatus.Deleted, 1),
            ]);
    }

    // TEST-009: no email is sent (summary or failure) when EmailEnabled = false.
    [Fact]
    public async Task SendSummaryAsync_EmailDisabled_DoesNotInvokeTransport()
    {
        var options = ValidEnabledOptions();
        options.EmailEnabled = false;
        var service = CreateService(options);

        await service.SendSummaryAsync(SampleSummary(), CancellationToken.None);

        _transportMock.Verify(
            t => t.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<EmailOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendFailureAsync_EmailDisabled_DoesNotInvokeTransport()
    {
        var options = ValidEnabledOptions();
        options.EmailEnabled = false;
        var service = CreateService(options);

        await service.SendFailureAsync(new InvalidOperationException("boom"), DateTimeOffset.UtcNow, CancellationToken.None);

        _transportMock.Verify(
            t => t.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<EmailOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // Supports AC-007: a summary email is sent, addressed correctly, when EmailEnabled = true.
    [Fact]
    public async Task SendSummaryAsync_EmailEnabled_InvokesTransportWithConfiguredRecipients()
    {
        var options = ValidEnabledOptions();
        options.EmailRecipients = "a@example.com,b@example.com";
        var service = CreateService(options);

        MimeMessage? sentMessage = null;
        _transportMock
            .Setup(t => t.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<EmailOptions>(), It.IsAny<CancellationToken>()))
            .Callback<MimeMessage, EmailOptions, CancellationToken>((message, _, _) => sentMessage = message)
            .Returns(Task.CompletedTask);

        await service.SendSummaryAsync(SampleSummary(), CancellationToken.None);

        _transportMock.Verify(
            t => t.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<EmailOptions>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.NotNull(sentMessage);
        Assert.Equal(2, sentMessage!.To.Count);
        Assert.Equal("noreply@example.com", ((MailboxAddress)sentMessage.From.Single()).Address);
    }

    // Supports AC-008/FR-009: a failure email is sent when EmailEnabled = true,
    // using the "F&B Outlet Conditions" branding rather than "Report Log".
    [Fact]
    public async Task SendFailureAsync_EmailEnabled_InvokesTransportWithBranding()
    {
        var service = CreateService(ValidEnabledOptions());

        MimeMessage? sentMessage = null;
        _transportMock
            .Setup(t => t.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<EmailOptions>(), It.IsAny<CancellationToken>()))
            .Callback<MimeMessage, EmailOptions, CancellationToken>((message, _, _) => sentMessage = message)
            .Returns(Task.CompletedTask);

        await service.SendFailureAsync(new InvalidOperationException("Graph auth failed"), DateTimeOffset.UtcNow, CancellationToken.None);

        _transportMock.Verify(
            t => t.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<EmailOptions>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.NotNull(sentMessage);
        Assert.Contains("F&B Outlet Conditions", sentMessage!.Subject);
        Assert.DoesNotContain("Report Log", sentMessage.Subject);
        var body = ((MimeKit.TextPart)sentMessage.Body!).Text;
        Assert.Contains("F&amp;B Outlet Conditions", body);
        Assert.DoesNotContain("Report Log", body);
    }

    // Supports AC-007/FR-013: the summary email reports the run end time,
    // duration, and configured age threshold, and uses the "F&B Outlet
    // Conditions" branding rather than "Report Log".
    [Fact]
    public async Task SendSummaryAsync_BodyAndSubject_ContainRunEndedDurationAgeThresholdAndBranding()
    {
        var service = CreateService(ValidEnabledOptions());

        MimeMessage? sentMessage = null;
        _transportMock
            .Setup(t => t.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<EmailOptions>(), It.IsAny<CancellationToken>()))
            .Callback<MimeMessage, EmailOptions, CancellationToken>((message, _, _) => sentMessage = message)
            .Returns(Task.CompletedTask);

        await service.SendSummaryAsync(SampleSummary(), CancellationToken.None);

        Assert.NotNull(sentMessage);
        var body = ((MimeKit.TextPart)sentMessage!.Body!).Text;

        Assert.Contains("Run ended (UTC)", body);
        Assert.Contains("Duration", body);
        Assert.Contains("Age threshold (days)", body);
        Assert.Contains("90", body);
        Assert.Contains("F&amp;B Outlet Conditions", body);
        Assert.DoesNotContain("Report Log", body);
        Assert.DoesNotContain("Report Log", sentMessage.Subject);
    }

    // When the transport itself throws (e.g. SMTP connection failure), the
    // failure is logged but not rethrown (ERR-004) — the cleanup run outcome
    // is independent of notification delivery.
    [Fact]
    public async Task SendSummaryAsync_TransportThrows_DoesNotPropagateException()
    {
        var service = CreateService(ValidEnabledOptions());
        _transportMock
            .Setup(t => t.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<EmailOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP connection refused"));

        var exception = await Record.ExceptionAsync(() => service.SendSummaryAsync(SampleSummary(), CancellationToken.None));

        Assert.Null(exception);
    }
}
