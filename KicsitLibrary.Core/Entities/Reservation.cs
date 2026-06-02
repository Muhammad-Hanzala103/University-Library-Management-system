using System;

namespace KicsitLibrary.Core.Entities
{
    public class Reservation : EntityBase
    {
        public string ReservationNumber { get; set; } = string.Empty;
        
        public string MemberType { get; set; } = "Student";
        
        public int? StudentId { get; set; }
        public virtual Student? Student { get; set; }
        
        public int? FacultyStaffId { get; set; }
        public virtual FacultyStaff? FacultyStaff { get; set; }
        
        public int BookMasterId { get; set; }
        public virtual BookMaster BookMaster { get; set; } = null!;
        
        public string? AccessionNumber { get; set; }
        
        public DateTime ReservationDate { get; set; } = DateTime.UtcNow;
        public DateTime ExpiryDate { get; set; }
        
        public string Status { get; set; } = "Pending";
        public string? Remarks { get; set; }
    }
}
