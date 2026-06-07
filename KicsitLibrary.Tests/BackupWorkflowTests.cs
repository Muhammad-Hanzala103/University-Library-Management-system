using System.IO.Compression;
using System.Text.RegularExpressions;
using KicsitLibrary.Core;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Services.Backup;
using KicsitLibrary.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Tests;

public class BackupWorkflowTests
{
    [Fact]
    public async Task BackupService_CreatesRealDatabaseBackupFile()
    {
        await using var environment = await BackupTestEnvironment.CreateAsync();

        var result = await environment.Service.CreateBackupAsync(environment.Request());

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(File.Exists(result.BackupFilePath));
        Assert.True(result.BackupSizeBytes > 0);
    }

    [Fact]
    public async Task BackupVerification_RunsIntegrityCheckAndPasses()
    {
        await using var environment = await BackupTestEnvironment.CreateAsync();
        var backup = await environment.Service.CreateBackupAsync(environment.Request());

        var verification = await environment.Service.VerifyBackupAsync(
            backup.BackupFilePath,
            backup.BackupHistoryId);

        Assert.True(verification.Succeeded, verification.ErrorMessage);
        Assert.True(verification.IntegrityCheckPassed);
        Assert.Equal("Backup integrity check passed.", verification.Message);
    }

    [Fact]
    public async Task Backup_ComputesSha256Checksum()
    {
        await using var environment = await BackupTestEnvironment.CreateAsync();

        var result = await environment.Service.CreateBackupAsync(environment.Request());

        Assert.Matches("^[A-F0-9]{64}$", result.ChecksumSha256);
    }

    [Fact]
    public async Task BackupHistory_RowIsSaved()
    {
        await using var environment = await BackupTestEnvironment.CreateAsync();

        var result = await environment.Service.CreateBackupAsync(environment.Request());
        var history = await environment.Database.Context.BackupHistories.SingleAsync();

        Assert.Equal(result.BackupFilePath, history.BackupFilePath);
        Assert.Equal("Completed", history.Status);
        Assert.Equal("Passed", history.VerificationStatus);
    }

    [Fact]
    public async Task BackupMetadata_ExcludesSmtpPassword()
    {
        await using var environment = await BackupTestEnvironment.CreateAsync();
        environment.Database.Context.SystemSettings.Add(new SystemSettings
        {
            Key = "SmtpPassword",
            Value = "top-secret-test-password",
            Group = "Notifications"
        });
        await environment.Database.Context.SaveChangesAsync();

        var result = await environment.Service.CreateBackupAsync(environment.Request());
        var metadata = await File.ReadAllTextAsync(result.MetadataFilePath);

        Assert.Contains(ProductBrand.Name, metadata);
        Assert.DoesNotContain("top-secret-test-password", metadata);
        Assert.DoesNotContain("SmtpPassword", metadata);
    }

    [Fact]
    public async Task BackupFileName_IsTimestampedAndSanitized()
    {
        await using var environment = await BackupTestEnvironment.CreateAsync(
            fullName: "Admin: Test/User*");

        var result = await environment.Service.CreateBackupAsync(environment.Request());
        var fileName = Path.GetFileName(result.BackupFilePath);

        Assert.Matches(
            new Regex(@"^Ilm-o-Kutub_Backup_Admin__Test_User_\d{8}_\d{6}_\d{3}\.db$"),
            fileName);
        Assert.DoesNotContain(":", fileName);
        Assert.DoesNotContain("/", fileName);
    }

    [Fact]
    public async Task SecondBackup_DoesNotOverwriteFirstBackup()
    {
        await using var environment = await BackupTestEnvironment.CreateAsync();

        var first = await environment.Service.CreateBackupAsync(environment.Request());
        var second = await environment.Service.CreateBackupAsync(environment.Request());

        Assert.NotEqual(first.BackupFilePath, second.BackupFilePath);
        Assert.True(File.Exists(first.BackupFilePath));
        Assert.True(File.Exists(second.BackupFilePath));
    }

    [Fact]
    public async Task Compression_CreatesZipWithDatabaseAndMetadata()
    {
        await using var environment = await BackupTestEnvironment.CreateAsync();
        var request = environment.Request();
        request.CompressBackup = true;

        var result = await environment.Service.CreateBackupAsync(request);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(File.Exists(result.CompressedFilePath));
        using var archive = ZipFile.OpenRead(result.CompressedFilePath);
        Assert.Contains(archive.Entries, entry => entry.Name.EndsWith(".db"));
        Assert.Contains(archive.Entries, entry => entry.Name.EndsWith(".metadata.json"));
    }

    [Fact]
    public async Task InvalidDestination_ReturnsClearFailureAndHistory()
    {
        await using var environment = await BackupTestEnvironment.CreateAsync();
        var invalidFolder = Path.Combine(environment.BackupFolder, "existing-file");
        await File.WriteAllTextAsync(invalidFolder, "not a directory");
        var request = environment.Request();
        request.DestinationFolder = invalidFolder;

        var result = await environment.Service.CreateBackupAsync(request);

        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        Assert.Contains(
            await environment.Database.Context.BackupHistories.ToListAsync(),
            item => item.Status == "Failed");
    }

    [Fact]
    public async Task BackupCreationAndVerification_WriteActivityLogs()
    {
        await using var environment = await BackupTestEnvironment.CreateAsync();

        await environment.Service.CreateBackupAsync(environment.Request());
        var actions = await environment.Database.Context.ActivityLogs
            .Select(item => item.Action)
            .ToListAsync();

        Assert.Contains("Backup Created", actions);
        Assert.Contains("Backup Verification Passed", actions);
    }

    [Fact]
    public async Task GetBackupHistory_ReturnsLatestRecordsFirst()
    {
        await using var environment = await BackupTestEnvironment.CreateAsync();
        var first = await environment.Service.CreateBackupAsync(environment.Request("First"));
        await Task.Delay(5);
        var second = await environment.Service.CreateBackupAsync(environment.Request("Second"));

        var history = await environment.Service.GetBackupHistoryAsync(new BackupHistoryFilter());

        Assert.Equal(2, history.Count);
        Assert.Equal(second.BackupFilePath, history[0].BackupFilePath);
        Assert.Equal(first.BackupFilePath, history[1].BackupFilePath);
    }

    [Fact]
    public async Task LowPrivilegeUser_CannotCreateBackup()
    {
        await using var environment = await BackupTestEnvironment.CreateAsync(isAdmin: false);

        var result = await environment.Service.CreateBackupAsync(environment.Request());

        Assert.False(result.Succeeded);
        Assert.Contains("cannot create", result.ErrorMessage);
        Assert.Empty(Directory.GetFiles(environment.BackupFolder, "*.db"));
    }

    [Fact]
    public async Task BackupStatusSummary_ReflectsSuccessfulVerifiedBackup()
    {
        await using var environment = await BackupTestEnvironment.CreateAsync();
        await environment.Service.CreateBackupAsync(environment.Request());

        var summary = await environment.Service.GetBackupStatusSummaryAsync();

        Assert.Equal(1, summary.TotalBackups);
        Assert.Equal(1, summary.SuccessfulBackups);
        Assert.Equal(1, summary.VerifiedBackups);
        Assert.True(summary.TotalBackupSizeBytes > 0);
    }

    private sealed class BackupTestEnvironment : IAsyncDisposable
    {
        private BackupTestEnvironment(
            SqliteTestDatabase database,
            BackupService service,
            User user,
            string backupFolder)
        {
            Database = database;
            Service = service;
            User = user;
            BackupFolder = backupFolder;
        }

        public SqliteTestDatabase Database { get; }
        public BackupService Service { get; }
        public User User { get; }
        public string BackupFolder { get; }

        public static async Task<BackupTestEnvironment> CreateAsync(
            bool isAdmin = true,
            string fullName = "Backup Administrator")
        {
            var database = await SqliteTestDatabase.CreateAsync();
            var data = await database.AddCirculationDataAsync();
            data.User.FullName = fullName;
            data.User.UserRoles.Clear();
            data.User.UserRoles.Add(new UserRole
            {
                UserId = data.User.Id,
                Role = new Role
                {
                    Name = isAdmin ? "Admin" : "Read Only Viewer"
                }
            });
            var authentication = new FakeAuthenticationService(
                data.User,
                canView: isAdmin,
                canManage: isAdmin);
            var backupFolder = Path.Combine(
                Path.GetTempPath(),
                "KicsitLibrary.Tests",
                "Backups",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(backupFolder);
            return new BackupTestEnvironment(
                database,
                new BackupService(database.Context, authentication),
                data.User,
                backupFolder);
        }

        public BackupRequest Request(string reason = "Test backup") =>
            new()
            {
                RequestedByUserId = User.Id,
                RequestedByUserName = User.FullName,
                DestinationFolder = BackupFolder,
                IncludeTimestamp = true,
                IncludeMetadataFile = true,
                VerifyAfterCreation = true,
                Reason = reason
            };

        public async ValueTask DisposeAsync()
        {
            await Database.DisposeAsync();
            if (Directory.Exists(BackupFolder))
            {
                Directory.Delete(BackupFolder, recursive: true);
            }
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
                _ => false
            });
        public Task LogoutAsync() => Task.CompletedTask;
    }
}
