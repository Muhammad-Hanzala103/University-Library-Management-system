using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data;
using KicsitLibrary.Data.Repositories;
using KicsitLibrary.Services.Logging;
using KicsitLibrary.Services.Notifications;
using KicsitLibrary.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KicsitLibrary.Tests;

public class OverdueSchedulerTests
{
    [Fact]
    public async Task RunAsync_DoesNothingWhenSchedulerIsDisabled()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await database.AddIssueAsync(data, LocalDateToUtc(DateTime.Now.Date.AddDays(-2)));
        await database.ConfigureSchedulerSettingsAsync(enabled: false);
        using var harness = CreateHarness(database);

        var result = await harness.Scheduler.RunAsync(data.User.Id);

        Assert.True(result.WasSkipped);
        Assert.False(result.Succeeded);
        Assert.Equal("Scheduler is disabled.", result.Message);
        Assert.Empty(await database.Context.NotificationRecords.ToListAsync());
        Assert.Equal(0, harness.Transport.SendCount);
    }

    [Fact]
    public async Task RunAsync_CreatesOverdueNotificationRecordsWhenEnabled()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await database.AddIssueAsync(data, LocalDateToUtc(DateTime.Now.Date.AddDays(-2)));
        await database.ConfigureSchedulerSettingsAsync(enabled: true);
        using var harness = CreateHarness(database);

        var result = await harness.Scheduler.RunAsync(data.User.Id);

        database.Context.ChangeTracker.Clear();
        Assert.True(result.Succeeded);
        Assert.Equal(2, result.CreatedCount);
        Assert.Equal(2, await database.Context.NotificationRecords.CountAsync());
    }

    [Fact]
    public async Task RunAsync_DoesNotDuplicateRecordsWhenRunTwice()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await database.AddIssueAsync(data, LocalDateToUtc(DateTime.Now.Date.AddDays(-2)));
        await database.ConfigureSchedulerSettingsAsync(enabled: true);
        using var harness = CreateHarness(database);

        await harness.Scheduler.RunAsync(data.User.Id);
        var second = await harness.Scheduler.RunAsync(data.User.Id);

        database.Context.ChangeTracker.Clear();
        Assert.Equal(0, second.CreatedCount);
        Assert.Equal(2, second.SkippedCount);
        Assert.Equal(2, await database.Context.NotificationRecords.CountAsync());
    }

    [Fact]
    public async Task RunAsync_DoesNotSendEmailsWhenAutomaticDeliveryIsDisabled()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await database.AddIssueAsync(data, LocalDateToUtc(DateTime.Now.Date.AddDays(-2)));
        await database.ConfigureSchedulerSettingsAsync(
            enabled: true,
            sendPendingEmails: false);
        await database.ConfigureValidEmailSettingsAsync();
        using var harness = CreateHarness(database);

        var result = await harness.Scheduler.RunAsync(data.User.Id);

        database.Context.ChangeTracker.Clear();
        Assert.True(result.Succeeded);
        Assert.Equal(0, result.EmailsAttempted);
        Assert.Equal(0, harness.Transport.SendCount);
        Assert.Equal(
            NotificationStatus.Pending,
            (await database.Context.NotificationRecords.SingleAsync(
                record => record.Channel == "Email")).Status);
    }

    [Fact]
    public async Task RunAsync_SendsPendingEmailsWhenAutomaticDeliveryIsEnabled()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await database.AddIssueAsync(data, LocalDateToUtc(DateTime.Now.Date.AddDays(-2)));
        await database.ConfigureSchedulerSettingsAsync(
            enabled: true,
            sendPendingEmails: true);
        await database.ConfigureValidEmailSettingsAsync();
        using var harness = CreateHarness(database);

        var result = await harness.Scheduler.RunAsync(data.User.Id);

        database.Context.ChangeTracker.Clear();
        Assert.True(result.Succeeded);
        Assert.Equal(1, result.EmailsAttempted);
        Assert.Equal(1, result.EmailsSent);
        Assert.Equal(1, harness.Transport.SendCount);
        Assert.Equal(
            NotificationStatus.Sent,
            (await database.Context.NotificationRecords.SingleAsync(
                record => record.Channel == "Email")).Status);
    }

    [Fact]
    public async Task RunAsync_RespectsNotificationRetryLimit()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await database.ConfigureSchedulerSettingsAsync(
            enabled: true,
            sendPendingEmails: true);
        await database.ConfigureValidEmailSettingsAsync(maxRetryCount: 1);
        var notification = await database.AddNotificationAsync(data);
        notification.RetryCount = 1;
        await database.Context.SaveChangesAsync();
        using var harness = CreateHarness(database);

        var result = await harness.Scheduler.RunAsync(data.User.Id);

        database.Context.ChangeTracker.Clear();
        var stored = await database.Context.NotificationRecords.SingleAsync();
        Assert.False(result.Succeeded);
        Assert.Equal(0, result.EmailsAttempted);
        Assert.Equal(1, result.EmailsFailed);
        Assert.Equal(0, harness.Transport.SendCount);
        Assert.Equal(NotificationStatus.Failed, stored.Status);
        Assert.Contains("Maximum retry count", stored.FailureReason);
    }

    [Fact]
    public async Task RunAsync_PreventsOverlappingRuns()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await database.ConfigureSchedulerSettingsAsync(enabled: true);
        var blockingService = new BlockingOverdueService();
        using var harness = CreateHarness(database, blockingService);

        var firstRun = harness.Scheduler.RunAsync();
        await blockingService.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var secondRun = await harness.Scheduler.RunAsync();
        blockingService.Release.TrySetResult();
        var firstResult = await firstRun;

        Assert.True(firstResult.Succeeded);
        Assert.True(secondRun.WasSkipped);
        Assert.Contains("another run is active", secondRun.Message);
        Assert.Equal(1, blockingService.RunCount);
    }

    [Fact]
    public async Task RunAsync_StoresLastRunStatusAndUsesIntervalFallback()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await database.ConfigureSchedulerSettingsAsync(enabled: true);
        await database.SetSystemSettingAsync(
            "OverdueSchedulerIntervalMinutes",
            "invalid");
        using var harness = CreateHarness(database);

        var result = await harness.Scheduler.RunAsync();
        var status = await harness.Scheduler.GetStatusAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(60, status.IntervalMinutes);
        Assert.NotNull(status.LastRunAt);
        Assert.NotNull(status.LastSuccessAt);
        Assert.False(status.IsRunning);
        Assert.Contains("Processed", status.LastMessage);
    }

    [Fact]
    public async Task RunAsync_RecordsDependencyFailure()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await database.ConfigureSchedulerSettingsAsync(enabled: true);
        using var harness = CreateHarness(
            database,
            new ThrowingOverdueService("Dependency failed."));

        var result = await harness.Scheduler.RunAsync();
        var status = await harness.Scheduler.GetStatusAsync();

        Assert.False(result.Succeeded);
        Assert.Contains("Dependency failed.", result.FailureReason);
        Assert.NotNull(status.LastFailureAt);
        Assert.Contains("Dependency failed.", status.LastMessage);
        Assert.Contains(
            await database.Context.ActivityLogs.ToListAsync(),
            log => log.Action == "Overdue Scheduler Failed");
    }

    [Fact]
    public async Task RunAsync_HonorsCancellationToken()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await database.ConfigureSchedulerSettingsAsync(enabled: true);
        var cancellableService = new CancellableOverdueService();
        using var harness = CreateHarness(database, cancellableService);
        using var cancellation = new CancellationTokenSource();

        var run = harness.Scheduler.RunAsync(cancellationToken: cancellation.Token);
        await cancellableService.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        var result = await run;
        var status = await harness.Scheduler.GetStatusAsync();

        Assert.False(result.Succeeded);
        Assert.Contains("cancelled", result.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.False(status.IsRunning);
        Assert.NotNull(status.LastFailureAt);
    }

    private static SchedulerHarness CreateHarness(
        SqliteTestDatabase database,
        IOverdueService? overdueService = null)
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<KicsitLibraryDbContext>(options =>
            options.UseSqlite(database.ConnectionString));
        services.AddScoped(provider =>
            provider.GetRequiredService<IDbContextFactory<KicsitLibraryDbContext>>()
                .CreateDbContext());
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IActivityLogService, ActivityLogService>();
        services.AddScoped<IEmailSettingsService, EmailSettingsService>();
        var transport = new FakeEmailTransport();
        services.AddSingleton<IEmailTransport>(transport);
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IOverdueService, OverdueService>();
        if (overdueService != null)
        {
            services.AddScoped(_ => overdueService);
        }
        services.AddSingleton<IOverdueSchedulerService, OverdueSchedulerService>();

        var provider = services.BuildServiceProvider();
        return new SchedulerHarness(
            provider,
            provider.GetRequiredService<IOverdueSchedulerService>(),
            transport);
    }

    private static DateTime LocalDateToUtc(DateTime localDate)
    {
        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(localDate.AddHours(12), DateTimeKind.Unspecified),
            TimeZoneInfo.Local);
    }

    private sealed record SchedulerHarness(
        ServiceProvider Provider,
        IOverdueSchedulerService Scheduler,
        FakeEmailTransport Transport) : IDisposable
    {
        public void Dispose()
        {
            Provider.Dispose();
        }
    }

    private abstract class TestOverdueService : IOverdueService
    {
        public virtual Task<IReadOnlyList<OverdueItem>> GetOverdueItemsAsync(
            DateTime? localDate = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<OverdueItem>>([]);
        }

        public abstract Task<OverdueProcessingResult> ProcessOverdueNotificationsAsync(
            int? userId = null,
            CancellationToken cancellationToken = default);

        public Task<OverdueProcessingResult> CreateReminderForIssueAsync(
            int issueRecordId,
            int? userId = null)
        {
            throw new NotSupportedException();
        }

        public Task<NotificationRecord?> GetLastReminderAsync(int issueRecordId)
        {
            throw new NotSupportedException();
        }

        public Task<NotificationEligibilityResult> CanCreateReminderAsync(
            int issueRecordId,
            DateTime? asOfUtc = null)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class BlockingOverdueService : TestOverdueService
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int RunCount { get; private set; }

        public override async Task<OverdueProcessingResult> ProcessOverdueNotificationsAsync(
            int? userId = null,
            CancellationToken cancellationToken = default)
        {
            RunCount++;
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return new OverdueProcessingResult();
        }
    }

    private sealed class ThrowingOverdueService(string message) : TestOverdueService
    {
        public override Task<OverdueProcessingResult> ProcessOverdueNotificationsAsync(
            int? userId = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException<OverdueProcessingResult>(
                new InvalidOperationException(message));
        }
    }

    private sealed class CancellableOverdueService : TestOverdueService
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task<OverdueProcessingResult> ProcessOverdueNotificationsAsync(
            int? userId = null,
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new OverdueProcessingResult();
        }
    }
}
