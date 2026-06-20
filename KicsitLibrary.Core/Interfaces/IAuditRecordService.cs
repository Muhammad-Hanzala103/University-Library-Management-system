using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Core.Interfaces;

public interface IAuditRecordService
{
    Task<IReadOnlyList<AuditRecordListItem>> GetAuditRecordsAsync(
        AuditRecordFilter filter,
        CancellationToken cancellationToken = default);
    Task<AuditRecordDetails> GetAuditRecordDetailsAsync(
        int auditRecordId,
        CancellationToken cancellationToken = default);
    Task<AuditActionResult> CreateAuditRecordAsync(
        AuditRecordDetails request,
        CancellationToken cancellationToken = default);
    Task<AuditActionResult> UpdateAuditRecordAsync(
        int auditRecordId,
        AuditRecordDetails request,
        CancellationToken cancellationToken = default);
    Task<AuditActionResult> ChangeAuditStatusAsync(
        int auditRecordId,
        AuditStatus status,
        string remarks,
        CancellationToken cancellationToken = default);
    Task<AuditActionResult> DeleteAuditRecordAsync(
        int auditRecordId,
        string reason,
        CancellationToken cancellationToken = default);
    Task<AuditStatusSummary> GetAuditStatusSummaryAsync(
        CancellationToken cancellationToken = default);
    Task<bool> VerifyLedgerIntegrityAsync(
        CancellationToken cancellationToken = default);
}
