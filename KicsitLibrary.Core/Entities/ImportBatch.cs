using System;
using System.Collections.Generic;

namespace KicsitLibrary.Core.Entities
{
    public class ImportBatch : EntityBase
    {
        public string BatchNumber { get; set; } = string.Empty;
        public string ImportType { get; set; } = "Books";
        public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
        public int TotalRows { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        
        public int ImportedByUserId { get; set; }
        public virtual User ImportedByUser { get; set; } = null!;

        public virtual ICollection<ImportError> ImportErrors { get; set; } = new List<ImportError>();
    }
}
