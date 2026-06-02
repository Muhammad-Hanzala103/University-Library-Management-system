using System;

namespace KicsitLibrary.Core.Entities
{
    public class AuditFile
    {
        public int Id { get; set; }
        public int AuditRecordId { get; set; }
        public virtual AuditRecord AuditRecord { get; set; } = null!;
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
