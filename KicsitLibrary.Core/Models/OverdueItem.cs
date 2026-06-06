using System;
using KicsitLibrary.Core.Enums;

namespace KicsitLibrary.Core.Models
{
    public class OverdueItem
    {
        public int IssueRecordId { get; set; }
        public int BookCopyId { get; set; }
        public int BookMasterId { get; set; }
        public string AccessionNumber { get; set; } = string.Empty;
        public string BookTitle { get; set; } = string.Empty;
        public MemberType MemberType { get; set; }
        public int MemberId { get; set; }
        public string MemberName { get; set; } = string.Empty;
        public string MemberCode { get; set; } = string.Empty;
        public string MemberEmail { get; set; } = string.Empty;
        public DateTime IssueDate { get; set; }
        public DateTime ExpectedReturnDate { get; set; }
        public int DaysOverdue { get; set; }
        public decimal FinePerDay { get; set; }
        public decimal CurrentFineAmount { get; set; }
        public DateTime? LastNotificationDate { get; set; }
        public NotificationStatus? NotificationStatus { get; set; }
        public bool CanSendReminder { get; set; }
        public string ReasonIfCannotSend { get; set; } = string.Empty;
    }
}
