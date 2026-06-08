using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Core.Interfaces
{
    public interface ISettingsManagementService
    {
        /// <summary>
        /// Get all settings with masking applied to sensitive values.
        /// </summary>
        Task<List<SettingsItemView>> GetSettingsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get settings for a specific category.
        /// </summary>
        Task<List<SettingsItemView>> GetSettingsByCategoryAsync(string category, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get details for a specific setting.
        /// </summary>
        Task<SettingsItemView?> GetSettingDetailsAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update a setting value with validation and authorization check.
        /// </summary>
        Task<SettingsUpdateResult> UpdateSettingAsync(SettingsUpdateRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reset a setting to its default value.
        /// </summary>
        Task<SettingsUpdateResult> ResetSettingToDefaultAsync(string key, int? userId, string? reason, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validate a setting value without saving.
        /// </summary>
        Task<SettingsValidationResult> ValidateSettingAsync(string key, string value, CancellationToken cancellationToken = default);

        /// <summary>
        /// Export a masked snapshot of settings.
        /// </summary>
        Task<SettingsExportResult> ExportSettingsSnapshotAsync(int? userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get audit trail for settings changes.
        /// </summary>
        Task<List<SettingsAuditItem>> GetSettingsAuditAsync(string? filterKey = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get all available settings categories.
        /// </summary>
        Task<List<SettingsCategoryItem>> GetSettingsCategoriesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get the default value for a setting.
        /// </summary>
        string? GetDefaultValue(string key);

        /// <summary>
        /// Check if a setting requires application restart when changed.
        /// </summary>
        bool DoesSettingRequireRestart(string key);

        /// <summary>
        /// Check if a setting is sensitive (should be masked).
        /// </summary>
        bool IsSettingSensitive(string key);
    }
}
