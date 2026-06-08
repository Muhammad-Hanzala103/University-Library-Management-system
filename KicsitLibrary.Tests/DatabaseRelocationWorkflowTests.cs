using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data;
using KicsitLibrary.Services.Backup;
using KicsitLibrary.Services.Restore;
using KicsitLibrary.Services.Runtime;
using KicsitLibrary.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Tests;

public class DatabaseRelocationWorkflowTests
{
    [Fact]
    public async Task PreviewRelocation_ShowsCurrentAndTargetPaths()
    {
        await using var env = await RelocationEnvironment.CreateAsync();

        var preview = await env.Service.PreviewRelocationAsync();

        Assert.True(preview.Succeeded, string.Join(" ", preview.BlockingReasons));
        Assert.Equal(env.Database.DatabasePath, preview.CurrentDatabasePath);
        Assert.Equal(env.TargetPath, preview.TargetDatabasePath);
    }

    [Fact]
    public async Task Relocation_BlockedWithoutReason()
    {
        await using var env = await RelocationEnvironment.CreateAsync();
        var request = env.Request();
        request.Reason = string.Empty;

        var result = await env.Service.RelocateDatabaseAsync(request);

        Assert.False(result.Succeeded);
        Assert.Contains("reason", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Relocation_BlockedWithoutConfirmationText()
    {
        await using var env = await RelocationEnvironment.CreateAsync();
        var request = env.Request();
        request.ConfirmationText = "relocate";

        var result = await env.Service.RelocateDatabaseAsync(request);

        Assert.False(result.Succeeded);
        Assert.Contains("RELOCATE", result.ErrorMessage);
    }

    [Fact]
    public async Task Relocation_BlockedForNonAdmin()
    {
        await using var env = await RelocationEnvironment.CreateAsync(isAdmin: false);

        var result = await env.Service.RelocateDatabaseAsync(env.Request());

        Assert.False(result.Succeeded);
        Assert.Contains("cannot relocate", result.ErrorMessage);
    }

    [Fact]
    public async Task Admin_CanRelocateWithValidRequest()
    {
        await using var env = await RelocationEnvironment.CreateAsync();

        var result = await env.Service.RelocateDatabaseAsync(env.Request());

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(result.Copied);
        Assert.False(result.Moved);
    }

    [Fact]
    public async Task Relocation_CreatesSafetyBackup()
    {
        await using var env = await RelocationEnvironment.CreateAsync();

        var result = await env.Service.RelocateDatabaseAsync(env.Request());

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(File.Exists(result.SafetyBackupPath));
        Assert.Contains("Relocation Safety", result.SafetyBackupPath);
    }

    [Fact]
    public async Task Relocation_VerifiesSourceAndTargetIntegrity()
    {
        await using var env = await RelocationEnvironment.CreateAsync();

        var result = await env.Service.RelocateDatabaseAsync(env.Request());
        var sourceValidation = await RestoreSqliteUtility.ValidateAsync(env.Database.DatabasePath);
        var targetValidation = await RestoreSqliteUtility.ValidateAsync(env.TargetPath);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(sourceValidation.Succeeded, sourceValidation.ErrorMessage);
        Assert.True(targetValidation.Succeeded, targetValidation.ErrorMessage);
    }

    [Fact]
    public async Task Relocation_CopiesDatabaseToRuntimeTarget()
    {
        await using var env = await RelocationEnvironment.CreateAsync();

        await env.Service.RelocateDatabaseAsync(env.Request());

        Assert.True(File.Exists(env.TargetPath));
        Assert.True(new FileInfo(env.TargetPath).Length > 0);
    }

    [Fact]
    public async Task Relocation_DoesNotDeleteSourceDatabase()
    {
        await using var env = await RelocationEnvironment.CreateAsync();

        await env.Service.RelocateDatabaseAsync(env.Request());

        Assert.True(File.Exists(env.Database.DatabasePath));
    }

    [Fact]
    public async Task UseReleaseDataRoot_RemainsFalseUnlessRequested()
    {
        await using var env = await RelocationEnvironment.CreateAsync();
        var request = env.Request();
        request.EnableReleaseDataRootAfterMove = false;

        await env.Service.RelocateDatabaseAsync(request);

        Assert.Equal("False", await env.GetSettingAsync("UseReleaseDataRoot"));
    }

    [Fact]
    public async Task UseReleaseDataRoot_BecomesTrueWhenRequestedAndRelocationSucceeds()
    {
        await using var env = await RelocationEnvironment.CreateAsync();

        var result = await env.Service.RelocateDatabaseAsync(env.Request());

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal("True", await env.GetSettingAsync("UseReleaseDataRoot"));
        Assert.Equal("Release", await env.GetSettingAsync("RuntimeStorageMode"));
    }

    [Fact]
    public void StartupDatabasePath_RemainsDevelopmentWhenUseReleaseDataRootFalse()
    {
        var configured = CreateTempPath("StartupDevelopment", "KicsitLibrary.db");
        var settings = new Dictionary<string, string>
        {
            ["UseReleaseDataRoot"] = "False",
            ["RuntimeStorageMode"] = "Development",
            ["RuntimeDataRoot"] = Path.GetDirectoryName(configured)!,
            ["DatabaseFileName"] = "KicsitLibrary.db"
        };

        var path = StartupDatabasePathResolver.ResolveReleasePathFromSettings(
            configured,
            settings);

        Assert.Equal(Path.GetFullPath(configured), path);
    }

    [Fact]
    public async Task StartupDatabasePath_UsesReleaseRootWhenUseReleaseDataRootTrue()
    {
        var root = CreateTempRoot("StartupRelease");
        Directory.CreateDirectory(root);
        var target = Path.Combine(root, "KicsitLibrary.db");
        await File.WriteAllTextAsync(target, "placeholder");
        var configured = CreateTempPath("StartupReleaseConfigured", "KicsitLibrary.db");
        var settings = new Dictionary<string, string>
        {
            ["UseReleaseDataRoot"] = "True",
            ["RuntimeStorageMode"] = "Release",
            ["RuntimeDataRoot"] = root,
            ["DatabaseFileName"] = "KicsitLibrary.db"
        };

        var path = StartupDatabasePathResolver.ResolveReleasePathFromSettings(
            configured,
            settings);

        Assert.Equal(target, path);
        DeleteDirectory(root);
    }

    [Fact]
    public async Task InvalidTargetOutsideRuntimeRoot_IsRejected()
    {
        await using var env = await RelocationEnvironment.CreateAsync();
        var outside = CreateTempPath("OutsideRuntimeRoot", "KicsitLibrary.db");

        var preview = await env.Service.ValidateRelocationTargetAsync(outside);

        Assert.False(preview.CanRelocate);
        Assert.Contains(preview.BlockingReasons, reason => reason.Contains("runtime data root"));
    }

    [Fact]
    public async Task Relocation_WritesHistoryAndActivityLogs()
    {
        await using var env = await RelocationEnvironment.CreateAsync();

        await env.Service.RelocateDatabaseAsync(env.Request());
        var history = await env.Service.GetRelocationHistoryAsync();
        var actions = env.Database.Context.ActivityLogs.Select(item => item.Action).ToList();

        Assert.Single(history);
        Assert.Contains("Database Relocation Completed", actions);
        Assert.Contains("Database Relocation Safety Backup Created", actions);
    }

    private sealed class RelocationEnvironment : IAsyncDisposable
    {
        private RelocationEnvironment(
            SqliteTestDatabase database,
            DatabaseRelocationService service,
            User user,
            string root)
        {
            Database = database;
            Service = service;
            User = user;
            Root = root;
            TargetPath = Path.Combine(root, "KicsitLibrary.db");
        }

        public SqliteTestDatabase Database { get; }
        public DatabaseRelocationService Service { get; }
        public User User { get; }
        public string Root { get; }
        public string TargetPath { get; }

        public static async Task<RelocationEnvironment> CreateAsync(bool isAdmin = true)
        {
            var database = await SqliteTestDatabase.CreateAsync();
            await DatabaseCompatibilityInitializer.ApplyAsync(database.Context);
            var data = await database.AddCirculationDataAsync();
            data.User.UserRoles.Clear();
            data.User.UserRoles.Add(new UserRole
            {
                UserId = data.User.Id,
                Role = new Role { Name = isAdmin ? "Admin" : "Read Only Viewer" }
            });
            await database.Context.SaveChangesAsync();
            var root = CreateTempRoot("RelocationRoot");
            await database.SetSystemSettingAsync("RuntimeDataRoot", root, "Runtime");
            await database.SetSystemSettingAsync("UseReleaseDataRoot", "False", "Runtime");
            await database.SetSystemSettingAsync("RuntimeStorageMode", "Development", "Runtime");
            await database.SetSystemSettingAsync("DatabaseFileName", "KicsitLibrary.db", "Runtime");
            await database.SetSystemSettingAsync("BackupDefaultFolder", Path.Combine(root, "Backups"), "Backup");
            var auth = new FakeAuthenticationService(data.User, isAdmin);
            var ownership = new FakeDatabaseOwnershipService();
            var backup = new BackupService(database.Context, auth, ownership);
            return new RelocationEnvironment(
                database,
                new DatabaseRelocationService(database.Context, auth, backup, ownership),
                data.User,
                root);
        }

        public DatabaseRelocationRequest Request() =>
            new()
            {
                RequestedByUserId = User.Id,
                RequestedByUserName = User.FullName,
                Reason = "Approved release database relocation.",
                SourceDatabasePath = Database.DatabasePath,
                TargetDatabasePath = TargetPath,
                CreateSafetyBackup = true,
                VerifyBeforeMove = true,
                VerifyAfterMove = true,
                EnableReleaseDataRootAfterMove = true,
                ConfirmationText = "RELOCATE"
            };

        public async Task<string> GetSettingAsync(string key) =>
            (await Database.Context.SystemSettings.SingleAsync(item => item.Key == key)).Value;

        public async ValueTask DisposeAsync()
        {
            await Database.DisposeAsync();
            DeleteDirectory(Root);
        }
    }

    private static string CreateTempRoot(string name) =>
        Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "KicsitLibrary.Tests",
            "DatabaseRelocation",
            name,
            Guid.NewGuid().ToString("N")));

    private static string CreateTempPath(string name, string fileName) =>
        Path.Combine(CreateTempRoot(name), fileName);

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed class FakeAuthenticationService(User currentUser, bool isAdmin) : IAuthenticationService
    {
        public User? CurrentUser { get; } = currentUser;
        public Task<User?> LoginAsync(string username, string password) => Task.FromResult(CurrentUser);
        public Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword) => Task.FromResult(false);
        public Task<bool> VerifyUserPermissionAsync(int userId, string permissionCode) =>
            Task.FromResult(isAdmin && permissionCode is "VIEW_BACKUPS" or "MANAGE_BACKUPS");
        public Task LogoutAsync() => Task.CompletedTask;
    }

    private sealed class FakeDatabaseOwnershipService : IDatabaseOwnershipService
    {
        public Task<DatabaseOwnershipResult> AcquireApplicationInstanceLockAsync(string databasePath, CancellationToken cancellationToken = default) => Task.FromResult(new DatabaseOwnershipResult { Succeeded = true });
        public Task ReleaseApplicationInstanceLockAsync() => Task.CompletedTask;
        public Task<DatabaseOwnershipStatus> GetApplicationInstanceStatusAsync(CancellationToken cancellationToken = default) => Task.FromResult(new DatabaseOwnershipStatus { IsOwned = true });
        public Task<CriticalOperationLockResult> AcquireCriticalOperationLockAsync(string operationName, string databasePath, CancellationToken cancellationToken = default) => Task.FromResult(new CriticalOperationLockResult { Succeeded = true });
        public Task ReleaseCriticalOperationLockAsync(string operationName, string databasePath) => Task.CompletedTask;
        public Task<T> RunWithCriticalOperationLockAsync<T>(string operationName, string databasePath, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default) => operation(cancellationToken);
        public Task RunWithCriticalOperationLockAsync(string operationName, string databasePath, Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default) => operation(cancellationToken);
        public Task<OwnershipHealthCheckResult> GetOwnershipHealthAsync(CancellationToken cancellationToken = default) => Task.FromResult(new OwnershipHealthCheckResult { Succeeded = true });
        public Task<int> CleanupStaleLockFilesAsync(bool bypassAuthorization = false, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }
}
