namespace KicsitLibrary.Core.Models;

public sealed class DocumentUploadRequest
{
    public string DocumentTitle { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SourceFilePath { get; set; } = string.Empty;
    public int UploadedByUserId { get; set; }
    public string UploadedByUserName { get; set; } = string.Empty;
    public string RelatedEntityType { get; set; } = string.Empty;
    public int? RelatedEntityId { get; set; }
    public string VersionNumber { get; set; } = "1.0";
    public DateTime? ExpiryDate { get; set; }
    public string Remarks { get; set; } = string.Empty;
}

public sealed class DocumentUploadResult
{
    public bool Succeeded { get; set; }
    public int? DocumentUploadId { get; set; }
    public string StoredFilePath { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string FileSha256 { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public sealed class DocumentValidationResult
{
    public bool Succeeded { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string DetectedContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public class DocumentListItem
{
    public int DocumentUploadId { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string VersionNumber { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime UploadDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string ActiveStatus { get; set; } = string.Empty;
    public string RelatedEntityType { get; set; } = string.Empty;
    public int? RelatedEntityId { get; set; }
}

public sealed class DocumentDetails : DocumentListItem
{
    public string Description { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string FileSha256 { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public bool FileExists { get; set; }
    public string FileStatusMessage { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string DeletedBy { get; set; } = string.Empty;
    public string DeletedReason { get; set; } = string.Empty;
}

public sealed class DocumentDownloadResult
{
    public bool Succeeded { get; set; }
    public int DocumentUploadId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public sealed class DocumentDeleteResult
{
    public bool Succeeded { get; set; }
    public int DocumentUploadId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public sealed class DocumentStorageSettings
{
    public string StorageRoot { get; set; } = string.Empty;
    public int MaxFileSizeMb { get; set; } = 25;
    public bool AllowPhysicalDelete { get; set; }
    public IReadOnlyList<string> AllowedExtensions { get; set; } =
        [".pdf", ".docx", ".xlsx", ".jpg", ".jpeg", ".png"];
}

public sealed class DocumentTypeSummary
{
    public string DocumentType { get; set; } = string.Empty;
    public int ActiveCount { get; set; }
    public int InactiveCount { get; set; }
    public int MissingFileCount { get; set; }
    public int ExpiredCount { get; set; }
}

public sealed class DocumentFilter
{
    public string SearchText { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string ActiveStatus { get; set; } = string.Empty;
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public bool ExpiredOnly { get; set; }
    public bool MissingFileOnly { get; set; }
    public string RelatedEntityType { get; set; } = string.Empty;
    public int? RelatedEntityId { get; set; }
    public int Limit { get; set; } = 500;
}
