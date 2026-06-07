using System.Globalization;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data;
using KicsitLibrary.Reports.Contracts;
using KicsitLibrary.Reports.Models;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Services.Clearance;

public sealed class ClearanceService : IClearanceService
{
    private static readonly BookStatus[] LossStatuses =
        [BookStatus.Lost, BookStatus.Damaged, BookStatus.Missing, BookStatus.UnderRepair];

    private readonly KicsitLibraryDbContext _context;
    private readonly IAuthenticationService _authenticationService;
    private readonly IReportExporter _pdfExporter;

    public ClearanceService(
        KicsitLibraryDbContext context,
        IAuthenticationService authenticationService,
        IEnumerable<IReportExporter> exporters)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _authenticationService = authenticationService ??
            throw new ArgumentNullException(nameof(authenticationService));
        _pdfExporter = exporters.FirstOrDefault(exporter => exporter.Format == ReportFormat.PDF) ??
            throw new InvalidOperationException("A PDF report exporter is required for clearance certificates.");
    }

    public async Task<ClearanceCheckResult> CheckStudentClearanceAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var student = await _context.Students.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == studentId, cancellationToken) ??
            throw new InvalidOperationException("Student was not found.");
        return await BuildCheckAsync(
            MemberType.Student,
            student.Id,
            student.RegistrationNumber,
            student.Name,
            student.Department,
            student.Program,
            cancellationToken);
    }

    public async Task<ClearanceCheckResult> CheckFacultyStaffClearanceAsync(
        int facultyStaffId,
        CancellationToken cancellationToken = default)
    {
        var member = await _context.FacultyStaff.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == facultyStaffId, cancellationToken) ??
            throw new InvalidOperationException("Faculty or staff member was not found.");
        return await BuildCheckAsync(
            MemberType.FacultyStaff,
            member.Id,
            member.PersonnelNumber,
            member.Name,
            member.Department,
            member.Designation,
            cancellationToken);
    }

    public Task<ClearanceActionResult> ApproveStudentClearanceAsync(
        int studentId,
        string remarks,
        CancellationToken cancellationToken = default)
    {
        return ApproveAsync(MemberType.Student, studentId, remarks, cancellationToken);
    }

    public Task<ClearanceActionResult> ApproveFacultyStaffClearanceAsync(
        int facultyStaffId,
        string remarks,
        CancellationToken cancellationToken = default)
    {
        return ApproveAsync(MemberType.FacultyStaff, facultyStaffId, remarks, cancellationToken);
    }

    public Task<ClearanceActionResult> RevokeStudentClearanceAsync(
        int studentId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        return RevokeAsync(MemberType.Student, studentId, reason, cancellationToken);
    }

    public Task<ClearanceActionResult> RevokeFacultyStaffClearanceAsync(
        int facultyStaffId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        return RevokeAsync(MemberType.FacultyStaff, facultyStaffId, reason, cancellationToken);
    }

    public async Task<IReadOnlyList<ClearanceHistoryItem>> GetClearanceHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        var logs = await _context.ActivityLogs.AsNoTracking()
            .Include(log => log.User)
            .Where(log => log.Action.StartsWith("Clearance "))
            .OrderByDescending(log => log.CreatedAt)
            .ToListAsync(cancellationToken);

        return logs.Select(log =>
        {
            var values = ParseDetail(log.Detail);
            Enum.TryParse<MemberType>(Get(values, "MemberType"), out var memberType);
            int.TryParse(Get(values, "MemberId"), out var memberId);
            return new ClearanceHistoryItem
            {
                ActivityLogId = log.Id,
                MemberType = memberType,
                MemberId = memberId,
                MemberCode = Get(values, "MemberCode"),
                MemberName = Get(values, "MemberName"),
                Action = log.Action,
                Remarks = Get(values, "Remarks"),
                PerformedBy = log.User?.FullName ?? "Unknown User",
                PerformedAt = log.CreatedAt.ToLocalTime()
            };
        }).ToList();
    }

    public async Task<ClearanceActionResult> GenerateClearanceCertificateAsync(
        MemberType memberType,
        int memberId,
        string? outputDirectory = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var certificate = await BuildCertificateDataAsync(
                memberType,
                memberId,
                cancellationToken);
            var report = BuildCertificateReport(certificate);
            var directory = string.IsNullOrWhiteSpace(outputDirectory)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "KICSIT Library Certificates")
                : outputDirectory;
            var export = await _pdfExporter.ExportAsync(
                report,
                new ReportExportRequest
                {
                    Format = ReportFormat.PDF,
                    OutputDirectory = directory,
                    FileName = $"Clearance Certificate {certificate.MemberCode} {DateTime.Now:yyyyMMdd_HHmmss}"
                },
                cancellationToken);
            if (!export.Succeeded)
            {
                return Failure(export.ErrorMessage ?? export.Message);
            }

            AddActivityLog(
                "Clearance Certificate Generated",
                memberType,
                memberId,
                certificate.MemberCode,
                certificate.MemberName,
                export.FilePath ?? string.Empty);
            await _context.SaveChangesAsync(cancellationToken);
            return new ClearanceActionResult
            {
                Succeeded = true,
                Message = $"Clearance certificate generated at {export.FilePath}.",
                FilePath = export.FilePath,
                CertificateData = certificate,
                CompletedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return Failure(ex.Message);
        }
    }

    private async Task<ClearanceCheckResult> BuildCheckAsync(
        MemberType memberType,
        int memberId,
        string memberCode,
        string memberName,
        string department,
        string programOrDesignation,
        CancellationToken cancellationToken)
    {
        var issues = await _context.IssueRecords.AsNoTracking()
            .Include(issue => issue.ReceiveRecord)
            .Include(issue => issue.BookCopy).ThenInclude(copy => copy.BookMaster)
            .Where(issue => memberType == MemberType.Student
                ? issue.StudentId == memberId
                : issue.FacultyStaffId == memberId)
            .ToListAsync(cancellationToken);
        var fines = await _context.Fines.AsNoTracking()
            .Include(fine => fine.IssueRecord).ThenInclude(issue => issue.BookCopy).ThenInclude(copy => copy.BookMaster)
            .Where(fine => memberType == MemberType.Student
                ? fine.StudentId == memberId
                : fine.FacultyStaffId == memberId)
            .ToListAsync(cancellationToken);

        var activeIssues = issues.Where(issue => issue.ReceiveRecord == null).ToList();
        var pendingFines = fines
            .Where(fine => fine.PaymentStatus is FineStatus.Unpaid or FineStatus.Partial &&
                fine.RemainingAmount > 0)
            .ToList();
        var lossCases = issues
            .Where(issue => LossStatuses.Contains(issue.BookCopy.AvailabilityStatus) &&
                (issue.ReceiveRecord == null ||
                 pendingFines.Any(fine => fine.IssueRecordId == issue.Id)))
            .ToList();

        var blockingItems = new List<ClearanceBlockingItem>();
        blockingItems.AddRange(activeIssues.Select(issue => new ClearanceBlockingItem
        {
            BlockType = "Active Issue",
            AccessionNumber = issue.AccessionNumber,
            BookTitle = issue.BookCopy.BookMaster.Title,
            Reason = $"Book is still issued and due on {issue.ExpectedReturnDate.ToLocalTime():dd-MMM-yyyy}.",
            CreatedAt = issue.IssueDate.ToLocalTime()
        }));
        blockingItems.AddRange(pendingFines.Select(fine => new ClearanceBlockingItem
        {
            BlockType = "Pending Fine",
            AccessionNumber = fine.AccessionNumber,
            BookTitle = fine.IssueRecord.BookCopy.BookMaster.Title,
            Amount = fine.RemainingAmount,
            Reason = $"{fine.PaymentStatus} fine balance is pending.",
            CreatedAt = fine.CreatedAt.ToLocalTime()
        }));
        blockingItems.AddRange(lossCases.Select(issue => new ClearanceBlockingItem
        {
            BlockType = "Lost or Damaged Case",
            AccessionNumber = issue.AccessionNumber,
            BookTitle = issue.BookCopy.BookMaster.Title,
            Reason = $"Copy status is {issue.BookCopy.AvailabilityStatus}.",
            CreatedAt = issue.UpdatedAt?.ToLocalTime() ?? issue.CreatedAt.ToLocalTime()
        }));

        var canClear = activeIssues.Count == 0 && pendingFines.Count == 0 && lossCases.Count == 0;
        return new ClearanceCheckResult
        {
            MemberType = memberType,
            MemberId = memberId,
            MemberCode = memberCode,
            MemberName = memberName,
            Department = department,
            ProgramOrDesignation = programOrDesignation,
            CanClear = canClear,
            PendingBooksCount = activeIssues.Count,
            PendingFineAmount = pendingFines.Sum(fine => fine.RemainingAmount),
            LostOrDamagedCaseCount = lossCases.Count,
            BlockingItems = blockingItems,
            Message = canClear
                ? "No active issues, pending fines, or unresolved lost/damaged cases were found."
                : $"{blockingItems.Count} clearance blocking item(s) require resolution."
        };
    }

    private async Task<ClearanceActionResult> ApproveAsync(
        MemberType memberType,
        int memberId,
        string remarks,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(remarks))
        {
            return Failure("Clearance approval remarks are required.");
        }

        var currentUser = _authenticationService.CurrentUser;
        if (currentUser == null)
        {
            return Failure("An authenticated user is required to approve clearance.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var check = memberType == MemberType.Student
                ? await CheckStudentClearanceAsync(memberId, cancellationToken)
                : await CheckFacultyStaffClearanceAsync(memberId, cancellationToken);
            if (!check.CanClear)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new ClearanceActionResult
                {
                    Succeeded = false,
                    Message = "Clearance approval was blocked.",
                    ErrorMessage = check.Message,
                    CheckResult = check,
                    CompletedAt = DateTime.UtcNow
                };
            }

            var clearanceDate = DateTime.UtcNow;
            if (memberType == MemberType.Student)
            {
                var member = await _context.Students.FindAsync([memberId], cancellationToken) ??
                    throw new InvalidOperationException("Student was not found.");
                if (member.ClearanceStatus == ClearanceStatus.Cleared)
                {
                    return Failure("Student clearance is already approved.");
                }
                member.ClearanceStatus = ClearanceStatus.Cleared;
                member.ClearanceDate = clearanceDate;
                member.ClearanceRemarks = remarks.Trim();
                member.ClearedByUserId = currentUser.Id;
            }
            else
            {
                var member = await _context.FacultyStaff.FindAsync([memberId], cancellationToken) ??
                    throw new InvalidOperationException("Faculty or staff member was not found.");
                if (member.ClearanceStatus == ClearanceStatus.Cleared)
                {
                    return Failure("Faculty/staff clearance is already approved.");
                }
                member.ClearanceStatus = ClearanceStatus.Cleared;
                member.ClearanceDate = clearanceDate;
                member.ClearanceRemarks = remarks.Trim();
                member.ClearedByUserId = currentUser.Id;
            }

            AddActivityLog(
                "Clearance Approved",
                memberType,
                memberId,
                check.MemberCode,
                check.MemberName,
                remarks.Trim());
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ClearanceActionResult
            {
                Succeeded = true,
                Message = $"{check.MemberName} clearance was approved.",
                CheckResult = check,
                CompletedAt = clearanceDate
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Failure(ex.Message);
        }
    }

    private async Task<ClearanceActionResult> RevokeAsync(
        MemberType memberType,
        int memberId,
        string reason,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Failure("A clearance revoke reason is required.");
        }
        if (_authenticationService.CurrentUser == null)
        {
            return Failure("An authenticated user is required to revoke clearance.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            string memberCode;
            string memberName;
            if (memberType == MemberType.Student)
            {
                var member = await _context.Students.FindAsync([memberId], cancellationToken) ??
                    throw new InvalidOperationException("Student was not found.");
                memberCode = member.RegistrationNumber;
                memberName = member.Name;
                member.ClearanceStatus = ClearanceStatus.NotCleared;
                member.ClearanceDate = null;
                member.ClearanceRemarks = $"Revoked: {reason.Trim()}";
                member.ClearedByUserId = null;
            }
            else
            {
                var member = await _context.FacultyStaff.FindAsync([memberId], cancellationToken) ??
                    throw new InvalidOperationException("Faculty or staff member was not found.");
                memberCode = member.PersonnelNumber;
                memberName = member.Name;
                member.ClearanceStatus = ClearanceStatus.NotCleared;
                member.ClearanceDate = null;
                member.ClearanceRemarks = $"Revoked: {reason.Trim()}";
                member.ClearedByUserId = null;
            }

            AddActivityLog(
                "Clearance Revoked",
                memberType,
                memberId,
                memberCode,
                memberName,
                reason.Trim());
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ClearanceActionResult
            {
                Succeeded = true,
                Message = $"{memberName} clearance was revoked.",
                CompletedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Failure(ex.Message);
        }
    }

    private async Task<ClearanceCertificateData> BuildCertificateDataAsync(
        MemberType memberType,
        int memberId,
        CancellationToken cancellationToken)
    {
        var check = memberType == MemberType.Student
            ? await CheckStudentClearanceAsync(memberId, cancellationToken)
            : await CheckFacultyStaffClearanceAsync(memberId, cancellationToken);
        ClearanceStatus status;
        DateTime? clearanceDate;
        string? remarks;
        int? clearedByUserId;

        if (memberType == MemberType.Student)
        {
            var member = await _context.Students.AsNoTracking()
                .FirstAsync(item => item.Id == memberId, cancellationToken);
            status = member.ClearanceStatus;
            clearanceDate = member.ClearanceDate;
            remarks = member.ClearanceRemarks;
            clearedByUserId = member.ClearedByUserId;
        }
        else
        {
            var member = await _context.FacultyStaff.AsNoTracking()
                .FirstAsync(item => item.Id == memberId, cancellationToken);
            status = member.ClearanceStatus;
            clearanceDate = member.ClearanceDate;
            remarks = member.ClearanceRemarks;
            clearedByUserId = member.ClearedByUserId;
        }

        if (status != ClearanceStatus.Cleared || !clearanceDate.HasValue)
        {
            throw new InvalidOperationException("A clearance certificate can only be generated for an approved member.");
        }
        if (!check.CanClear)
        {
            throw new InvalidOperationException(
                "Certificate generation is blocked because new library dues are pending.");
        }

        var clearedBy = clearedByUserId.HasValue
            ? await _context.Users.AsNoTracking()
                .Where(user => user.Id == clearedByUserId.Value)
                .Select(user => user.FullName)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        var institution = await _context.SystemSettings.AsNoTracking()
            .Where(setting => setting.Key == "InstituteName")
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken) ?? "KICSIT Library";
        var issueQuery = _context.IssueRecords.AsNoTracking()
            .Where(issue => memberType == MemberType.Student
                ? issue.StudentId == memberId
                : issue.FacultyStaffId == memberId);
        var totalIssues = await issueQuery.CountAsync(cancellationToken);
        var returnedIssues = await issueQuery.CountAsync(
            issue => issue.ReceiveRecord != null,
            cancellationToken);
        var fineQuery = _context.Fines.AsNoTracking()
            .Where(fine => memberType == MemberType.Student
                ? fine.StudentId == memberId
                : fine.FacultyStaffId == memberId);
        var fineTotals = await fineQuery
            .Select(fine => new { fine.FineAmount, fine.PaidAmount })
            .ToListAsync(cancellationToken);
        var totalFine = fineTotals.Sum(fine => fine.FineAmount);
        var paidFine = fineTotals.Sum(fine => fine.PaidAmount);

        return new ClearanceCertificateData
        {
            CertificateNumber =
                $"CLR-{(memberType == MemberType.Student ? "STU" : "FAC")}-{memberId:D6}-{clearanceDate.Value:yyyyMMdd}",
            MemberType = memberType,
            MemberCode = check.MemberCode,
            MemberName = check.MemberName,
            Department = check.Department,
            ProgramOrDesignation = check.ProgramOrDesignation,
            ClearanceDate = clearanceDate.Value.ToLocalTime(),
            ClearedBy = clearedBy ?? "Unknown User",
            Remarks = remarks ?? string.Empty,
            BorrowingSummary = $"{totalIssues} total issue(s), {returnedIssues} returned.",
            FineSummary = $"Total fines: {totalFine:N2}; paid: {paidFine:N2}; pending: 0.00.",
            PendingBooksCount = 0,
            PendingFineAmount = 0,
            InstitutionName = institution
        };
    }

    private static ReportResult BuildCertificateReport(ClearanceCertificateData certificate)
    {
        ReportRow Row(string label, object? value)
        {
            return new ReportRow
            {
                Values = new Dictionary<string, object?>
                {
                    ["Label"] = label,
                    ["Value"] = value
                }
            };
        }

        return new ReportResult
        {
            ReportTitle = "LIBRARY CLEARANCE CERTIFICATE",
            InstitutionName = certificate.InstitutionName,
            GeneratedAt = DateTime.UtcNow,
            GeneratedBy = certificate.ClearedBy,
            Columns =
            [
                new ReportColumn { Key = "Label", Header = "Certificate Detail" },
                new ReportColumn { Key = "Value", Header = "Value" }
            ],
            Rows =
            [
                Row("Certificate Number", certificate.CertificateNumber),
                Row("Member Type", certificate.MemberType),
                Row("Name", certificate.MemberName),
                Row("Registration / Personnel Number", certificate.MemberCode),
                Row("Department", certificate.Department),
                Row("Program / Designation", certificate.ProgramOrDesignation),
                Row("Clearance Date", certificate.ClearanceDate),
                Row("Cleared By", certificate.ClearedBy),
                Row("Remarks", certificate.Remarks),
                Row("Borrowing Summary", certificate.BorrowingSummary),
                Row("Fine Summary", certificate.FineSummary),
                Row("Certification", "This member has no pending library books, fines, or unresolved loss/damage cases."),
                Row("Librarian Signature", "____________________________")
            ],
            TotalCount = 13,
            SummaryItems = new Dictionary<string, string>
            {
                ["Pending Books"] = "0",
                ["Pending Fine"] = "0.00",
                ["Generated Date"] = DateTime.Now.ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture)
            }
        };
    }

    private void AddActivityLog(
        string action,
        MemberType memberType,
        int memberId,
        string memberCode,
        string memberName,
        string remarks)
    {
        _context.ActivityLogs.Add(new ActivityLog
        {
            Action = action,
            Detail =
                $"MemberType={memberType};MemberId={memberId};MemberCode={Sanitize(remarks: memberCode)};" +
                $"MemberName={Sanitize(remarks: memberName)};Remarks={Sanitize(remarks)}",
            UserId = _authenticationService.CurrentUser?.Id,
            IpAddress = "127.0.0.1"
        });
    }

    private static Dictionary<string, string> ParseDetail(string detail)
    {
        return detail.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);
    }

    private static string Get(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) ? value : string.Empty;
    }

    private static string Sanitize(string remarks)
    {
        return remarks.Replace(";", ",", StringComparison.Ordinal)
            .Replace("=", "-", StringComparison.Ordinal);
    }

    private static ClearanceActionResult Failure(string error)
    {
        return new ClearanceActionResult
        {
            Succeeded = false,
            Message = "Clearance action failed.",
            ErrorMessage = error,
            CompletedAt = DateTime.UtcNow
        };
    }
}
