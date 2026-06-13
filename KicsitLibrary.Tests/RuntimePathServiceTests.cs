using KicsitLibrary.Core;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Services.Backup;
using KicsitLibrary.Services.Documents;
using KicsitLibrary.Services.Runtime;
using KicsitLibrary.Tests.Infrastructure;

namespace KicsitLibrary.Tests;

public class RuntimePathServiceTests
{
    [Fact]
    public async Task RuntimePathService_UsesLocalApplicationDataWhenReleaseModeEnabledAndRootEmpty()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await database.SetSystemSettingAsync("UseReleaseDataRoot", "True", "Runtime");
        await database.SetSystemSettingAsync("RuntimeDataRoot", "", "Runtime");
        var service = new RuntimePathService(database.Context);

        var root = await service.GetDataRootAsync();

        Assert.StartsWith(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            root,
            StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(ProductBrand.Name, root);
    }

    [Fact]
    public async Task RuntimePathService_UsesConfiguredRuntimeDataRoot()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var configuredRoot = CreateTempRoot("ConfiguredRoot");
        await database.SetSystemSettingAsync("RuntimeDataRoot", configuredRoot, "Runtime");
        var service = new RuntimePathService(database.Context);

        var root = await service.GetDataRootAsync();
        var documents = await service.GetDocumentStorageRootAsync();

        Assert.Equal(Path.GetFullPath(configuredRoot), root);
        Assert.Equal(Path.Combine(Path.GetFullPath(configuredRoot), "Documents"), documents);
        DeleteDirectory(configuredRoot);
    }

    [Fact]
    public async Task RuntimePathService_RejectsPathTraversalFolderNames()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await database.SetSystemSettingAsync("UseReleaseDataRoot", "True", "Runtime");
        await database.SetSystemSettingAsync("DocumentsFolderName", "..\\Outside", "Runtime");
        var service = new RuntimePathService(database.Context);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetDocumentStorageRootAsync());

        Assert.Contains("simple folder names", error.Message);
    }

    [Fact]
    public async Task RuntimePathService_EnsureRuntimeFoldersCreatesRequiredFolders()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var root = CreateTempRoot("EnsureFolders");
        await database.SetSystemSettingAsync("RuntimeDataRoot", root, "Runtime");
        var service = new RuntimePathService(database.Context);

        await service.EnsureRuntimeFoldersAsync();

        Assert.True(Directory.Exists(root));
        Assert.True(Directory.Exists(Path.Combine(root, "Documents")));
        Assert.True(Directory.Exists(Path.Combine(root, "Backups")));
        Assert.True(Directory.Exists(Path.Combine(root, "Reports")));
        Assert.True(Directory.Exists(Path.Combine(root, "Certificates")));
        Assert.True(Directory.Exists(Path.Combine(root, "RestoreStaging")));
        Assert.True(Directory.Exists(Path.Combine(root, "Logs")));
        Assert.True(Directory.Exists(Path.Combine(root, "Temp")));
        Assert.True(Directory.Exists(Path.Combine(root, "Locks")));
        DeleteDirectory(root);
    }

    [Fact]
    public async Task DocumentStorageService_UsesRuntimeDefaultWhenDocumentStorageRootEmpty()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var root = CreateTempRoot("DocumentRuntimeRoot");
        await database.SetSystemSettingAsync("RuntimeDataRoot", root, "Runtime");
        await database.SetSystemSettingAsync("DocumentStorageRoot", "", "Documents");
        var runtimePaths = new RuntimePathService(database.Context);
        var storage = new DocumentStorageService(database.Context, runtimePaths);

        var settings = await storage.GetSettingsAsync();
        var resolved = storage.ResolveStorageRoot(settings);

        Assert.Equal(Path.Combine(root, "Documents"), settings.StorageRoot);
        Assert.Equal(Path.Combine(root, "Documents"), resolved);
        DeleteDirectory(root);
    }

    [Fact]
    public async Task BackupService_UsesRuntimeDefaultWhenBackupDefaultFolderEmpty()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var root = CreateTempRoot("BackupRuntimeRoot");
        await database.SetSystemSettingAsync("RuntimeDataRoot", root, "Runtime");
        await database.SetSystemSettingAsync("BackupDefaultFolder", "", "Backup");
        var authentication = new FakeAuthenticationService(data.User);
        var runtimePaths = new RuntimePathService(database.Context);
        var backup = new BackupService(
            database.Context,
            authentication,
            new FakeDatabaseOwnershipService(),
            runtimePaths);

        var settings = await backup.GetBackupSettingsAsync();

        Assert.Equal(Path.Combine(root, "Backups"), settings.DefaultFolder);
        DeleteDirectory(root);
    }

    [Fact]
    public async Task RuntimePathService_DatabasePathStaysExecutableRelativeWhenReleaseRootDisabled()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await database.SetSystemSettingAsync("UseReleaseDataRoot", "False", "Runtime");
        await database.SetSystemSettingAsync("RuntimeStorageMode", "Development", "Runtime");
        await database.SetSystemSettingAsync("RuntimeDataRoot", "", "Runtime");
        await database.SetSystemSettingAsync("DatabaseFileName", "KicsitLibrary.db", "Runtime");
        var service = new RuntimePathService(database.Context);

        var path = await service.GetDatabasePathAsync();

        Assert.Equal(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "KicsitLibrary.db")),
            path);
    }

    [Fact]
    public async Task RuntimePathService_DatabasePathUsesReleaseRootOnlyWhenEnabled()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var root = CreateTempRoot("DatabaseReleaseRoot");
        await database.SetSystemSettingAsync("RuntimeDataRoot", root, "Runtime");
        await database.SetSystemSettingAsync("UseReleaseDataRoot", "True", "Runtime");
        await database.SetSystemSettingAsync("DatabaseFileName", "KicsitLibrary.db", "Runtime");
        var service = new RuntimePathService(database.Context);

        var path = await service.GetDatabasePathAsync();

        Assert.Equal(Path.Combine(root, "KicsitLibrary.db"), path);
        DeleteDirectory(root);
    }

    private static string CreateTempRoot(string name) =>
        Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "KicsitLibrary.Tests",
            "RuntimePaths",
            name,
            Guid.NewGuid().ToString("N")));

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed class FakeAuthenticationService(User currentUser) : IAuthenticationService
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
            Task.FromResult(permissionCode is "VIEW_BACKUPS" or "MANAGE_BACKUPS");

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

        public Task ReleaseApplicationInstanceLockAsync() => Task.CompletedTask;

        public Task<DatabaseOwnershipStatus> GetApplicationInstanceStatusAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DatabaseOwnershipStatus { IsOwned = true });

        public Task<CriticalOperationLockResult> AcquireCriticalOperationLockAsync(
            string operationName,
            string databasePath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CriticalOperationLockResult { Succeeded = true });

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
            Task.FromResult(new OwnershipHealthCheckResult { Succeeded = true });

        public Task<int> CleanupStaleLockFilesAsync(
            bool bypassAuthorization = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }
}


