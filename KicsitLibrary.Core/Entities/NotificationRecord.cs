using System;
using KicsitLibrary.Core.Enums;

namespace KicsitLibrary.Core.Entities
{
    public class NotificationRecord : EntityBase
    {
        public MemberType MemberType { get; set; } = MemberType.Student;
        
        public int? StudentId { get; set; }
        public virtual Student? Student { get; set; }
        
        public int? FacultyStaffId { get; set; }
        public virtual FacultyStaff? FacultyStaff { get; set; }
        
        public NotificationType NotificationType { get; set; } = NotificationType.OverdueReminder;
        public string Channel { get; set; } = "Email";
        public string Subject { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
        
        public DateTime? SentAt { get; set; }
        public string? FailureReason { get; set; }
    }
}
