using System;

namespace KicsitLibrary.Core.Entities
{
    public class DeletedRecordArchive : EntityBase
    {
        public string TableName { get; set; } = string.Empty;
        public int RecordId { get; set; }
        public string SerializedData { get; set; } = string.Empty;
        
        public virtual User DeletedByUser { get; set; } = null!;
    }
}
