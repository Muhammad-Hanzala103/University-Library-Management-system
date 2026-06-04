using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Data;

namespace KicsitLibrary.Services.Circulation
{
    public class CirculationService : ICirculationService
    {
        private readonly KicsitLibraryDbContext _context;
        private readonly IActivityLogService _logService;

        public CirculationService(KicsitLibraryDbContext context, IActivityLogService logService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        // ==========================================
        // VALIDATION & ELIGIBILITY
        // ==========================================
        public async Task<BookCopy?> GetCopyDetailsForCirculationAsync(string accessionNumber)
        {
            return await _context.BookCopies
                .Include(bc => bc.BookMaster)
                .FirstOrDefaultAsync(bc => bc.AccessionNumber == accessionNumber && !bc.IsDeleted);
        }

        public async Task<object?> GetMemberDetailsAsync(string identifier, MemberType type)
        {
            if (type == MemberType.Student)
            {
                return await _context.Students
                    .FirstOrDefaultAsync(s => s.RegistrationNumber == identifier && !s.IsDeleted);
            }
            else
            {
                return await _context.FacultyStaff
                    .FirstOrDefaultAsync(fs => fs.PersonnelNumber == identifier && !fs.IsDeleted);
            }
        }

        public async Task<(int CurrentIssuedCount, int MaxAllowedLimit, decimal PendingFinesTotal, bool HasActiveOverdue, string EligibilityMessage)> CheckMemberEligibilityAsync(int memberId, MemberType memberType)
        {
            int currentIssued = 0;
            int maxAllowed = 3; // default student limit
            decimal pendingFines = 0;
            bool hasOverdue = false;
            bool isActive = true;
            string message = "Eligible";

            // 1. Get Limits from SystemSettings
            var settings = await _context.SystemSettings.ToListAsync();
            var studentLimitVal = settings.FirstOrDefault(s => s.Key == "StudentIssueLimit")?.Value;
            var facultyLimitVal = settings.FirstOrDefault(s => s.Key == "FacultyIssueLimit")?.Value;
            var staffLimitVal = settings.FirstOrDefault(s => s.Key == "StaffIssueLimit")?.Value;

            int.TryParse(studentLimitVal ?? "3", out int studentLimit);
            int.TryParse(facultyLimitVal ?? "10", out int facultyLimit);
            int.TryParse(staffLimitVal ?? "5", out int staffLimit);

            // 2. Load member details and check active status
            if (memberType == MemberType.Student)
            {
                var student = await _context.Students
                    .Include(s => s.IssueRecords).ThenInclude(ir => ir.ReceiveRecord)
                    .Include(s => s.Fines)
                    .FirstOrDefaultAsync(s => s.Id == memberId && !s.IsDeleted);

                if (student != null)
                {
                    isActive = student.ActiveStatus;
                    maxAllowed = studentLimit;
                    
                    // Count active issues (where ReceiveRecord is null)
                    currentIssued = student.IssueRecords.Count(ir => ir.ReceiveRecord == null);
                    
                    // Check for overdue
                    hasOverdue = student.IssueRecords.Any(ir => ir.ReceiveRecord == null && DateTime.UtcNow > ir.ExpectedReturnDate);

                    // Pending Fines
                    pendingFines = student.Fines
                        .Where(f => !f.IsDeleted && f.PaymentStatus != FineStatus.Paid && f.PaymentStatus != FineStatus.Waived)
                        .Sum(f => f.RemainingAmount);
                }
                else
                {
                    isActive = false;
                }
            }
            else
            {
                var fs = await _context.FacultyStaff
                    .Include(fs => fs.IssueRecords).ThenInclude(ir => ir.ReceiveRecord)
                    .Include(fs => fs.Fines)
                    .FirstOrDefaultAsync(fs => fs.Id == memberId && !fs.IsDeleted);

                if (fs != null)
                {
                    isActive = fs.ActiveStatus;
                    maxAllowed = fs.FacultyType == FacultyType.Staff ? staffLimit : facultyLimit;
                    
                    currentIssued = fs.IssueRecords.Count(ir => ir.ReceiveRecord == null);
                    hasOverdue = fs.IssueRecords.Any(ir => ir.ReceiveRecord == null && DateTime.UtcNow > ir.ExpectedReturnDate);

                    pendingFines = fs.Fines
                        .Where(f => !f.IsDeleted && f.PaymentStatus != FineStatus.Paid && f.PaymentStatus != FineStatus.Waived)
                        .Sum(f => f.RemainingAmount);
                }
                else
                {
                    isActive = false;
                }
            }

            // 3. Formulate message
            if (!isActive)
            {
                message = "Member account is deactivated or blocked.";
            }
            else if (hasOverdue)
            {
                message = "Member has outstanding overdue books that must be returned first.";
            }
            else if (pendingFines > 0)
            {
                message = $"Member has unpaid fines (Rs. {pendingFines:N0}) that must be settled.";
            }
            else if (currentIssued >= maxAllowed)
            {
                message = $"Member has reached the maximum borrowing limit of {maxAllowed} books.";
            }

            return (currentIssued, maxAllowed, pendingFines, hasOverdue, message);
        }

        // ==========================================
        // CIRCULATION TRANSITIONS
        // ==========================================
        public async Task<IssueRecord> IssueBookAsync(string accessionNumber, int memberId, MemberType memberType, int issuedByUserId)
        {
            // 1. Get Book Copy details
            var copy = await _context.BookCopies
                .Include(bc => bc.BookMaster)
                .FirstOrDefaultAsync(bc => bc.AccessionNumber == accessionNumber && !bc.IsDeleted);

            if (copy == null)
            {
                throw new InvalidOperationException($"Book copy with Accession Number '{accessionNumber}' does not exist.");
            }

            if (copy.AvailabilityStatus != BookStatus.Available)
            {
                throw new InvalidOperationException($"Book copy '{accessionNumber}' is currently not available. Status: {copy.AvailabilityStatus}");
            }

            // 2. Validate member eligibility
            var eligibility = await CheckMemberEligibilityAsync(memberId, memberType);
            if (eligibility.EligibilityMessage != "Eligible")
            {
                throw new InvalidOperationException($"Checkout Blocked: {eligibility.EligibilityMessage}");
            }

            // 3. Load System Settings for duration & daily fine rate
            var defaultIssueDaysVal = (await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "DefaultIssueDays"))?.Value ?? "14";
            var finePerDayVal = (await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "FinePerDay"))?.Value ?? "10";

            int.TryParse(defaultIssueDaysVal, out int issueDays);
            decimal.TryParse(finePerDayVal, out decimal finePerDay);

            // 4. Create IssueRecord
            var issueRecord = new IssueRecord
            {
                AccessionNumber = copy.AccessionNumber,
                BookCopyId = copy.Id,
                MemberType = memberType,
                StudentId = memberType == MemberType.Student ? memberId : null,
                FacultyStaffId = memberType == MemberType.FacultyStaff ? memberId : null,
                IssueDate = DateTime.UtcNow,
                ExpectedReturnDate = DateTime.UtcNow.AddDays(issueDays),
                FinePerDay = finePerDay,
                IssuedByUserId = issuedByUserId
            };

            // Update BookCopy status
            copy.AvailabilityStatus = BookStatus.Issued;

            await _context.IssueRecords.AddAsync(issueRecord);
            await _context.SaveChangesAsync();

            // Log Transaction
            var memberLabel = memberType == MemberType.Student ? $"Student ID {memberId}" : $"Faculty ID {memberId}";
            await _logService.LogActivityAsync("Book Checkout", $"Book {accessionNumber} successfully checked out to {memberLabel}.", issuedByUserId);

            return issueRecord;
        }

        public async Task<ReceiveRecord> ReceiveBookAsync(string accessionNumber, string condition, decimal collectedAmount, string? waiverReason, string? remarks, int receivedByUserId)
        {
            // 1. Get active issue record
            var issueRecord = await _context.IssueRecords
                .Include(ir => ir.BookCopy).ThenInclude(bc => bc.BookMaster)
                .Include(ir => ir.ReceiveRecord)
                .FirstOrDefaultAsync(ir => ir.AccessionNumber == accessionNumber && ir.ReceiveRecord == null && !ir.IsDeleted);

            if (issueRecord == null)
            {
                throw new InvalidOperationException($"No active borrow record found for Accession Number '{accessionNumber}'.");
            }

            // 2. Calculate overdue details & fines
            int lateDays = 0;
            decimal calculatedFine = 0;
            if (DateTime.UtcNow > issueRecord.ExpectedReturnDate)
            {
                lateDays = (int)(DateTime.UtcNow - issueRecord.ExpectedReturnDate).TotalDays;
                calculatedFine = lateDays * issueRecord.FinePerDay;
            }

            // If lost or damaged, add purchase price + admin surcharge (Rs. 200)
            if (condition == "Lost" || condition == "Damaged")
            {
                var bookPrice = issueRecord.BookCopy.BookMaster.PurchasePrice;
                calculatedFine += bookPrice + 200;
            }

            // 3. Process fines if any
            if (calculatedFine > 0)
            {
                decimal waivedAmt = 0;
                decimal remaining = calculatedFine - collectedAmount;

                if (remaining > 0 && !string.IsNullOrWhiteSpace(waiverReason))
                {
                    waivedAmt = remaining;
                    remaining = 0;
                }

                var fineRecord = new Fine
                {
                    FineRecordNumber = $"FINE-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}",
                    MemberType = issueRecord.MemberType,
                    StudentId = issueRecord.StudentId,
                    FacultyStaffId = issueRecord.FacultyStaffId,
                    IssueRecordId = issueRecord.Id,
                    AccessionNumber = accessionNumber,
                    FineType = condition == "Normal" ? "Overdue" : condition,
                    FinePerDay = issueRecord.FinePerDay,
                    DaysOverdue = lateDays,
                    FineAmount = calculatedFine,
                    PaidAmount = collectedAmount,
                    WaivedAmount = waivedAmt,
                    WaiverReason = waiverReason,
                    RemainingAmount = remaining,
                    PaymentStatus = remaining == 0 ? (waivedAmt > 0 ? FineStatus.Waived : FineStatus.Paid) : FineStatus.Partial,
                    PaymentDate = collectedAmount > 0 ? DateTime.UtcNow : null,
                    CollectedByUserId = collectedAmount > 0 ? receivedByUserId : null
                };

                await _context.Fines.AddAsync(fineRecord);
            }

            // 4. Create Receive Record
            var receiveRecord = new ReceiveRecord
            {
                IssueRecordId = issueRecord.Id,
                ReceiveDate = DateTime.UtcNow,
                FineType = condition == "Normal" ? "Overdue" : condition,
                FineAmount = calculatedFine,
                Reason = condition,
                Remarks = remarks,
                BookConditionAfterReturn = condition,
                FineCollectedOrUnpaid = collectedAmount >= calculatedFine ? "Paid" : (!string.IsNullOrWhiteSpace(waiverReason) ? "Waived" : "Unpaid"),
                FineWaiverReason = waiverReason,
                ReceivedByUserId = receivedByUserId
            };

            // Update Book Copy status
            var copy = issueRecord.BookCopy;
            if (condition == "Lost")
            {
                copy.AvailabilityStatus = BookStatus.Lost;
            }
            else if (condition == "Damaged")
            {
                copy.AvailabilityStatus = BookStatus.Damaged;
            }
            else
            {
                copy.AvailabilityStatus = BookStatus.Available;
            }

            await _context.ReceiveRecords.AddAsync(receiveRecord);
            await _context.SaveChangesAsync();

            // Log Transaction
            await _logService.LogActivityAsync("Book Check-in", $"Book {accessionNumber} returned in {condition} condition. Fine: Rs. {calculatedFine:N0}.", receivedByUserId);

            return receiveRecord;
        }

        public async Task<IssueRecord> RenewBookAsync(int issueRecordId, int renewedByUserId)
        {
            var record = await _context.IssueRecords
                .Include(ir => ir.BookCopy)
                .FirstOrDefaultAsync(ir => ir.Id == issueRecordId && ir.ReceiveRecord == null && !ir.IsDeleted);

            if (record == null)
            {
                throw new InvalidOperationException("Active issue record not found.");
            }

            // Check if there is a pending reservation for this BookMaster
            var hasReservation = await _context.Reservations
                .AnyAsync(r => r.BookMasterId == record.BookCopy.BookMasterId && r.Status == ReservationStatus.Pending && !r.IsDeleted);

            if (hasReservation)
            {
                throw new InvalidOperationException("Renewal Denied: This book has a pending hold reservation by another member.");
            }

            // Extend duration
            var defaultIssueDaysVal = (await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "DefaultIssueDays"))?.Value ?? "14";
            int.TryParse(defaultIssueDaysVal, out int days);

            record.ExpectedReturnDate = DateTime.UtcNow.AddDays(days);
            record.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _logService.LogActivityAsync("Book Renewal", $"Extended loan for copy {record.AccessionNumber} to {record.ExpectedReturnDate:dd-MMM-yyyy}.", renewedByUserId);

            return record;
        }

        public async Task<Reservation> CreateReservationAsync(int bookMasterId, int memberId, MemberType memberType)
        {
            // Verify book exists
            var book = await _context.BookMasters.FindAsync(bookMasterId);
            if (book == null || book.IsDeleted)
            {
                throw new InvalidOperationException("Book catalog title does not exist.");
            }

            var reservationExpiryVal = (await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "ReservationExpiryDays"))?.Value ?? "3";
            int.TryParse(reservationExpiryVal, out int expiryDays);

            var reservation = new Reservation
            {
                BookMasterId = bookMasterId,
                MemberType = memberType,
                StudentId = memberType == MemberType.Student ? memberId : null,
                FacultyStaffId = memberType == MemberType.FacultyStaff ? memberId : null,
                ReservationDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddDays(expiryDays),
                Status = ReservationStatus.Pending
            };

            await _context.Reservations.AddAsync(reservation);
            await _context.SaveChangesAsync();

            await _logService.LogActivityAsync("Book Hold Request", $"Book ID {bookMasterId} put on hold for member.", 1);

            return reservation;
        }

        // ==========================================
        // FINES BILLING MANAGEMENT
        // ==========================================
        public async Task<IEnumerable<Fine>> GetActiveFinesAsync(string? searchQuery)
        {
            var query = _context.Fines
                .Include(f => f.Student)
                .Include(f => f.FacultyStaff)
                .Include(f => f.IssueRecord).ThenInclude(ir => ir.BookCopy).ThenInclude(bc => bc.BookMaster)
                .Where(f => !f.IsDeleted && f.RemainingAmount > 0);

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                var q = searchQuery.Trim().ToLowerInvariant();
                query = query.Where(f => 
                    f.AccessionNumber.Contains(q) || 
                    f.FineRecordNumber.Contains(q) ||
                    (f.Student != null && f.Student.Name.ToLower().Contains(q)) ||
                    (f.FacultyStaff != null && f.FacultyStaff.Name.ToLower().Contains(q)));
            }

            return await query.OrderByDescending(f => f.CreatedAt).ToListAsync();
        }

        public async Task CollectFinePaymentAsync(int fineId, decimal amount, string? remarks, int userId)
        {
            var fine = await _context.Fines.FindAsync(fineId);
            if (fine == null || fine.IsDeleted)
            {
                throw new InvalidOperationException("Fine record not found.");
            }

            if (amount > fine.RemainingAmount)
            {
                throw new InvalidOperationException($"Cannot collect more than outstanding balance of Rs. {fine.RemainingAmount:N0}.");
            }

            fine.PaidAmount += amount;
            fine.RemainingAmount = Math.Max(0, fine.RemainingAmount - amount);
            fine.PaymentStatus = fine.RemainingAmount == 0 ? FineStatus.Paid : FineStatus.Partial;
            fine.PaymentDate = DateTime.UtcNow;
            fine.CollectedByUserId = userId;
            fine.Remarks = string.IsNullOrWhiteSpace(remarks) ? fine.Remarks : remarks;

            fine.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _logService.LogActivityAsync("Fine Collected", $"Collected Rs. {amount:N0} for fine record {fine.FineRecordNumber}.", userId);
        }

        public async Task WaiveFineAsync(int fineId, decimal amount, string reason, int userId)
        {
            var fine = await _context.Fines.FindAsync(fineId);
            if (fine == null || fine.IsDeleted)
            {
                throw new InvalidOperationException("Fine record not found.");
            }

            if (amount > fine.RemainingAmount)
            {
                throw new InvalidOperationException($"Cannot waive more than outstanding balance of Rs. {fine.RemainingAmount:N0}.");
            }

            fine.WaivedAmount += amount;
            fine.RemainingAmount = Math.Max(0, fine.RemainingAmount - amount);
            fine.PaymentStatus = fine.RemainingAmount == 0 ? FineStatus.Waived : FineStatus.Partial;
            fine.WaiverReason = reason;
            fine.Remarks = $"Waived Rs. {amount:N0}. Reason: {reason}";

            fine.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _logService.LogActivityAsync("Fine Waived", $"Waived Rs. {amount:N0} for fine record {fine.FineRecordNumber}. Reason: {reason}", userId);
        }
    }
}
