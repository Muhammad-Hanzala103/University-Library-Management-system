using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data;

namespace KicsitLibrary.Services
{
    public sealed class SettingsManagementService : ISettingsManagementService
    {
        private readonly KicsitLibraryDbContext _context;
        private readonly IAuthenticationService _authService;
        private readonly IActivityLogService _activityLogService;
        private readonly IRuntimePathService _runtimePathService;

        // Sensitive setting keys that should be masked
        private static readonly HashSet<string> SensitiveKeys = new()
        {
            "SmtpPassword",
            "SmtpUser"
        };

        // Settings that require application restart
        private static readonly HashSet<string> RestartRequiredKeys = new()
        {
            "UseReleaseDataRoot",
            "RuntimeDataRoot",
            "RuntimeStorageMode",
            "DatabaseFileName"
        };

        // Settings grouped by category
        private static readonly Dictionary<string, string[]> CategoryMappings = new()
        {
            ["General"] = new[] { "FinePerDay", "StudentIssueLimit", "FacultyIssueLimit", "StaffIssueLimit", "DefaultIssueDays", "ReservationExpiryDays" },
            ["Branding"] = new[] { "SmtpFromName", "ReportHeader", "ReportFooter" },
            ["Email and SMTP"] = new[] { "SmtpHost", "SmtpPort", "SmtpUseSsl", "SmtpUser", "SmtpPassword", "SmtpFromEmail", "SmtpFromName", "EmailNotificationEnabled" },
            ["Backup"] = new[] { "BackupDefaultFolder", "BackupVerifyAfterCreation", "BackupCompressionEnabled" },
            ["Restore"] = new[] { "RestoreStagingRoot", "RestoreCheckInability" },
            ["Automatic Backup"] = new[] { "AutomaticBackupEnabled", "AutomaticBackupIntervalHours", "AutomaticBackupStartTime", "MaxBackupRetryCount" },
            ["Retention"] = new[] { "BackupRetentionDays", "AutomaticBackupRetentionDays" },
            ["Ownership and Locks"] = new[] { "SingleInstanceMode", "CriticalOperationLockTimeoutSeconds", "LockFileRetentionMinutes" },
            ["Documents"] = new[] { "DocumentStorageRoot", "DocumentMaxFileSizeMb", "DocumentAllowPhysicalDelete", "DocumentAllowedExtensions" },
            ["Runtime Paths"] = new[] { "RuntimeDataRoot", "RuntimeStorageMode", "UseReleaseDataRoot", "DatabaseFileName", "DocumentsFolderName", "BackupsFolderName", "ReportsFolderName", "CertificatesFolderName", "RestoreStagingFolderName", "LogsFolderName", "TempFolderName", "LocksFolderName" },
            ["Reports"] = new[] { "ReportHeader", "ReportFooter", "ReportDateFormat" },
            ["Hints and Theme"] = new[] { "ShowHelpfulHints" },
            ["Security"] = new[] { "OverdueSchedulerEnabled", "OverdueSchedulerStartupRun", "OverdueSchedulerAutoEmail", "MaxNotificationRetryCount" },
            ["Advanced"] = new[] { "OverdueSchedulerIntervalHours", "NotificationCooldownHours" }
        };

        // Default values for settings (used when not found or needs reset)
        private static readonly Dictionary<string, string> DefaultValues = new()
        {
            ["FinePerDay"] = "10",
            ["StudentIssueLimit"] = "3",
            ["FacultyIssueLimit"] = "10",
            ["StaffIssueLimit"] = "5",
            ["DefaultIssueDays"] = "14",
            ["ReservationExpiryDays"] = "3",
            ["SmtpHost"] = "",
            ["SmtpPort"] = "587",
            ["SmtpUseSsl"] = "False",
            ["SmtpUser"] = "",
            ["SmtpPassword"] = "",
            ["SmtpFromEmail"] = "library@institution.edu",
            ["SmtpFromName"] = "Ilm-o-Kutub System",
            ["EmailNotificationEnabled"] = "False",
            ["BackupDefaultFolder"] = "",
            ["BackupVerifyAfterCreation"] = "True",
            ["BackupCompressionEnabled"] = "False",
            ["AutomaticBackupEnabled"] = "False",
            ["AutomaticBackupIntervalHours"] = "24",
            ["AutomaticBackupRetentionDays"] = "30",
            ["BackupRetentionDays"] = "90",
            ["CriticalOperationLockTimeoutSeconds"] = "30",
            ["LockFileRetentionMinutes"] = "120",
            ["DocumentMaxFileSizeMb"] = "25",
            ["DocumentAllowedExtensions"] = ".pdf,.docx,.xlsx,.jpg,.jpeg,.png",
            ["UseReleaseDataRoot"] = "False",
            ["RuntimeStorageMode"] = "Development",
            ["DatabaseFileName"] = "KicsitLibrary.db",
            ["ShowHelpfulHints"] = "True",
            ["OverdueSchedulerEnabled"] = "False",
            ["OverdueSchedulerStartupRun"] = "False",
            ["OverdueSchedulerAutoEmail"] = "False",
            ["MaxNotificationRetryCount"] = "3",
            ["OverdueSchedulerIntervalHours"] = "24",
            ["NotificationCooldownHours"] = "24"
        };

        public SettingsManagementService(
            KicsitLibraryDbContext context,
            IAuthenticationService authService,
            IActivityLogService activityLogService,
            IRuntimePathService runtimePathService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _activityLogService = activityLogService ?? throw new ArgumentNullException(nameof(activityLogService));
            _runtimePathService = runtimePathService ?? throw new ArgumentNullException(nameof(runtimePathService));
        }

        public async Task<List<SettingsItemView>> GetSettingsAsync(CancellationToken cancellationToken = default)
        {
            var settings = await _context.SystemSettings
                .AsNoTracking()
                .Where(s => !s.IsDeleted)
                .ToListAsync(cancellationToken);

            return settings.Select(s => MapToView(s)).ToList();
        }

        public async Task<List<SettingsItemView>> GetSettingsByCategoryAsync(string category, CancellationToken cancellationToken = default)
        {
            if (!CategoryMappings.TryGetValue(category, out var keys))
                return new();

            var settings = await _context.SystemSettings
                .AsNoTracking()
                .Where(s => !s.IsDeleted && keys.Contains(s.Key))
                .ToListAsync(cancellationToken);

            return settings.Select(s => MapToView(s)).ToList();
        }

        public async Task<SettingsItemView?> GetSettingDetailsAsync(string key, CancellationToken cancellationToken = default)
        {
            var setting = await _context.SystemSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == key && !s.IsDeleted, cancellationToken);

            return setting != null ? MapToView(setting) : null;
        }

        public async Task<SettingsUpdateResult> UpdateSettingAsync(SettingsUpdateRequest request, CancellationToken cancellationToken = default)
        {
            // Authorization check
            if (_authService.CurrentUser == null)
            {
                return new()
                {
                    Succeeded = false,
                    Key = request.Key,
                    ErrorMessage = "Not authenticated"
                };
            }

            var hasPermission = await _authService.VerifyUserPermissionAsync(_authService.CurrentUser.Id, "MANAGE_SYSTEM");
            if (!hasPermission && _authService.CurrentUser.UserRoles.All(ur => ur.Role.Name != "Super Admin"))
            {
                await _activityLogService.LogActivityAsync(
                    "Settings Update Unauthorized",
                    $"User attempted to update setting '{request.Key}' without permission",
                    _authService.CurrentUser.Id);
                return new()
                {
                    Succeeded = false,
                    Key = request.Key,
                    ErrorMessage = "Insufficient permissions to update settings"
                };
            }

            // Validation
            var validation = await ValidateSettingAsync(request.Key, request.NewValue, cancellationToken);
            if (!validation.IsValid)
            {
                return new()
                {
                    Succeeded = false,
                    Key = request.Key,
                    ErrorMessage = validation.ErrorMessage,
                    RequiresRestart = RestartRequiredKeys.Contains(request.Key)
                };
            }

            // Get current setting
            var setting = await _context.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == request.Key && !s.IsDeleted, cancellationToken);

            if (setting == null)
            {
                return new()
                {
                    Succeeded = false,
                    Key = request.Key,
                    ErrorMessage = $"Setting '{request.Key}' not found"
                };
            }

            var oldValue = setting.Value;
            var oldValueMasked = MaskSensitiveValue(request.Key, oldValue);
            var newValueMasked = MaskSensitiveValue(request.Key, request.NewValue);

            // Update
            setting.Value = request.NewValue;
            setting.UpdatedAt = DateTime.UtcNow;
            setting.UpdatedByUserId = _authService.CurrentUser.Id;

            await _context.SaveChangesAsync(cancellationToken);

            // Log activity
            var logDetail = $"Updated setting '{request.Key}' from '{oldValueMasked}' to '{newValueMasked}'";
            if (!string.IsNullOrEmpty(request.Reason))
                logDetail += $". Reason: {request.Reason}";

            await _activityLogService.LogActivityAsync("Settings Updated", logDetail, _authService.CurrentUser.Id);

            return new()
            {
                Succeeded = true,
                Key = request.Key,
                OldValueMasked = oldValueMasked,
                NewValueMasked = newValueMasked,
                RequiresRestart = RestartRequiredKeys.Contains(request.Key),
                Message = RestartRequiredKeys.Contains(request.Key)
                    ? "Setting updated. Application restart is required for this change to take effect."
                    : "Setting updated successfully."
            };
        }

        public async Task<SettingsUpdateResult> ResetSettingToDefaultAsync(string key, int? userId, string? reason, CancellationToken cancellationToken = default)
        {
            if (!DefaultValues.TryGetValue(key, out var defaultValue))
            {
                return new()
                {
                    Succeeded = false,
                    Key = key,
                    ErrorMessage = $"No default value defined for setting '{key}'"
                };
            }

            return await UpdateSettingAsync(
                new SettingsUpdateRequest
                {
                    Key = key,
                    NewValue = defaultValue,
                    RequestedByUserId = userId,
                    Reason = $"Reset to default. {reason}"
                },
                cancellationToken);
        }

        public async Task<SettingsValidationResult> ValidateSettingAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            return key switch
            {
                "SmtpPort" => ValidateInteger(value, 1, 65535, "SMTP port must be between 1 and 65535"),
                "SmtpUseSsl" => ValidateBoolean(value),
                "AutomaticBackupIntervalHours" => ValidateInteger(value, 1, int.MaxValue, "Backup interval must be positive"),
                "AutomaticBackupRetentionDays" => ValidateInteger(value, 1, int.MaxValue, "Retention days must be positive"),
                "BackupRetentionDays" => ValidateInteger(value, 1, int.MaxValue, "Retention days must be positive"),
                "DocumentMaxFileSizeMb" => ValidateInteger(value, 1, 500, "Document size must be between 1 and 500 MB"),
                "DocumentAllowedExtensions" => ValidateExtensions(value),
                "DatabaseFileName" => ValidateDatabaseFileName(value),
                "CriticalOperationLockTimeoutSeconds" => ValidateInteger(value, 1, 300, "Lock timeout must be between 1 and 300 seconds"),
                "LockFileRetentionMinutes" => ValidateInteger(value, 5, 10080, "Lock retention must be between 5 and 10080 minutes"),
                "OverdueSchedulerIntervalHours" => ValidateInteger(value, 1, int.MaxValue, "Scheduler interval must be positive"),
                "NotificationCooldownHours" => ValidateInteger(value, 0, int.MaxValue, "Cooldown hours must be non-negative"),
                "StudentIssueLimit" => ValidateInteger(value, 1, 100, "Issue limit must be between 1 and 100"),
                "FacultyIssueLimit" => ValidateInteger(value, 1, 100, "Issue limit must be between 1 and 100"),
                "StaffIssueLimit" => ValidateInteger(value, 1, 100, "Issue limit must be between 1 and 100"),
                "DefaultIssueDays" => ValidateInteger(value, 1, 365, "Issue days must be between 1 and 365"),
                "ReservationExpiryDays" => ValidateInteger(value, 1, 90, "Expiry days must be between 1 and 90"),
                "FinePerDay" => ValidateDecimal(value, 0, 10000, "Fine per day must be between 0 and 10000"),
                "RuntimeDataRoot" => ValidateRuntimePath(value),
                _ => new SettingsValidationResult { IsValid = true }
            };
        }

        public async Task<SettingsExportResult> ExportSettingsSnapshotAsync(int? userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var settings = await GetSettingsAsync(cancellationToken);
                var exportData = new
                {
                    ExportDate = DateTime.UtcNow,
                    ProductName = "Ilm-o-Kutub System",
                    Settings = settings.Where(s => !SensitiveKeys.Contains(s.Key)).ToList()
                };

                var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });

                var paths = _runtimePathService.GetRuntimePaths();
                var exportFolder = Path.Combine(paths.ReportsFolder, "Settings Exports");
                Directory.CreateDirectory(exportFolder);

                var fileName = $"Settings_Snapshot_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
                var filePath = Path.Combine(exportFolder, fileName);

                await File.WriteAllTextAsync(filePath, json, cancellationToken);

                if (userId.HasValue)
                {
                    await _activityLogService.LogActivityAsync(
                        "Settings Exported",
                        $"Settings snapshot exported to {fileName}",
                        userId.Value);
                }

                return new()
                {
                    Succeeded = true,
                    FilePath = filePath
                };
            }
            catch (Exception ex)
            {
                return new()
                {
                    Succeeded = false,
                    ErrorMessage = $"Failed to export settings: {ex.Message}"
                };
            }
        }

        public async Task<List<SettingsAuditItem>> GetSettingsAuditAsync(string? filterKey = null, CancellationToken cancellationToken = default)
        {
            var logs = await _context.ActivityLogs
                .AsNoTracking()
                .Where(l => l.Action == "Settings Updated" && (filterKey == null || l.Detail.Contains(filterKey)))
                .OrderByDescending(l => l.CreatedAt)
                .Include(l => l.User)
                .Take(100)
                .ToListAsync(cancellationToken);

            return logs.Select(l => new SettingsAuditItem
            {
                Id = l.Id,
                SettingKey = ExtractSettingKeyFromDetail(l.Detail),
                ChangedAt = l.CreatedAt,
                ChangedBy = l.User?.FullName ?? "System",
                ValueWasMasked = true
            }).ToList();
        }

        public async Task<List<SettingsCategoryItem>> GetSettingsCategoriesAsync(CancellationToken cancellationToken = default)
        {
            var allSettings = await GetSettingsAsync(cancellationToken);
            var categories = new List<SettingsCategoryItem>();

            foreach (var (categoryName, keys) in CategoryMappings)
            {
                var categorySettings = allSettings
                    .Where(s => keys.Contains(s.Key))
                    .ToList();

                if (categorySettings.Count > 0)
                {
                    categories.Add(new SettingsCategoryItem
                    {
                        Name = categoryName,
                        Settings = categorySettings
                    });
                }
            }

            return categories.OrderBy(c => c.Name).ToList();
        }

        public string? GetDefaultValue(string key) =>
            DefaultValues.TryGetValue(key, out var value) ? value : null;

        public bool DoesSettingRequireRestart(string key) =>
            RestartRequiredKeys.Contains(key);

        public bool IsSettingSensitive(string key) =>
            SensitiveKeys.Contains(key);

        #region Private Helpers

        private SettingsItemView MapToView(SystemSettings setting)
        {
            var category = CategoryMappings.FirstOrDefault(kvp => kvp.Value.Contains(setting.Key)).Key ?? "Advanced";
            var isSensitive = IsSettingSensitive(setting.Key);
            var maskedValue = MaskSensitiveValue(setting.Key, setting.Value);

            return new SettingsItemView
            {
                Key = setting.Key,
                DisplayName = HumanizeKey(setting.Key),
                Category = category,
                Description = setting.Description,
                Value = setting.Value,
                MaskedValue = maskedValue,
                DefaultValue = GetDefaultValue(setting.Key) ?? string.Empty,
                DataType = DetermineDataType(setting.Key),
                IsSensitive = isSensitive,
                IsEditable = true,
                RequiresRestart = DoesSettingRequireRestart(setting.Key),
                UpdatedAt = setting.UpdatedAt,
                UpdatedBy = setting.UpdatedByUser?.FullName
            };
        }

        private string MaskSensitiveValue(string key, string value) =>
            IsSettingSensitive(key) && !string.IsNullOrEmpty(value)
                ? "***"
                : value;

        private string HumanizeKey(string key)
        {
            var result = System.Text.RegularExpressions.Regex.Replace(key, "([a-z])([A-Z])", "$1 $2");
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(result);
        }

        private string DetermineDataType(string key) =>
            key switch
            {
                "SmtpPort" or "StudentIssueLimit" or "FacultyIssueLimit" or "StaffIssueLimit"
                    or "DefaultIssueDays" or "ReservationExpiryDays" or "AutomaticBackupIntervalHours"
                    or "AutomaticBackupRetentionDays" or "BackupRetentionDays" or "DocumentMaxFileSizeMb"
                    or "CriticalOperationLockTimeoutSeconds" or "LockFileRetentionMinutes"
                    or "OverdueSchedulerIntervalHours" or "NotificationCooldownHours"
                    or "MaxNotificationRetryCount" or "MaxBackupRetryCount" => "Integer",

                "SmtpUseSsl" or "BackupVerifyAfterCreation" or "BackupCompressionEnabled"
                    or "AutomaticBackupEnabled" or "DocumentAllowPhysicalDelete" or "UseReleaseDataRoot"
                    or "EmailNotificationEnabled" or "OverdueSchedulerEnabled" or "OverdueSchedulerStartupRun"
                    or "OverdueSchedulerAutoEmail" or "SingleInstanceMode" or "ShowHelpfulHints" => "Boolean",

                "FinePerDay" => "Decimal",
                _ => "String"
            };

        private SettingsValidationResult ValidateInteger(string value, int min, int max, string errorMessage)
        {
            if (!int.TryParse(value, out var intValue))
                return new() { IsValid = false, ErrorMessage = $"{errorMessage}" };
            if (intValue < min || intValue > max)
                return new() { IsValid = false, ErrorMessage = errorMessage };
            return new() { IsValid = true };
        }

        private SettingsValidationResult ValidateDecimal(string value, decimal min, decimal max, string errorMessage)
        {
            if (!decimal.TryParse(value, CultureInfo.InvariantCulture, out var decValue))
                return new() { IsValid = false, ErrorMessage = "Must be a valid decimal number" };
            if (decValue < min || decValue > max)
                return new() { IsValid = false, ErrorMessage = errorMessage };
            return new() { IsValid = true };
        }

        private SettingsValidationResult ValidateBoolean(string value)
        {
            if (!bool.TryParse(value, out _))
                return new() { IsValid = false, ErrorMessage = "Value must be 'True' or 'False'" };
            return new() { IsValid = true };
        }

        private SettingsValidationResult ValidateExtensions(string value)
        {
            if (string.IsNullOrEmpty(value))
                return new() { IsValid = false, ErrorMessage = "At least one extension must be allowed" };

            var extensions = value.Split(',').Select(e => e.Trim());
            foreach (var ext in extensions)
            {
                if (!ext.StartsWith("."))
                    return new() { IsValid = false, ErrorMessage = "All extensions must start with a dot (.)" };

                var dangerousExts = new[] { ".exe", ".bat", ".cmd", ".com", ".msi", ".dll", ".sys" };
                if (dangerousExts.Contains(ext.ToLower()))
                    return new() { IsValid = false, ErrorMessage = $"Extension '{ext}' is not allowed for security reasons" };
            }
            return new() { IsValid = true };
        }

        private SettingsValidationResult ValidateDatabaseFileName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return new() { IsValid = false, ErrorMessage = "Database file name cannot be empty" };
            if (value.Contains("/") || value.Contains("\\") || value.Contains(":"))
                return new() { IsValid = false, ErrorMessage = "Database file name cannot contain path separators" };
            if (!value.EndsWith(".db") && !value.EndsWith(".sqlite") && !value.EndsWith(".sqlite3"))
                return new() { IsValid = false, ErrorMessage = "Database file must have .db or .sqlite extension" };
            return new() { IsValid = true };
        }

        private SettingsValidationResult ValidateRuntimePath(string value)
        {
            if (string.IsNullOrEmpty(value))
                return new() { IsValid = true }; // Empty is allowed (uses default)

            // Basic checks
            if (value.Contains(".."))
                return new() { IsValid = false, ErrorMessage = "Path cannot contain directory traversal sequences (..)" };

            // Try to parse as path
            try
            {
                var path = Path.GetFullPath(value);
                return new() { IsValid = true };
            }
            catch
            {
                return new() { IsValid = false, ErrorMessage = "Invalid path format" };
            }
        }

        private string ExtractSettingKeyFromDetail(string detail)
        {
            var match = System.Text.RegularExpressions.Regex.Match(detail, @"'([^']+)'");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        #endregion
    }
}
