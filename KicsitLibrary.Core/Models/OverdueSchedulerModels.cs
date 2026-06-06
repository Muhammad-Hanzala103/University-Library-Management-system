using System;

namespace KicsitLibrary.Core.Models
{
    public class OverdueSchedulerStatus
    {
        public bool Enabled { get; set; }
        public bool RunOnStartup { get; set; }
        public int IntervalMinutes { get; set; } = 60;
        public int InitialDelaySeconds { get; set; } = 30;
        public bool SendPendingEmails { get; set; }
        public int MaxRunMinutes { get; set; } = 10;
        public DateTime? LastRunAt { get; set; }
        public DateTime? LastSuccessAt { get; set; }
        public DateTime? LastFailureAt { get; set; }
        public string LastMessage { get; set; } = string.Empty;
        public bool IsRunning { get; set; }
    }

    public class OverdueSchedulerRunResult
    {
        public DateTime StartedAt { get; set; }
        public DateTime FinishedAt { get; set; }
        public bool Succeeded { get; set; }
        public bool WasSkipped { get; set; }
        public int ProcessedCount { get; set; }
        public int CreatedCount { get; set; }
        public int SkippedCount { get; set; }
        public int FailedCount { get; set; }
        public int EmailsAttempted { get; set; }
        public int EmailsSent { get; set; }
        public int EmailsFailed { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? FailureReason { get; set; }
    }
}
