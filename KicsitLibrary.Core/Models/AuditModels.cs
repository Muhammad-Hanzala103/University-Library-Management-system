using KicsitLibrary.Core.Enums;

namespace KicsitLibrary.Core.Models;

public sealed class AuditRecordFilter
{
    public string? SearchText { get; set; }
    public string? AuditType { get; set; }
    public AuditStatus? Status { get; set; }
    public string? FinancialYear { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public bool PendingActionOnly { get; set; }
    public int Limit { get; set; } = 500;
}

public sealed class AuditRecordListItem
{
    public int AuditRecordId { get; set; }
    public string AuditNumber { get; set; } = string.Empty;
    public string AuditType { get; set; } = string.Empty;
    public DateTime AuditDate { get; set; }
    public string FinancialYear { get; set; } = string.Empty;
    public AuditStatus Status { get; set; }
    public string ResponsiblePerson { get; set; } = string.Empty;
    public string ActionRequired { get; set; } = string.Empty;
    public string ActionTaken { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
}

public sealed class AuditRecordDetails
{
    public int AuditRecordId { get; set; }
    public string AuditNumber { get; set; } = string.Empty;
    public DateTime AuditDate { get; set; } = DateTime.Today;
    public string AuditType { get; set; } = string.Empty;
    public string FinancialYear { get; set; } = string.Empty;
    public string InspectionDetail { get; set; } = string.Empty;
    public string FinancialDetail { get; set; } = string.Empty;
    public string Observations { get; set; } = string.Empty;
    public string Findings { get; set; } = string.Empty;
    public string Suggestions { get; set; } = string.Empty;
    public string ActionRequired { get; set; } = string.Empty;
    public string ActionTaken { get; set; } = string.Empty;
    public string ResponsiblePerson { get; set; } = string.Empty;
    public AuditStatus Status { get; set; } = AuditStatus.Draft;
    public string Remarks { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string UpdatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public IReadOnlyList<AuditAttachmentItem> Attachments { get; set; } = [];
}

public sealed class AuditActionResult
{
    public bool Succeeded { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public AuditRecordDetails? AuditRecord { get; set; }
}

public sealed class AuditStatusSummary
{
    public int TotalCount { get; set; }
    public int DraftCount { get; set; }
    public int SubmittedCount { get; set; }
    public int UnderReviewCount { get; set; }
    public int CompletedCount { get; set; }
    public int ClosedCount { get; set; }
    public int PendingActionCount { get; set; }
}

public sealed class AuditAttachmentItem
{
    public int AuditFileId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}
