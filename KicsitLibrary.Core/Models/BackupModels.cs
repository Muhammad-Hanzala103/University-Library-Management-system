namespace KicsitLibrary.Core.Models;

public sealed class BackupRequest
{
    public int RequestedByUserId { get; set; }
    public string RequestedByUserName { get; set; } = string.Empty;
    public string DestinationFolder { get; set; } = string.Empty;
    public bool IncludeTimestamp { get; set; } = true;
    public bool IncludeMetadataFile { get; set; } = true;
    public bool VerifyAfterCreation { get; set; } = true;
    public bool CompressBackup { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class BackupResult
{
    public bool Succeeded { get; set; }
    public int? BackupHistoryId { get; set; }
    public string BackupFilePath { get; set; } = string.Empty;
    public string MetadataFilePath { get; set; } = string.Empty;
    public string CompressedFilePath { get; set; } = string.Empty;
    public long BackupSizeBytes { get; set; }
    public string ChecksumSha256 { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public sealed class BackupHistoryFilter
{
    public string? SearchText { get; set; }
    public string? Status { get; set; }
    public string? VerificationStatus { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? CreatedBy { get; set; }
    public int Limit { get; set; } = 500;
}

public sealed class BackupHistoryItem
{
    public int BackupHistoryId { get; set; }
    public string BackupFileName { get; set; } = string.Empty;
    public string BackupFilePath { get; set; } = string.Empty;
    public string CompressedFilePath { get; set; } = string.Empty;
    public long BackupSizeBytes { get; set; }
    public string ChecksumSha256 { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string VerificationStatus { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string MetadataJson { get; set; } = string.Empty;
}

public sealed class BackupVerificationResult
{
    public bool Succeeded { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string ChecksumSha256 { get; set; } = string.Empty;
    public bool IntegrityCheckPassed { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime VerifiedAt { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public sealed class BackupSettings
{
    public string DefaultFolder { get; set; } = string.Empty;
    public bool CompressionEnabled { get; set; }
    public bool VerifyAfterCreation { get; set; } = true;
    public int RetentionDays { get; set; } = 30;
    public int MaxHistoryRows { get; set; } = 500;
}

public sealed class BackupStatusSummary
{
    public int TotalBackups { get; set; }
    public int SuccessfulBackups { get; set; }
    public int FailedBackups { get; set; }
    public int VerifiedBackups { get; set; }
    public long TotalBackupSizeBytes { get; set; }
    public DateTime? LastBackupAt { get; set; }
    public string LastBackupStatus { get; set; } = string.Empty;
}
