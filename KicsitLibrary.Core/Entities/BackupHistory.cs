namespace KicsitLibrary.Core.Entities;

public class BackupHistory : EntityBase
{
    public string BackupFileName { get; set; } = string.Empty;
    public string BackupFilePath { get; set; } = string.Empty;
    public string? CompressedFilePath { get; set; }
    public long BackupSizeBytes { get; set; }
    public string? ChecksumSha256 { get; set; }
    public int CreatedByUserId { get; set; }
    public string CreatedByUserName { get; set; } = string.Empty;
    public DateTime? VerifiedAt { get; set; }
    public string VerificationStatus { get; set; } = "Pending";
    public string? Reason { get; set; }
    public string Status { get; set; } = "InProgress";
    public string? ErrorMessage { get; set; }
    public string? MetadataJson { get; set; }
}
