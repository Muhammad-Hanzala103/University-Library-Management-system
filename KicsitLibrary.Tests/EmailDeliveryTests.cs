using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data.Repositories;
using KicsitLibrary.Services.Logging;
using KicsitLibrary.Services.Notifications;
using KicsitLibrary.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Tests;

public class EmailDeliveryTests
{
    [Fact]
    public async Task SendNotificationAsync_SendsPendingEmailWhenSettingsAreValid()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await database.ConfigureValidEmailSettingsAsync();
        var notification = await database.AddNotificationAsync(data);
        var services = CreateServices(database);

        var result = await services.Service.SendNotificationAsync(notification.Id, data.User.Id);

        Assert.True(result.Succeeded);
        Assert.True(result.Attempted);
        Assert.Equal(1, services.Transport.SendCount);
        Assert.Equal(data.Student.Email, services.Transport.Messages.Single().ToEmail);
    }

    [Fact]
    public async Task SuccessfulSend_SetsStatusToSentAndSentAt()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await database.ConfigureValidEmailSettingsAsync();
        var notification = await database.AddNotificationAsync(data);
        var services = CreateServices(database);

        await services.Service.SendNotificationAsync(notification.Id, data.User.Id);
        await database.Context.Entry(notification).ReloadAsync();

        Assert.Equal(NotificationStatus.Sent, notification.Status);
        Assert.NotNull(notification.SentAt);
        Assert.Null(notification.FailureReason);
        Assert.Equal(1, notification.RetryCount);
        Assert.NotNull(notification.LastAttemptAt);
    }

    [Fact]
    public async Task FailedTransport_SetsFailedStatusAndReason()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await database.ConfigureValidEmailSettingsAsync();
        var notification = await database.AddNotificationAsync(data);
        var services = CreateServices(database);
        services.Transport.EnqueueResult(new EmailSendResult
        {
            Succeeded = false,
            FailureReason = "SMTP server unavailable."
        });

        var result = await services.Service.SendNotificationAsync(notification.Id, data.User.Id);

        Assert.False(result.Succeeded);
        Assert.True(result.Attempted);
        Assert.Equal(NotificationStatus.Failed, result.Notification!.Status);
        Assert.Equal("SMTP server unavailable.", result.Notification.FailureReason);
    }

    [Fact]
    public async Task MissingRecipientEmail_FailsWithoutCallingTransport()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await database.ConfigureValidEmailSettingsAsync();
        var notification = await database.AddNotificationAsync(data, recipientEmail: null);
        var services = CreateServices(database);

        var result = await services.Service.SendNotificationAsync(notification.Id, data.User.Id);

        Assert.False(result.Succeeded);
        Assert.False(result.Attempted);
        Assert.Equal(0, services.Transport.SendCount);
        Assert.Equal(NotificationStatus.Failed, result.Notification!.Status);
        Assert.Equal("Recipient email is missing.", result.Notification.FailureReason);
    }

    [Fact]
    public async Task EmailDisabled_DoesNotCallTransportAndLeavesPendingReason()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await database.ConfigureValidEmailSettingsAsync(enabled: false);
        var notification = await database.AddNotificationAsync(data);
        var services = CreateServices(database);

        var result = await services.Service.SendNotificationAsync(notification.Id, data.User.Id);

        Assert.False(result.Attempted);
        Assert.Equal(0, services.Transport.SendCount);
        Assert.Equal(NotificationStatus.Pending, result.Notification!.Status);
        Assert.Equal("Email notifications are disabled.", result.Notification.FailureReason);
        Assert.Equal(0, result.Notification.RetryCount);
    }

    [Fact]
    public async Task RetryNotificationRecordAsync_IncrementsRetryCountOnAttempt()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await database.ConfigureValidEmailSettingsAsync();
        var notification = await database.AddNotificationAsync(
            data,
            status: NotificationStatus.Failed);
        var services = CreateServices(database);

        var result = await services.Service.RetryNotificationRecordAsync(notification.Id, data.User.Id);

        Assert.True(result.Attempted);
        Assert.Equal(1, result.Notification!.RetryCount);
        Assert.Equal(1, services.Transport.SendCount);
    }

    [Fact]
    public async Task RetryNotificationRecordAsync_DoesNotExceedMaximumRetryCount()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await database.ConfigureValidEmailSettingsAsync(maxRetryCount: 1);
        var notification = await database.AddNotificationAsync(
            data,
            status: NotificationStatus.Failed);
        notification.RetryCount = 1;
        await database.Context.SaveChangesAsync();
        var services = CreateServices(database);

        var result = await services.Service.RetryNotificationRecordAsync(notification.Id, data.User.Id);

        Assert.False(result.Attempted);
        Assert.Equal(0, services.Transport.SendCount);
        Assert.Equal(1, result.Notification!.RetryCount);
        Assert.Equal("Maximum retry count of 1 reached.", result.Notification.FailureReason);
    }

    [Fact]
    public async Task SendPendingEmailNotificationsAsync_ProcessesOnlyPendingEmailRecords()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await database.ConfigureValidEmailSettingsAsync();
        await database.AddNotificationAsync(data);
        await database.AddNotificationAsync(data, status: NotificationStatus.Sent);
        await database.AddNotificationAsync(data, status: NotificationStatus.Failed);
        await database.AddNotificationAsync(data, channel: "InApp");
        var services = CreateServices(database);

        var result = await services.Service.SendPendingEmailNotificationsAsync(data.User.Id);

        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(1, result.SentCount);
        Assert.Equal(1, services.Transport.SendCount);
    }

    [Fact]
    public async Task InAppNotification_IsNotSentThroughSmtp()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await database.ConfigureValidEmailSettingsAsync();
        var notification = await database.AddNotificationAsync(data, channel: "InApp");
        var services = CreateServices(database);

        var result = await services.Service.SendNotificationAsync(notification.Id, data.User.Id);

        Assert.False(result.Attempted);
        Assert.Equal(0, services.Transport.SendCount);
        Assert.Contains("Only email", result.Message);
    }

    [Fact]
    public async Task SmtpPassword_IsRedactedFromNotificationAndActivityLog()
    {
        const string password = "secret-value-123";
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await database.ConfigureValidEmailSettingsAsync(password: password);
        var notification = await database.AddNotificationAsync(data);
        var services = CreateServices(database);
        services.Transport.EnqueueResult(new EmailSendResult
        {
            Succeeded = false,
            FailureReason = $"Authentication failed for password {password}."
        });

        var result = await services.Service.SendNotificationAsync(notification.Id, data.User.Id);
        var logs = await database.Context.ActivityLogs.ToListAsync();

        Assert.DoesNotContain(password, result.Notification!.FailureReason);
        Assert.All(logs, log => Assert.DoesNotContain(password, log.Detail));
        Assert.Contains("[REDACTED]", result.Notification.FailureReason);
    }

    private static (NotificationService Service, FakeEmailTransport Transport) CreateServices(
        SqliteTestDatabase database)
    {
        var logService = new ActivityLogService(new Repository<ActivityLog>(database.Context));
        var transport = new FakeEmailTransport();
        return (
            new NotificationService(
                database.Context,
                logService,
                transport,
                new EmailSettingsService(database.Context)),
            transport);
    }
}
