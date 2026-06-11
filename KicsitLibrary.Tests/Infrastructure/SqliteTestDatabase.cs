using KicsitLibrary.Core;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Data;
using KicsitLibrary.Services.Authentication;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Tests.Infrastructure;

internal sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly string _databasePath;

    private SqliteTestDatabase(string databasePath, KicsitLibraryDbContext context)
    {
        _databasePath = databasePath;
        ConnectionString = $"Data Source={databasePath};Pooling=False";
        Context = context;
    }

    public KicsitLibraryDbContext Context { get; }
    public string ConnectionString { get; }
    public string DatabasePath => _databasePath;

    public static async Task<SqliteTestDatabase> CreateAsync(bool seed = false)
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            "KicsitLibrary.Tests",
            "Databases",
            Guid.NewGuid().ToString("N"),
            "KicsitLibrary.Tests.db");
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        var options = new DbContextOptionsBuilder<KicsitLibraryDbContext>()
            .UseSqlite($"Data Source={databasePath};Pooling=False")
            .Options;

        var context = new KicsitLibraryDbContext(options);
        await context.Database.EnsureCreatedAsync();

        if (seed)
        {
            await DbSeeder.SeedAsync(context, new PasswordHasher());
        }

        return new SqliteTestDatabase(databasePath, context);
    }

    public async Task<TestLibraryData> AddCirculationDataAsync()
    {
        var user = new User
        {
            Username = $"tester-{Guid.NewGuid():N}",
            PasswordHash = "test-only",
            FullName = "Test Librarian",
            Email = "librarian@test.invalid"
        };
        var student = new Student
        {
            RegistrationNumber = new string(Guid.NewGuid().ToString("N").Where(char.IsDigit).Take(10).ToArray()),
            AdmissionNumber = "ADM-1",
            RollNumber = "ROLL-1",
            Name = "Test Student",
            FatherName = "Test Parent",
            Program = "BSCS",
            Department = "CS",
            Batch = "2026",
            Semester = "1",
            Session = "2026-2030",
            Email = "student@test.invalid",
            Phone = "0000000000",
            Address = "Test Address",
            PageNumber = 1,
            RegisterNumber = 1
        };
        var publisher = new Publisher { Name = "Test Publisher" };
        var category = new Category { Name = "Test Category" };
        var department = new DepartmentCategory { Name = "Test Department" };
        var literature = new LiteratureCategory { Name = "Test Literature" };

        Context.AddRange(user, student, publisher, category, department, literature);
        await Context.SaveChangesAsync();

        var book = new BookMaster
        {
            Title = "Test Book",
            UniqueTitleNumber = $"TITLE-{Guid.NewGuid():N}",
            PublisherId = publisher.Id,
            CategoryId = category.Id,
            DepartmentCategoryId = department.Id,
            LiteratureCategoryId = literature.Id,
            PublicationYear = 2026,
            PurchasePrice = 1000,
            Status = BookStatus.Available
        };
        Context.BookMasters.Add(book);
        await Context.SaveChangesAsync();

        var copy = new BookCopy
        {
            AccessionNumber = $"ACC-{Guid.NewGuid():N}",
            BookMasterId = book.Id,
            CopyNumber = 1,
            AvailabilityStatus = BookStatus.Available
        };
        Context.BookCopies.Add(copy);
        Context.SystemSettings.AddRange(
            new SystemSettings { Key = "StudentIssueLimit", Value = "3", Group = "Borrowing" },
            new SystemSettings { Key = "DefaultIssueDays", Value = "14", Group = "Borrowing" },
            new SystemSettings { Key = "FinePerDay", Value = "10", Group = "Billing" },
            new SystemSettings { Key = "ReservationExpiryDays", Value = "3", Group = "Reservation" },
            new SystemSettings { Key = "NotificationCooldownHours", Value = "24", Group = "Notifications" },
            new SystemSettings { Key = "MaxNotificationRetryCount", Value = "3", Group = "Notifications" });
        await Context.SaveChangesAsync();

        return new TestLibraryData(user, student, book, copy);
    }

    public async Task<BookCopy> AddCopyAsync(TestLibraryData data)
    {
        var copy = new BookCopy
        {
            AccessionNumber = $"ACC-{Guid.NewGuid():N}",
            BookMasterId = data.Book.Id,
            CopyNumber = await Context.BookCopies.CountAsync() + 1,
            AvailabilityStatus = BookStatus.Issued
        };
        Context.BookCopies.Add(copy);
        await Context.SaveChangesAsync();
        return copy;
    }

    public async Task<IssueRecord> AddIssueAsync(
        TestLibraryData data,
        DateTime expectedReturnDateUtc,
        BookCopy? copy = null)
    {
        copy ??= data.Copy;
        copy.AvailabilityStatus = BookStatus.Issued;
        var issue = new IssueRecord
        {
            AccessionNumber = copy.AccessionNumber,
            BookCopyId = copy.Id,
            MemberType = MemberType.Student,
            StudentId = data.Student.Id,
            IssueDate = expectedReturnDateUtc.AddDays(-14),
            ExpectedReturnDate = expectedReturnDateUtc,
            FinePerDay = 10,
            IssuedByUserId = data.User.Id
        };
        Context.IssueRecords.Add(issue);
        await Context.SaveChangesAsync();
        return issue;
    }

    public async Task ConfigureValidEmailSettingsAsync(
        bool enabled = true,
        int maxRetryCount = 3,
        string password = "development-test-password")
    {
        var values = new Dictionary<string, string>
        {
            ["EmailNotificationEnabled"] = enabled.ToString(),
            ["MaxNotificationRetryCount"] = maxRetryCount.ToString(),
            ["SmtpHost"] = "smtp.test.invalid",
            ["SmtpPort"] = "587",
            ["SmtpUseSsl"] = "True",
            ["SmtpUser"] = "test-user",
            ["SmtpPassword"] = password,
            ["SmtpFromEmail"] = "library@test.invalid",
            ["SmtpFromName"] = ProductBrand.Name
        };

        foreach (var value in values)
        {
            var setting = await Context.SystemSettings
                .FirstOrDefaultAsync(item => item.Key == value.Key);
            if (setting == null)
            {
                Context.SystemSettings.Add(new SystemSettings
                {
                    Key = value.Key,
                    Value = value.Value,
                    Group = "Notifications"
                });
            }
            else
            {
                setting.Value = value.Value;
            }
        }

        await Context.SaveChangesAsync();
    }

    public async Task ConfigureSchedulerSettingsAsync(
        bool enabled,
        bool runOnStartup = false,
        bool sendPendingEmails = false,
        int intervalMinutes = 60,
        int initialDelaySeconds = 30,
        int maxRunMinutes = 10)
    {
        var values = new Dictionary<string, string>
        {
            ["OverdueSchedulerEnabled"] = enabled.ToString(),
            ["OverdueSchedulerRunOnStartup"] = runOnStartup.ToString(),
            ["OverdueSchedulerIntervalMinutes"] = intervalMinutes.ToString(),
            ["OverdueSchedulerInitialDelaySeconds"] = initialDelaySeconds.ToString(),
            ["OverdueSchedulerSendPendingEmails"] = sendPendingEmails.ToString(),
            ["OverdueSchedulerMaxRunMinutes"] = maxRunMinutes.ToString(),
            ["OverdueSchedulerLastRunAt"] = string.Empty,
            ["OverdueSchedulerLastSuccessAt"] = string.Empty,
            ["OverdueSchedulerLastFailureAt"] = string.Empty,
            ["OverdueSchedulerLastMessage"] = string.Empty,
            ["OverdueSchedulerIsRunning"] = "False"
        };

        foreach (var value in values)
        {
            await SetSystemSettingAsync(value.Key, value.Value, "Scheduler");
        }
    }

    public async Task ConfigureAutomaticBackupSettingsAsync(
        bool enabled,
        string destinationFolder,
        bool runOnStartup = false,
        int intervalHours = 24,
        int initialDelaySeconds = 60,
        bool compress = false,
        bool verifyAfterCreation = true,
        bool retentionEnabled = false,
        int retentionDays = 30,
        int maxHistoryRows = 500,
        bool deletePhysicalFiles = false)
    {
        var values = new Dictionary<string, string>
        {
            ["AutomaticBackupEnabled"] = enabled.ToString(),
            ["AutomaticBackupRunOnStartup"] = runOnStartup.ToString(),
            ["AutomaticBackupIntervalHours"] = intervalHours.ToString(),
            ["AutomaticBackupInitialDelaySeconds"] = initialDelaySeconds.ToString(),
            ["AutomaticBackupCompress"] = compress.ToString(),
            ["AutomaticBackupVerifyAfterCreation"] = verifyAfterCreation.ToString(),
            ["AutomaticBackupDestinationFolder"] = destinationFolder,
            ["AutomaticBackupRetentionEnabled"] = retentionEnabled.ToString(),
            ["AutomaticBackupRetentionDays"] = retentionDays.ToString(),
            ["AutomaticBackupMaxHistoryRows"] = maxHistoryRows.ToString(),
            ["AutomaticBackupDeletePhysicalFiles"] = deletePhysicalFiles.ToString(),
            ["AutomaticBackupLastRunAt"] = string.Empty,
            ["AutomaticBackupLastSuccessAt"] = string.Empty,
            ["AutomaticBackupLastFailureAt"] = string.Empty,
            ["AutomaticBackupLastMessage"] = string.Empty,
            ["AutomaticBackupIsRunning"] = "False"
        };

        foreach (var value in values)
        {
            await SetSystemSettingAsync(
                value.Key,
                value.Value,
                "AutomaticBackup");
        }
    }

    public async Task SetSystemSettingAsync(
        string key,
        string value,
        string group = "Scheduler")
    {
        var setting = await Context.SystemSettings
            .FirstOrDefaultAsync(item => item.Key == key);
        if (setting == null)
        {
            Context.SystemSettings.Add(new SystemSettings
            {
                Key = key,
                Value = value,
                Group = group
            });
        }
        else
        {
            setting.Value = value;
            setting.Group = group;
        }

        await Context.SaveChangesAsync();
    }

    public async Task<NotificationRecord> AddNotificationAsync(
        TestLibraryData data,
        string channel = "Email",
        NotificationStatus status = NotificationStatus.Pending,
        string? recipientEmail = "student@test.invalid")
    {
        var notification = new NotificationRecord
        {
            MemberType = MemberType.Student,
            StudentId = data.Student.Id,
            NotificationType = NotificationType.OverdueReminder,
            Channel = channel,
            RecipientName = data.Student.Name,
            RecipientCode = data.Student.RegistrationNumber,
            RecipientEmail = recipientEmail,
            Subject = "Test notification",
            Message = "Test notification body",
            Status = status,
            FailureReason = status == NotificationStatus.Pending
                ? "Email delivery pending."
                : null
        };
        Context.NotificationRecords.Add(notification);
        await Context.SaveChangesAsync();
        return notification;
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();

        var databaseDirectory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(databaseDirectory) &&
            Directory.Exists(databaseDirectory) &&
            databaseDirectory.StartsWith(
                Path.Combine(Path.GetTempPath(), "KicsitLibrary.Tests", "Databases"),
                StringComparison.OrdinalIgnoreCase))
        {
            Directory.Delete(databaseDirectory, recursive: true);
        }
        else if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}

internal sealed record TestLibraryData(
    User User,
    Student Student,
    BookMaster Book,
    BookCopy Copy);
