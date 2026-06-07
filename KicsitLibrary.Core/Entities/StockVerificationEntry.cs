using KicsitLibrary.Core.Enums;

namespace KicsitLibrary.Core.Entities;

public class StockVerificationEntry : EntityBase
{
    public int SessionId { get; set; }
    public int BookCopyId { get; set; }
    public BookStatus ExpectedStatus { get; set; }
    public BookStatus? ActualStatus { get; set; }
    public string? VerificationRemarks { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public int? VerifiedByUserId { get; set; }
    public bool IsMismatch { get; set; }
    public bool IsReconciled { get; set; }
    public DateTime? ReconciledAt { get; set; }
    public int? ReconciledByUserId { get; set; }
    public string? ReconciliationReason { get; set; }
    public virtual StockVerificationSessionRecord Session { get; set; } = null!;
    public virtual BookCopy BookCopy { get; set; } = null!;
}
