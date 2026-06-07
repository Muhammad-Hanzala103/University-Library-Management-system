using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Services.Inventory;

public sealed class StockVerificationService(
    KicsitLibraryDbContext context,
    IAuthenticationService authenticationService) : IStockVerificationService
{
    private static readonly HashSet<BookStatus> AllowedStatuses =
    [
        BookStatus.Available, BookStatus.Issued, BookStatus.Reserved, BookStatus.Lost,
        BookStatus.Damaged, BookStatus.Missing, BookStatus.UnderRepair, BookStatus.Deleted
    ];

    public async Task<IReadOnlyList<StockVerificationItem>> GetStockVerificationItemsAsync(
        StockVerificationFilter filter, CancellationToken cancellationToken = default)
    {
        await RequireViewAsync();
        filter ??= new StockVerificationFilter();
        var sessionId = filter.SessionId ?? await context.StockVerificationSessions
            .OrderByDescending(x => x.StartedAt).Select(x => (int?)x.Id).FirstOrDefaultAsync(cancellationToken);
        if (!sessionId.HasValue) return [];
        var query = context.StockVerificationEntries.AsNoTracking()
            .Where(x => x.SessionId == sessionId)
            .Include(x => x.BookCopy).ThenInclude(x => x.BookMaster).ThenInclude(x => x.Category)
            .Include(x => x.BookCopy.BookMaster.DepartmentCategory).AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.SearchText))
            query = query.Where(x => x.BookCopy.AccessionNumber.Contains(filter.SearchText) || x.BookCopy.BookMaster.Title.Contains(filter.SearchText));
        if (!string.IsNullOrWhiteSpace(filter.Category)) query = query.Where(x => x.BookCopy.BookMaster.Category.Name.Contains(filter.Category));
        if (!string.IsNullOrWhiteSpace(filter.Department)) query = query.Where(x => x.BookCopy.BookMaster.DepartmentCategory.Name.Contains(filter.Department));
        if (!string.IsNullOrWhiteSpace(filter.Rack)) query = query.Where(x => x.BookCopy.RackNumber != null && x.BookCopy.RackNumber.Contains(filter.Rack));
        if (!string.IsNullOrWhiteSpace(filter.Shelf)) query = query.Where(x => x.BookCopy.ShelfNumber != null && x.BookCopy.ShelfNumber.Contains(filter.Shelf));
        if (filter.ExpectedStatus.HasValue) query = query.Where(x => x.ExpectedStatus == filter.ExpectedStatus);
        if (filter.ActualStatus.HasValue) query = query.Where(x => x.ActualStatus == filter.ActualStatus);
        if (filter.MismatchedOnly) query = query.Where(x => x.IsMismatch);
        if (filter.UnverifiedOnly) query = query.Where(x => x.ActualStatus == null);
        var rows = await query.OrderBy(x => x.BookCopy.AccessionNumber).ToListAsync(cancellationToken);
        var users = await context.Users.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);
        return rows.Select(x => Map(x, users)).ToList();
    }

    public async Task<StockVerificationResult> StartVerificationSessionAsync(string remarks, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync()) return Failure("The current user cannot start stock verification.");
        if (await context.StockVerificationSessions.AnyAsync(x => x.Status == "InProgress", cancellationToken))
            return Failure("Complete the current verification session before starting another.");
        var user = authenticationService.CurrentUser!;
        var session = new StockVerificationSessionRecord
        {
            SessionNumber = $"SV-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..25],
            StartedAt = DateTime.UtcNow, Status = "InProgress", StartedByUserId = user.Id,
            Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim()
        };
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        context.StockVerificationSessions.Add(session);
        await context.SaveChangesAsync(cancellationToken);
        var copies = await context.BookCopies.IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken);
        context.StockVerificationEntries.AddRange(copies.Select(copy => new StockVerificationEntry
        {
            SessionId = session.Id, BookCopyId = copy.Id,
            ExpectedStatus = copy.IsDeleted ? BookStatus.Deleted : copy.AvailabilityStatus
        }));
        AddLog("Stock Verification Started", session.Id, $"SessionNumber={session.SessionNumber};Copies={copies.Count}");
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new StockVerificationResult { Succeeded = true, Message = "Stock verification session started.", Session = Map(session, user.FullName) };
    }

    public async Task<StockVerificationResult> VerifyBookCopyAsync(
        int sessionId, int bookCopyId, BookStatus actualStatus, string remarks,
        bool reconcile, string reconciliationReason, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync()) return Failure("The current user cannot verify stock.");
        if (!AllowedStatuses.Contains(actualStatus)) return Failure("Actual status is not supported for stock verification.");
        var entry = await context.StockVerificationEntries.Include(x => x.BookCopy)
            .FirstOrDefaultAsync(x => x.SessionId == sessionId && x.BookCopyId == bookCopyId, cancellationToken);
        if (entry == null) return Failure("Stock verification item was not found.");
        var session = await context.StockVerificationSessions.FindAsync([sessionId], cancellationToken);
        if (session?.Status != "InProgress") return Failure("The stock verification session is not active.");
        var mismatch = entry.ExpectedStatus != actualStatus;
        if (mismatch && string.IsNullOrWhiteSpace(remarks)) return Failure("Verification remarks are required when actual status differs.");
        if (reconcile && string.IsNullOrWhiteSpace(reconciliationReason)) return Failure("Reconciliation reason is required.");
        var user = authenticationService.CurrentUser!;
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        entry.ActualStatus = actualStatus; entry.IsMismatch = mismatch; entry.VerificationRemarks = remarks?.Trim();
        entry.VerifiedAt = DateTime.UtcNow; entry.VerifiedByUserId = user.Id;
        if (reconcile)
        {
            entry.BookCopy.AvailabilityStatus = actualStatus == BookStatus.Deleted ? entry.BookCopy.AvailabilityStatus : actualStatus;
            if (actualStatus == BookStatus.Deleted) { entry.BookCopy.IsDeleted = true; entry.BookCopy.DeletedAt = DateTime.UtcNow; entry.BookCopy.DeletedReason = reconciliationReason.Trim(); entry.BookCopy.DeletedByUserId = user.Id; }
            else if (entry.BookCopy.IsDeleted) { entry.BookCopy.IsDeleted = false; entry.BookCopy.DeletedAt = null; entry.BookCopy.DeletedReason = null; entry.BookCopy.DeletedByUserId = null; }
            entry.IsReconciled = true; entry.ReconciledAt = DateTime.UtcNow; entry.ReconciledByUserId = user.Id; entry.ReconciliationReason = reconciliationReason.Trim();
            AddLog("Stock Verification Reconciled", entry.Id, $"BookCopyId={bookCopyId};Status={actualStatus};Reason={Sanitize(reconciliationReason)}");
        }
        AddLog("Book Copy Verified", entry.Id, $"BookCopyId={bookCopyId};Expected={entry.ExpectedStatus};Actual={actualStatus};Mismatch={mismatch}");
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var item = (await GetStockVerificationItemsAsync(new StockVerificationFilter { SessionId = sessionId }, cancellationToken)).Single(x => x.BookCopyId == bookCopyId);
        return new StockVerificationResult { Succeeded = true, Message = reconcile ? "Book copy verified and reconciled." : "Book copy verified.", Item = item };
    }

    public async Task<StockVerificationResult> BulkMarkUnverifiedAsync(int sessionId, string remarks, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync()) return Failure("The current user cannot update stock verification.");
        if (string.IsNullOrWhiteSpace(remarks)) return Failure("Remarks are required when marking unverified copies missing.");
        var entries = await context.StockVerificationEntries.Where(x => x.SessionId == sessionId && x.ActualStatus == null).ToListAsync(cancellationToken);
        var user = authenticationService.CurrentUser!;
        foreach (var entry in entries)
        {
            entry.ActualStatus = BookStatus.Missing; entry.IsMismatch = entry.ExpectedStatus != BookStatus.Missing;
            entry.VerificationRemarks = remarks.Trim(); entry.VerifiedAt = DateTime.UtcNow; entry.VerifiedByUserId = user.Id;
        }
        AddLog("Unverified Stock Marked Missing", sessionId, $"Count={entries.Count};Remarks={Sanitize(remarks)}");
        await context.SaveChangesAsync(cancellationToken);
        return new StockVerificationResult { Succeeded = true, Message = $"{entries.Count} unverified copy/copies marked Missing.", Summary = await GetStockVerificationSummaryAsync(sessionId, cancellationToken) };
    }

    public async Task<StockVerificationResult> CompleteVerificationSessionAsync(int sessionId, string remarks, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync()) return Failure("The current user cannot complete stock verification.");
        var session = await context.StockVerificationSessions.FindAsync([sessionId], cancellationToken);
        if (session == null) return Failure("Stock verification session was not found.");
        if (session.Status == "Completed") return Failure("Stock verification session is already completed.");
        var user = authenticationService.CurrentUser!;
        session.Status = "Completed"; session.CompletedAt = DateTime.UtcNow; session.CompletedByUserId = user.Id;
        session.Remarks = string.IsNullOrWhiteSpace(remarks) ? session.Remarks : remarks.Trim();
        AddLog("Stock Verification Completed", session.Id, $"SessionNumber={session.SessionNumber}");
        await context.SaveChangesAsync(cancellationToken);
        return new StockVerificationResult { Succeeded = true, Message = "Stock verification session completed.", Session = Map(session, user.FullName), Summary = await GetStockVerificationSummaryAsync(sessionId, cancellationToken) };
    }

    public async Task<StockVerificationSummary> GetStockVerificationSummaryAsync(int sessionId, CancellationToken cancellationToken = default)
    {
        await RequireViewAsync();
        var values = await context.StockVerificationEntries.AsNoTracking().Where(x => x.SessionId == sessionId)
            .Select(x => new { x.ActualStatus, x.ExpectedStatus, x.IsMismatch }).ToListAsync(cancellationToken);
        int Count(BookStatus status) => values.Count(x => (x.ActualStatus ?? x.ExpectedStatus) == status);
        return new StockVerificationSummary
        {
            TotalCopies = values.Count, AvailableCount = Count(BookStatus.Available), IssuedCount = Count(BookStatus.Issued),
            ReservedCount = Count(BookStatus.Reserved), LostCount = Count(BookStatus.Lost), DamagedCount = Count(BookStatus.Damaged),
            MissingCount = Count(BookStatus.Missing), UnderRepairCount = Count(BookStatus.UnderRepair), DeletedCount = Count(BookStatus.Deleted),
            MatchedCount = values.Count(x => x.ActualStatus.HasValue && !x.IsMismatch),
            MismatchedCount = values.Count(x => x.ActualStatus.HasValue && x.IsMismatch),
            UnverifiedCount = values.Count(x => !x.ActualStatus.HasValue)
        };
    }

    private void AddLog(string action, int entityId, string detail) => context.ActivityLogs.Add(new ActivityLog
    {
        Action = action, UserId = authenticationService.CurrentUser!.Id, IpAddress = "127.0.0.1",
        Detail = $"EntityName=StockVerification;EntityId={entityId};{detail}"
    });
    private async Task RequireViewAsync() { if (!await InventoryAuthorization.CanViewAsync(authenticationService)) throw new UnauthorizedAccessException("The current user cannot view stock verification."); }
    private Task<bool> CanManageAsync() => InventoryAuthorization.CanManageAsync(authenticationService);
    private static StockVerificationSession Map(StockVerificationSessionRecord x, string user) => new()
    { StockVerificationSessionId = x.Id, SessionNumber = x.SessionNumber, StartedAt = x.StartedAt, CompletedAt = x.CompletedAt, Status = x.Status, StartedBy = user, Remarks = x.Remarks ?? "" };
    private static StockVerificationItem Map(StockVerificationEntry x, IReadOnlyDictionary<int, string> users) => new()
    {
        StockVerificationItemId = x.Id, SessionId = x.SessionId, BookCopyId = x.BookCopyId,
        AccessionNumber = x.BookCopy.AccessionNumber, BookTitle = x.BookCopy.BookMaster.Title,
        Category = x.BookCopy.BookMaster.Category.Name, Department = x.BookCopy.BookMaster.DepartmentCategory.Name,
        Rack = x.BookCopy.RackNumber ?? "", Shelf = x.BookCopy.ShelfNumber ?? "", ExpectedStatus = x.ExpectedStatus,
        ActualStatus = x.ActualStatus, VerificationRemarks = x.VerificationRemarks ?? "", VerifiedAt = x.VerifiedAt,
        VerifiedBy = x.VerifiedByUserId.HasValue && users.TryGetValue(x.VerifiedByUserId.Value, out var name) ? name : "",
        IsMismatch = x.IsMismatch, IsReconciled = x.IsReconciled
    };
    private static StockVerificationResult Failure(string error) => new() { Message = "Stock verification action failed.", ErrorMessage = error };
    private static string Sanitize(string value) => value.Replace(";", ",").Replace("=", "-");
}
