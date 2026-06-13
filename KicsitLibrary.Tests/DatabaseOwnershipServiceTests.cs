using System.Text.Json;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data;
using KicsitLibrary.Services.Backup;
using KicsitLibrary.Services.Ownership;
using KicsitLibrary.Services.Restore;
using KicsitLibrary.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KicsitLibrary.Tests;

public class DatabaseOwnershipServiceTests
{
    [Fact]
    public async Task ApplicationInstanceLock_CanBeAcquiredAndReleased()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var harness = await OwnershipHarness.CreateAsync(database, "Admin");

        var acquired = await harness.Ownership.AcquireApplicationInstanceLockAsync(
            database.DatabasePath);
        await harness.Ownership.ReleaseApplicationInstanceLockAsync();
        var status = await harness.Ownership.GetApplicationInstanceStatusAsync();

        Assert.True(acquired.Succeeded, acquired.ErrorMessage);
        Assert.False(status.IsOwned);
    }

    [Fact]
    public async Task SecondInstanceLock_IsBlockedWhenSingleInstanceModeEnabled()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await database.SetSystemSettingAsync("SingleInstanceMode", "True", "System");
        await using var first = await OwnershipHarness.CreateAsync(database, "Admin");
        await using var second = await OwnershipHarness.CreateAsync(database, "Admin");

        var firstResult = await first.Ownership.AcquireApplicationInstanceLockAsync(
            database.DatabasePath);
        var secondResult = await second.Ownership.AcquireApplicationInstanceLockAsync(
            database.DatabasePath);

        Assert.True(firstResult.Succeeded, firstResult.ErrorMessage);
        Assert.False(secondResult.Succeeded);
        Assert.Contains("already running", secondResult.ErrorMessage);
    }

    [Fact]
    public async Task CriticalOperationLock_PreventsOverlappingBackupOperation()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await database.SetSystemSettingAsync(
            "CriticalOperationLockTimeoutSeconds",
            "1",
            "System");
        await using var first = await OwnershipHarness.CreateAsync(database, "Admin");
        await using var second = await OwnershipHarness.CreateAsync(database, "Admin");

        var firstLock = await first.Ownership.AcquireCriticalOperationLockAsync(
            "Backup Creation",
            database.DatabasePath);
        var secondLock = await second.Ownership.AcquireCriticalOperationLockAsync(
            "Backup Creation",
            database.DatabasePath);

        Assert.True(firstLock.Succeeded, firstLock.ErrorMessage);
        Assert.False(secondLock.Succeeded);
        Assert.Equal(DatabaseOwnershipService.LockTimeoutMessage, secondLock.ErrorMessage);
    }

    [Fact]
    public async Task CriticalOperationLockTimeout_ReturnsClearMessage()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await database.SetSystemSettingAsync(
            "CriticalOperationLockTimeoutSeconds",
            "1",
            "System");
        await using var first = await OwnershipHarness.CreateAsync(database, "Admin");
        await using var second = await OwnershipHarness.CreateAsync(database, "Admin");
        await first.Ownership.AcquireCriticalOperationLockAsync(
            "Database Compatibility Initialization",
            database.DatabasePath);

        var result = await second.Ownership.AcquireCriticalOperationLockAsync(
            "Database Compatibility Initialization",
            database.DatabasePath);

        Assert.False(result.Succeeded);
        Assert.Equal(
            "Another Ilm-o-Kutub System operation is already using this database or backup folder.",
            result.ErrorMessage);
    }

    [Fact]
    public async Task CriticalOperationLock_WritesMetadataWithoutSensitiveValues()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await database.SetSystemSettingAsync(
            "SmtpPassword",
            "sensitive-smtp-password",
            "Notifications");
        await database.SetSystemSettingAsync(
            "ConnectionStringPassword",
            "sensitive-connection-password",
            "System");
        await using var harness = await OwnershipHarness.CreateAsync(database, "Admin");

        var result = await harness.Ownership.AcquireCriticalOperationLockAsync(
            "Backup Creation",
            database.DatabasePath);
        await using var stream = new FileStream(
            result.LockFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        var metadata = await reader.ReadToEndAsync();

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Contains("\"OperationName\"", metadata);
        Assert.Contains("\"OwnerProcessId\"", metadata);
        Assert.Contains("\"ExpiresAt\"", metadata);
        Assert.DoesNotContain("sensitive-smtp-password", metadata);
        Assert.DoesNotContain("sensitive-connection-password", metadata);
        Assert.DoesNotContain("SmtpPassword", metadata);
    }

    [Fact]
    public async Task CriticalOperationRelease_IsIdempotent()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var harness = await OwnershipHarness.CreateAsync(database, "Admin");

        await harness.Ownership.AcquireCriticalOperationLockAsync(
            "Backup Creation",
            database.DatabasePath);
        await harness.Ownership.ReleaseCriticalOperationLockAsync(
            "Backup Creation",
            database.DatabasePath);
        await harness.Ownership.ReleaseCriticalOperationLockAsync(
            "Backup Creation",
            database.DatabasePath);

        var health = await harness.Ownership.GetOwnershipHealthAsync();
        Assert.True(health.BackupFolderLockAvailable);
    }

    [Fact]
    public async Task StaleLockFileDetection_ReportsExpiredLock()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var harness = await OwnershipHarness.CreateAsync(database, "Admin");
        var stalePath = await CreateExpiredLockFileAsync(
            harness.Ownership,
            "Backup Creation",
            database.DatabasePath);

        var health = await harness.Ownership.GetOwnershipHealthAsync();

        Assert.True(File.Exists(stalePath));
        Assert.True(health.DetectedStaleLockFiles >= 1);
        Assert.Contains("Expired", health.BackupFolderLockMessage);
    }

    [Fact]
    public async Task StaleCleanup_DoesNotDeleteActiveLock()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var harness = await OwnershipHarness.CreateAsync(database, "Admin");
        var active = await harness.Ownership.AcquireCriticalOperationLockAsync(
            "Backup Creation",
            database.DatabasePath);

        var cleaned = await harness.Ownership.CleanupStaleLockFilesAsync();

        Assert.Equal(0, cleaned);
        Assert.True(File.Exists(active.LockFilePath));
    }

    [Fact]
    public async Task StaleCleanup_DeletesExpiredSafeLock()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var harness = await OwnershipHarness.CreateAsync(database, "Admin");
        var stalePath = await CreateExpiredLockFileAsync(
            harness.Ownership,
            "Backup Creation",
            database.DatabasePath);

        var cleaned = await harness.Ownership.CleanupStaleLockFilesAsync();

        Assert.Equal(1, cleaned);
        Assert.False(File.Exists(stalePath));
    }

    [Fact]
    public async Task UnauthorizedRole_CannotCleanupStaleLocks()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var harness = await OwnershipHarness.CreateAsync(
            database,
            "Librarian");
        await CreateExpiredLockFileAsync(
            harness.Ownership,
            "Backup Creation",
            database.DatabasePath);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => harness.Ownership.CleanupStaleLockFilesAsync());
        Assert.Contains(
            await database.Context.ActivityLogs.Select(item => item.Action).ToListAsync(),
            action => action == "Cleanup Stale Locks Denied");
    }

    [Fact]
    public async Task Admin_CanCleanupSafeStaleLocks()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var harness = await OwnershipHarness.CreateAsync(database, "Admin");
        await CreateExpiredLockFileAsync(
            harness.Ownership,
            "Restore Staging",
            database.DatabasePath);

        var cleaned = await harness.Ownership.CleanupStaleLockFilesAsync();

        Assert.Equal(1, cleaned);
    }

    [Fact]
    public async Task BackupCreation_UsesCriticalLock()
    {
        await using var environment = await BackupOwnershipEnvironment.CreateAsync();
        await environment.Blocker.Ownership.AcquireCriticalOperationLockAsync(
            "Backup Creation",
            environment.Database.DatabasePath);

        var result = await environment.BackupService.CreateBackupAsync(
            environment.BackupRequest());

        Assert.False(result.Succeeded);
        Assert.Equal(DatabaseOwnershipService.LockTimeoutMessage, result.ErrorMessage);
        Assert.Empty(Directory.GetFiles(environment.BackupFolder, "*.db"));
    }

    [Fact]
    public async Task RestoreStaging_UsesCriticalLock()
    {
        await using var environment = await BackupOwnershipEnvironment.CreateAsync();
        await environment.Blocker.Ownership.AcquireCriticalOperationLockAsync(
            "Restore Staging",
            environment.Database.DatabasePath);
        var restore = new RestoreService(
            environment.Database.Context,
            environment.Authentication,
            environment.BackupService,
            environment.Owner.Ownership);

        var result = await restore.RestoreFromBackupAsync(new RestoreRequest
        {
            BackupFilePath = Path.Combine(environment.Root, "missing.db"),
            RequestedByUserId = environment.User.Id,
            RequestedByUserName = environment.User.FullName,
            Reason = "Lock test",
            CreateSafetyBackup = true,
            VerifyBeforeRestore = true,
            VerifyAfterRestore = true,
            ConfirmationText = "RESTORE"
        });

        Assert.False(result.Succeeded);
        Assert.Equal(DatabaseOwnershipService.LockTimeoutMessage, result.ErrorMessage);
    }

    [Fact]
    public async Task AutomaticScheduler_SkipsWhenCriticalLockUnavailable()
    {
        await using var environment = await BackupOwnershipEnvironment.CreateAsync();
        await environment.Database.ConfigureAutomaticBackupSettingsAsync(
            true,
            environment.BackupFolder);
        await environment.Blocker.Ownership.AcquireCriticalOperationLockAsync(
            "Automatic Backup Scheduler",
            environment.Database.DatabasePath);
        await using var schedulerHarness =
            await SchedulerOwnershipHarness.CreateAsync(environment);

        var result = await schedulerHarness.Scheduler.RunScheduledBackupAsync();

        Assert.True(result.WasSkipped);
        Assert.Equal(DatabaseOwnershipService.LockTimeoutMessage, result.Message);
    }

    [Fact]
    public async Task RetentionPhysicalDeletion_IsBlockedWithoutCriticalLock()
    {
        await using var environment = await BackupOwnershipEnvironment.CreateAsync();
        await environment.Database.ConfigureAutomaticBackupSettingsAsync(
            true,
            environment.BackupFolder,
            retentionEnabled: true,
            deletePhysicalFiles: true);
        await AddHistoryAsync(
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
        await environment.Blocker.Ownership.AcquireCriticalOperationLockAsync(
            "Backup Retention",
            environment.Database.DatabasePath);
        var retention = new BackupRetentionService(
            environment.Database.Context,
            environment.Authentication,
            environment.Owner.Ownership);

        var result = await retention.ApplyAsync(new AutomaticBackupSchedulerSettings
        {
            RetentionEnabled = true,
            RetentionDays = 30,
            MaxHistoryRows = 500,
            DeletePhysicalFiles = true,
            DestinationFolder = environment.BackupFolder
        });

        Assert.False(result.Succeeded);
        Assert.Equal(DatabaseOwnershipService.LockTimeoutMessage, result.ErrorMessage);
    }

    private static async Task<string> CreateExpiredLockFileAsync(
        IDatabaseOwnershipService ownership,
        string operationName,
        string databasePath)
    {
        var active = await ownership.AcquireCriticalOperationLockAsync(
            operationName,
            databasePath);
        var lockFilePath = active.LockFilePath;
        await ownership.ReleaseCriticalOperationLockAsync(operationName, databasePath);
        var expired = new CriticalOperationLease
        {
            OperationName = operationName,
            LockName = active.LockName,
            LockFilePath = lockFilePath,
            AcquiredAt = DateTime.UtcNow.AddHours(-3),
            ExpiresAt = DateTime.UtcNow.AddHours(-2),
            OwnerProcessId = 999999,
            OwnerMachineName = "expired-test-machine",
            OwnerUserName = "expired-test-user"
        };
        await File.WriteAllTextAsync(
            lockFilePath,
            JsonSerializer.Serialize(expired, new JsonSerializerOptions { WriteIndented = true }));
        return lockFilePath;
    }

    private static async Task<BackupHistory> AddHistoryAsync(
        SqliteTestDatabase database,
        User user,
        string folder,
        string fileName,
        DateTime createdAtUtc)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, fileName);
        await File.WriteAllTextAsync(path, "test backup");
        var history = new BackupHistory
        {
            BackupFileName = fileName,
            BackupFilePath = path,
            BackupSizeBytes = new FileInfo(path).Length,
            ChecksumSha256 = new string('A', 64),
            CreatedByUserId = user.Id,
            CreatedByUserName = user.FullName,
            VerificationStatus = "Passed",
            Status = "Completed",
            Reason = "ownership retention test"
        };
        database.Context.BackupHistories.Add(history);
        await database.Context.SaveChangesAsync();
        history.CreatedAt = createdAtUtc;
        await database.Context.SaveChangesAsync();
        return history;
    }

    private sealed class BackupOwnershipEnvironment : IAsyncDisposable
    {
        private BackupOwnershipEnvironment(
            SqliteTestDatabase database,
            OwnershipHarness owner,
            OwnershipHarness blocker,
            FakeAuthenticationService authentication,
            User user,
            string root)
        {
            Database = database;
            Owner = owner;
            Blocker = blocker;
            Authentication = authentication;
            User = user;
            Root = root;
            BackupFolder = Path.Combine(root, "Backups");
            BackupService = new BackupService(
                database.Context,
                authentication,
                owner.Ownership);
        }

        public SqliteTestDatabase Database { get; }
        public OwnershipHarness Owner { get; }
        public OwnershipHarness Blocker { get; }
        public FakeAuthenticationService Authentication { get; }
        public User User { get; }
        public string Root { get; }
        public string BackupFolder { get; }
        public BackupService BackupService { get; }

        public static async Task<BackupOwnershipEnvironment> CreateAsync()
        {
            var database = await SqliteTestDatabase.CreateAsync();
            await database.SetSystemSettingAsync(
                "CriticalOperationLockTimeoutSeconds",
                "1",
                "System");
            var data = await database.AddCirculationDataAsync();
            data.User.UserRoles.Clear();
            data.User.UserRoles.Add(new UserRole
            {
                UserId = data.User.Id,
                Role = new Role { Name = "Admin" }
            });
            await database.Context.SaveChangesAsync();
            var authentication = new FakeAuthenticationService(
                data.User,
                canView: true,
                canManage: true);
            var owner = await OwnershipHarness.CreateAsync(
                database,
                "Admin",
                authentication);
            var blocker = await OwnershipHarness.CreateAsync(database, "Admin");
            var root = Path.Combine(
                Path.GetTempPath(),
                "KicsitLibrary.Tests",
                "OwnershipBackups",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "Backups"));
            return new BackupOwnershipEnvironment(
                database,
                owner,
                blocker,
                authentication,
                data.User,
                root);
        }

        public BackupRequest BackupRequest() =>
            new()
            {
                RequestedByUserId = User.Id,
                RequestedByUserName = User.FullName,
                DestinationFolder = BackupFolder,
                IncludeMetadataFile = true,
                VerifyAfterCreation = true,
                Reason = "ownership lock test"
            };

        public async ValueTask DisposeAsync()
        {
            await Owner.DisposeAsync();
            await Blocker.DisposeAsync();
            await Database.DisposeAsync();
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class SchedulerOwnershipHarness : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;

        private SchedulerOwnershipHarness(
            ServiceProvider provider,
            IAutomaticBackupSchedulerService scheduler)
        {
            _provider = provider;
            Scheduler = scheduler;
        }

        public IAutomaticBackupSchedulerService Scheduler { get; }

        public static Task<SchedulerOwnershipHarness> CreateAsync(
            BackupOwnershipEnvironment environment)
        {
            var services = new ServiceCollection();
            services.AddDbContextFactory<KicsitLibraryDbContext>(options =>
                options.UseSqlite(environment.Database.ConnectionString));
            services.AddScoped(provider =>
                provider.GetRequiredService<IDbContextFactory<KicsitLibraryDbContext>>()
                    .CreateDbContext());
            services.AddSingleton<IAuthenticationService>(environment.Authentication);
            services.AddSingleton<IActivityLogService, RecordingActivityLogService>();
            services.AddSingleton<IDatabaseOwnershipService, DatabaseOwnershipService>();
            services.AddScoped<IBackupService, BackupService>();
            services.AddScoped<IBackupRetentionService, BackupRetentionService>();
            services.AddSingleton<IAutomaticBackupSchedulerService, AutomaticBackupSchedulerService>();
            var provider = services.BuildServiceProvider();
            return Task.FromResult(new SchedulerOwnershipHarness(
                provider,
                provider.GetRequiredService<IAutomaticBackupSchedulerService>()));
        }

        public ValueTask DisposeAsync()
        {
            _provider.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class OwnershipHarness : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;

        private OwnershipHarness(
            ServiceProvider provider,
            IDatabaseOwnershipService ownership)
        {
            _provider = provider;
            Ownership = ownership;
        }

        public IDatabaseOwnershipService Ownership { get; }

        public static async Task<OwnershipHarness> CreateAsync(
            SqliteTestDatabase database,
            string roleName,
            FakeAuthenticationService? authentication = null)
        {
            authentication ??= new FakeAuthenticationService(
                await CreateUserAsync(database, roleName),
                canView: true,
                canManage: roleName is "Admin" or "Super Admin");
            var services = new ServiceCollection();
            services.AddDbContextFactory<KicsitLibraryDbContext>(options =>
                options.UseSqlite(database.ConnectionString));
            services.AddScoped(provider =>
                provider.GetRequiredService<IDbContextFactory<KicsitLibraryDbContext>>()
                    .CreateDbContext());
            services.AddSingleton<IAuthenticationService>(authentication);
            services.AddSingleton<IActivityLogService, RecordingActivityLogService>();
            services.AddSingleton<IDatabaseOwnershipService, DatabaseOwnershipService>();
            var provider = services.BuildServiceProvider();
            return new OwnershipHarness(
                provider,
                provider.GetRequiredService<IDatabaseOwnershipService>());
        }

        public async ValueTask DisposeAsync()
        {
            await Ownership.ReleaseApplicationInstanceLockAsync();
            _provider.Dispose();
        }

        private static async Task<User> CreateUserAsync(
            SqliteTestDatabase database,
            string roleName)
        {
            var user = new User
            {
                Username = $"ownership-{Guid.NewGuid():N}",
                PasswordHash = "test-only",
                FullName = $"{roleName} Ownership User",
                Email = "ownership@test.invalid"
            };
            user.UserRoles.Add(new UserRole
            {
                Role = new Role
                {
                    Name = roleName,
                    RolePermissions = roleName is "Admin" or "Super Admin"
                        ? new List<RolePermission>
                        {
                            new()
                            {
                                Permission = new Permission
                                {
                                    Code = "MANAGE_OWNERSHIP_STATUS",
                                    Name = "Manage Ownership Status"
                                }
                            }
                        }
                        : []
                }
            });
            database.Context.Users.Add(user);
            await database.Context.SaveChangesAsync();
            return user;
        }
    }

    private sealed class FakeAuthenticationService(
        User currentUser,
        bool canView,
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
                "VIEW_BACKUPS" => canView,
                "MANAGE_BACKUPS" => canManage,
                "MANAGE_AUTOMATIC_BACKUPS" => canManage,
                "VIEW_RESTORES" => canView,
                "MANAGE_RESTORES" => canManage,
                "VIEW_OWNERSHIP_STATUS" => canView,
                "MANAGE_OWNERSHIP_STATUS" => canManage,
                _ => false
            });

        public Task<(bool Success, string Message)> RequestPasswordResetAsync(string usernameOrEmail) => Task.FromResult((true, ""));
            public Task<bool> ResetPasswordAsync(string usernameOrEmail, string token, string newPassword) => Task.FromResult(true);
            public Task<bool> GenerateAndSendOtpAsync(int userId) => Task.FromResult(true);
            public Task<bool> VerifyOtpAsync(int userId, string otp) => Task.FromResult(true);
            public Task LogoutAsync() => Task.CompletedTask;
    }

    private sealed class RecordingActivityLogService(
        IDbContextFactory<KicsitLibraryDbContext> dbContextFactory) : IActivityLogService
    {
        public async Task LogActivityAsync(
            string action,
            string detail,
            int? userId = null,
            string? ipAddress = null)
        {
            await using var context = await dbContextFactory.CreateDbContextAsync();
            context.ActivityLogs.Add(new ActivityLog
            {
                Action = action,
                Detail = detail,
                UserId = userId,
                IpAddress = ipAddress ?? "127.0.0.1"
            });
            await context.SaveChangesAsync();
        }
    }
}


