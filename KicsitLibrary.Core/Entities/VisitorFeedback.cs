using System;

namespace KicsitLibrary.Core.Entities
{
    public class VisitorFeedback : EntityBase
    {
        public string VisitorName { get; set; } = string.Empty;
        public string? CNIC { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string VisitPurpose { get; set; } = string.Empty;
        public string FeedbackType { get; set; } = "Suggestion"; // e.g. Suggestion, Complaint, Appreciation, Inquiry, Other
        public string FeedbackText { get; set; } = string.Empty;
        public string Status { get; set; } = "New"; // New, Reviewed, Closed
        public string? ReviewedRemarks { get; set; }
        public int? ReviewedByUserId { get; set; }
    }
}
