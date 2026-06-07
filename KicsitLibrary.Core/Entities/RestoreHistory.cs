namespace KicsitLibrary.Core.Entities;

public class RestoreHistory : EntityBase
{
    public string BackupFilePath { get; set; } = string.Empty;
    public string? SafetyBackupFilePath { get; set; }
    public string RestoredDatabasePath { get; set; } = string.Empty;
    public int RequestedByUserId { get; set; }
    public string RequestedByUserName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string Status { get; set; } = "Started";
    public string? Reason { get; set; }
    public string? ErrorMessage { get; set; }
    public bool RolledBack { get; set; }
    public string? ChecksumSha256 { get; set; }
    public string? MetadataJson { get; set; }
}
