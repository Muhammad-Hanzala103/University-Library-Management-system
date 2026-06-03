using System;
using KicsitLibrary.Core.Enums;

namespace KicsitLibrary.Core.Entities
{
    public class IssueRecord : EntityBase
    {
        public string AccessionNumber { get; set; } = string.Empty;
        
        public int BookCopyId { get; set; }
        public virtual BookCopy BookCopy { get; set; } = null!;
        
        public MemberType MemberType { get; set; } = MemberType.Student; // Student, FacultyStaff
        
        public int? StudentId { get; set; }
        public virtual Student? Student { get; set; }
        
        public int? FacultyStaffId { get; set; }
        public virtual FacultyStaff? FacultyStaff { get; set; }
        
        public DateTime IssueDate { get; set; } = DateTime.UtcNow;
        public DateTime ExpectedReturnDate { get; set; }
        
        public decimal FinePerDay { get; set; }
        public string? Remarks { get; set; }
        
        public int IssuedByUserId { get; set; }
        public virtual User IssuedByUser { get; set; } = null!;

        public virtual ReceiveRecord? ReceiveRecord { get; set; }
        public virtual Fine? Fine { get; set; }
    }
}
