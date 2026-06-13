using KicsitLibrary.Core.Enums;
using KicsitLibrary.Data;
using KicsitLibrary.Reports.Models;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Reports.Providers;

public sealed class ReservationReportDataProvider(KicsitLibraryDbContext context)
    : ReportDataProviderBase(context)
{
    private static readonly ReportDefinition ReportDefinition = new()
    {
        Key = ReportKeys.Reservations,
        Title = "Reservation Report",
        Description = "Existing reservation records and expiry state.",
        Category = "Circulation Reports",
        Columns =
        [
            Column("ReservationId", "Reservation Id"),
            Column("BookTitle", "Book Title"),
            Column("MemberType", "Member Type"),
            Column("MemberName", "Member Name"),
            Column("MemberCode", "Member Code"),
            Column("QueuePosition", "Queue Position"),
            Column("ReservationDate", "Reservation Date", "dd-MMM-yyyy"),
            Column("ExpiryDate", "Expiry Date", "dd-MMM-yyyy"),
            Column("Status", "Status"),
            Column("Remarks", "Remarks")
        ],
        Filters =
        [
            Filter(ReportFilterKeys.SearchText, "Search Text", ReportFilterType.Text),
            Filter(ReportFilterKeys.MemberType, "Member Type", ReportFilterType.Enum, Enum.GetNames<MemberType>()),
            Filter(ReportFilterKeys.Status, "Status", ReportFilterType.Enum, Enum.GetNames<ReservationStatus>()),
            Filter(ReportFilterKeys.DateRange, "Reservation Date", ReportFilterType.DateRange),
            Filter(ReportFilterKeys.ExpiredOnly, "Expired Only", ReportFilterType.Boolean)
        ]
    };

    public override ReportDefinition Definition => ReportDefinition;

    public override async Task<ReportResult> GenerateAsync(
        IReadOnlyCollection<ReportFilter> filters,
        string generatedBy,
        CancellationToken cancellationToken = default)
    {
        var reservations = await Context.Reservations.AsNoTracking()
            .Include(item => item.BookMaster)
            .Include(item => item.Student)
            .Include(item => item.FacultyStaff)
            .OrderByDescending(item => item.ReservationDate)
            .ToListAsync(cancellationToken);
        var search = FilterReader.Text(filters, ReportFilterKeys.SearchText);
        var memberType = FilterReader.Text(filters, ReportFilterKeys.MemberType);
        var status = FilterReader.Text(filters, ReportFilterKeys.Status);
        var fromDate = FilterReader.StartDate(filters, ReportFilterKeys.DateRange);
        var toDate = FilterReader.EndDate(filters, ReportFilterKeys.DateRange);
        var expiredOnly = FilterReader.Boolean(filters, ReportFilterKeys.ExpiredOnly);
        var today = DateTime.Now.Date;
        var queuePositions = reservations
            .Where(item => item.Status is ReservationStatus.Pending or ReservationStatus.Available)
            .GroupBy(item => item.BookMasterId)
            .SelectMany(group => group
                .OrderBy(item => item.ReservationDate)
                .ThenBy(item => item.Id)
                .Select((item, index) => new { item.Id, Position = index + 1 }))
            .ToDictionary(item => item.Id, item => item.Position);

        var rows = reservations.Where(item =>
        {
            var memberName = item.MemberType == MemberType.Student
                ? item.Student?.Name : item.FacultyStaff?.Name;
            var memberCode = item.MemberType == MemberType.Student
                ? item.Student?.RegistrationNumber : item.FacultyStaff?.PersonnelNumber;
            return TextMatches(search, item.ReservationNumber, item.BookMaster.Title, memberName, memberCode, item.Remarks) &&
                ExactMatches(memberType, item.MemberType.ToString()) &&
                ExactMatches(status, item.Status.ToString()) &&
                (!fromDate.HasValue || AsLocalDate(item.ReservationDate) >= fromDate.Value.Date) &&
                (!toDate.HasValue || AsLocalDate(item.ReservationDate) <= toDate.Value.Date) &&
                (!expiredOnly || item.Status == ReservationStatus.Expired ||
                    (AsLocalDate(item.ExpiryDate) < today &&
                     item.Status is ReservationStatus.Pending or ReservationStatus.Available));
        })
        .Select(item => Row(
            ("ReservationId", item.Id),
            ("BookTitle", item.BookMaster.Title),
            ("MemberType", item.MemberType.ToString()),
            ("MemberName", item.MemberType == MemberType.Student ? item.Student?.Name : item.FacultyStaff?.Name),
            ("MemberCode", item.MemberType == MemberType.Student ? item.Student?.RegistrationNumber : item.FacultyStaff?.PersonnelNumber),
            ("QueuePosition", queuePositions.GetValueOrDefault(item.Id)),
            ("ReservationDate", AsLocalDate(item.ReservationDate)),
            ("ExpiryDate", AsLocalDate(item.ExpiryDate)),
            ("Status", item.Status.ToString()),
            ("Remarks", item.Remarks)))
        .ToList();

        return await CreateResultAsync(filters, generatedBy, rows,
            new Dictionary<string, string>
            {
                ["Reservations"] = rows.Count.ToString(),
                ["Pending"] = rows.Count(row => Equals(row["Status"], ReservationStatus.Pending.ToString())).ToString(),
                ["Available"] = rows.Count(row => Equals(row["Status"], ReservationStatus.Available.ToString())).ToString(),
                ["Issued"] = rows.Count(row => Equals(row["Status"], ReservationStatus.Issued.ToString())).ToString(),
                ["Cancelled"] = rows.Count(row => Equals(row["Status"], ReservationStatus.Cancelled.ToString())).ToString(),
                ["Expired"] = rows.Count(row => Equals(row["Status"], ReservationStatus.Expired.ToString())).ToString()
            }, cancellationToken);
    }
}

public sealed class LostDamagedBooksReportDataProvider(KicsitLibraryDbContext context)
    : ReportDataProviderBase(context)
{
    private static readonly BookStatus[] IncludedStatuses =
        [BookStatus.Lost, BookStatus.Damaged, BookStatus.Missing, BookStatus.UnderRepair];

    private static readonly ReportDefinition ReportDefinition = new()
    {
        Key = ReportKeys.LostDamagedBooks,
        Title = "Lost And Damaged Books Report",
        Description = "Copies marked lost, damaged, missing, or under repair.",
        Category = "Catalog Reports",
        Columns =
        [
            Column("AccessionNumber", "Accession Number"),
            Column("BookTitle", "Book Title"),
            Column("Author", "Author"),
            Column("Category", "Category"),
            Column("Department", "Department"),
            Column("Status", "Status"),
            Column("LastIssuedTo", "Last Issued To"),
            Column("LastIssueDate", "Last Issue Date", "dd-MMM-yyyy"),
            Column("LastReceiveDate", "Last Receive Date", "dd-MMM-yyyy"),
            Column("Location", "Location"),
            Column("Remarks", "Remarks")
        ],
        Filters =
        [
            Filter(ReportFilterKeys.SearchText, "Search Text", ReportFilterType.Text),
            Filter(ReportFilterKeys.Status, "Status", ReportFilterType.Enum,
                IncludedStatuses.Select(status => status.ToString()).ToArray()),
            Filter(ReportFilterKeys.Category, "Category", ReportFilterType.Text),
            Filter(ReportFilterKeys.Department, "Department", ReportFilterType.Text),
            Filter(ReportFilterKeys.DateRange, "Last Issue Date", ReportFilterType.DateRange)
        ]
    };

    public override ReportDefinition Definition => ReportDefinition;

    public override async Task<ReportResult> GenerateAsync(
        IReadOnlyCollection<ReportFilter> filters,
        string generatedBy,
        CancellationToken cancellationToken = default)
    {
        var copies = await Context.BookCopies.AsNoTracking()
            .Include(copy => copy.BookMaster).ThenInclude(book => book.BookAuthors).ThenInclude(item => item.Author)
            .Include(copy => copy.BookMaster.Category)
            .Include(copy => copy.BookMaster.DepartmentCategory)
            .Include(copy => copy.IssueRecords).ThenInclude(issue => issue.Student)
            .Include(copy => copy.IssueRecords).ThenInclude(issue => issue.FacultyStaff)
            .Where(copy => IncludedStatuses.Contains(copy.AvailabilityStatus))
            .OrderBy(copy => copy.AccessionNumber)
            .ToListAsync(cancellationToken);
        var search = FilterReader.Text(filters, ReportFilterKeys.SearchText);
        var status = FilterReader.Text(filters, ReportFilterKeys.Status);
        var category = FilterReader.Text(filters, ReportFilterKeys.Category);
        var department = FilterReader.Text(filters, ReportFilterKeys.Department);
        var fromDate = FilterReader.StartDate(filters, ReportFilterKeys.DateRange);
        var toDate = FilterReader.EndDate(filters, ReportFilterKeys.DateRange);

        var rows = copies.Select(copy =>
        {
            var lastIssue = copy.IssueRecords.OrderByDescending(issue => issue.IssueDate).FirstOrDefault();
            var lastMember = lastIssue?.MemberType == MemberType.Student
                ? lastIssue.Student?.Name : lastIssue?.FacultyStaff?.Name;
            return new { Copy = copy, LastIssue = lastIssue, LastMember = lastMember };
        })
        .Where(item =>
            TextMatches(search, item.Copy.AccessionNumber, item.Copy.BookMaster.Title, item.LastMember) &&
            ExactMatches(status, item.Copy.AvailabilityStatus.ToString()) &&
            ExactMatches(category, item.Copy.BookMaster.Category?.Name) &&
            ExactMatches(department, item.Copy.BookMaster.DepartmentCategory?.Name) &&
            (!fromDate.HasValue || item.Copy.LastIssuedDate?.ToLocalTime().Date >= fromDate.Value.Date) &&
            (!toDate.HasValue || item.Copy.LastIssuedDate?.ToLocalTime().Date <= toDate.Value.Date))
        .Select(item => Row(
            ("AccessionNumber", item.Copy.AccessionNumber),
            ("BookTitle", item.Copy.BookMaster.Title),
            ("Author", string.Join(", ", item.Copy.BookMaster.BookAuthors.Select(bookAuthor => bookAuthor.Author?.Name).Where(n => n != null))),
            ("Category", item.Copy.BookMaster.Category?.Name ?? string.Empty),
            ("Department", item.Copy.BookMaster.DepartmentCategory?.Name ?? string.Empty),
            ("Status", item.Copy.AvailabilityStatus.ToString()),
            ("LastIssuedTo", item.LastMember),
            ("LastIssueDate", item.Copy.LastIssuedDate?.ToLocalTime()),
            ("LastReceiveDate", item.Copy.LastReceivedDate?.ToLocalTime()),
            ("Location", item.Copy.Location),
            ("Remarks", item.Copy.PhysicalCondition)))
        .ToList();

        return await CreateResultAsync(filters, generatedBy, rows,
            IncludedStatuses.ToDictionary(
                statusValue => statusValue.ToString(),
                statusValue => rows.Count(row => Equals(row["Status"], statusValue.ToString())).ToString()),
            cancellationToken);
    }
}

public sealed class DeletedBooksArchiveReportDataProvider(KicsitLibraryDbContext context)
    : ReportDataProviderBase(context)
{
    private static readonly ReportDefinition ReportDefinition = new()
    {
        Key = ReportKeys.DeletedBooksArchive,
        Title = "Deleted Books Archive Report",
        Description = "Archived snapshots for deleted book and copy records.",
        Category = "Compliance Reports",
        Columns =
        [
            Column("ArchiveId", "Archive Id"),
            Column("EntityName", "Entity Name"),
            Column("RecordId", "Record Id"),
            Column("DeletedReason", "Deleted Reason"),
            Column("DeletedBy", "Deleted By"),
            Column("DeletedAt", "Deleted At", "dd-MMM-yyyy HH:mm"),
            Column("SnapshotData", "Snapshot Data")
        ],
        Filters =
        [
            Filter(ReportFilterKeys.SearchText, "Search Text", ReportFilterType.Text),
            Filter(ReportFilterKeys.EntityName, "Entity Name", ReportFilterType.Enum, "BookMaster", "BookCopy"),
            Filter(ReportFilterKeys.DeletedBy, "Deleted By", ReportFilterType.Text),
            Filter(ReportFilterKeys.DateRange, "Deleted Date", ReportFilterType.DateRange)
        ]
    };

    public override ReportDefinition Definition => ReportDefinition;

    public override async Task<ReportResult> GenerateAsync(
        IReadOnlyCollection<ReportFilter> filters,
        string generatedBy,
        CancellationToken cancellationToken = default)
    {
        var archives = await Context.DeletedRecordArchives.AsNoTracking()
            .Include(item => item.DeletedByUser)
            .Where(item => item.TableName == "BookMaster" || item.TableName == "BookCopy")
            .OrderByDescending(item => item.DeletedAt ?? item.CreatedAt)
            .ToListAsync(cancellationToken);
        var search = FilterReader.Text(filters, ReportFilterKeys.SearchText);
        var entity = FilterReader.Text(filters, ReportFilterKeys.EntityName);
        var deletedBy = FilterReader.Text(filters, ReportFilterKeys.DeletedBy);
        var fromDate = FilterReader.StartDate(filters, ReportFilterKeys.DateRange);
        var toDate = FilterReader.EndDate(filters, ReportFilterKeys.DateRange);

        var rows = archives.Where(item =>
        {
            var deletedAt = (item.DeletedAt ?? item.CreatedAt).ToLocalTime().Date;
            return TextMatches(search, item.TableName, item.DeletedReason, item.SerializedData) &&
                ExactMatches(entity, item.TableName) &&
                TextMatches(deletedBy, item.DeletedByUser.FullName, item.DeletedByUser.Username) &&
                (!fromDate.HasValue || deletedAt >= fromDate.Value.Date) &&
                (!toDate.HasValue || deletedAt <= toDate.Value.Date);
        })
        .Select(item => Row(
            ("ArchiveId", item.Id),
            ("EntityName", item.TableName),
            ("RecordId", item.RecordId),
            ("DeletedReason", item.DeletedReason),
            ("DeletedBy", item.DeletedByUser.FullName),
            ("DeletedAt", (item.DeletedAt ?? item.CreatedAt).ToLocalTime()),
            ("SnapshotData", item.SerializedData)))
        .ToList();

        return await CreateResultAsync(filters, generatedBy, rows,
            new Dictionary<string, string>
            {
                ["Archived Records"] = rows.Count.ToString(),
                ["Books"] = rows.Count(row => Equals(row["EntityName"], "BookMaster")).ToString(),
                ["Copies"] = rows.Count(row => Equals(row["EntityName"], "BookCopy")).ToString()
            }, cancellationToken);
    }
}
