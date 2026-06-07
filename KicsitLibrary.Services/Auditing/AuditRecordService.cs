using System.Text.Json;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Services.Auditing;

public sealed class AuditRecordService : IAuditRecordService
{
    private readonly KicsitLibraryDbContext _context;
    private readonly IAuthenticationService _authenticationService;

    public AuditRecordService(
        KicsitLibraryDbContext context,
        IAuthenticationService authenticationService)
    {
        _context = context;
        _authenticationService = authenticationService;
    }

    public async Task<IReadOnlyList<AuditRecordListItem>> GetAuditRecordsAsync(
        AuditRecordFilter filter,
        CancellationToken cancellationToken = default)
    {
        await RequireViewAsync();
        filter ??= new AuditRecordFilter();
        var query = _context.AuditRecords.AsNoTracking()
            .Include(item => item.CreatedByUser)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var search = filter.SearchText.Trim();
            query = query.Where(item =>
                item.AuditNumber.Contains(search) ||
                item.AuditType.Contains(search) ||
                item.Observations.Contains(search) ||
                item.Findings.Contains(search) ||
                item.Suggestions.Contains(search) ||
                item.ResponsiblePerson.Contains(search));
        }
        if (!string.IsNullOrWhiteSpace(filter.AuditType))
            query = query.Where(item => item.AuditType == filter.AuditType);
        if (filter.Status.HasValue)
            query = query.Where(item => item.Status == filter.Status.Value);
        if (!string.IsNullOrWhiteSpace(filter.FinancialYear))
            query = query.Where(item => item.FinancialYear == filter.FinancialYear);
        if (filter.FromDate.HasValue)
            query = query.Where(item => item.AuditDate >= filter.FromDate.Value.Date);
        if (filter.ToDate.HasValue)
            query = query.Where(item => item.AuditDate < filter.ToDate.Value.Date.AddDays(1));
        if (filter.PendingActionOnly)
            query = query.Where(item =>
                item.ActionRequired != string.Empty &&
                item.ActionTaken == string.Empty);

        return await query.OrderByDescending(item => item.AuditDate)
            .ThenByDescending(item => item.Id)
            .Take(Math.Clamp(filter.Limit <= 0 ? 500 : filter.Limit, 1, 2000))
            .Select(item => new AuditRecordListItem
            {
                AuditRecordId = item.Id,
                AuditNumber = item.AuditNumber,
                AuditType = item.AuditType,
                AuditDate = item.AuditDate,
                FinancialYear = item.FinancialYear,
                Status = item.Status,
                ResponsiblePerson = item.ResponsiblePerson,
                ActionRequired = item.ActionRequired,
                ActionTaken = item.ActionTaken,
                CreatedBy = item.CreatedByUser.FullName,
                UpdatedAt = item.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<AuditRecordDetails> GetAuditRecordDetailsAsync(
        int auditRecordId,
        CancellationToken cancellationToken = default)
    {
        await RequireViewAsync();
        var record = await _context.AuditRecords.AsNoTracking()
            .Include(item => item.CreatedByUser)
            .Include(item => item.UpdatedByUser)
            .Include(item => item.AuditFiles)
            .FirstOrDefaultAsync(item => item.Id == auditRecordId, cancellationToken) ??
            throw new InvalidOperationException("Audit record was not found.");
        return Map(record);
    }

    public async Task<AuditActionResult> CreateAuditRecordAsync(
        AuditRecordDetails request,
        CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync())
            return Failure("The current user cannot create audit records.");
        var validation = Validate(request);
        if (validation != null)
            return Failure(validation);
        if (await _context.AuditRecords.AnyAsync(
            item => item.AuditNumber == request.AuditNumber.Trim(),
            cancellationToken))
            return Failure("Audit number already exists.");

        var user = _authenticationService.CurrentUser!;
        var record = new AuditRecord { CreatedByUserId = user.Id };
        Apply(record, request);
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _context.AuditRecords.Add(record);
            await _context.SaveChangesAsync(cancellationToken);
            AddLog("Audit Record Created", record, $"Status={record.Status}", user.Id);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Success("Audit record created.", await GetAuditRecordDetailsAsync(record.Id, cancellationToken));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Failure(ex.Message);
        }
    }

    public async Task<AuditActionResult> UpdateAuditRecordAsync(
        int auditRecordId,
        AuditRecordDetails request,
        CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync())
            return Failure("The current user cannot update audit records.");
        var validation = Validate(request);
        if (validation != null)
            return Failure(validation);
        if (await _context.AuditRecords.AnyAsync(
            item => item.Id != auditRecordId &&
                item.AuditNumber == request.AuditNumber.Trim(),
            cancellationToken))
            return Failure("Audit number already exists.");

        var record = await _context.AuditRecords.FindAsync([auditRecordId], cancellationToken);
        if (record == null)
            return Failure("Audit record was not found.");
        var user = _authenticationService.CurrentUser!;
        Apply(record, request);
        record.UpdatedByUserId = user.Id;
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            AddLog("Audit Record Updated", record, $"Status={record.Status}", user.Id);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Success("Audit record updated.", await GetAuditRecordDetailsAsync(record.Id, cancellationToken));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Failure(ex.Message);
        }
    }

    public async Task<AuditActionResult> ChangeAuditStatusAsync(
        int auditRecordId,
        AuditStatus status,
        string remarks,
        CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync())
            return Failure("The current user cannot change audit status.");
        if (string.IsNullOrWhiteSpace(remarks))
            return Failure("Status change remarks are required.");
        var record = await _context.AuditRecords.FindAsync([auditRecordId], cancellationToken);
        if (record == null)
            return Failure("Audit record was not found.");

        var user = _authenticationService.CurrentUser!;
        var previous = record.Status;
        record.Status = status;
        record.Remarks = Append(record.Remarks, remarks.Trim());
        record.UpdatedByUserId = user.Id;
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            AddLog(
                "Audit Status Changed",
                record,
                $"PreviousStatus={previous};NewStatus={status};Remarks={Sanitize(remarks)}",
                user.Id);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Success("Audit status changed.", await GetAuditRecordDetailsAsync(record.Id, cancellationToken));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Failure(ex.Message);
        }
    }

    public async Task<AuditActionResult> DeleteAuditRecordAsync(
        int auditRecordId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync())
            return Failure("The current user cannot delete audit records.");
        if (string.IsNullOrWhiteSpace(reason))
            return Failure("A delete reason is required.");
        var record = await _context.AuditRecords
            .Include(item => item.AuditFiles)
            .FirstOrDefaultAsync(item => item.Id == auditRecordId, cancellationToken);
        if (record == null)
            return Failure("Audit record was not found.");

        var user = _authenticationService.CurrentUser!;
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            record.IsDeleted = true;
            record.DeletedAt = DateTime.UtcNow;
            record.DeletedReason = reason.Trim();
            record.DeletedByUserId = user.Id;
            _context.DeletedRecordArchives.Add(new DeletedRecordArchive
            {
                TableName = "AuditRecords",
                RecordId = record.Id,
                SerializedData = JsonSerializer.Serialize(new
                {
                    record.AuditNumber,
                    record.AuditDate,
                    record.AuditType,
                    record.FinancialYear,
                    record.Status,
                    record.Observations,
                    record.Findings,
                    record.Suggestions,
                    record.ActionRequired,
                    record.ActionTaken,
                    AttachmentCount = record.AuditFiles.Count
                }),
                DeletedByUserId = user.Id,
                DeletedAt = DateTime.UtcNow,
                DeletedReason = reason.Trim()
            });
            AddLog("Audit Record Deleted", record, $"Reason={Sanitize(reason)}", user.Id);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new AuditActionResult { Succeeded = true, Message = "Audit record was soft-deleted." };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Failure(ex.Message);
        }
    }

    public async Task<AuditStatusSummary> GetAuditStatusSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        await RequireViewAsync();
        var values = await _context.AuditRecords.AsNoTracking()
            .Select(item => new { item.Status, item.ActionRequired, item.ActionTaken })
            .ToListAsync(cancellationToken);
        return new AuditStatusSummary
        {
            TotalCount = values.Count,
            DraftCount = values.Count(item => item.Status == AuditStatus.Draft),
            SubmittedCount = values.Count(item => item.Status == AuditStatus.Submitted),
            UnderReviewCount = values.Count(item => item.Status == AuditStatus.UnderReview),
            CompletedCount = values.Count(item => item.Status == AuditStatus.Completed),
            ClosedCount = values.Count(item => item.Status == AuditStatus.Closed),
            PendingActionCount = values.Count(item =>
                !string.IsNullOrWhiteSpace(item.ActionRequired) &&
                string.IsNullOrWhiteSpace(item.ActionTaken))
        };
    }

    private async Task RequireViewAsync()
    {
        if (!await AuditAuthorization.CanViewAsync(_authenticationService))
            throw new UnauthorizedAccessException("The current user cannot view audit records.");
    }

    private Task<bool> CanManageAsync() =>
        AuditAuthorization.CanManageAsync(_authenticationService);

    private static string? Validate(AuditRecordDetails request)
    {
        if (request == null)
            return "Audit data is required.";
        if (string.IsNullOrWhiteSpace(request.AuditNumber))
            return "Audit number is required.";
        if (request.AuditDate == default)
            return "Audit date is required.";
        if (string.IsNullOrWhiteSpace(request.AuditType))
            return "Audit type is required.";
        if (!Enum.IsDefined(request.Status))
            return "Audit status is required.";
        return null;
    }

    private static void Apply(AuditRecord record, AuditRecordDetails request)
    {
        record.AuditNumber = request.AuditNumber.Trim();
        record.AuditDate = request.AuditDate;
        record.AuditType = request.AuditType.Trim();
        record.FinancialYear = request.FinancialYear.Trim();
        record.InspectionDetail = request.InspectionDetail.Trim();
        record.FinancialDetail = request.FinancialDetail.Trim();
        record.Observations = request.Observations.Trim();
        record.Findings = request.Findings.Trim();
        record.Suggestions = request.Suggestions.Trim();
        record.ActionRequired = request.ActionRequired.Trim();
        record.ActionTaken = request.ActionTaken.Trim();
        record.ResponsiblePerson = request.ResponsiblePerson.Trim();
        record.Status = request.Status;
        record.Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim();
    }

    private void AddLog(string action, AuditRecord record, string metadata, int userId)
    {
        _context.ActivityLogs.Add(new ActivityLog
        {
            Action = action,
            Detail =
                $"EntityName=AuditRecord;EntityId={record.Id};AuditNumber={Sanitize(record.AuditNumber)};{metadata}",
            UserId = userId,
            IpAddress = "127.0.0.1"
        });
    }

    private static AuditRecordDetails Map(AuditRecord record) =>
        new()
        {
            AuditRecordId = record.Id,
            AuditNumber = record.AuditNumber,
            AuditDate = record.AuditDate,
            AuditType = record.AuditType,
            FinancialYear = record.FinancialYear,
            InspectionDetail = record.InspectionDetail,
            FinancialDetail = record.FinancialDetail,
            Observations = record.Observations,
            Findings = record.Findings,
            Suggestions = record.Suggestions,
            ActionRequired = record.ActionRequired,
            ActionTaken = record.ActionTaken,
            ResponsiblePerson = record.ResponsiblePerson,
            Status = record.Status,
            Remarks = record.Remarks ?? string.Empty,
            CreatedBy = record.CreatedByUser?.FullName ?? "Unknown User",
            UpdatedBy = record.UpdatedByUser?.FullName ?? string.Empty,
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt,
            Attachments = record.AuditFiles.OrderByDescending(item => item.UploadedAt)
                .Select(item => new AuditAttachmentItem
                {
                    AuditFileId = item.Id,
                    FileName = item.FileName,
                    FilePath = item.FilePath,
                    UploadedAt = item.UploadedAt
                }).ToList()
        };

    private static AuditActionResult Success(string message, AuditRecordDetails details) =>
        new() { Succeeded = true, Message = message, AuditRecord = details };

    private static AuditActionResult Failure(string error) =>
        new() { Message = "Audit action failed.", ErrorMessage = error };

    private static string Append(string? existing, string value) =>
        string.IsNullOrWhiteSpace(existing) ? value : $"{existing} | {value}";

    private static string Sanitize(string value) =>
        value.Replace(";", ",", StringComparison.Ordinal)
            .Replace("=", "-", StringComparison.Ordinal);
}
