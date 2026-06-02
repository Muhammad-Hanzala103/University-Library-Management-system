using System;
using System.Collections.Generic;

namespace KicsitLibrary.Core.Entities
{
    public class AuditRecord : EntityBase
    {
        public string AuditNumber { get; set; } = string.Empty;
        public DateTime AuditDate { get; set; }
        public string AuditType { get; set; } = "InternalAudit";
        public string FinancialYear { get; set; } = string.Empty;
        public string InspectionDetail { get; set; } = string.Empty;
        public string FinancialDetail { get; set; } = string.Empty;
        
        public string Observations { get; set; } = string.Empty;
        public string Findings { get; set; } = string.Empty;
        public string Suggestions { get; set; } = string.Empty;
        public string ActionRequired { get; set; } = string.Empty;
        public string ActionTaken { get; set; } = string.Empty;
        
        public string ResponsiblePerson { get; set; } = string.Empty;
        public string Status { get; set; } = "Draft";
        public string? Remarks { get; set; }
        
        public int CreatedByUserId { get; set; }
        public virtual User CreatedByUser { get; set; } = null!;
        
        public int? UpdatedByUserId { get; set; }
        public virtual User? UpdatedByUser { get; set; }

        public virtual ICollection<AuditFile> AuditFiles { get; set; } = new List<AuditFile>();
    }
}
