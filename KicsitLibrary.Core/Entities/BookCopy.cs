using System;
using System.Collections.Generic;

namespace KicsitLibrary.Core.Entities
{
    public class BookCopy : EntityBase
    {
        public string AccessionNumber { get; set; } = string.Empty;
        
        public int BookMasterId { get; set; }
        public virtual BookMaster BookMaster { get; set; } = null!;
        
        public int CopyNumber { get; set; }
        public string? Barcode { get; set; }
        public string? QRCode { get; set; }
        
        public string? RackNumber { get; set; }
        public string? ShelfNumber { get; set; }
        public string? RowNumber { get; set; }
        public string? Location { get; set; }
        
        public string PhysicalCondition { get; set; } = "Normal";
        public string AvailabilityStatus { get; set; } = "Available";
        
        public string? CurrentHolderType { get; set; }
        public int? CurrentHolderId { get; set; }
        
        public DateTime? LastIssuedDate { get; set; }
        public DateTime? LastReceivedDate { get; set; }

        public virtual ICollection<IssueRecord> IssueRecords { get; set; } = new List<IssueRecord>();
    }
}
