namespace KicsitLibrary.Core.Models;

public sealed class AutomaticBackupSchedulerSettings
{
    public bool Enabled { get; set; }
    public bool RunOnStartup { get; set; }
    public int IntervalHours { get; set; } = 24;
    public int InitialDelaySeconds { get; set; } = 60;
    public bool Compress { get; set; }
    public bool VerifyAfterCreation { get; set; } = true;
    public string DestinationFolder { get; set; } = string.Empty;
    public bool RetentionEnabled { get; set; }
    public int RetentionDays { get; set; } = 30;
    public int MaxHistoryRows { get; set; } = 500;
    public bool DeletePhysicalFiles { get; set; }
}

public sealed class AutomaticBackupStatus
{
    public bool Enabled { get; set; }
    public bool RunOnStartup { get; set; }
    public bool RetentionEnabled { get; set; }
    public bool DeletePhysicalFiles { get; set; }
    public int IntervalHours { get; set; } = 24;
    public int InitialDelaySeconds { get; set; } = 60;
    public DateTime? LastRunAt { get; set; }
    public DateTime? LastSuccessAt { get; set; }
    public DateTime? LastFailureAt { get; set; }
    public string LastMessage { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
    public bool CanManage { get; set; }
}

public sealed class AutomaticBackupRunResult
{
    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
    public bool Succeeded { get; set; }
    public bool WasSkipped { get; set; }
    public string BackupFilePath { get; set; } = string.Empty;
    public string CompressedFilePath { get; set; } = string.Empty;
    public long BackupSizeBytes { get; set; }
    public string ChecksumSha256 { get; set; } = string.Empty;
    public int RetentionPreviewCount { get; set; }
    public int RetentionDeletedCount { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public sealed class BackupRetentionCandidate
{
    public int BackupHistoryId { get; set; }
    public string BackupFilePath { get; set; } = string.Empty;
    public string CompressedFilePath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int AgeDays { get; set; }
    public long SizeBytes { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool CanDelete { get; set; }
    public string CannotDeleteReason { get; set; } = string.Empty;
}

public sealed class BackupRetentionPreviewResult
{
    public bool Succeeded { get; set; }
    public int CandidateCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public IReadOnlyList<BackupRetentionCandidate> Candidates { get; set; } =
        Array.Empty<BackupRetentionCandidate>();
    public string Message { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public sealed class BackupRetentionDeleteResult
{
    public bool Succeeded { get; set; }
    public int DeletedHistoryCount { get; set; }
    public int DeletedPhysicalFileCount { get; set; }
    public int SkippedCount { get; set; }
    public long TotalFreedBytes { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}
