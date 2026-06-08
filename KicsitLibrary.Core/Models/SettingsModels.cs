using System;
using System.Collections.Generic;

namespace KicsitLibrary.Core.Models
{
    /// <summary>
    /// Represents a category of settings for UI grouping.
    /// </summary>
    public sealed class SettingsCategoryItem
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<SettingsItemView> Settings { get; set; } = new();
    }

    /// <summary>
    /// View model for displaying a single setting in the UI.
    /// </summary>
    public sealed class SettingsItemView
    {
        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Value { get; set; } = string.Empty;
        public string MaskedValue { get; set; } = string.Empty;
        public string DefaultValue { get; set; } = string.Empty;
        public string DataType { get; set; } = "String"; // String, Integer, Boolean, Decimal
        public bool IsSensitive { get; set; }
        public bool IsEditable { get; set; } = true;
        public bool RequiresRestart { get; set; }
        public string? ValidationMessage { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }

    /// <summary>
    /// Request model for updating a setting.
    /// </summary>
    public sealed class SettingsUpdateRequest
    {
        public string Key { get; set; } = string.Empty;
        public string NewValue { get; set; } = string.Empty;
        public int? RequestedByUserId { get; set; }
        public string? RequestedByUserName { get; set; }
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Result of a setting update operation.
    /// </summary>
    public sealed class SettingsUpdateResult
    {
        public bool Succeeded { get; set; }
        public string Key { get; set; } = string.Empty;
        public string? OldValueMasked { get; set; }
        public string? NewValueMasked { get; set; }
        public bool RequiresRestart { get; set; }
        public string? Message { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Validation result for a setting value.
    /// </summary>
    public sealed class SettingsValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public string? WarningMessage { get; set; }
    }

    /// <summary>
    /// Result of exporting settings snapshot.
    /// </summary>
    public sealed class SettingsExportResult
    {
        public bool Succeeded { get; set; }
        public string? FilePath { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Result of importing settings.
    /// </summary>
    public sealed class SettingsImportResult
    {
        public bool Succeeded { get; set; }
        public int ImportedCount { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Audit item for settings changes.
    /// </summary>
    public sealed class SettingsAuditItem
    {
        public int Id { get; set; }
        public string SettingKey { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public DateTime ChangedAt { get; set; }
        public string? ChangedBy { get; set; }
        public string? Reason { get; set; }
        public bool ValueWasMasked { get; set; }
    }
}
