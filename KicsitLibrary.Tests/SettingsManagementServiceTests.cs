using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using KicsitLibrary.Core;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data.Repositories;
using KicsitLibrary.Services;
using KicsitLibrary.Services.Logging;
using KicsitLibrary.Services.Runtime;
using KicsitLibrary.Tests.Infrastructure;

namespace KicsitLibrary.Tests
{
    public class SettingsManagementServiceTests
    {
        [Fact]
        public async Task GetSettingsAsync_ReturnsSeededSettings()
        {
            await using var database = await SqliteTestDatabase.CreateAsync(seed: true);
            var service = CreateService(database);

            var settings = await service.GetSettingsAsync();

            Assert.NotEmpty(settings);
            Assert.Contains(settings, s => s.Key == "DefaultIssueDays");
            Assert.Contains(settings, s => s.Key == "FinePerDay");
        }

        [Fact]
        public async Task GetSettingsByCategoryAsync_FiltersCorrectly()
        {
            await using var database = await SqliteTestDatabase.CreateAsync(seed: true);
            var service = CreateService(database);

            var generalSettings = await service.GetSettingsByCategoryAsync("General");

            Assert.NotEmpty(generalSettings);
            Assert.All(generalSettings, s => Assert.Equal("General", s.Category));
        }

        [Fact]
        public async Task GetSettingDetailsAsync_ReturnsSetting()
        {
            await using var database = await SqliteTestDatabase.CreateAsync(seed: true);
            var service = CreateService(database);

            var setting = await service.GetSettingDetailsAsync("DefaultIssueDays");

            Assert.NotNull(setting);
            Assert.Equal("DefaultIssueDays", setting.Key);
            Assert.Equal("General", setting.Category);
        }

        [Fact]
        public async Task MaskSensitiveValue_SmtpPassword_IsMasked()
        {
            await using var database = await SqliteTestDatabase.CreateAsync(seed: true);
            await database.SetSystemSettingAsync("SmtpPassword", "SuperSecretPassword", "Email and SMTP");
            var service = CreateService(database);

            var setting = await service.GetSettingDetailsAsync("SmtpPassword");

            Assert.NotNull(setting);
            Assert.Equal("***", setting.MaskedValue);
        }

        [Fact]
        public async Task GetSettingsAsync_SmtpPassword_IsNeverReturnedInPlainText()
        {
            await using var database = await SqliteTestDatabase.CreateAsync(seed: true);
            await database.SetSystemSettingAsync("SmtpPassword", "SuperSecretPassword", "Email and SMTP");
            var service = CreateService(database);

            var settings = await service.GetSettingsAsync();
            var passwordSetting = settings.First(s => s.Key == "SmtpPassword");

            Assert.Equal("***", passwordSetting.MaskedValue);
            // Verify that while the database has the actual value, the UI-facing View masks it
            Assert.True(passwordSetting.IsSensitive);
        }

        [Fact]
        public async Task UpdateSettingAsync_Admin_CanUpdateValidSetting()
        {
            await using var database = await SqliteTestDatabase.CreateAsync(seed: true);
            var adminUser = await GetSeededAdminUserAsync(database);
            var auth = new FakeAuthenticationService(adminUser, isAdmin: true);
            var service = CreateService(database, auth);

            var result = await service.UpdateSettingAsync(new SettingsUpdateRequest
            {
                Key = "DefaultIssueDays",
                NewValue = "21",
                Reason = "Updated library policy"
            });

            Assert.True(result.Succeeded);
            var updated = await service.GetSettingDetailsAsync("DefaultIssueDays");
            Assert.Equal("21", updated!.Value);
        }

        [Fact]
        public async Task UpdateSettingAsync_NonAdmin_CannotUpdateSetting()
        {
            await using var database = await SqliteTestDatabase.CreateAsync(seed: true);
            var regularUser = new User
            {
                Id = 999,
                Username = "viewer",
                FullName = "Regular Viewer"
            };
            var auth = new FakeAuthenticationService(regularUser, isAdmin: false);
            var service = CreateService(database, auth);

            var result = await service.UpdateSettingAsync(new SettingsUpdateRequest
            {
                Key = "DefaultIssueDays",
                NewValue = "21",
                Reason = "Attempted hack"
            });

            Assert.False(result.Succeeded);
            Assert.Contains("permissions", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

            // Verify unauthorized attempt logged to ActivityLog
            var logs = database.Context.ActivityLogs.ToList();
            Assert.Contains(logs, l => l.Action == "Settings Update Unauthorized");
        }

        [Fact]
        public async Task UpdateSettingAsync_InvalidSmtpPort_IsRejected()
        {
            await using var database = await SqliteTestDatabase.CreateAsync(seed: true);
            var adminUser = await GetSeededAdminUserAsync(database);
            var auth = new FakeAuthenticationService(adminUser, isAdmin: true);
            var service = CreateService(database, auth);

            var result = await service.UpdateSettingAsync(new SettingsUpdateRequest
            {
                Key = "SmtpPort",
                NewValue = "70000" // Invalid port (> 65535)
            });

            Assert.False(result.Succeeded);
            Assert.Contains("between 1 and 65535", result.ErrorMessage);
        }

        [Fact]
        public async Task UpdateSettingAsync_InvalidBoolean_IsRejected()
        {
            await using var database = await SqliteTestDatabase.CreateAsync(seed: true);
            var adminUser = await GetSeededAdminUserAsync(database);
            var auth = new FakeAuthenticationService(adminUser, isAdmin: true);
            var service = CreateService(database, auth);

            var result = await service.UpdateSettingAsync(new SettingsUpdateRequest
            {
                Key = "SmtpUseSsl",
                NewValue = "Maybe" // Invalid boolean
            });

            Assert.False(result.Succeeded);
            Assert.Contains("Value must be 'True' or 'False'", result.ErrorMessage);
        }

        [Fact]
        public async Task UpdateSettingAsync_InvalidDocumentExtensions_IsRejected()
        {
            await using var database = await SqliteTestDatabase.CreateAsync(seed: true);
            var adminUser = await GetSeededAdminUserAsync(database);
            var auth = new FakeAuthenticationService(adminUser, isAdmin: true);
            var service = CreateService(database, auth);

            // 1. Extension not starting with dot
            var result1 = await service.UpdateSettingAsync(new SettingsUpdateRequest
            {
                Key = "DocumentAllowedExtensions",
                NewValue = "pdf,docx"
            });
            Assert.False(result1.Succeeded);
            Assert.Contains("start with a dot", result1.ErrorMessage);

            // 2. Dangerous executable extension
            var result2 = await service.UpdateSettingAsync(new SettingsUpdateRequest
            {
                Key = "DocumentAllowedExtensions",
                NewValue = ".pdf,.exe"
            });
            Assert.False(result2.Succeeded);
            Assert.Contains("security reasons", result2.ErrorMessage);
        }

        [Fact]
        public async Task UpdateSettingAsync_DatabaseFileNameWithPathSeparator_IsRejected()
        {
            await using var database = await SqliteTestDatabase.CreateAsync(seed: true);
            var adminUser = await GetSeededAdminUserAsync(database);
            var auth = new FakeAuthenticationService(adminUser, isAdmin: true);
            var service = CreateService(database, auth);

            var result = await service.UpdateSettingAsync(new SettingsUpdateRequest
            {
                Key = "DatabaseFileName",
                NewValue = "folder/KicsitLibrary.db"
            });

            Assert.False(result.Succeeded);
            Assert.Contains("path separators", result.ErrorMessage);
        }

        [Fact]
        public async Task UpdateSettingAsync_RuntimeDataRootWithTraversal_IsRejected()
        {
            await using var database = await SqliteTestDatabase.CreateAsync(seed: true);
            var adminUser = await GetSeededAdminUserAsync(database);
            var auth = new FakeAuthenticationService(adminUser, isAdmin: true);
            var service = CreateService(database, auth);

            var result = await service.UpdateSettingAsync(new SettingsUpdateRequest
            {
                Key = "RuntimeDataRoot",
                NewValue = "C:\\app\\..\\traversal"
            });

            Assert.False(result.Succeeded);
            Assert.Contains("traversal", result.ErrorMessage);
        }

        [Fact]
        public async Task ResetSettingToDefaultAsync_ResetsCorrectly()
        {
            await using var database = await SqliteTestDatabase.CreateAsync(seed: true);
            var adminUser = await GetSeededAdminUserAsync(database);
            var auth = new FakeAuthenticationService(adminUser, isAdmin: true);
            var service = CreateService(database, auth);

            // First edit it to some non-default value
            await service.UpdateSettingAsync(new SettingsUpdateRequest
            {
                Key = "DefaultIssueDays",
                NewValue = "45"
            });

            // Now reset it
            var result = await service.ResetSettingToDefaultAsync("DefaultIssueDays", adminUser.Id, "Policy reset");

            Assert.True(result.Succeeded);
            var setting = await service.GetSettingDetailsAsync("DefaultIssueDays");
            Assert.Equal("14", setting!.Value); // 14 is default
        }

        [Fact]
        public async Task UpdateSettingAsync_WritesActivityLog()
        {
            await using var database = await SqliteTestDatabase.CreateAsync(seed: true);
            var adminUser = await GetSeededAdminUserAsync(database);
            var auth = new FakeAuthenticationService(adminUser, isAdmin: true);
            var service = CreateService(database, auth);

            await service.UpdateSettingAsync(new SettingsUpdateRequest
            {
                Key = "DefaultIssueDays",
                NewValue = "25",
                Reason = "Audit requirement"
            });

            var logs = database.Context.ActivityLogs.ToList();
            Assert.Contains(logs, l => l.Action == "Settings Updated" && l.Detail.Contains("DefaultIssueDays"));
        }

        [Fact]
        public async Task ExportSettingsSnapshotAsync_MasksSensitiveValues()
        {
            await using var database = await SqliteTestDatabase.CreateAsync(seed: true);
            var adminUser = await GetSeededAdminUserAsync(database);
            await database.SetSystemSettingAsync("SmtpPassword", "SuperSecretPassword", "Email and SMTP");
            var auth = new FakeAuthenticationService(adminUser, isAdmin: true);
            var service = CreateService(database, auth);

            var result = await service.ExportSettingsSnapshotAsync(adminUser.Id);

            Assert.True(result.Succeeded);
            Assert.True(File.Exists(result.FilePath));

            var content = await File.ReadAllTextAsync(result.FilePath);
            Assert.DoesNotContain("SuperSecretPassword", content);
            Assert.DoesNotContain("SmtpPassword", content); // Excluded entirely from serialization
            Assert.Contains("FinePerDay", content);

            // Cleanup
            if (File.Exists(result.FilePath))
            {
                File.Delete(result.FilePath);
            }
        }

        #region Helpers

        private static SettingsManagementService CreateService(SqliteTestDatabase database, IAuthenticationService? auth = null)
        {
            var context = database.Context;
            var authService = auth ?? new FakeAuthenticationService(null, false);
            var logService = new ActivityLogService(new Repository<ActivityLog>(context));
            var runtimePaths = new RuntimePathService(context);

            return new SettingsManagementService(context, authService, logService, runtimePaths);
        }

        private static async Task<User> GetSeededAdminUserAsync(SqliteTestDatabase database)
        {
            // SeedAsync seeds a Super Admin user. Let's find it.
            var admin = database.Context.Users
                .FirstOrDefault(u => u.Username == "admin");

            if (admin == null)
            {
                admin = new User
                {
                    Id = 1,
                    Username = "admin",
                    FullName = "Administrator"
                };
                database.Context.Users.Add(admin);
                await database.Context.SaveChangesAsync();
            }

            return admin;
        }

        private sealed class FakeAuthenticationService : IAuthenticationService
        {
            private readonly bool _isAdmin;

            public FakeAuthenticationService(User? currentUser, bool isAdmin)
            {
                CurrentUser = currentUser;
                _isAdmin = isAdmin;
            }

            public User? CurrentUser { get; }

            public Task<User?> LoginAsync(string username, string password) =>
                Task.FromResult(CurrentUser);

            public Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword) =>
                Task.FromResult(false);

            public Task<bool> VerifyUserPermissionAsync(int userId, string permissionCode) =>
                Task.FromResult(_isAdmin);

            public Task<(bool Success, string Message)> RequestPasswordResetAsync(string usernameOrEmail) => Task.FromResult((true, ""));
            public Task<bool> ResetPasswordAsync(string usernameOrEmail, string token, string newPassword) => Task.FromResult(true);
            public Task<bool> GenerateAndSendOtpAsync(int userId) => Task.FromResult(true);
            public Task<bool> VerifyOtpAsync(int userId, string otp) => Task.FromResult(true);
            public Task LogoutAsync() => Task.CompletedTask;
        }

        #endregion
    }
}


