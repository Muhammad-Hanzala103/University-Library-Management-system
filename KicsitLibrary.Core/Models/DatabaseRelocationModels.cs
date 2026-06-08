namespace KicsitLibrary.Core.Models;

public sealed class DatabaseRelocationRequest
{
    public int RequestedByUserId { get; set; }
    public string RequestedByUserName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string SourceDatabasePath { get; set; } = string.Empty;
    public string TargetDatabasePath { get; set; } = string.Empty;
    public bool CreateSafetyBackup { get; set; } = true;
    public bool VerifyBeforeMove { get; set; } = true;
    public bool VerifyAfterMove { get; set; } = true;
    public bool EnableReleaseDataRootAfterMove { get; set; }
    public string ConfirmationText { get; set; } = string.Empty;
}

public sealed class DatabaseRelocationResult
{
    public bool Succeeded { get; set; }
    public string SourceDatabasePath { get; set; } = string.Empty;
    public string TargetDatabasePath { get; set; } = string.Empty;
    public string SafetyBackupPath { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
    public bool Moved { get; set; }
    public bool Copied { get; set; }
    public bool SettingsUpdated { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public bool RollbackPerformed { get; set; }
}

public sealed class DatabaseRelocationPreview
{
    public bool Succeeded { get; set; }
    public string CurrentDatabasePath { get; set; } = string.Empty;
    public string TargetDatabasePath { get; set; } = string.Empty;
    public bool TargetExists { get; set; }
    public long CurrentSizeBytes { get; set; }
    public long TargetSizeBytes { get; set; }
    public bool SafetyBackupRequired { get; set; } = true;
    public bool CanRelocate { get; set; }
    public List<string> BlockingReasons { get; set; } = [];
    public string Message { get; set; } = string.Empty;
}

public sealed class DatabaseRelocationStatus
{
    public string CurrentDatabasePath { get; set; } = string.Empty;
    public string TargetDatabasePath { get; set; } = string.Empty;
    public string RuntimeDataRoot { get; set; } = string.Empty;
    public string RuntimeStorageMode { get; set; } = "Development";
    public bool UseReleaseDataRoot { get; set; }
    public bool CanManage { get; set; }
    public string LastStatus { get; set; } = string.Empty;
    public string LastMessage { get; set; } = string.Empty;
}

public sealed class DatabaseRelocationHistoryItem
{
    public int DatabaseRelocationHistoryId { get; set; }
    public string SourceDatabasePath { get; set; } = string.Empty;
    public string TargetDatabasePath { get; set; } = string.Empty;
    public string SafetyBackupPath { get; set; } = string.Empty;
    public string RequestedByUserName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public bool RollbackPerformed { get; set; }
    public string MetadataJson { get; set; } = string.Empty;
}
