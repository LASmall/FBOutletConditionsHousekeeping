using FBOutletConditionsHousekeeping.Core.Configuration;
using FBOutletConditionsHousekeeping.Core.Models;
using FBOutletConditionsHousekeeping.Core.Services;
using FBOutletConditionsHousekeeping.Core.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FBOutletConditionsHousekeeping.Core.Tests;

/// <summary>
/// Validates US-001's ordering, dry-run, and partial-failure-isolation
/// business rules, independent of any live SharePoint/Cloudinary/SMTP
/// connection, using mocked service interfaces.
/// </summary>
public sealed class CleanupOrchestratorTests
{
    private static readonly DateTimeOffset RunNow = new(2026, 10, 8, 2, 30, 0, TimeSpan.Zero);

    private readonly Mock<ISharePointReportLogService> _sharePointMock = new();
    private readonly Mock<ICloudinaryImageService> _cloudinaryMock = new();
    private readonly Mock<IEmailNotificationService> _emailMock = new();

    private CleanupOrchestrator CreateOrchestrator(int entryAgeDays = 90, bool dryRun = false)
    {
        var options = Options.Create(new CleanupOptions { EntryAgeDays = entryAgeDays, DryRun = dryRun });

        return new CleanupOrchestrator(
            _sharePointMock.Object,
            _cloudinaryMock.Object,
            _emailMock.Object,
            options,
            new FixedTimeProvider(RunNow),
            NullLogger<CleanupOrchestrator>.Instance);
    }

    private static async IAsyncEnumerable<ReportLogEntry> AsAsyncEnumerable(params ReportLogEntry[] entries)
    {
        // Forces a genuine async state machine (avoids CS1998) while still
        // yielding synchronously-available test data.
        await Task.Yield();
        foreach (var entry in entries)
        {
            yield return entry;
        }
    }

#pragma warning disable CS0162 // Unreachable code detected — the yield below exists only to satisfy the iterator-method requirement.
    private static async IAsyncEnumerable<ReportLogEntry> ThrowingAsyncEnumerable()
    {
        await Task.Yield();
        throw new InvalidOperationException("Simulated Graph authentication failure.");
        yield return null!;
    }
#pragma warning restore CS0162

    // TEST-001: an entry older than EntryAgeDays is included in the eligible set.
    [Fact]
    public async Task RunAsync_EntryOlderThanEntryAgeDays_IsEligibleAndDeleted()
    {
        var oldEntry = new ReportLogEntry("item-1", RunNow.AddDays(-91), []);
        _sharePointMock.Setup(s => s.GetAllEntriesAsync(It.IsAny<CancellationToken>()))
            .Returns(AsAsyncEnumerable(oldEntry));

        var orchestrator = CreateOrchestrator(entryAgeDays: 90);
        var summary = await orchestrator.RunAsync(CancellationToken.None);

        Assert.Equal(1, summary.EligibleCount);
        Assert.Equal(1, summary.DeletedCount);
        _sharePointMock.Verify(s => s.DeleteEntryAsync("item-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    // TEST-002: an entry not older than EntryAgeDays is excluded from the eligible set.
    [Fact]
    public async Task RunAsync_EntryNotOlderThanEntryAgeDays_IsExcluded()
    {
        var recentEntry = new ReportLogEntry("item-2", RunNow.AddDays(-10), []);
        _sharePointMock.Setup(s => s.GetAllEntriesAsync(It.IsAny<CancellationToken>()))
            .Returns(AsAsyncEnumerable(recentEntry));

        var orchestrator = CreateOrchestrator(entryAgeDays: 90);
        var summary = await orchestrator.RunAsync(CancellationToken.None);

        Assert.Equal(1, summary.EntriesScanned);
        Assert.Equal(0, summary.EligibleCount);
        _sharePointMock.Verify(s => s.DeleteEntryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // TEST-003: all Cloudinary images for an entry are deleted before the SharePoint
    // item delete call is made; SharePoint delete only called after all image deletes succeed.
    [Fact]
    public async Task RunAsync_DeletesCloudinaryImagesBeforeSharePointItem()
    {
        var entry = new ReportLogEntry("item-3", RunNow.AddDays(-100), ["https://res.cloudinary.com/demo/image/upload/v1/a.jpg", "https://res.cloudinary.com/demo/image/upload/v1/b.jpg"]);
        _sharePointMock.Setup(s => s.GetAllEntriesAsync(It.IsAny<CancellationToken>()))
            .Returns(AsAsyncEnumerable(entry));

        var callOrder = new List<string>();
        _cloudinaryMock.Setup(c => c.DeleteImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("cloudinary"))
            .ReturnsAsync(true);
        _sharePointMock.Setup(s => s.DeleteEntryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("sharepoint"))
            .Returns(Task.CompletedTask);

        var orchestrator = CreateOrchestrator(entryAgeDays: 90);
        var summary = await orchestrator.RunAsync(CancellationToken.None);

        Assert.Equal(["cloudinary", "cloudinary", "sharepoint"], callOrder);
        Assert.Equal(CleanupItemStatus.Deleted, summary.Items.Single().Status);
        Assert.Equal(2, summary.Items.Single().ImagesDeleted);
    }

    // TEST-004: a failed Cloudinary image deletion prevents the SharePoint item
    // delete call and records a failure.
    [Fact]
    public async Task RunAsync_FailedCloudinaryDeletion_PreventsSharePointDeleteAndRecordsFailure()
    {
        var entry = new ReportLogEntry("item-4", RunNow.AddDays(-100), ["https://res.cloudinary.com/demo/image/upload/v1/a.jpg"]);
        _sharePointMock.Setup(s => s.GetAllEntriesAsync(It.IsAny<CancellationToken>()))
            .Returns(AsAsyncEnumerable(entry));
        _cloudinaryMock.Setup(c => c.DeleteImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var orchestrator = CreateOrchestrator(entryAgeDays: 90);
        var summary = await orchestrator.RunAsync(CancellationToken.None);

        var result = summary.Items.Single();
        Assert.Equal(CleanupItemStatus.Failed, result.Status);
        Assert.NotNull(result.FailureReason);
        _sharePointMock.Verify(s => s.DeleteEntryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // TEST-005: DryRun = true results in zero calls to the Cloudinary delete API
    // and zero calls to the SharePoint delete API.
    [Fact]
    public async Task RunAsync_DryRun_IssuesNoDeleteCalls()
    {
        var entry = new ReportLogEntry("item-5", RunNow.AddDays(-100), ["https://res.cloudinary.com/demo/image/upload/v1/a.jpg"]);
        _sharePointMock.Setup(s => s.GetAllEntriesAsync(It.IsAny<CancellationToken>()))
            .Returns(AsAsyncEnumerable(entry));

        var orchestrator = CreateOrchestrator(entryAgeDays: 90, dryRun: true);
        var summary = await orchestrator.RunAsync(CancellationToken.None);

        _cloudinaryMock.Verify(c => c.DeleteImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _sharePointMock.Verify(s => s.DeleteEntryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(CleanupItemStatus.WouldDelete, summary.Items.Single().Status);
        Assert.Equal(1, summary.DeletedCount);
    }

    // TEST-006: an entry with fewer than 5 populated image fields only processes
    // the populated ones.
    [Fact]
    public async Task RunAsync_EntryWithFewerThanFiveImages_OnlyProcessesPopulatedOnes()
    {
        var entry = new ReportLogEntry("item-6", RunNow.AddDays(-100), ["https://res.cloudinary.com/demo/image/upload/v1/a.jpg", "https://res.cloudinary.com/demo/image/upload/v1/b.jpg"]);
        _sharePointMock.Setup(s => s.GetAllEntriesAsync(It.IsAny<CancellationToken>()))
            .Returns(AsAsyncEnumerable(entry));
        _cloudinaryMock.Setup(c => c.DeleteImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var orchestrator = CreateOrchestrator(entryAgeDays: 90);
        var summary = await orchestrator.RunAsync(CancellationToken.None);

        _cloudinaryMock.Verify(c => c.DeleteImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        Assert.Equal(2, summary.Items.Single().ImagesDeleted);
        Assert.Equal(CleanupItemStatus.Deleted, summary.Items.Single().Status);
    }

    // TEST-007: summary email is sent with correct counts when EmailEnabled = true
    // (verified here via the orchestrator always invoking SendSummaryAsync with an
    // accurately-populated CleanupSummary; EmailEnabled gating itself is tested in
    // EmailNotificationServiceTests since that is where it is implemented).
    [Fact]
    public async Task RunAsync_SendsSummaryEmailWithCorrectCounts()
    {
        var deletedEntry = new ReportLogEntry("item-7a", RunNow.AddDays(-100), []);
        var skippedEntry = new ReportLogEntry("item-7b", null, []);
        var recentEntry = new ReportLogEntry("item-7c", RunNow.AddDays(-1), []);
        _sharePointMock.Setup(s => s.GetAllEntriesAsync(It.IsAny<CancellationToken>()))
            .Returns(AsAsyncEnumerable(deletedEntry, skippedEntry, recentEntry));

        CleanupSummary? capturedSummary = null;
        _emailMock.Setup(e => e.SendSummaryAsync(It.IsAny<CleanupSummary>(), It.IsAny<CancellationToken>()))
            .Callback<CleanupSummary, CancellationToken>((summary, _) => capturedSummary = summary)
            .Returns(Task.CompletedTask);

        var orchestrator = CreateOrchestrator(entryAgeDays: 90);
        await orchestrator.RunAsync(CancellationToken.None);

        _emailMock.Verify(e => e.SendSummaryAsync(It.IsAny<CleanupSummary>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(capturedSummary);
        Assert.Equal(3, capturedSummary!.EntriesScanned);
        Assert.Equal(1, capturedSummary.DeletedCount);
        Assert.Equal(1, capturedSummary.SkippedCount);
    }

    // TEST-008: a failure email is sent when the orchestrator run throws, and the
    // exception is rethrown so the Azure Functions host marks the invocation failed.
    [Fact]
    public async Task RunAsync_WhenUnderlyingServiceThrows_SendsFailureEmailAndRethrows()
    {
        _sharePointMock.Setup(s => s.GetAllEntriesAsync(It.IsAny<CancellationToken>()))
            .Returns(ThrowingAsyncEnumerable());

        var orchestrator = CreateOrchestrator(entryAgeDays: 90);

        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.RunAsync(CancellationToken.None));

        _emailMock.Verify(
            e => e.SendFailureAsync(It.IsAny<InvalidOperationException>(), RunNow, It.IsAny<CancellationToken>()),
            Times.Once);
        _emailMock.Verify(e => e.SendSummaryAsync(It.IsAny<CleanupSummary>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
