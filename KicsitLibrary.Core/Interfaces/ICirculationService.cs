using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;

namespace KicsitLibrary.Core.Interfaces
{
    public interface ICirculationService
    {
        // ==========================================
        // CIRCULATION TRANSITIONS
        // ==========================================
        Task<IssueRecord> IssueBookAsync(string accessionNumber, int memberId, MemberType memberType, int issuedByUserId);
        Task<ReceiveRecord> ReceiveBookAsync(string accessionNumber, string condition, decimal collectedAmount, string? waiverReason, string? remarks, int receivedByUserId);
        Task<IssueRecord> RenewBookAsync(int issueRecordId, int renewedByUserId);
        Task<Reservation> CreateReservationAsync(int bookMasterId, int memberId, MemberType memberType);

        // ==========================================
        // VALIDATION & ELIGIBILITY
        // ==========================================
        Task<(int CurrentIssuedCount, int MaxAllowedLimit, decimal PendingFinesTotal, bool HasActiveOverdue, string EligibilityMessage)> CheckMemberEligibilityAsync(int memberId, MemberType memberType);
        Task<BookCopy?> GetCopyDetailsForCirculationAsync(string accessionNumber);
        Task<object?> GetMemberDetailsAsync(string identifier, MemberType type);

        // ==========================================
        // FINES BILLING MANAGEMENT
        // ==========================================
        Task<IEnumerable<Fine>> GetActiveFinesAsync(string? searchQuery);
        Task CollectFinePaymentAsync(int fineId, decimal amount, string? remarks, int userId);
        Task WaiveFineAsync(int fineId, decimal amount, string reason, int userId);
    }
}
