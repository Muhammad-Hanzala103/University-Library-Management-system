using System;
using KicsitLibrary.Core.Enums;

namespace KicsitLibrary.Core.Entities
{
    public class Fine : EntityBase
    {
        public string FineRecordNumber { get; set; } = string.Empty;
        
        public MemberType MemberType { get; set; } = MemberType.Student;
        
        public int? StudentId { get; set; }
        public virtual Student? Student { get; set; }
        
        public int? FacultyStaffId { get; set; }
        public virtual FacultyStaff? FacultyStaff { get; set; }
        
        public int IssueRecordId { get; set; }
        public virtual IssueRecord IssueRecord { get; set; } = null!;
        
        public string AccessionNumber { get; set; } = string.Empty;
        public string? FineType { get; set; }
        public decimal FinePerDay { get; set; }
        public int DaysOverdue { get; set; }
        public decimal FineAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public FineStatus PaymentStatus { get; set; } = FineStatus.Unpaid;
        public DateTime? PaymentDate { get; set; }
        
        public decimal WaivedAmount { get; set; }
        public string? WaiverReason { get; set; }
        
        public int? CollectedByUserId { get; set; }
        public virtual User? CollectedByUser { get; set; }
        
        public string? Remarks { get; set; }
    }
}
