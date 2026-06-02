using System;

namespace KicsitLibrary.Core.Entities
{
    public class VisitFile
    {
        public int Id { get; set; }
        public int VisitRecordId { get; set; }
        public virtual VisitRecord VisitRecord { get; set; } = null!;
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
