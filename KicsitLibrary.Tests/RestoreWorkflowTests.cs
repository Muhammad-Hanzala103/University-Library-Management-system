using System.Text.Json;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data;
using KicsitLibrary.Services.Backup;
using KicsitLibrary.Services.Restore;
using KicsitLibrary.Tests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Tests;

public class RestoreWorkflowTests
{
    [Fact]
    public async Task PreviewRestore_ValidatesRealBackupAndReportsCounts()
    {
        await using var environment = await RestoreTestEnvironment.CreateAsync();
        var backup = await environment.CreateBackupAsync();

        var preview = await environment.RestoreService.PreviewRestoreAsync(backup.BackupFilePath);

        Assert.True(preview.Succeeded, preview.ErrorMessage);
        Assert.True(preview.IntegrityCheckPassed);
        Assert.True(preview.DetectedTablesCount > 0);
        Assert.True(preview.DetectedUserCount > 0);
        Assert.True(preview.DetectedBookCopyCount > 0);
    }

    [Fact]
    public async Task ValidateBackup_RunsIntegrityCheckAndComputesChecksum()
    {
        await using var environment = await RestoreTestEnvironment.CreateAsync();
        var backup = await environment.CreateBackupAsync();

        var result = await environment.RestoreService.ValidateBackupForRestoreAsync(
            backup.BackupFilePath);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(result.IntegrityCheckPassed);
        Assert.Matches("^[A-F0-9]{64}$", result.ChecksumSha256);
    }

    [Fact]
    public async Task Restore_IsBlockedWhenBackupDoesNotExist()
    {
        await using var environment = await RestoreTestEnvironment.CreateAsync();

        var result = await environment.RestoreService.RestoreFromBackupAsync(
            environment.Request(Path.Combine(environment.Root, "missing.db")));

        Assert.False(result.Succeeded);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Restore_IsBlockedWhenReasonIsMissing()
    {
        await using var environment = await RestoreTestEnvironment.CreateAsync();
        var backup = await environment.CreateBackupAsync();
        var request = environment.Request(backup.BackupFilePath);
        request.Reason = string.Empty;

        var result = await environment.RestoreService.RestoreFromBackupAsync(request);

        Assert.False(result.Succeeded);
        Assert.Contains("reason", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Restore_IsBlockedWhenConfirmationIsNotExact()
    {
        await using var environment = await RestoreTestEnvironment.CreateAsync();
        var backup = await environment.CreateBackupAsync();
        var request = environment.Request(backup.BackupFilePath);
        request.ConfirmationText = "restore";

        var result = await environment.RestoreService.RestoreFromBackupAsync(request);

        Assert.False(result.Succeeded);
        Assert.Contains("RESTORE", result.ErrorMessage);
    }

    [Fact]
    public async Task Restore_RequiresMandatorySafetyBackup()
    {
        await using var environment = await RestoreTestEnvironment.CreateAsync();
        var backup = await environment.CreateBackupAsync();
        var request = environment.Request(backup.BackupFilePath);
        request.CreateSafetyBackup = false;

        var result = await environment.RestoreService.RestoreFromBackupAsync(request);

        Assert.False(result.Succeeded);
        Assert.Contains("mandatory", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Restore_CreatesSafetyBackupBeforeStaging()
    {
        await using var environment = await RestoreTestEnvironment.CreateAsync();
        var backup = await environment.CreateBackupAsync();

        var result = await environment.RestoreService.RestoreFromBackupAsync(
            environment.Request(backup.BackupFilePath));

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(result.RequiresApplicationRestart);
        Assert.True(File.Exists(result.SafetyBackupFilePath));
        Assert.Contains("Restore Safety", result.SafetyBackupFilePath);
    }

    [Fact]
    public async Task Restore_WritesVerifiedPendingRequest()
    {
        await using var environment = await RestoreTestEnvironment.CreateAsync();
        var backup = await environment.CreateBackupAsync();

        var result = await environment.RestoreService.RestoreFromBackupAsync(
            environment.Request(backup.BackupFilePath));
        var pendingPath = PendingRestoreProcessor.GetPendingRequestPath(
            environment.Database.DatabasePath);
        var metadata = JsonSerializer.Deserialize<PendingRestoreMetadata>(
            await File.ReadAllTextAsync(pendingPath));

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.NotNull(metadata);
        Assert.True(File.Exists(metadata.StagedBackupFilePath));
        Assert.Equal(backup.ChecksumSha256, metadata.ChecksumSha256);
    }

    [Fact]
    public async Task RestoreHistory_RowIsSavedForAttempt()
    {
        await using var environment = await RestoreTestEnvironment.CreateAsync();
        var backup = await environment.CreateBackupAsync();

        await environment.RestoreService.RestoreFromBackupAsync(
            environment.Request(backup.BackupFilePath));
        var history = await environment.Database.Context.RestoreHistories.SingleAsync();

        Assert.Equal("PendingRestart", history.Status);
        Assert.False(string.IsNullOrWhiteSpace(history.SafetyBackupFilePath));
        Assert.Matches("^[A-F0-9]{64}$", history.ChecksumSha256);
    }

    [Fact]
    public async Task UnauthorizedUser_CannotRestoreAndAttemptIsLogged()
    {
        await using var environment = await RestoreTestEnvironment.CreateAsync(isAdmin: false);
        var backupPath = await environment.CreateIndependentBackupFileAsync();

        var result = await environment.RestoreService.RestoreFromBackupAsync(
            environment.Request(backupPath));

        Assert.False(result.Succeeded);
        Assert.Contains("cannot restore", result.ErrorMessage);
        Assert.Contains(
            await environment.Database.Context.ActivityLogs.Select(item => item.Action).ToListAsync(),
            action => action == "Restore Failed");
    }

    [Fact]
    public async Task Admin_CanStageRestore()
    {
        await using var environment = await RestoreTestEnvironment.CreateAsync();
        var backup = await environment.CreateBackupAsync();

        var result = await environment.RestoreService.RestoreFromBackupAsync(
            environment.Request(backup.BackupFilePath));

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal("PendingRestart",
            (await environment.Database.Context.RestoreHistories.SingleAsync()).Status);
    }

    [Fact]
    public async Task PendingRestoreMetadata_ExcludesSmtpPassword()
    {
        await using var environment = await RestoreTestEnvironment.CreateAsync();
        await environment.Database.SetSystemSettingAsync(
            "SmtpPassword",
            "restore-test-secret",
            "Notifications");
        var backup = await environment.CreateBackupAsync();

        await environment.RestoreService.RestoreFromBackupAsync(
            environment.Request(backup.BackupFilePath));
        var json = await File.ReadAllTextAsync(
            PendingRestoreProcessor.GetPendingRequestPath(environment.Database.DatabasePath));

        Assert.DoesNotContain("restore-test-secret", json);
        Assert.DoesNotContain("SmtpPassword", json);
    }

    [Fact]
    public async Task CorruptedBackup_IsRejected()
    {
        await using var environment = await RestoreTestEnvironment.CreateAsync();
        var corruptPath = Path.Combine(environment.Root, "corrupt.db");
        await File.WriteAllTextAsync(corruptPath, "not a sqlite database");

        var result = await environment.RestoreService.RestoreFromBackupAsync(
            environment.Request(corruptPath));

        Assert.False(result.Succeeded);
        Assert.False(File.Exists(
            PendingRestoreProcessor.GetPendingRequestPath(environment.Database.DatabasePath)));
    }

    [Fact]
    public async Task StartupProcessor_ReplacesDatabaseAndPerformsPostRestoreIntegrityCheck()
    {
        await using var environment = await RestoreTestEnvironment.CreateAsync();
        var backup = await environment.CreateBackupAsync();
        var request = environment.Request(backup.BackupFilePath);
        var staged = await environment.RestoreService.RestoreFromBackupAsync(request);
        var targetPath = environment.Database.DatabasePath;
        await environment.Database.Context.DisposeAsync();

        var result = await PendingRestoreProcessor.ApplyPendingRestoreAsync(targetPath);
        var validation = await RestoreSqliteUtility.ValidateAsync(targetPath);

        Assert.True(staged.Succeeded, staged.ErrorMessage);
        Assert.NotNull(result);
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(validation.Succeeded, validation.ErrorMessage);
        Assert.False(File.Exists(PendingRestoreProcessor.GetPendingRequestPath(targetPath)));
    }

    [Fact]
    public async Task StartupProcessor_RollsBackWhenFailureOccursAfterReplacement()
    {
        await using var environment = await RestoreTestEnvironment.CreateAsync();
        var backup = await environment.CreateBackupAsync();
        await environment.RestoreService.RestoreFromBackupAsync(
            environment.Request(backup.BackupFilePath));
        var targetPath = environment.Database.DatabasePath;
        var originalChecksum = (await RestoreSqliteUtility.ValidateAsync(targetPath)).ChecksumSha256;
        await environment.Database.Context.DisposeAsync();

        var result = await PendingRestoreProcessor.ApplyPendingRestoreAsync(
            targetPath,
            _ => throw new InvalidOperationException("Simulated post-replacement failure."));
        var restoredChecksum = (await RestoreSqliteUtility.ValidateAsync(targetPath)).ChecksumSha256;

        Assert.NotNull(result);
        Assert.True(result.RolledBack, result.ErrorMessage);
        Assert.Equal("RolledBack", result.Status);
        Assert.Equal(originalChecksum, restoredChecksum);
    }

    [Fact]
    public async Task RestoreHistoryAndSummary_ReturnLatestPendingAttempt()
    {
        await using var environment = await RestoreTestEnvironment.CreateAsync();
        var backup = await environment.CreateBackupAsync();
        await environment.RestoreService.RestoreFromBackupAsync(
            environment.Request(backup.BackupFilePath));

        var history = await environment.RestoreService.GetRestoreHistoryAsync(
            new RestoreHistoryFilter());
        var summary = await environment.RestoreService.GetRestoreStatusSummaryAsync();

        Assert.Single(history);
        Assert.Equal("PendingRestart", history[0].Status);
        Assert.Equal(1, summary.TotalRestores);
        Assert.Equal(1, summary.PendingRestarts);
    }

    [Fact]
    public async Task CompatibilityInitializer_CreatesRestoreHistoryTableAndIndexes()
    {
        await using var environment = await RestoreTestEnvironment.CreateAsync();

        await DatabaseCompatibilityInitializer.ApplyAsync(environment.Database.Context);
        var connection = environment.Database.Context.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE (type='table' AND name='RestoreHistories') " +
            "OR (type='index' AND name LIKE 'IX_RestoreHistories_%');";
        var count = Convert.ToInt32(await command.ExecuteScalarAsync());

        Assert.Equal(4, count);
    }

    [Fact]
    public async Task RestoreActions_WritePreviewValidationSafetyAndStagingLogs()
    {
        await using var environment = await RestoreTestEnvironment.CreateAsync();
        var backup = await environment.CreateBackupAsync();
        await environment.RestoreService.PreviewRestoreAsync(backup.BackupFilePath);
        await environment.RestoreService.RestoreFromBackupAsync(
            environment.Request(backup.BackupFilePath));

        var actions = await environment.Database.Context.ActivityLogs
            .Select(item => item.Action)
            .ToListAsync();

        Assert.Contains("Restore Preview Completed", actions);
        Assert.Contains("Restore Validation Passed", actions);
        Assert.Contains("Restore Safety Backup Created", actions);
        Assert.Contains("Restore Staged", actions);
    }

    [Fact]
    public async Task ExistingBackupCreation_StillWorksAfterRestoreFeatureRegistration()
    {
        await using var environment = await RestoreTestEnvironment.CreateAsync();

        var backup = await environment.CreateBackupAsync();

        Assert.True(backup.Succeeded, backup.ErrorMessage);
        Assert.True(File.Exists(backup.BackupFilePath));
    }

    private sealed class RestoreTestEnvironment : IAsyncDisposable
    {
        private RestoreTestEnvironment(
            SqliteTestDatabase database,
            BackupService backupService,
            RestoreService restoreService,
            User user,
            string root)
        {
            Database = database;
            BackupService = backupService;
            RestoreService = restoreService;
            User = user;
            Root = root;
        }

        public SqliteTestDatabase Database { get; }
        public BackupService BackupService { get; }
        public RestoreService RestoreService { get; }
        public User User { get; }
        public string Root { get; }

        public static async Task<RestoreTestEnvironment> CreateAsync(bool isAdmin = true)
        {
            var database = await SqliteTestDatabase.CreateAsync();
            var data = await database.AddCirculationDataAsync();
            data.User.FullName = isAdmin ? "Restore Administrator" : "Read Only User";
            data.User.UserRoles.Clear();
            data.User.UserRoles.Add(new UserRole
            {
                UserId = data.User.Id,
                Role = new Role { Name = isAdmin ? "Admin" : "Read Only Viewer" }
            });
            var root = Path.Combine(
                Path.GetTempPath(),
                "KicsitLibrary.Tests",
                "Restores",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            await database.SetSystemSettingAsync(
                "BackupDefaultFolder",
                Path.Combine(root, "Backups"),
                "Backup");
            var authentication = new FakeAuthenticationService(data.User, isAdmin);
            var backupService = new BackupService(database.Context, authentication);
            return new RestoreTestEnvironment(
                database,
                backupService,
                new RestoreService(database.Context, authentication, backupService),
                data.User,
                root);
        }

        public Task<BackupResult> CreateBackupAsync() =>
            BackupService.CreateBackupAsync(new BackupRequest
            {
                RequestedByUserId = User.Id,
                RequestedByUserName = User.FullName,
                DestinationFolder = Path.Combine(Root, "Backups"),
                IncludeMetadataFile = true,
                VerifyAfterCreation = true,
                Reason = "Restore workflow source backup"
            });

        public async Task<string> CreateIndependentBackupFileAsync()
        {
            var path = Path.Combine(Root, "independent.db");
            await using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE TestData (Id INTEGER PRIMARY KEY, Value TEXT); INSERT INTO TestData(Value) VALUES ('valid');";
            await command.ExecuteNonQueryAsync();
            return path;
        }

        public RestoreRequest Request(string backupFilePath) =>
            new()
            {
                BackupFilePath = backupFilePath,
                RequestedByUserId = User.Id,
                RequestedByUserName = User.FullName,
                Reason = "Approved test restore",
                CreateSafetyBackup = true,
                VerifyBeforeRestore = true,
                VerifyAfterRestore = true,
                RequireConfirmationText = true,
                ConfirmationText = "RESTORE",
                AllowRestoreWhileAppRunning = false
            };

        public async ValueTask DisposeAsync()
        {
            try
            {
                await Database.DisposeAsync();
            }
            catch (ObjectDisposedException)
            {
                // Startup processor tests close the context before replacing the file.
            }

            var workDirectory = Path.GetDirectoryName(
                PendingRestoreProcessor.GetPendingRequestPath(Database.DatabasePath));
            if (!string.IsNullOrWhiteSpace(workDirectory) && Directory.Exists(workDirectory))
            {
                Directory.Delete(workDirectory, recursive: true);
            }
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class FakeAuthenticationService(
        User currentUser,
        bool isAdmin) : IAuthenticationService
    {
        public User? CurrentUser { get; } = currentUser;
        public Task<User?> LoginAsync(string username, string password) =>
            Task.FromResult(CurrentUser);
        public Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword) =>
            Task.FromResult(false);
        public Task<bool> VerifyUserPermissionAsync(int userId, string permissionCode) =>
            Task.FromResult(isAdmin &&
                permissionCode is "VIEW_BACKUPS" or "MANAGE_BACKUPS" or
                    "VIEW_RESTORES" or "MANAGE_RESTORES");
        public Task LogoutAsync() => Task.CompletedTask;
    }
}
