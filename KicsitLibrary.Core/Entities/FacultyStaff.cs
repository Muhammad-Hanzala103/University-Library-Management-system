using System;
using System.Collections.Generic;
using KicsitLibrary.Core.Enums;

namespace KicsitLibrary.Core.Entities
{
    public class FacultyStaff : EntityBase
    {
        public string PersonnelNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public FacultyType FacultyType { get; set; } = FacultyType.PermanentFaculty;
        public string Department { get; set; } = string.Empty;
        public string Designation { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? CNIC { get; set; }
        public string Address { get; set; } = string.Empty;
        public bool ActiveStatus { get; set; } = true;
        
        public DateTime JoiningDate { get; set; } = DateTime.UtcNow;
        public DateTime? LeavingDate { get; set; }
        public string? Remarks { get; set; }

        public virtual ICollection<IssueRecord> IssueRecords { get; set; } = new List<IssueRecord>();
        public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
        public virtual ICollection<Fine> Fines { get; set; } = new List<Fine>();
        public virtual ICollection<NotificationRecord> NotificationRecords { get; set; } = new List<NotificationRecord>();
    }
}
