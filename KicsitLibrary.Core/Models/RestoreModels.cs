namespace KicsitLibrary.Core.Models;

public sealed class RestoreRequest
{
    public string BackupFilePath { get; set; } = string.Empty;
    public int RequestedByUserId { get; set; }
    public string RequestedByUserName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public bool CreateSafetyBackup { get; set; } = true;
    public bool VerifyBeforeRestore { get; set; } = true;
    public bool VerifyAfterRestore { get; set; } = true;
    public bool RequireConfirmationText { get; set; } = true;
    public string ConfirmationText { get; set; } = string.Empty;
    public bool AllowRestoreWhileAppRunning { get; set; }
}

public sealed class RestorePreviewResult
{
    public bool Succeeded { get; set; }
    public string BackupFilePath { get; set; } = string.Empty;
    public long BackupSizeBytes { get; set; }
    public string ChecksumSha256 { get; set; } = string.Empty;
    public bool IntegrityCheckPassed { get; set; }
    public DateTime BackupCreatedAt { get; set; }
    public int DetectedTablesCount { get; set; }
    public int DetectedUserCount { get; set; }
    public int DetectedBookCopyCount { get; set; }
    public int DetectedIssueRecordCount { get; set; }
    public bool DetectedBackupHistoryRecord { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public sealed class RestoreValidationResult
{
    public bool Succeeded { get; set; }
    public string BackupFilePath { get; set; } = string.Empty;
    public bool IntegrityCheckPassed { get; set; }
    public string ChecksumSha256 { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ValidationMessage { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public sealed class RestoreResult
{
    public bool Succeeded { get; set; }
    public int? RestoreHistoryId { get; set; }
    public string BackupFilePath { get; set; } = string.Empty;
    public string SafetyBackupFilePath { get; set; } = string.Empty;
    public string RestoredDatabasePath { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public bool RequiresApplicationRestart { get; set; }
    public bool RolledBack { get; set; }
}

public sealed class RestoreHistoryFilter
{
    public string? SearchText { get; set; }
    public string? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Limit { get; set; } = 500;
}

public sealed class RestoreHistoryItem
{
    public int RestoreHistoryId { get; set; }
    public string BackupFilePath { get; set; } = string.Empty;
    public string SafetyBackupFilePath { get; set; } = string.Empty;
    public string RestoredDatabasePath { get; set; } = string.Empty;
    public string RequestedByUserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public bool RolledBack { get; set; }
    public string ChecksumSha256 { get; set; } = string.Empty;
    public string MetadataJson { get; set; } = string.Empty;
}

public sealed class RestoreStatusSummary
{
    public int TotalRestores { get; set; }
    public int PendingRestarts { get; set; }
    public int SuccessfulRestores { get; set; }
    public int FailedRestores { get; set; }
    public int RolledBackRestores { get; set; }
    public DateTime? LastRestoreAt { get; set; }
    public string LastRestoreStatus { get; set; } = string.Empty;
}

public sealed class PendingRestoreMetadata
{
    public int SchemaVersion { get; set; } = 1;
    public string ProductName { get; set; } = ProductBrand.Name;
    public int RestoreHistoryId { get; set; }
    public string OriginalBackupFilePath { get; set; } = string.Empty;
    public string StagedBackupFilePath { get; set; } = string.Empty;
    public string TargetDatabasePath { get; set; } = string.Empty;
    public string SafetyBackupFilePath { get; set; } = string.Empty;
    public string ChecksumSha256 { get; set; } = string.Empty;
    public int RequestedByUserId { get; set; }
    public string RequestedByUserName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public bool VerifyAfterRestore { get; set; } = true;
}

public sealed class PendingRestoreResult
{
    public bool Succeeded { get; set; }
    public bool RolledBack { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string EmergencyBackupFilePath { get; set; } = string.Empty;
    public DateTime FinishedAt { get; set; }
    public PendingRestoreMetadata Request { get; set; } = new();
}
