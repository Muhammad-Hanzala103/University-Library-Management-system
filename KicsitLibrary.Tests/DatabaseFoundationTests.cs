using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Data.Repositories;
using KicsitLibrary.Services.Logging;
using KicsitLibrary.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Tests;

public class DatabaseFoundationTests
{
    [Fact]
    public async Task SeedData_CanBeInsertedIntoFreshSqliteDatabase()
    {
        await using var database = await SqliteTestDatabase.CreateAsync(seed: true);

        Assert.True(await database.Context.Users.AnyAsync());
        Assert.True(await database.Context.Roles.AnyAsync());
        Assert.True(await database.Context.SystemSettings.AnyAsync());
        var requiredSettings = new[]
        {
            "FinePerDay",
            "NotificationCooldownHours",
            "EmailNotificationEnabled",
            "WhatsAppNotificationEnabled",
            "ReminderBeforeDueDays",
            "MaxNotificationRetryCount",
            "SmtpHost",
            "SmtpPort",
            "SmtpUseSsl",
            "SmtpUser",
            "SmtpPassword",
            "SmtpFromEmail",
            "SmtpFromName"
        };
        foreach (var key in requiredSettings)
        {
            Assert.True(await database.Context.SystemSettings.AnyAsync(setting => setting.Key == key));
        }

        var emailEnabled = await database.Context.SystemSettings
            .SingleAsync(setting => setting.Key == "EmailNotificationEnabled");
        Assert.Equal("False", emailEnabled.Value);
    }

    [Fact]
    public async Task NotificationRecord_CanBeCreatedWithoutSendingEmail()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var notification = new NotificationRecord
        {
            MemberType = MemberType.Student,
            StudentId = data.Student.Id,
            NotificationType = NotificationType.OverdueReminder,
            Channel = "Email",
            Subject = "Test reminder",
            Message = "Test only",
            Status = NotificationStatus.Pending
        };

        database.Context.NotificationRecords.Add(notification);
        await database.Context.SaveChangesAsync();

        var stored = await database.Context.NotificationRecords.SingleAsync();
        Assert.Equal(NotificationStatus.Pending, stored.Status);
        Assert.Null(stored.SentAt);
        Assert.Null(stored.FailureReason);
    }

    [Fact]
    public async Task ActivityLog_CanBeWritten()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var service = new ActivityLogService(new Repository<ActivityLog>(database.Context));

        await service.LogActivityAsync("Test Action", "Test detail", data.User.Id);

        var stored = await database.Context.ActivityLogs.SingleAsync();
        Assert.Equal("Test Action", stored.Action);
        Assert.Equal(data.User.Id, stored.UserId);
    }
}
