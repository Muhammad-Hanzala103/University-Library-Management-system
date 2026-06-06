using System;

namespace KicsitLibrary.Core.Models
{
    public class EmailMessage
    {
        public string ToEmail { get; set; } = string.Empty;
        public string ToName { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string PlainTextBody { get; set; } = string.Empty;
        public string? HtmlBody { get; set; }
        public string FromEmail { get; set; } = string.Empty;
        public string FromName { get; set; } = string.Empty;
    }

    public class EmailSendResult
    {
        public bool Succeeded { get; set; }
        public string? FailureReason { get; set; }
        public string? ProviderMessageId { get; set; }
        public DateTime? SentAt { get; set; }
    }

    public class EmailTransportOptions
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public bool UseSsl { get; set; } = true;
        public string User { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
        public string FromName { get; set; } = "KICSIT Library";
        public bool EmailNotificationEnabled { get; set; }
        public int MaxNotificationRetryCount { get; set; } = 3;
    }

    public class EmailSettingsValidationResult
    {
        public bool IsValid { get; set; }
        public bool IsEnabled { get; set; }
        public string Message { get; set; } = string.Empty;
        public EmailTransportOptions Options { get; set; } = new();
    }
}
