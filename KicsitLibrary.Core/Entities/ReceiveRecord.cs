using System;

namespace KicsitLibrary.Core.Entities
{
    public class ReceiveRecord : EntityBase
    {
        public int IssueRecordId { get; set; }
        public virtual IssueRecord IssueRecord { get; set; } = null!;
        
        public DateTime ReceiveDate { get; set; } = DateTime.UtcNow;
        public string? FineType { get; set; }
        public decimal FineAmount { get; set; }
        public string? Reason { get; set; }
        public string? Remarks { get; set; }
        
        public string BookConditionAfterReturn { get; set; } = "Normal";
        public string FineCollectedOrUnpaid { get; set; } = "Unpaid";
        public string? FineWaiverReason { get; set; }
        
        public int ReceivedByUserId { get; set; }
        public virtual User ReceivedByUser { get; set; } = null!;
    }
}
