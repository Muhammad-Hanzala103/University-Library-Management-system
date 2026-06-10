using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Data.Repositories;
using KicsitLibrary.Services.Logging;
using KicsitLibrary.Services.Notifications;
using KicsitLibrary.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Tests;

public class OverdueNotificationTests
{
    [Fact]
    public async Task GetOverdueItemsAsync_ReturnsOnlyActiveOverdueIssues()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var overdue = await database.AddIssueAsync(data, LocalDateToUtc(DateTime.Now.Date.AddDays(-3)));
        var futureCopy = await database.AddCopyAsync(data);
        await database.AddIssueAsync(data, LocalDateToUtc(DateTime.Now.Date.AddDays(2)), futureCopy);
        var service = CreateServices(database).Overdue;

        var items = await service.GetOverdueItemsAsync();

        var item = Assert.Single(items);
        Assert.Equal(overdue.Id, item.IssueRecordId);
        Assert.True(item.DaysOverdue > 0);
        Assert.True(item.CurrentFineAmount >= 0);
    }

    [Fact]
    public async Task GetOverdueItemsAsync_DoesNotReturnReturnedIssue()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var issue = await database.AddIssueAsync(data, LocalDateToUtc(DateTime.Now.Date.AddDays(-3)));
        database.Context.ReceiveRecords.Add(new ReceiveRecord
        {
            IssueRecordId = issue.Id,
            ReceiveDate = issue.ExpectedReturnDate.AddDays(-1),
            ReceivedByUserId = data.User.Id,
            BookConditionAfterReturn = "Normal"
        });
        await database.Context.SaveChangesAsync();
        var service = CreateServices(database).Overdue;

        var items = await service.GetOverdueItemsAsync();

        Assert.Empty(items);
    }

    [Fact]
    public async Task GetOverdueItemsAsync_DoesNotReturnFutureDueIssue()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await database.AddIssueAsync(data, LocalDateToUtc(DateTime.Now.Date.AddDays(1)));
        var service = CreateServices(database).Overdue;

        var items = await service.GetOverdueItemsAsync();

        Assert.Empty(items);
    }

    [Fact]
    public async Task ProcessOverdueNotificationsAsync_CreatesInAppAndEmailRecords()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var issue = await database.AddIssueAsync(data, LocalDateToUtc(DateTime.Now.Date.AddDays(-2)));
        var service = CreateServices(database).Overdue;

        var result = await service.ProcessOverdueNotificationsAsync(data.User.Id);

        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(2, result.CreatedCount);
        var records = await database.Context.NotificationRecords
            .Where(record => record.IssueRecordId == issue.Id)
            .ToListAsync();
        Assert.Equal(2, records.Count);
        Assert.Contains(records, record => record.Channel == "InApp");
        Assert.Contains(records, record =>
            record.Channel == "Email" &&
            record.Status == NotificationStatus.Pending &&
            record.FailureReason == "Email delivery pending.");
    }

    [Fact]
    public async Task ProcessOverdueNotificationsAsync_RunningTwiceDoesNotDuplicateRecords()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await database.AddIssueAsync(data, LocalDateToUtc(DateTime.Now.Date.AddDays(-2)));
        var service = CreateServices(database).Overdue;

        await service.ProcessOverdueNotificationsAsync(data.User.Id);
        var second = await service.ProcessOverdueNotificationsAsync(data.User.Id);

        Assert.Equal(2, await database.Context.NotificationRecords.CountAsync());
        Assert.Equal(0, second.CreatedCount);
        Assert.Equal(2, second.SkippedCount);
    }

    [Fact]
    public async Task ProcessOverdueNotificationsAsync_MissingEmailCreatesFailedEmailRecord()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        data.Student.Email = string.Empty;
        await database.Context.SaveChangesAsync();
        await database.AddIssueAsync(data, LocalDateToUtc(DateTime.Now.Date.AddDays(-2)));
        var service = CreateServices(database).Overdue;

        var result = await service.ProcessOverdueNotificationsAsync(data.User.Id);

        var email = await database.Context.NotificationRecords
            .SingleAsync(record => record.Channel == "Email");
        Assert.Equal(NotificationStatus.Failed, email.Status);
        Assert.Equal("Member email is missing.", email.FailureReason);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(2, result.CreatedCount);
    }

    [Fact]
    public async Task NotificationCooldownHours_PreventsRepeatedReminderAcrossDates()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var setting = await database.Context.SystemSettings
            .SingleAsync(item => item.Key == "NotificationCooldownHours");
        setting.Value = "48";
        await database.Context.SaveChangesAsync();
        await database.AddIssueAsync(data, LocalDateToUtc(DateTime.Now.Date.AddDays(-4)));
        var service = CreateServices(database).Overdue;
        await service.ProcessOverdueNotificationsAsync(data.User.Id);

        var records = await database.Context.NotificationRecords.ToListAsync();
        foreach (var record in records)
        {
            record.CreatedAt = DateTime.UtcNow.AddHours(-25);
            record.DeduplicationKey = $"{record.IssueRecordId}:{record.NotificationType}:{record.Channel}:previous";
        }
        await database.Context.SaveChangesAsync();

        var second = await service.ProcessOverdueNotificationsAsync(data.User.Id);

        Assert.Equal(2, await database.Context.NotificationRecords.CountAsync());
        Assert.Equal(0, second.CreatedCount);
        Assert.Equal(2, second.SkippedCount);
    }

    [Fact]
    public async Task MarkAsReadAsync_SetsReadAt()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await database.AddIssueAsync(data, LocalDateToUtc(DateTime.Now.Date.AddDays(-2)));
        var services = CreateServices(database);
        await services.Overdue.ProcessOverdueNotificationsAsync(data.User.Id);
        var record = await database.Context.NotificationRecords.FirstAsync();

        var updated = await services.Notification.MarkAsReadAsync(record.Id, data.User.Id);

        Assert.NotNull(updated.ReadAt);
    }

    [Fact]
    public async Task RetryNotificationRecordAsync_IncrementsRetryCountWithoutSending()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await database.AddIssueAsync(data, LocalDateToUtc(DateTime.Now.Date.AddDays(-2)));
        var services = CreateServices(database);
        await services.Overdue.ProcessOverdueNotificationsAsync(data.User.Id);
        var email = await database.Context.NotificationRecords
            .SingleAsync(record => record.Channel == "Email");
        await database.ConfigureValidEmailSettingsAsync();

        var result = await services.Notification.RetryNotificationRecordAsync(email.Id, data.User.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Notification!.RetryCount);
        Assert.Equal(NotificationStatus.Sent, result.Notification.Status);
        Assert.NotNull(result.Notification.SentAt);
    }

    private static (OverdueService Overdue, NotificationService Notification) CreateServices(
        SqliteTestDatabase database)
    {
        var logService = new ActivityLogService(new Repository<ActivityLog>(database.Context));
        var notificationService = new NotificationService(
            database.Context,
            logService,
            new FakeEmailTransport(),
            new EmailSettingsService(database.Context));
        return (
            new OverdueService(database.Context, notificationService, logService),
            notificationService);
    }

    private static DateTime LocalDateToUtc(DateTime localDate)
    {
        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(localDate.AddHours(12), DateTimeKind.Unspecified),
            TimeZoneInfo.Local);
    }
}
