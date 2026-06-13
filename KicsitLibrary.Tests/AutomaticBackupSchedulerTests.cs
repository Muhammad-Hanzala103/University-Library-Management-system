using System.IO.Compression;
using System.Text.Json;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data;
using KicsitLibrary.Services.Backup;
using KicsitLibrary.Services.Restore;
using KicsitLibrary.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KicsitLibrary.Tests;

public class AutomaticBackupSchedulerTests
{
    [Fact]
    public async Task Scheduler_IsDisabledByDefaultAndDoesNothing()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var user = await CreateUserAsync(database);
        using var harness = CreateHarness(database, user, true);

        var result = await harness.Scheduler.RunScheduledBackupAsync();
        var settings = await harness.Scheduler.GetSchedulerSettingsAsync();

        Assert.True(result.WasSkipped);
        Assert.False(result.Succeeded);
        Assert.False(settings.Enabled);
        Assert.Empty(await database.Context.BackupHistories.ToListAsync());
    }

    [Fact]
    public async Task RunBackupNow_CreatesRealVerifiedBackupWhenAuthorized()
    {
        await using var environment = await AutomaticBackupTestEnvironment.CreateAsync();
        await environment.ConfigureAsync(enabled: false);

        var result = await environment.Harness.Scheduler.RunBackupNowAsync();
        var history = await environment.Database.Context.BackupHistories.SingleAsync();

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(File.Exists(result.BackupFilePath));
        Assert.Equal("Passed", history.VerificationStatus);
        Assert.Equal("Completed", history.Status);
    }

    [Fact]
    public async Task Scheduler_PreventsOverlappingRuns()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var user = await CreateUserAsync(database);
        var backupFolder = CreateTempFolder("AutomaticOverlap");
        await database.ConfigureAutomaticBackupSettingsAsync(true, backupFolder);
        var blocking = new BlockingBackupService();
        using var harness = CreateHarness(
            database,
            user,
            true,
            backupService: blocking);

        var first = harness.Scheduler.RunScheduledBackupAsync();
        await blocking.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = await harness.Scheduler.RunScheduledBackupAsync();
        blocking.Release.TrySetResult();
        var firstResult = await first;

        Assert.True(firstResult.Succeeded);
        Assert.True(second.WasSkipped);
        Assert.Contains("another scheduler run", second.Message);
        Assert.Equal(1, blocking.RunCount);
        Directory.Delete(backupFolder, recursive: true);
    }

    [Fact]
    public async Task Scheduler_SkipsWhenPendingRestoreExists()
    {
        await using var environment = await AutomaticBackupTestEnvironment.CreateAsync();
        await environment.ConfigureAsync(enabled: true);
        var pendingPath = PendingRestoreProcessor.GetPendingRequestPath(
            environment.Database.DatabasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(pendingPath)!);
        await File.WriteAllTextAsync(pendingPath, "{}");

        var result = await environment.Harness.Scheduler.RunScheduledBackupAsync();

        Assert.True(result.WasSkipped);
        Assert.Contains("restore is pending", result.Message);
        Assert.Empty(await environment.Database.Context.BackupHistories.ToListAsync());
    }

    [Fact]
    public async Task Scheduler_UpdatesLastRunStatusSettings()
    {
        await using var environment = await AutomaticBackupTestEnvironment.CreateAsync();
        await environment.ConfigureAsync(enabled: false);

        var result = await environment.Harness.Scheduler.RunBackupNowAsync();
        var status = await environment.Harness.Scheduler.GetSchedulerStatusAsync();

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.NotNull(status.LastRunAt);
        Assert.NotNull(status.LastSuccessAt);
        Assert.False(status.IsRunning);
        Assert.Contains("completed", status.LastMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scheduler_UsesExistingBackupServiceAndStoresBackupHistory()
    {
        await using var environment = await AutomaticBackupTestEnvironment.CreateAsync();
        await environment.ConfigureAsync(enabled: false);

        await environment.Harness.Scheduler.RunBackupNowAsync();
        var history = await environment.Database.Context.BackupHistories.SingleAsync();

        Assert.StartsWith("Ilm-o-Kutub_Backup_", history.BackupFileName);
        Assert.Equal("Automatic backup run manually", history.Reason);
        Assert.True(history.BackupSizeBytes > 0);
    }

    [Fact]
    public async Task AutomaticCompression_CreatesZipWhenEnabled()
    {
        await using var environment = await AutomaticBackupTestEnvironment.CreateAsync();
        await environment.ConfigureAsync(enabled: false, compress: true);

        var result = await environment.Harness.Scheduler.RunBackupNowAsync();

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(File.Exists(result.CompressedFilePath));
        using var archive = ZipFile.OpenRead(result.CompressedFilePath);
        Assert.Contains(archive.Entries, entry => entry.Name.EndsWith(".db"));
    }

    [Fact]
    public async Task RetentionPreview_IdentifiesOldBackupCandidates()
    {
        await using var environment = await AutomaticBackupTestEnvironment.CreateAsync();
        await environment.ConfigureAsync(retentionEnabled: true);
        await AddHistoryAsync(
            environment.Database,
            environment.User,
            environment.BackupFolder,
            "old.db",
            DateTime.UtcNow.AddDays(-60));
        await AddHistoryAsync(
            environment.Database,
            environment.User,
            environment.BackupFolder,
            "new.db",
            DateTime.UtcNow);

        var preview = await environment.Harness.Scheduler.PreviewRetentionAsync();

        Assert.True(preview.Succeeded, preview.ErrorMessage);
        Assert.Equal(1, preview.CandidateCount);
        Assert.Contains(preview.Candidates, item => item.CanDelete);
    }

    [Fact]
    public async Task RetentionPreview_KeepsLatestSuccessfulBackup()
    {
        await using var environment = await AutomaticBackupTestEnvironment.CreateAsync();
        await environment.ConfigureAsync(retentionEnabled: true);
        var onlyBackup = await AddHistoryAsync(
            environment.Database,
            environment.User,
            environment.BackupFolder,
            "only-old.db",
            DateTime.UtcNow.AddDays(-90));

        var preview = await environment.Harness.Scheduler.PreviewRetentionAsync();

        Assert.Equal(0, preview.CandidateCount);
        Assert.Contains(
            preview.Candidates,
            item => item.BackupHistoryId == onlyBackup.Id &&
                !item.CanDelete &&
                item.CannotDeleteReason.Contains("latest successful"));
    }

    [Fact]
    public async Task RetentionPreview_KeepsFailedVerificationBackups()
    {
        await using var environment = await AutomaticBackupTestEnvironment.CreateAsync();
        await environment.ConfigureAsync(retentionEnabled: true);
        var failed = await AddHistoryAsync(
            environment.Database,
            environment.User,
            environment.BackupFolder,
            "failed-verification.db",
            DateTime.UtcNow.AddDays(-90),
            verificationStatus: "Failed");
        await AddHistoryAsync(
            environment.Database,
            environment.User,
            environment.BackupFolder,
            "latest.db",
            DateTime.UtcNow);

        var preview = await environment.Harness.Scheduler.PreviewRetentionAsync();

        Assert.Contains(
            preview.Candidates,
            item => item.BackupHistoryId == failed.Id &&
                !item.CanDelete &&
                item.CannotDeleteReason.Contains("Failed verification"));
    }

    [Fact]
    public async Task Retention_DoesNotDeleteRestoreSafetyBackups()
    {
        await using var environment = await AutomaticBackupTestEnvironment.CreateAsync();
        await environment.ConfigureAsync(
            retentionEnabled: true,
            deletePhysicalFiles: true);
        var safety = await AddHistoryAsync(
            environment.Database,
            environment.User,
            Path.Combine(environment.BackupFolder, "Restore Safety"),
            "safety.db",
            DateTime.UtcNow.AddDays(-90),
            reason: "Mandatory pre-restore safety backup: test");
        await AddHistoryAsync(
            environment.Database,
            environment.User,
            environment.BackupFolder,
            "latest.db",
            DateTime.UtcNow);
        environment.Database.Context.RestoreHistories.Add(new RestoreHistory
        {
            BackupFilePath = Path.Combine(environment.BackupFolder, "source.db"),
            SafetyBackupFilePath = safety.BackupFilePath,
            RestoredDatabasePath = environment.Database.DatabasePath,
            RequestedByUserId = environment.User.Id,
            RequestedByUserName = environment.User.FullName,
            StartedAt = DateTime.UtcNow.AddDays(-1),
            Status = "PendingRestart"
        });
        await environment.Database.Context.SaveChangesAsync();

        var result = await environment.Harness.Scheduler.ApplyRetentionAsync();
        environment.Database.Context.ChangeTracker.Clear();
        var stored = await environment.Database.Context.BackupHistories
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == safety.Id);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.False(stored.IsDeleted);
        Assert.True(File.Exists(safety.BackupFilePath));
    }

    [Fact]
    public async Task Retention_DoesNotDeletePendingRestoreStagedFiles()
    {
        await using var environment = await AutomaticBackupTestEnvironment.CreateAsync();
        await environment.ConfigureAsync(
            retentionEnabled: true,
            deletePhysicalFiles: true);
        var pending = await AddHistoryAsync(
            environment.Database,
            environment.User,
            environment.BackupFolder,
            "pending.db",
            DateTime.UtcNow.AddDays(-80));
        await AddHistoryAsync(
            environment.Database,
            environment.User,
            environment.BackupFolder,
            "latest.db",
            DateTime.UtcNow);
        var pendingPath = PendingRestoreProcessor.GetPendingRequestPath(
            environment.Database.DatabasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(pendingPath)!);
        await File.WriteAllTextAsync(
            pendingPath,
            JsonSerializer.Serialize(new PendingRestoreMetadata
            {
                OriginalBackupFilePath = pending.BackupFilePath,
                StagedBackupFilePath = pending.BackupFilePath,
                TargetDatabasePath = environment.Database.DatabasePath,
                SafetyBackupFilePath = string.Empty,
                ChecksumSha256 = pending.ChecksumSha256 ?? string.Empty,
                RequestedByUserId = environment.User.Id,
                RequestedByUserName = environment.User.FullName,
                Reason = "test",
                RequestedAt = DateTime.UtcNow
            }));

        var preview = await environment.Harness.Scheduler.PreviewRetentionAsync();
        var result = await environment.Harness.Scheduler.ApplyRetentionAsync();

        Assert.Contains(
            preview.Candidates,
            item => item.BackupHistoryId == pending.Id &&
                item.CannotDeleteReason.Contains("pending restore"));
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(File.Exists(pending.BackupFilePath));
    }

    [Fact]
    public async Task Retention_WithPhysicalDeletionDisabled_OnlyUpdatesHistory()
    {
        await using var environment = await AutomaticBackupTestEnvironment.CreateAsync();
        await environment.ConfigureAsync(
            retentionEnabled: true,
            deletePhysicalFiles: false);
        var old = await AddHistoryAsync(
            environment.Database,
            environment.User,
            environment.BackupFolder,
            "old.db",
            DateTime.UtcNow.AddDays(-90));
        await AddHistoryAsync(
            environment.Database,
            environment.User,
            environment.BackupFolder,
            "latest.db",
            DateTime.UtcNow);

        var result = await environment.Harness.Scheduler.ApplyRetentionAsync();
        environment.Database.Context.ChangeTracker.Clear();
        var stored = await environment.Database.Context.BackupHistories
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == old.Id);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(1, result.DeletedHistoryCount);
        Assert.Equal(0, result.DeletedPhysicalFileCount);
        Assert.True(stored.IsDeleted);
        Assert.Equal("RetentionDeleted", stored.Status);
        Assert.True(File.Exists(old.BackupFilePath));
    }

    [Fact]
    public async Task Retention_WithPhysicalDeletionEnabled_DeletesOnlySafeLinkedBackupFiles()
    {
        await using var environment = await AutomaticBackupTestEnvironment.CreateAsync();
        await environment.ConfigureAsync(
            retentionEnabled: true,
            deletePhysicalFiles: true);
        var old = await AddHistoryAsync(
            environment.Database,
            environment.User,
            environment.BackupFolder,
            "old.db",
            DateTime.UtcNow.AddDays(-90),
            createZip: true);
        await AddHistoryAsync(
            environment.Database,
            environment.User,
            environment.BackupFolder,
            "latest.db",
            DateTime.UtcNow);
        var unlinked = Path.Combine(environment.BackupFolder, "unlinked.db");
        await File.WriteAllTextAsync(unlinked, "not linked");

        var result = await environment.Harness.Scheduler.ApplyRetentionAsync();

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(1, result.DeletedHistoryCount);
        Assert.True(result.DeletedPhysicalFileCount >= 2);
        Assert.False(File.Exists(old.BackupFilePath));
        Assert.False(File.Exists(Path.ChangeExtension(old.BackupFilePath, ".metadata.json")));
        Assert.False(File.Exists(old.CompressedFilePath));
        Assert.True(File.Exists(unlinked));
    }

    [Fact]
    public async Task UnauthorizedRole_CannotRunScheduler()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var user = await CreateUserAsync(database, isAdmin: false);
        var backupFolder = CreateTempFolder("AutomaticUnauthorizedRun");
        await database.ConfigureAutomaticBackupSettingsAsync(true, backupFolder);
        using var harness = CreateHarness(database, user, canManage: false);

        var result = await harness.Scheduler.RunBackupNowAsync();

        Assert.False(result.Succeeded);
        Assert.True(result.WasSkipped);
        Assert.Contains("cannot run", result.ErrorMessage);
        Assert.Empty(Directory.GetFiles(backupFolder));
        Directory.Delete(backupFolder, recursive: true);
    }

    [Fact]
    public async Task UnauthorizedRole_CannotApplyRetention()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var user = await CreateUserAsync(database, isAdmin: false);
        var backupFolder = CreateTempFolder("AutomaticUnauthorizedRetention");
        await database.ConfigureAutomaticBackupSettingsAsync(
            true,
            backupFolder,
            retentionEnabled: true);
        using var harness = CreateHarness(database, user, canManage: false);

        var result = await harness.Scheduler.ApplyRetentionAsync();
        var actions = await database.Context.ActivityLogs
            .Select(item => item.Action)
            .ToListAsync();

        Assert.False(result.Succeeded);
        Assert.Contains("cannot apply", result.ErrorMessage);
        Assert.Contains("Automatic Backup Retention Blocked", actions);
        Directory.Delete(backupFolder, recursive: true);
    }

    [Fact]
    public async Task ActivityLogs_AreWrittenForSchedulerAndRetention()
    {
        await using var environment = await AutomaticBackupTestEnvironment.CreateAsync();
        await environment.ConfigureAsync(retentionEnabled: true);
        await AddHistoryAsync(
            environment.Database,
            environment.User,
            environment.BackupFolder,
            "old.db",
            DateTime.UtcNow.AddDays(-90));

        await environment.Harness.Scheduler.RunBackupNowAsync();
        await environment.Harness.Scheduler.ApplyRetentionAsync();
        var actions = await environment.Database.Context.ActivityLogs
            .Select(item => item.Action)
            .ToListAsync();

        Assert.Contains("Automatic Backup Now Started", actions);
        Assert.Contains("Automatic Backup Completed", actions);
        Assert.Contains("Automatic Backup Retention Deleted", actions);
        Assert.Contains("Automatic Backup Retention Completed", actions);
    }

    private static async Task<User> CreateUserAsync(
        SqliteTestDatabase database,
        bool isAdmin = true)
    {
        var data = await database.AddCirculationDataAsync();
        data.User.FullName = isAdmin
            ? "Automatic Backup Administrator"
            : "Read Only User";
        data.User.UserRoles.Clear();
        data.User.UserRoles.Add(new UserRole
        {
            UserId = data.User.Id,
            Role = new Role
            {
                Name = isAdmin ? "Admin" : "Read Only Viewer"
            }
        });
        await database.Context.SaveChangesAsync();
        return data.User;
    }

    private static SchedulerHarness CreateHarness(
        SqliteTestDatabase database,
        User user,
        bool canManage,
        IBackupService? backupService = null)
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<KicsitLibraryDbContext>(options =>
            options.UseSqlite(database.ConnectionString));
        services.AddScoped(provider =>
            provider.GetRequiredService<IDbContextFactory<KicsitLibraryDbContext>>()
                .CreateDbContext());
        services.AddSingleton<IAuthenticationService>(
            new FakeAuthenticationService(user, canManage));

        // Priority 8D ownership lock dependency
        services.AddSingleton<IDatabaseOwnershipService>(
            new FakeDatabaseOwnershipService());

        if (backupService == null)
        {
            services.AddScoped<IBackupService, BackupService>();
        }
        else
        {
            services.AddScoped(_ => backupService);
        }

        services.AddScoped<IBackupRetentionService, BackupRetentionService>();
        services.AddSingleton<IAutomaticBackupSchedulerService, AutomaticBackupSchedulerService>();
        var provider = services.BuildServiceProvider();
        return new SchedulerHarness(
            provider,
            provider.GetRequiredService<IAutomaticBackupSchedulerService>());
    }

    private static async Task<BackupHistory> AddHistoryAsync(
        SqliteTestDatabase database,
        User user,
        string folder,
        string fileName,
        DateTime createdAtUtc,
        string status = "Completed",
        string verificationStatus = "Passed",
        string? reason = "retention test backup",
        bool createZip = false)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, fileName);
        await File.WriteAllTextAsync(path, "test backup content");
        var metadataPath = Path.ChangeExtension(path, ".metadata.json");
        await File.WriteAllTextAsync(metadataPath, "{}");
        var compressedPath = string.Empty;
        if (createZip)
        {
            compressedPath = Path.ChangeExtension(path, ".zip");
            await File.WriteAllTextAsync(compressedPath, "zip content");
        }

        var history = new BackupHistory
        {
            BackupFileName = Path.GetFileName(path),
            BackupFilePath = path,
            CompressedFilePath = string.IsNullOrWhiteSpace(compressedPath)
                ? null
                : compressedPath,
            BackupSizeBytes = new FileInfo(path).Length,
            ChecksumSha256 = new string('A', 64),
            CreatedByUserId = user.Id,
            CreatedByUserName = user.FullName,
            VerifiedAt = DateTime.UtcNow,
            VerificationStatus = verificationStatus,
            Reason = reason,
            Status = status,
            MetadataJson = "{}"
        };
        database.Context.BackupHistories.Add(history);
        await database.Context.SaveChangesAsync();
        history.CreatedAt = createdAtUtc;
        await database.Context.SaveChangesAsync();
        return history;
    }

    private static string CreateTempFolder(string name)
    {
        var folder = Path.Combine(
            Path.GetTempPath(),
            "KicsitLibrary.Tests",
            name,
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private sealed class AutomaticBackupTestEnvironment : IAsyncDisposable
    {
        private AutomaticBackupTestEnvironment(
            SqliteTestDatabase database,
            User user,
            string root,
            SchedulerHarness harness)
        {
            Database = database;
            User = user;
            Root = root;
            BackupFolder = Path.Combine(root, "Backups");
            Harness = harness;
        }

        public SqliteTestDatabase Database { get; }
        public User User { get; }
        public string Root { get; }
        public string BackupFolder { get; }
        public SchedulerHarness Harness { get; }

        public static async Task<AutomaticBackupTestEnvironment> CreateAsync()
        {
            var database = await SqliteTestDatabase.CreateAsync();
            var user = await CreateUserAsync(database);
            var root = CreateTempFolder("AutomaticBackups");
            var environment = new AutomaticBackupTestEnvironment(
                database,
                user,
                root,
                CreateHarness(database, user, canManage: true));
            Directory.CreateDirectory(environment.BackupFolder);
            return environment;
        }

        public Task ConfigureAsync(
            bool enabled = false,
            bool compress = false,
            bool retentionEnabled = false,
            bool deletePhysicalFiles = false) =>
            Database.ConfigureAutomaticBackupSettingsAsync(
                enabled,
                BackupFolder,
                compress: compress,
                retentionEnabled: retentionEnabled,
                deletePhysicalFiles: deletePhysicalFiles);

        public async ValueTask DisposeAsync()
        {
            Harness.Dispose();
            await Database.DisposeAsync();
            var workDirectory = Path.GetDirectoryName(
                PendingRestoreProcessor.GetPendingRequestPath(
                    Database.DatabasePath));
            if (!string.IsNullOrWhiteSpace(workDirectory) &&
                Directory.Exists(workDirectory))
            {
                Directory.Delete(workDirectory, recursive: true);
            }
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed record SchedulerHarness(
        ServiceProvider Provider,
        IAutomaticBackupSchedulerService Scheduler) : IDisposable
    {
        public void Dispose() => Provider.Dispose();
    }

    private sealed class FakeAuthenticationService(
        User currentUser,
        bool canManage) : IAuthenticationService
    {
        public User? CurrentUser { get; } = currentUser;

        public Task<User?> LoginAsync(string username, string password) =>
            Task.FromResult(CurrentUser);

        public Task<bool> ChangePasswordAsync(
            int userId,
            string oldPassword,
            string newPassword) =>
            Task.FromResult(false);

        public Task<bool> VerifyUserPermissionAsync(
            int userId,
            string permissionCode) =>
            Task.FromResult(permissionCode switch
            {
                "VIEW_BACKUPS" => canManage,
                "MANAGE_BACKUPS" => canManage,
                "MANAGE_AUTOMATIC_BACKUPS" => canManage,
                _ => false
            });

        public Task<bool> RequestPasswordResetAsync(string usernameOrEmail) => Task.FromResult(true);
            public Task<bool> ResetPasswordAsync(string usernameOrEmail, string token, string newPassword) => Task.FromResult(true);
            public Task<bool> GenerateAndSendOtpAsync(int userId) => Task.FromResult(true);
            public Task<bool> VerifyOtpAsync(int userId, string otp) => Task.FromResult(true);
            public Task LogoutAsync() => Task.CompletedTask;
    }

    private sealed class FakeDatabaseOwnershipService : IDatabaseOwnershipService
    {
        public Task<DatabaseOwnershipResult> AcquireApplicationInstanceLockAsync(
            string databasePath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DatabaseOwnershipResult { Succeeded = true });

        public Task ReleaseApplicationInstanceLockAsync() =>
            Task.CompletedTask;

        public Task<DatabaseOwnershipStatus> GetApplicationInstanceStatusAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DatabaseOwnershipStatus { IsOwned = true });

        public Task<CriticalOperationLockResult> AcquireCriticalOperationLockAsync(
            string operationName,
            string databasePath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CriticalOperationLockResult
            {
                Succeeded = true,
                OperationName = operationName
            });

        public Task ReleaseCriticalOperationLockAsync(
            string operationName,
            string databasePath) =>
            Task.CompletedTask;

        public Task<T> RunWithCriticalOperationLockAsync<T>(
            string operationName,
            string databasePath,
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default) =>
            operation(cancellationToken);

        public Task RunWithCriticalOperationLockAsync(
            string operationName,
            string databasePath,
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default) =>
            operation(cancellationToken);

        public Task<OwnershipHealthCheckResult> GetOwnershipHealthAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new OwnershipHealthCheckResult
            {
                Succeeded = true,
                DatabaseLockAvailable = true,
                BackupFolderLockAvailable = true,
                RestoreLockAvailable = true,
                SchedulerLockAvailable = true,
                DetectedStaleLockFiles = 0,
                Message = "Health check completed."
            });

        public Task<int> CleanupStaleLockFilesAsync(
            bool bypassAuthorization = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }

    private sealed class BlockingBackupService : IBackupService
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int RunCount { get; private set; }

        public async Task<BackupResult> CreateBackupAsync(
            BackupRequest request,
            CancellationToken cancellationToken = default)
        {
            RunCount++;
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return new BackupResult
            {
                Succeeded = true,
                BackupFilePath = Path.Combine(
                    request.DestinationFolder,
                    "blocking.db"),
                BackupSizeBytes = 1,
                ChecksumSha256 = new string('B', 64),
                StartedAt = DateTime.UtcNow,
                FinishedAt = DateTime.UtcNow,
                Message = "Blocking backup completed."
            };
        }

        public Task<BackupVerificationResult> VerifyBackupAsync(
            string filePath,
            int? backupHistoryId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<BackupHistoryItem>> GetBackupHistoryAsync(
            BackupHistoryFilter filter,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BackupStatusSummary> GetBackupStatusSummaryAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BackupSettings> GetBackupSettingsAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BackupResult> OpenBackupFolderAsync(
            string? folderPath = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BackupResult> DeleteBackupHistoryRecordAsync(
            int backupHistoryId,
            string reason,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}


