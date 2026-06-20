using System;

namespace KicsitLibrary.Core.Entities
{
    public enum QueueMessageStatus
    {
        Pending,
        Sent,
        Failed
    }

    public enum QueueMessageChannel
    {
        SmsTwilio,
        SmsInfobip,
        WhatsAppTwilio,
        Email
    }

    public class NotificationQueue : EntityBase
    {
        public string Recipient { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public QueueMessageChannel Channel { get; set; }
        public QueueMessageStatus Status { get; set; } = QueueMessageStatus.Pending;
        public string? ErrorMessage { get; set; }
        public int AttemptCount { get; set; } = 0;
        public DateTime? SentAt { get; set; }
    }
}
