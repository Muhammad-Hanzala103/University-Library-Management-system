using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Data.Repositories;
using KicsitLibrary.Services;
using KicsitLibrary.Services.Logging;
using KicsitLibrary.Services.Runtime;
using KicsitLibrary.Tests.Infrastructure;
using Xunit;

namespace KicsitLibrary.Tests;

/// <summary>
/// Phase 12D — Release Security Hardening verification tests.
/// Ensures that public documentation does not expose sensitive credentials,
/// settings exports exclude sensitive values, and required security
/// documentation files exist in the repository.
/// </summary>
public sealed class SecurityHardeningTests
{
    // ─── Repo root resolution ───

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10; i++)
        {
            if (File.Exists(Path.Combine(dir, "KicsitLibrary.slnx")))
                return dir;
            var parent = Directory.GetParent(dir)?.FullName;
            if (parent == null) break;
            dir = parent;
        }
        return AppContext.BaseDirectory;
    }

    private static bool IsRepoRootValid()
    {
        var root = FindRepoRoot();
        return Directory.Exists(Path.Combine(root, "KicsitLibrary.Core"));
    }

    // ─── Password exposure tests ───

    [Theory]
    [InlineData("README.md")]
    [InlineData("INSTALLATION GUIDE.md")]
    public void PublicDoc_DoesNotContain_SeededPasswords(string fileName)
    {
        if (!IsRepoRootValid()) return;
        var filePath = Path.Combine(FindRepoRoot(), fileName);
        if (!File.Exists(filePath)) return;

        var content = File.ReadAllText(filePath);
        var passwords = new[]
        {
            "SuperAdmin123!",
            "Admin123!",
            "Librarian123!",
            "Assistant123!",
            "Auditor123!",
            "Viewer123!"
        };

        foreach (var password in passwords)
        {
            Assert.DoesNotContain(password, content);
        }
    }

    [Fact]
    public void DemoChecklist_DoesNotContain_PlaintextPassword()
    {
        if (!IsRepoRootValid()) return;
        var filePath = Path.Combine(FindRepoRoot(), "DEMO CHECKLIST.md");
        if (!File.Exists(filePath)) return;

        var content = File.ReadAllText(filePath);
        Assert.DoesNotContain("Admin123!", content);
        Assert.DoesNotContain("SuperAdmin123!", content);
    }

    [Fact]
    public void ReleaseNotes_DoesNotContain_PlaintextPassword()
    {
        if (!IsRepoRootValid()) return;
        var filePath = Path.Combine(FindRepoRoot(), "RELEASE NOTES.md");
        if (!File.Exists(filePath)) return;

        var content = File.ReadAllText(filePath);
        var passwords = new[] { "SuperAdmin123!", "Admin123!", "Librarian123!", "Assistant123!", "Auditor123!", "Viewer123!" };
        foreach (var password in passwords)
        {
            Assert.DoesNotContain(password, content);
        }
    }

    // ─── Settings export masking ───

    [Fact]
    public async Task SettingsExport_ExcludesSmtpPassword()
    {
        await using var database = await SqliteTestDatabase.CreateAsync(seed: true);
        await database.SetSystemSettingAsync("SmtpPassword", "MySecretPassword123", "Notifications");

        var auth = new FakeAuthService(null, false);
        var logService = new ActivityLogService(new Repository<ActivityLog>(database.Context));
        var runtimePaths = new RuntimePathService(database.Context);
        var service = new SettingsManagementService(database.Context, auth, logService, runtimePaths);

        var result = await service.ExportSettingsSnapshotAsync(null);
        Assert.True(result.Succeeded);
        Assert.True(File.Exists(result.FilePath));

        var content = File.ReadAllText(result.FilePath!);
        Assert.DoesNotContain("SmtpPassword", content);
        Assert.DoesNotContain("MySecretPassword123", content);

        File.Delete(result.FilePath!);
    }

    // ─── Security documentation existence ───

    [Theory]
    [InlineData("SECURITY CHECKLIST.md")]
    [InlineData("RELEASE SECURITY NOTES.md")]
    public void SecurityDocument_Exists(string fileName)
    {
        if (!IsRepoRootValid()) return;
        var filePath = Path.Combine(FindRepoRoot(), fileName);
        Assert.True(File.Exists(filePath), $"{fileName} must exist in the repository root.");
    }

    [Fact]
    public void SecurityScanScript_Exists()
    {
        if (!IsRepoRootValid()) return;
        var scriptPath = Path.Combine(FindRepoRoot(), "scripts", "security_scan.ps1");
        Assert.True(File.Exists(scriptPath), "scripts/security_scan.ps1 must exist.");
    }

    // ─── Gitignore coverage ───

    [Fact]
    public void Gitignore_ExcludesPrivateCredentialTemplate()
    {
        if (!IsRepoRootValid()) return;
        var gitignorePath = Path.Combine(FindRepoRoot(), ".gitignore");
        if (!File.Exists(gitignorePath)) return;

        var content = File.ReadAllText(gitignorePath);
        Assert.Contains("DEMO CREDENTIALS PRIVATE TEMPLATE", content);
    }

    // ─── Helpers ───

    private sealed class FakeAuthService : IAuthenticationService
    {
        private readonly bool _isAdmin;

        public FakeAuthService(User? currentUser, bool isAdmin)
        {
            CurrentUser = currentUser;
            _isAdmin = isAdmin;
        }

        public User? CurrentUser { get; }
        public Task<User?> LoginAsync(string username, string password) => Task.FromResult(CurrentUser);
        public Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword) => Task.FromResult(false);
        public Task<bool> VerifyUserPermissionAsync(int userId, string permissionCode) => Task.FromResult(_isAdmin);
        public Task LogoutAsync() => Task.CompletedTask;
    }
}
