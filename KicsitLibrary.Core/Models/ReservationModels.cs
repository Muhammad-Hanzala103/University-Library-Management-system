using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;

namespace KicsitLibrary.Core.Models;

public sealed class ReservationQueueItem
{
    public int ReservationId { get; set; }
    public string ReservationNumber { get; set; } = string.Empty;
    public int QueuePosition { get; set; }
    public int BookMasterId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public MemberType MemberType { get; set; }
    public int MemberId { get; set; }
    public string MemberCode { get; set; } = string.Empty;
    public string MemberName { get; set; } = string.Empty;
    public DateTime ReservationDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public ReservationStatus Status { get; set; }
    public string? AccessionNumber { get; set; }
    public string? Remarks { get; set; }
}

public sealed class ReservationEligibilityResult
{
    public bool IsEligible { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ActiveReservationCount { get; set; }
    public int AvailableCopyCount { get; set; }
    public bool DirectIssueAvailable => AvailableCopyCount > 0;
    public bool HasDuplicateReservation { get; set; }
    public bool HasActiveIssueForTitle { get; set; }
    public decimal PendingFineAmount { get; set; }
    public bool IsActiveMember { get; set; }
    public bool IsCleared { get; set; }
}

public sealed class ReservationActionResult
{
    public bool Succeeded { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public Reservation? Reservation { get; set; }
    public DateTime CompletedAt { get; set; }
}

public sealed class ReservationAvailabilityResult : ReservationActionResult
{
    public int BookMasterId { get; set; }
    public int AvailableCopyCount { get; set; }
    public int? QueuePosition { get; set; }
    public int NotificationRecordsCreated { get; set; }
}

public sealed class ReservationFulfillmentResult : ReservationActionResult
{
    public IssueRecord? IssueRecord { get; set; }
    public BookCopy? AssignedCopy { get; set; }
}
