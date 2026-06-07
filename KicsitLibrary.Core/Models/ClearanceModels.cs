using KicsitLibrary.Core.Enums;

namespace KicsitLibrary.Core.Models;

public sealed class ClearanceCheckResult
{
    public MemberType MemberType { get; set; }
    public int MemberId { get; set; }
    public string MemberCode { get; set; } = string.Empty;
    public string MemberName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string ProgramOrDesignation { get; set; } = string.Empty;
    public bool CanClear { get; set; }
    public int PendingBooksCount { get; set; }
    public decimal PendingFineAmount { get; set; }
    public int LostOrDamagedCaseCount { get; set; }
    public IReadOnlyList<ClearanceBlockingItem> BlockingItems { get; set; } = [];
    public string Message { get; set; } = string.Empty;
}

public sealed class ClearanceBlockingItem
{
    public string BlockType { get; set; } = string.Empty;
    public string? AccessionNumber { get; set; }
    public string? BookTitle { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class ClearanceCertificateData
{
    public string CertificateNumber { get; set; } = string.Empty;
    public MemberType MemberType { get; set; }
    public string MemberCode { get; set; } = string.Empty;
    public string MemberName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string ProgramOrDesignation { get; set; } = string.Empty;
    public DateTime ClearanceDate { get; set; }
    public string ClearedBy { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public string BorrowingSummary { get; set; } = string.Empty;
    public string FineSummary { get; set; } = string.Empty;
    public int PendingBooksCount { get; set; }
    public decimal PendingFineAmount { get; set; }
    public string InstitutionName { get; set; } = string.Empty;
}

public sealed class ClearanceActionResult
{
    public bool Succeeded { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public ClearanceCheckResult? CheckResult { get; set; }
    public ClearanceCertificateData? CertificateData { get; set; }
    public string? FilePath { get; set; }
    public DateTime CompletedAt { get; set; }
}

public sealed class ClearanceHistoryItem
{
    public int ActivityLogId { get; set; }
    public MemberType MemberType { get; set; }
    public int MemberId { get; set; }
    public string MemberCode { get; set; } = string.Empty;
    public string MemberName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime PerformedAt { get; set; }
}
