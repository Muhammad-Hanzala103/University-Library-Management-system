namespace KicsitLibrary.Core.Entities;

public class DatabaseRelocationHistory : EntityBase
{
    public string SourceDatabasePath { get; set; } = string.Empty;
    public string TargetDatabasePath { get; set; } = string.Empty;
    public string? SafetyBackupPath { get; set; }
    public int RequestedByUserId { get; set; }
    public string RequestedByUserName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string Status { get; set; } = "Started";
    public string? Reason { get; set; }
    public string? ErrorMessage { get; set; }
    public bool RollbackPerformed { get; set; }
    public string? MetadataJson { get; set; }
}
