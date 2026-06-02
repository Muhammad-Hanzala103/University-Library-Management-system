using System;

namespace KicsitLibrary.Core.Entities
{
    public class NotificationRecord : EntityBase
    {
        public string MemberType { get; set; } = "Student";
        
        public int? StudentId { get; set; }
        public virtual Student? Student { get; set; }
        
        public int? FacultyStaffId { get; set; }
        public virtual FacultyStaff? FacultyStaff { get; set; }
        
        public string NotificationType { get; set; } = "OverdueReminder";
        public string Channel { get; set; } = "Email";
        public string Subject { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        
        public DateTime? SentAt { get; set; }
        public string? FailureReason { get; set; }
    }
}
