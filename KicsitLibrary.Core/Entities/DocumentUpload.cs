using System;

namespace KicsitLibrary.Core.Entities
{
    public class DocumentUpload : EntityBase
    {
        public string DocumentTitle { get; set; } = string.Empty;
        public string DocumentType { get; set; } = "LibrarySop";
        public string VersionNumber { get; set; } = "1.0";
        public DateTime UploadDate { get; set; } = DateTime.UtcNow;
        public int UploadedByUserId { get; set; }
        public virtual User UploadedByUser { get; set; } = null!;
        public string FilePath { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool ActiveStatus { get; set; } = true;
        public DateTime? ExpiryDate { get; set; }
        public string? Remarks { get; set; }
    }
}
