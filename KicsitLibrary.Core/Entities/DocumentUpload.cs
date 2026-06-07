using System;

namespace KicsitLibrary.Core.Entities
{
    public class DocumentUpload : EntityBase
    {
        public string DocumentTitle { get; set; } = string.Empty;
        public string DocumentType { get; set; } = "LibrarySop";
        public string VersionNumber { get; set; } = "1.0";
        public DateTime UploadDate { get; set; } = DateTime.UtcNow;
        public string UploadedBy { get; set; } = string.Empty;
        public int UploadedByUserId { get; set; }
        public virtual User UploadedByUser { get; set; } = null!;
        public string OriginalFileName { get; set; } = string.Empty;
        public string StoredFileName { get; set; } = string.Empty;
        public string StoredFilePath { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string FileSha256 { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool ActiveStatus { get; set; } = true;
        public DateTime? ExpiryDate { get; set; }
        public string? Remarks { get; set; }
        public string RelatedEntityType { get; set; } = string.Empty;
        public int? RelatedEntityId { get; set; }
        public string DeletedBy { get; set; } = string.Empty;
    }
}
