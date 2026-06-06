using KicsitLibrary.Core.Entities;

namespace KicsitLibrary.Core.Models
{
    public class NotificationEligibilityResult
    {
        public bool CanCreate { get; set; }
        public string Reason { get; set; } = string.Empty;
        public NotificationRecord? LastNotification { get; set; }
    }

    public class NotificationCreateResult
    {
        public bool Created { get; set; }
        public string Message { get; set; } = string.Empty;
        public NotificationRecord Notification { get; set; } = null!;
    }

    public class OverdueProcessingResult
    {
        public int ProcessedCount { get; set; }
        public int CreatedCount { get; set; }
        public int SkippedCount { get; set; }
        public int FailedCount { get; set; }
        public string Message { get; set; } = string.Empty;

        public void Add(OverdueProcessingResult result)
        {
            ProcessedCount += result.ProcessedCount;
            CreatedCount += result.CreatedCount;
            SkippedCount += result.SkippedCount;
            FailedCount += result.FailedCount;
        }
    }
}
