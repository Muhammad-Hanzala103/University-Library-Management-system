using System;
using System.Collections.Generic;

namespace KicsitLibrary.Core.Entities
{
    public class Student : EntityBase
    {
        public string RegistrationNumber { get; set; } = string.Empty;
        public string AdmissionNumber { get; set; } = string.Empty;
        public string RollNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string FatherName { get; set; } = string.Empty;
        public string Program { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Batch { get; set; } = string.Empty;
        public string Semester { get; set; } = string.Empty;
        public string Session { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? CNIC { get; set; }
        public string Address { get; set; } = string.Empty;
        public string? PhotoPath { get; set; }
        
        public int PageNumber { get; set; }
        public int RegisterNumber { get; set; }
        
        public string LibraryStatus { get; set; } = "Active";
        public string ClearanceStatus { get; set; } = "Not Cleared";
        public DateTime? ClearanceDate { get; set; }
        public string? ClearanceRemarks { get; set; }
        public bool ActiveStatus { get; set; } = true;

        public virtual ICollection<IssueRecord> IssueRecords { get; set; } = new List<IssueRecord>();
        public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
        public virtual ICollection<Fine> Fines { get; set; } = new List<Fine>();
        public virtual ICollection<NotificationRecord> NotificationRecords { get; set; } = new List<NotificationRecord>();
    }
}
