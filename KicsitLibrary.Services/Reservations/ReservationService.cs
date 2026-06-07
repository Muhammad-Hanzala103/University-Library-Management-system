using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Services.Reservations;

public sealed class ReservationService : IReservationService
{
    private static readonly ReservationStatus[] ActiveStatuses =
        [ReservationStatus.Pending, ReservationStatus.Available];

    private readonly KicsitLibraryDbContext _context;
    private readonly IAuthenticationService _authenticationService;
    private readonly ICirculationService _circulationService;
    private readonly INotificationService _notificationService;

    public ReservationService(
        KicsitLibraryDbContext context,
        IAuthenticationService authenticationService,
        ICirculationService circulationService,
        INotificationService notificationService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _authenticationService = authenticationService ??
            throw new ArgumentNullException(nameof(authenticationService));
        _circulationService = circulationService ??
            throw new ArgumentNullException(nameof(circulationService));
        _notificationService = notificationService ??
            throw new ArgumentNullException(nameof(notificationService));
    }

    public async Task<ReservationEligibilityResult> CheckReservationEligibilityAsync(
        int bookMasterId,
        int memberId,
        MemberType memberType,
        CancellationToken cancellationToken = default)
    {
        var bookExists = await _context.BookMasters.AsNoTracking()
            .AnyAsync(book => book.Id == bookMasterId, cancellationToken);
        if (!bookExists)
        {
            return Ineligible("Book catalog title was not found.");
        }

        var member = await GetMemberAsync(memberId, memberType, cancellationToken);
        if (member == null)
        {
            return Ineligible("Member was not found.");
        }

        var activeCount = await MemberReservations(memberId, memberType)
            .CountAsync(reservation => ActiveStatuses.Contains(reservation.Status), cancellationToken);
        var duplicate = await MemberReservations(memberId, memberType)
            .AnyAsync(
                reservation =>
                    reservation.BookMasterId == bookMasterId &&
                    ActiveStatuses.Contains(reservation.Status),
                cancellationToken);
        var activeIssue = await MemberIssues(memberId, memberType)
            .AnyAsync(
                issue =>
                    issue.BookCopy.BookMasterId == bookMasterId &&
                    issue.ReceiveRecord == null,
                cancellationToken);
        var pendingFines = await MemberFines(memberId, memberType)
            .Where(fine =>
                fine.RemainingAmount > 0 &&
                (fine.PaymentStatus == FineStatus.Unpaid ||
                 fine.PaymentStatus == FineStatus.Partial))
            .SumAsync(fine => fine.RemainingAmount, cancellationToken);
        var availableCopies = await _context.BookCopies.AsNoTracking()
            .CountAsync(
                copy =>
                    copy.BookMasterId == bookMasterId &&
                    copy.AvailabilityStatus == BookStatus.Available,
                cancellationToken);

        var result = new ReservationEligibilityResult
        {
            IsActiveMember = member.Value.IsActive,
            IsCleared = member.Value.IsCleared,
            ActiveReservationCount = activeCount,
            HasDuplicateReservation = duplicate,
            HasActiveIssueForTitle = activeIssue,
            PendingFineAmount = pendingFines,
            AvailableCopyCount = availableCopies
        };

        if (!result.IsActiveMember)
            result.Message = "Member account is inactive.";
        else if (result.IsCleared)
            result.Message = "Member has already been library-cleared.";
        else if (duplicate)
            result.Message = "Member already has an active reservation for this title.";
        else if (activeIssue)
            result.Message = "Member already has an active issue for this title.";
        else if (pendingFines > 0)
            result.Message = $"Member has pending fines of Rs. {pendingFines:N2}.";
        else
        {
            result.IsEligible = true;
            result.Message = availableCopies > 0
                ? "Eligible. A copy is available for direct issue, but a reservation may still be created."
                : "Eligible for reservation.";
        }

        return result;
    }

    public async Task<ReservationActionResult> CreateReservationAsync(
        int bookMasterId,
        int memberId,
        MemberType memberType,
        string? remarks = null,
        CancellationToken cancellationToken = default)
    {
        var user = RequireCurrentUser();
        var eligibility = await CheckReservationEligibilityAsync(
            bookMasterId,
            memberId,
            memberType,
            cancellationToken);
        if (!eligibility.IsEligible)
            return Failure(eligibility.Message);

        var expiryDays = await GetExpiryDaysAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var reservation = new Reservation
        {
            ReservationNumber = $"RES-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            BookMasterId = bookMasterId,
            MemberType = memberType,
            StudentId = memberType == MemberType.Student ? memberId : null,
            FacultyStaffId = memberType == MemberType.FacultyStaff ? memberId : null,
            ReservationDate = now,
            ExpiryDate = now.AddDays(expiryDays),
            Status = ReservationStatus.Pending,
            Remarks = Clean(remarks)
        };

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _context.Reservations.Add(reservation);
            AddLog(
                "Reservation Created",
                $"Reservation {reservation.ReservationNumber} created for {memberType} {memberId}, title {bookMasterId}.",
                user.Id);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Success(reservation, "Reservation created successfully.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Failure(ex.Message);
        }
    }

    public async Task<IReadOnlyList<ReservationQueueItem>> GetReservationQueueAsync(
        int bookMasterId,
        CancellationToken cancellationToken = default)
    {
        var reservations = await BaseQuery()
            .Where(reservation =>
                reservation.BookMasterId == bookMasterId &&
                ActiveStatuses.Contains(reservation.Status))
            .OrderBy(reservation => reservation.ReservationDate)
            .ThenBy(reservation => reservation.Id)
            .ToListAsync(cancellationToken);
        return MapQueue(reservations);
    }

    public Task<ReservationActionResult> CancelReservationAsync(
        int reservationId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        return ChangeStatusAsync(
            reservationId,
            ReservationStatus.Cancelled,
            reason,
            "Reservation Cancelled",
            cancellationToken);
    }

    public Task<ReservationActionResult> ExpireReservationAsync(
        int reservationId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        return ChangeStatusAsync(
            reservationId,
            ReservationStatus.Expired,
            reason,
            "Reservation Expired",
            cancellationToken);
    }

    public async Task<int> ExpireOldReservationsAsync(
        DateTime? asOfUtc = null,
        CancellationToken cancellationToken = default)
    {
        var user = RequireCurrentUser();
        var cutoff = EnsureUtc(asOfUtc ?? DateTime.UtcNow);
        var expired = await _context.Reservations
            .Where(reservation =>
                ActiveStatuses.Contains(reservation.Status) &&
                reservation.ExpiryDate < cutoff)
            .ToListAsync(cancellationToken);
        if (expired.Count == 0)
            return 0;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var reservation in expired)
            {
                reservation.Status = ReservationStatus.Expired;
                reservation.Remarks = AppendReason(reservation.Remarks, "Expired automatically after the configured hold period.");
                AddLog(
                    "Reservation Expired",
                    $"Reservation {reservation.ReservationNumber} expired automatically.",
                    user.Id);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return expired.Count;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ReservationAvailabilityResult> MarkReservationAvailableAsync(
        int bookMasterId,
        CancellationToken cancellationToken = default)
    {
        var user = RequireCurrentUser();
        var availableCopies = await _context.BookCopies
            .CountAsync(
                copy =>
                    copy.BookMasterId == bookMasterId &&
                    copy.AvailabilityStatus == BookStatus.Available,
                cancellationToken);
        if (availableCopies == 0)
        {
            return AvailabilityFailure(bookMasterId, "No available copy exists for this title.");
        }

        var reservation = await _context.Reservations
            .Include(item => item.BookMaster)
            .Include(item => item.Student)
            .Include(item => item.FacultyStaff)
            .Where(item =>
                item.BookMasterId == bookMasterId &&
                ActiveStatuses.Contains(item.Status))
            .OrderBy(item => item.ReservationDate)
            .ThenBy(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (reservation == null)
        {
            return AvailabilityFailure(bookMasterId, "No active reservation exists for this title.");
        }
        if (reservation.Status == ReservationStatus.Available)
        {
            return new ReservationAvailabilityResult
            {
                Succeeded = true,
                Message = "The first reservation is already marked available.",
                Reservation = reservation,
                BookMasterId = bookMasterId,
                AvailableCopyCount = availableCopies,
                QueuePosition = 1,
                CompletedAt = DateTime.UtcNow
            };
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            reservation.Status = ReservationStatus.Available;
            reservation.ExpiryDate = DateTime.UtcNow.AddDays(
                await GetExpiryDaysAsync(cancellationToken));
            reservation.Remarks = AppendReason(
                reservation.Remarks,
                "A copy is available for collection.");
            AddLog(
                "Reservation Available",
                $"Reservation {reservation.ReservationNumber} is available for collection.",
                user.Id);
            await _context.SaveChangesAsync(cancellationToken);
            var notifications = await _notificationService
                .CreateReservationAvailableNotificationsAsync(
                    reservation,
                    user.Id,
                    cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ReservationAvailabilityResult
            {
                Succeeded = true,
                Message = "First queued reservation marked available.",
                Reservation = reservation,
                BookMasterId = bookMasterId,
                AvailableCopyCount = availableCopies,
                QueuePosition = 1,
                NotificationRecordsCreated = notifications,
                CompletedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AvailabilityFailure(bookMasterId, ex.Message);
        }
    }

    public async Task<ReservationFulfillmentResult> FulfillReservationAsync(
        int reservationId,
        string? accessionNumber = null,
        CancellationToken cancellationToken = default)
    {
        var user = RequireCurrentUser();
        var reservation = await _context.Reservations
            .Include(item => item.BookMaster)
            .FirstOrDefaultAsync(item => item.Id == reservationId, cancellationToken);
        if (reservation == null)
            return FulfillmentFailure("Reservation was not found.");
        if (!ActiveStatuses.Contains(reservation.Status))
            return FulfillmentFailure($"Reservation status {reservation.Status} cannot be fulfilled.");

        var firstReservationId = await _context.Reservations
            .Where(item =>
                item.BookMasterId == reservation.BookMasterId &&
                ActiveStatuses.Contains(item.Status))
            .OrderBy(item => item.ReservationDate)
            .ThenBy(item => item.Id)
            .Select(item => item.Id)
            .FirstAsync(cancellationToken);
        if (firstReservationId != reservation.Id)
            return FulfillmentFailure("Only the first member in the reservation queue can be fulfilled.");

        var copy = string.IsNullOrWhiteSpace(accessionNumber)
            ? await _context.BookCopies
                .Where(item =>
                    item.BookMasterId == reservation.BookMasterId &&
                    item.AvailabilityStatus == BookStatus.Available)
                .OrderBy(item => item.CopyNumber)
                .ThenBy(item => item.Id)
                .FirstOrDefaultAsync(cancellationToken)
            : await _context.BookCopies.FirstOrDefaultAsync(
                item =>
                    item.BookMasterId == reservation.BookMasterId &&
                    item.AccessionNumber == accessionNumber.Trim() &&
                    item.AvailabilityStatus == BookStatus.Available,
                cancellationToken);
        if (copy == null)
            return FulfillmentFailure("No available copy can be assigned to this reservation.");

        var memberId = reservation.MemberType == MemberType.Student
            ? reservation.StudentId
            : reservation.FacultyStaffId;
        if (!memberId.HasValue)
            return FulfillmentFailure("Reservation member linkage is invalid.");

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var issue = await _circulationService.IssueBookAsync(
                copy.AccessionNumber,
                memberId.Value,
                reservation.MemberType,
                user.Id);
            reservation.Status = ReservationStatus.Issued;
            reservation.AccessionNumber = copy.AccessionNumber;
            reservation.Remarks = AppendReason(
                reservation.Remarks,
                $"Fulfilled by issue record {issue.Id}.");
            AddLog(
                "Reservation Fulfilled",
                $"Reservation {reservation.ReservationNumber} fulfilled with copy {copy.AccessionNumber} and issue {issue.Id}.",
                user.Id);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ReservationFulfillmentResult
            {
                Succeeded = true,
                Message = "Reservation fulfilled and book issued successfully.",
                Reservation = reservation,
                IssueRecord = issue,
                AssignedCopy = copy,
                CompletedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return FulfillmentFailure(ex.Message);
        }
    }

    public async Task<IReadOnlyList<ReservationQueueItem>> GetReservationsAsync(
        string? searchText = null,
        ReservationStatus? status = null,
        MemberType? memberType = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = BaseQuery();
        if (status.HasValue)
            query = query.Where(item => item.Status == status.Value);
        if (memberType.HasValue)
            query = query.Where(item => item.MemberType == memberType.Value);
        if (fromDate.HasValue)
            query = query.Where(item => item.ReservationDate >= fromDate.Value.Date.ToUniversalTime());
        if (toDate.HasValue)
            query = query.Where(item => item.ReservationDate < toDate.Value.Date.AddDays(1).ToUniversalTime());
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var search = searchText.Trim();
            query = query.Where(item =>
                item.ReservationNumber.Contains(search) ||
                item.BookMaster.Title.Contains(search) ||
                (item.Student != null &&
                    (item.Student.Name.Contains(search) ||
                     item.Student.RegistrationNumber.Contains(search))) ||
                (item.FacultyStaff != null &&
                    (item.FacultyStaff.Name.Contains(search) ||
                     item.FacultyStaff.PersonnelNumber.Contains(search))));
        }

        var reservations = await query
            .OrderByDescending(item => item.ReservationDate)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);
        return await MapWithQueuePositionsAsync(reservations, cancellationToken);
    }

    public async Task<IReadOnlyList<ReservationQueueItem>> GetMemberReservationsAsync(
        int memberId,
        MemberType memberType,
        CancellationToken cancellationToken = default)
    {
        var reservations = await BaseQuery()
            .Where(item => memberType == MemberType.Student
                ? item.StudentId == memberId
                : item.FacultyStaffId == memberId)
            .OrderByDescending(item => item.ReservationDate)
            .ToListAsync(cancellationToken);
        return await MapWithQueuePositionsAsync(reservations, cancellationToken);
    }

    private async Task<ReservationActionResult> ChangeStatusAsync(
        int reservationId,
        ReservationStatus status,
        string reason,
        string action,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Failure("A reason is required.");
        var user = RequireCurrentUser();
        var reservation = await _context.Reservations
            .FirstOrDefaultAsync(item => item.Id == reservationId, cancellationToken);
        if (reservation == null)
            return Failure("Reservation was not found.");
        if (!ActiveStatuses.Contains(reservation.Status))
            return Failure($"Reservation status {reservation.Status} cannot be changed.");

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            reservation.Status = status;
            reservation.Remarks = AppendReason(reservation.Remarks, reason.Trim());
            AddLog(
                action,
                $"Reservation {reservation.ReservationNumber}: {reason.Trim()}",
                user.Id);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Success(reservation, $"Reservation marked {status}.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Failure(ex.Message);
        }
    }

    private IQueryable<Reservation> BaseQuery() =>
        _context.Reservations.AsNoTracking()
            .Include(item => item.BookMaster)
            .Include(item => item.Student)
            .Include(item => item.FacultyStaff);

    private IQueryable<Reservation> MemberReservations(int memberId, MemberType memberType) =>
        _context.Reservations.Where(item => memberType == MemberType.Student
            ? item.StudentId == memberId
            : item.FacultyStaffId == memberId);

    private IQueryable<IssueRecord> MemberIssues(int memberId, MemberType memberType) =>
        _context.IssueRecords.Where(item => memberType == MemberType.Student
            ? item.StudentId == memberId
            : item.FacultyStaffId == memberId);

    private IQueryable<Fine> MemberFines(int memberId, MemberType memberType) =>
        _context.Fines.Where(item => memberType == MemberType.Student
            ? item.StudentId == memberId
            : item.FacultyStaffId == memberId);

    private async Task<(bool IsActive, bool IsCleared)?> GetMemberAsync(
        int memberId,
        MemberType memberType,
        CancellationToken cancellationToken)
    {
        if (memberType == MemberType.Student)
        {
            var member = await _context.Students.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == memberId, cancellationToken);
            return member == null
                ? null
                : (member.ActiveStatus, member.ClearanceStatus == ClearanceStatus.Cleared);
        }

        var faculty = await _context.FacultyStaff.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == memberId, cancellationToken);
        return faculty == null
            ? null
            : (faculty.ActiveStatus, faculty.ClearanceStatus == ClearanceStatus.Cleared);
    }

    private async Task<int> GetExpiryDaysAsync(CancellationToken cancellationToken)
    {
        var value = await _context.SystemSettings.AsNoTracking()
            .Where(setting => setting.Key == "ReservationExpiryDays")
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken);
        return int.TryParse(value, out var days) && days > 0 ? days : 3;
    }

    private async Task<IReadOnlyList<ReservationQueueItem>> MapWithQueuePositionsAsync(
        IReadOnlyCollection<Reservation> reservations,
        CancellationToken cancellationToken)
    {
        var bookIds = reservations.Select(item => item.BookMasterId).Distinct().ToList();
        var active = await _context.Reservations.AsNoTracking()
            .Where(item =>
                bookIds.Contains(item.BookMasterId) &&
                ActiveStatuses.Contains(item.Status))
            .OrderBy(item => item.ReservationDate)
            .ThenBy(item => item.Id)
            .Select(item => new { item.Id, item.BookMasterId })
            .ToListAsync(cancellationToken);
        var positions = active.GroupBy(item => item.BookMasterId)
            .SelectMany(group => group.Select((item, index) => new { item.Id, Position = index + 1 }))
            .ToDictionary(item => item.Id, item => item.Position);
        return reservations.Select(item => Map(item, positions.GetValueOrDefault(item.Id))).ToList();
    }

    private static IReadOnlyList<ReservationQueueItem> MapQueue(
        IReadOnlyList<Reservation> reservations) =>
        reservations.Select((item, index) => Map(item, index + 1)).ToList();

    private static ReservationQueueItem Map(Reservation item, int position) =>
        new()
        {
            ReservationId = item.Id,
            ReservationNumber = item.ReservationNumber,
            QueuePosition = position,
            BookMasterId = item.BookMasterId,
            BookTitle = item.BookMaster.Title,
            MemberType = item.MemberType,
            MemberId = item.MemberType == MemberType.Student
                ? item.StudentId ?? 0
                : item.FacultyStaffId ?? 0,
            MemberCode = item.MemberType == MemberType.Student
                ? item.Student?.RegistrationNumber ?? string.Empty
                : item.FacultyStaff?.PersonnelNumber ?? string.Empty,
            MemberName = item.MemberType == MemberType.Student
                ? item.Student?.Name ?? string.Empty
                : item.FacultyStaff?.Name ?? string.Empty,
            ReservationDate = item.ReservationDate.ToLocalTime(),
            ExpiryDate = item.ExpiryDate.ToLocalTime(),
            Status = item.Status,
            AccessionNumber = item.AccessionNumber,
            Remarks = item.Remarks
        };

    private User RequireCurrentUser() =>
        _authenticationService.CurrentUser ??
        throw new InvalidOperationException("An authenticated user is required for reservation actions.");

    private void AddLog(string action, string detail, int userId) =>
        _context.ActivityLogs.Add(new ActivityLog
        {
            Action = action,
            Detail = detail,
            UserId = userId,
            IpAddress = "127.0.0.1"
        });

    private static ReservationEligibilityResult Ineligible(string message) =>
        new() { Message = message };

    private static ReservationActionResult Success(Reservation reservation, string message) =>
        new()
        {
            Succeeded = true,
            Message = message,
            Reservation = reservation,
            CompletedAt = DateTime.UtcNow
        };

    private static ReservationActionResult Failure(string error) =>
        new()
        {
            Succeeded = false,
            Message = "Reservation action failed.",
            ErrorMessage = error,
            CompletedAt = DateTime.UtcNow
        };

    private static ReservationAvailabilityResult AvailabilityFailure(int bookMasterId, string error) =>
        new()
        {
            Succeeded = false,
            Message = "Reservation availability action failed.",
            ErrorMessage = error,
            BookMasterId = bookMasterId,
            CompletedAt = DateTime.UtcNow
        };

    private static ReservationFulfillmentResult FulfillmentFailure(string error) =>
        new()
        {
            Succeeded = false,
            Message = "Reservation fulfillment failed.",
            ErrorMessage = error,
            CompletedAt = DateTime.UtcNow
        };

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string AppendReason(string? existing, string reason) =>
        string.IsNullOrWhiteSpace(existing) ? reason : $"{existing} | {reason}";

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}
