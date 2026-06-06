using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Helpers;
using KicsitLibrary.Data;
using KicsitLibrary.Reports.Models;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Reports.Providers;

public sealed class StudentClearanceReportDataProvider(KicsitLibraryDbContext context)
    : ReportDataProviderBase(context)
{
    private static readonly ReportDefinition ReportDefinition = new()
    {
        Key = ReportKeys.StudentClearance,
        Title = "Student Clearance Report",
        Description = "Student clearance state with outstanding books and fine balances.",
        Category = "Consumer Reports",
        Columns =
        [
            Column("RegistrationNumber", "Student Registration Number"),
            Column("AdmissionNumber", "Admission Number"),
            Column("StudentName", "Student Name"),
            Column("Department", "Department"),
            Column("Program", "Program"),
            Column("Batch", "Batch"),
            Column("Semester", "Semester"),
            Column("ClearanceStatus", "Clearance Status"),
            Column("PendingBooksCount", "Pending Books Count"),
            Column("PendingFineAmount", "Pending Fine Amount", "N2"),
            Column("ClearanceDate", "Clearance Date", "dd-MMM-yyyy"),
            Column("ClearanceRemarks", "Clearance Remarks")
        ],
        Filters =
        [
            Filter(ReportFilterKeys.SearchText, "Search Text", ReportFilterType.Text),
            Filter(ReportFilterKeys.Department, "Department", ReportFilterType.Text),
            Filter(ReportFilterKeys.Program, "Program", ReportFilterType.Text),
            Filter(ReportFilterKeys.Batch, "Batch", ReportFilterType.Text),
            Filter(ReportFilterKeys.ClearanceStatus, "Clearance Status", ReportFilterType.Enum, Enum.GetNames<ClearanceStatus>()),
            Filter(ReportFilterKeys.PendingOnly, "Pending Only", ReportFilterType.Boolean)
        ]
    };

    public override ReportDefinition Definition => ReportDefinition;

    public override async Task<ReportResult> GenerateAsync(
        IReadOnlyCollection<ReportFilter> filters,
        string generatedBy,
        CancellationToken cancellationToken = default)
    {
        var students = await Context.Students
            .AsNoTracking()
            .Include(student => student.IssueRecords)
                .ThenInclude(issue => issue.ReceiveRecord)
            .Include(student => student.Fines)
            .OrderBy(student => student.RegistrationNumber)
            .ToListAsync(cancellationToken);

        var search = FilterReader.Text(filters, ReportFilterKeys.SearchText);
        var department = FilterReader.Text(filters, ReportFilterKeys.Department);
        var program = FilterReader.Text(filters, ReportFilterKeys.Program);
        var batch = FilterReader.Text(filters, ReportFilterKeys.Batch);
        var status = FilterReader.Text(filters, ReportFilterKeys.ClearanceStatus);
        var pendingOnly = FilterReader.Boolean(filters, ReportFilterKeys.PendingOnly);

        var rows = students.Select(student =>
        {
            var pendingBooks = student.IssueRecords.Count(issue => issue.ReceiveRecord == null);
            var pendingFine = student.Fines
                .Where(fine => fine.PaymentStatus is FineStatus.Unpaid or FineStatus.Partial)
                .Sum(fine => Math.Max(0, fine.RemainingAmount));
            return new { Student = student, PendingBooks = pendingBooks, PendingFine = pendingFine };
        })
        .Where(item =>
            Matches(search, item.Student.RegistrationNumber, item.Student.AdmissionNumber, item.Student.Name) &&
            MatchesExact(department, item.Student.Department) &&
            MatchesExact(program, item.Student.Program) &&
            MatchesExact(batch, item.Student.Batch) &&
            MatchesExact(status, item.Student.ClearanceStatus.ToString()) &&
            (!pendingOnly || item.PendingBooks > 0 || item.PendingFine > 0))
        .Select(item => Row(
            ("RegistrationNumber", item.Student.RegistrationNumber),
            ("AdmissionNumber", item.Student.AdmissionNumber),
            ("StudentName", item.Student.Name),
            ("Department", item.Student.Department),
            ("Program", item.Student.Program),
            ("Batch", item.Student.Batch),
            ("Semester", item.Student.Semester),
            ("ClearanceStatus", item.Student.ClearanceStatus.ToString()),
            ("PendingBooksCount", item.PendingBooks),
            ("PendingFineAmount", item.PendingFine),
            ("ClearanceDate", item.Student.ClearanceDate?.ToLocalTime()),
            ("ClearanceRemarks", item.Student.ClearanceRemarks)))
        .ToList();

        var inconsistent = rows.Count(row =>
            Equals(row["ClearanceStatus"], ClearanceStatus.Cleared.ToString()) &&
            (Convert.ToInt32(row["PendingBooksCount"]) > 0 ||
             Convert.ToDecimal(row["PendingFineAmount"]) > 0));

        return await CreateResultAsync(filters, generatedBy, rows,
            new Dictionary<string, string>
            {
                ["Students"] = rows.Count.ToString(),
                ["Pending Clearance"] = rows.Count(row =>
                    !Equals(row["ClearanceStatus"], ClearanceStatus.Cleared.ToString())).ToString(),
                ["Pending Books"] = rows.Sum(row => Convert.ToInt32(row["PendingBooksCount"])).ToString(),
                ["Pending Fine"] = rows.Sum(row => Convert.ToDecimal(row["PendingFineAmount"])).ToString("N2"),
                ["Cleared With Outstanding Items"] = inconsistent.ToString()
            }, cancellationToken);
    }
}

public sealed class StudentBorrowingHistoryReportDataProvider(KicsitLibraryDbContext context)
    : ReportDataProviderBase(context)
{
    private static readonly ReportDefinition ReportDefinition = new()
    {
        Key = ReportKeys.StudentBorrowingHistory,
        Title = "Student Borrowing History Report",
        Description = "Complete active and returned borrowing history for students.",
        Category = "Consumer Reports",
        Columns =
        [
            Column("RegistrationNumber", "Student Registration Number"),
            Column("StudentName", "Student Name"),
            Column("AccessionNumber", "Accession Number"),
            Column("BookTitle", "Book Title"),
            Column("IssueDate", "Issue Date", "dd-MMM-yyyy"),
            Column("ExpectedReturnDate", "Expected Return Date", "dd-MMM-yyyy"),
            Column("ReceiveDate", "Receive Date", "dd-MMM-yyyy"),
            Column("DaysOverdue", "Days Overdue"),
            Column("FineAmount", "Fine Amount", "N2"),
            Column("Status", "Status")
        ],
        Filters =
        [
            Filter(ReportFilterKeys.SearchText, "Search Text", ReportFilterType.Text),
            Filter(ReportFilterKeys.RegistrationNumber, "Registration Number", ReportFilterType.Text),
            Filter(ReportFilterKeys.Department, "Department", ReportFilterType.Text),
            Filter(ReportFilterKeys.DateRange, "Issue Date", ReportFilterType.DateRange),
            Filter(ReportFilterKeys.OverdueOnly, "Overdue Only", ReportFilterType.Boolean)
        ]
    };

    public override ReportDefinition Definition => ReportDefinition;

    public override async Task<ReportResult> GenerateAsync(
        IReadOnlyCollection<ReportFilter> filters,
        string generatedBy,
        CancellationToken cancellationToken = default)
    {
        var issues = await Context.IssueRecords.AsNoTracking()
            .Include(issue => issue.Student)
            .Include(issue => issue.BookCopy).ThenInclude(copy => copy.BookMaster)
            .Include(issue => issue.ReceiveRecord)
            .Include(issue => issue.Fine)
            .Where(issue => issue.MemberType == MemberType.Student && issue.StudentId != null)
            .OrderByDescending(issue => issue.IssueDate)
            .ToListAsync(cancellationToken);

        var search = FilterReader.Text(filters, ReportFilterKeys.SearchText);
        var registration = FilterReader.Text(filters, ReportFilterKeys.RegistrationNumber);
        var department = FilterReader.Text(filters, ReportFilterKeys.Department);
        var fromDate = FilterReader.StartDate(filters, ReportFilterKeys.DateRange);
        var toDate = FilterReader.EndDate(filters, ReportFilterKeys.DateRange);
        var overdueOnly = FilterReader.Boolean(filters, ReportFilterKeys.OverdueOnly);
        var today = DateTime.Now.Date;

        var rows = issues.Select(issue =>
        {
            var endDate = issue.ReceiveRecord == null
                ? today
                : ToLocalDate(issue.ReceiveRecord.ReceiveDate);
            var overdueDays = OverdueCalculator.CalculateOverdueDays(
                ToLocalDate(issue.ExpectedReturnDate), endDate);
            return new { Issue = issue, OverdueDays = overdueDays };
        })
        .Where(item =>
            Matches(search, item.Issue.Student?.Name, item.Issue.AccessionNumber, item.Issue.BookCopy.BookMaster.Title) &&
            Matches(registration, item.Issue.Student?.RegistrationNumber) &&
            MatchesExact(department, item.Issue.Student?.Department) &&
            (!fromDate.HasValue || ToLocalDate(item.Issue.IssueDate) >= fromDate.Value.Date) &&
            (!toDate.HasValue || ToLocalDate(item.Issue.IssueDate) <= toDate.Value.Date) &&
            (!overdueOnly || item.OverdueDays > 0))
        .Select(item => Row(
            ("RegistrationNumber", item.Issue.Student?.RegistrationNumber),
            ("StudentName", item.Issue.Student?.Name),
            ("AccessionNumber", item.Issue.AccessionNumber),
            ("BookTitle", item.Issue.BookCopy.BookMaster.Title),
            ("IssueDate", ToLocalDate(item.Issue.IssueDate)),
            ("ExpectedReturnDate", ToLocalDate(item.Issue.ExpectedReturnDate)),
            ("ReceiveDate", item.Issue.ReceiveRecord == null ? null : ToLocalDate(item.Issue.ReceiveRecord.ReceiveDate)),
            ("DaysOverdue", item.OverdueDays),
            ("FineAmount", item.Issue.Fine?.FineAmount ?? item.Issue.ReceiveRecord?.FineAmount ?? 0),
            ("Status", item.Issue.ReceiveRecord == null ? "Active" : "Returned")))
        .ToList();

        return await CreateResultAsync(filters, generatedBy, rows,
            BorrowingSummary(rows), cancellationToken);
    }

    private static Dictionary<string, string> BorrowingSummary(IEnumerable<ReportRow> rows)
    {
        var materialized = rows.ToList();
        return new Dictionary<string, string>
        {
            ["Borrowing Records"] = materialized.Count.ToString(),
            ["Active"] = materialized.Count(row => Equals(row["Status"], "Active")).ToString(),
            ["Returned"] = materialized.Count(row => Equals(row["Status"], "Returned")).ToString(),
            ["Overdue Records"] = materialized.Count(row => Convert.ToInt32(row["DaysOverdue"]) > 0).ToString()
        };
    }
}

public sealed class FacultyBorrowingHistoryReportDataProvider(KicsitLibraryDbContext context)
    : ReportDataProviderBase(context)
{
    private static readonly ReportDefinition ReportDefinition = new()
    {
        Key = ReportKeys.FacultyBorrowingHistory,
        Title = "Faculty Staff Borrowing History Report",
        Description = "Complete active and returned borrowing history for faculty and staff.",
        Category = "Consumer Reports",
        Columns =
        [
            Column("PersonnelNumber", "Personnel Number"),
            Column("FacultyStaffName", "Faculty Staff Name"),
            Column("FacultyType", "Faculty Type"),
            Column("Department", "Department"),
            Column("AccessionNumber", "Accession Number"),
            Column("BookTitle", "Book Title"),
            Column("IssueDate", "Issue Date", "dd-MMM-yyyy"),
            Column("ExpectedReturnDate", "Expected Return Date", "dd-MMM-yyyy"),
            Column("ReceiveDate", "Receive Date", "dd-MMM-yyyy"),
            Column("DaysOverdue", "Days Overdue"),
            Column("FineAmount", "Fine Amount", "N2"),
            Column("Status", "Status")
        ],
        Filters =
        [
            Filter(ReportFilterKeys.SearchText, "Search Text", ReportFilterType.Text),
            Filter(ReportFilterKeys.PersonnelNumber, "Personnel Number", ReportFilterType.Text),
            Filter(ReportFilterKeys.FacultyType, "Faculty Type", ReportFilterType.Enum, Enum.GetNames<FacultyType>()),
            Filter(ReportFilterKeys.Department, "Department", ReportFilterType.Text),
            Filter(ReportFilterKeys.DateRange, "Issue Date", ReportFilterType.DateRange),
            Filter(ReportFilterKeys.OverdueOnly, "Overdue Only", ReportFilterType.Boolean)
        ]
    };

    public override ReportDefinition Definition => ReportDefinition;

    public override async Task<ReportResult> GenerateAsync(
        IReadOnlyCollection<ReportFilter> filters,
        string generatedBy,
        CancellationToken cancellationToken = default)
    {
        var issues = await Context.IssueRecords.AsNoTracking()
            .Include(issue => issue.FacultyStaff)
            .Include(issue => issue.BookCopy).ThenInclude(copy => copy.BookMaster)
            .Include(issue => issue.ReceiveRecord)
            .Include(issue => issue.Fine)
            .Where(issue => issue.MemberType == MemberType.FacultyStaff && issue.FacultyStaffId != null)
            .OrderByDescending(issue => issue.IssueDate)
            .ToListAsync(cancellationToken);

        var search = FilterReader.Text(filters, ReportFilterKeys.SearchText);
        var personnel = FilterReader.Text(filters, ReportFilterKeys.PersonnelNumber);
        var facultyType = FilterReader.Text(filters, ReportFilterKeys.FacultyType);
        var department = FilterReader.Text(filters, ReportFilterKeys.Department);
        var fromDate = FilterReader.StartDate(filters, ReportFilterKeys.DateRange);
        var toDate = FilterReader.EndDate(filters, ReportFilterKeys.DateRange);
        var overdueOnly = FilterReader.Boolean(filters, ReportFilterKeys.OverdueOnly);
        var today = DateTime.Now.Date;

        var rows = issues.Select(issue =>
        {
            var endDate = issue.ReceiveRecord == null ? today : ToLocalDate(issue.ReceiveRecord.ReceiveDate);
            var days = OverdueCalculator.CalculateOverdueDays(ToLocalDate(issue.ExpectedReturnDate), endDate);
            return new { Issue = issue, Days = days };
        })
        .Where(item =>
            Matches(search, item.Issue.FacultyStaff?.Name, item.Issue.AccessionNumber, item.Issue.BookCopy.BookMaster.Title) &&
            Matches(personnel, item.Issue.FacultyStaff?.PersonnelNumber) &&
            MatchesExact(facultyType, item.Issue.FacultyStaff?.FacultyType.ToString()) &&
            MatchesExact(department, item.Issue.FacultyStaff?.Department) &&
            (!fromDate.HasValue || ToLocalDate(item.Issue.IssueDate) >= fromDate.Value.Date) &&
            (!toDate.HasValue || ToLocalDate(item.Issue.IssueDate) <= toDate.Value.Date) &&
            (!overdueOnly || item.Days > 0))
        .Select(item => Row(
            ("PersonnelNumber", item.Issue.FacultyStaff?.PersonnelNumber),
            ("FacultyStaffName", item.Issue.FacultyStaff?.Name),
            ("FacultyType", item.Issue.FacultyStaff?.FacultyType.ToString()),
            ("Department", item.Issue.FacultyStaff?.Department),
            ("AccessionNumber", item.Issue.AccessionNumber),
            ("BookTitle", item.Issue.BookCopy.BookMaster.Title),
            ("IssueDate", ToLocalDate(item.Issue.IssueDate)),
            ("ExpectedReturnDate", ToLocalDate(item.Issue.ExpectedReturnDate)),
            ("ReceiveDate", item.Issue.ReceiveRecord == null ? null : ToLocalDate(item.Issue.ReceiveRecord.ReceiveDate)),
            ("DaysOverdue", item.Days),
            ("FineAmount", item.Issue.Fine?.FineAmount ?? item.Issue.ReceiveRecord?.FineAmount ?? 0),
            ("Status", item.Issue.ReceiveRecord == null ? "Active" : "Returned")))
        .ToList();

        return await CreateResultAsync(filters, generatedBy, rows,
            new Dictionary<string, string>
            {
                ["Borrowing Records"] = rows.Count.ToString(),
                ["Active"] = rows.Count(row => Equals(row["Status"], "Active")).ToString(),
                ["Returned"] = rows.Count(row => Equals(row["Status"], "Returned")).ToString(),
                ["Overdue Records"] = rows.Count(row => Convert.ToInt32(row["DaysOverdue"]) > 0).ToString()
            }, cancellationToken);
    }
}
