using System;

namespace KicsitLibrary.Core.Entities
{
    public class DeletedRecordArchive
    {
        public int Id { get; set; }
        public string TableName { get; set; } = string.Empty;
        public int RecordId { get; set; }
        public string SerializedData { get; set; } = string.Empty;
        
        public int DeletedByUserId { get; set; }
        public virtual User DeletedByUser { get; set; } = null!;
        
        public DateTime DeletedAt { get; set; } = DateTime.UtcNow;
        public string Reason { get; set; } = string.Empty;
    }
}
