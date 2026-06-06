using ClosedXML.Excel;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Reports.Contracts;
using KicsitLibrary.Reports.Export;
using KicsitLibrary.Reports.Models;
using KicsitLibrary.Reports.Providers;
using KicsitLibrary.Reports.Services;
using KicsitLibrary.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Tests;

public class Priority5BReportTests
{
    [Fact]
    public async Task StudentClearanceReport_ReturnsPendingCountsAndFineTotals()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var issue = await database.AddIssueAsync(data, DateTime.UtcNow.AddDays(-2));
        database.Context.Fines.Add(new Fine
        {
            FineRecordNumber = "CLR-FINE",
            MemberType = MemberType.Student,
            StudentId = data.Student.Id,
            IssueRecordId = issue.Id,
            AccessionNumber = data.Copy.AccessionNumber,
            FineAmount = 40,
            RemainingAmount = 25,
            PaymentStatus = FineStatus.Partial
        });
        await database.Context.SaveChangesAsync();

        var result = await new StudentClearanceReportDataProvider(database.Context)
            .GenerateAsync([], "Tester");

        var row = Assert.Single(result.Rows);
        Assert.Equal(1, row["PendingBooksCount"]);
        Assert.Equal(25m, row["PendingFineAmount"]);
    }

    [Fact]
    public async Task StudentBorrowingHistoryReport_ReturnsIssueHistory()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await database.AddIssueAsync(data, DateTime.UtcNow.AddDays(4));

        var result = await new StudentBorrowingHistoryReportDataProvider(database.Context)
            .GenerateAsync([], "Tester");

        Assert.Equal("Active", Assert.Single(result.Rows)["Status"]);
    }

    [Fact]
    public async Task FacultyBorrowingHistoryReport_ReturnsIssueHistory()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var faculty = new FacultyStaff
        {
            PersonnelNumber = "FAC-001",
            Name = "Test Faculty",
            FacultyType = FacultyType.PermanentFaculty,
            Department = "CS",
            Designation = "Lecturer",
            Email = "faculty@test.invalid",
            Phone = "000",
            Address = "Test"
        };
        database.Context.FacultyStaff.Add(faculty);
        await database.Context.SaveChangesAsync();
        database.Context.IssueRecords.Add(new IssueRecord
        {
            AccessionNumber = data.Copy.AccessionNumber,
            BookCopyId = data.Copy.Id,
            MemberType = MemberType.FacultyStaff,
            FacultyStaffId = faculty.Id,
            IssueDate = DateTime.UtcNow.AddDays(-2),
            ExpectedReturnDate = DateTime.UtcNow.AddDays(12),
            FinePerDay = 10,
            IssuedByUserId = data.User.Id
        });
        await database.Context.SaveChangesAsync();

        var result = await new FacultyBorrowingHistoryReportDataProvider(database.Context)
            .GenerateAsync([], "Tester");

        Assert.Equal("FAC-001", Assert.Single(result.Rows)["PersonnelNumber"]);
    }

    [Fact]
    public async Task ReservationReport_ReturnsReservationRows()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        database.Context.Reservations.Add(new Reservation
        {
            ReservationNumber = "RES-001",
            MemberType = MemberType.Student,
            StudentId = data.Student.Id,
            BookMasterId = data.Book.Id,
            ReservationDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddDays(2),
            Status = ReservationStatus.Pending
        });
        await database.Context.SaveChangesAsync();

        var result = await new ReservationReportDataProvider(database.Context)
            .GenerateAsync([], "Tester");

        Assert.Equal(data.Book.Title, Assert.Single(result.Rows)["BookTitle"]);
    }

    [Fact]
    public async Task LostDamagedBooksReport_FiltersDamagedRows()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        data.Copy.AvailabilityStatus = BookStatus.Damaged;
        data.Copy.PhysicalCondition = "Cover damaged";
        await database.Context.SaveChangesAsync();

        var result = await new LostDamagedBooksReportDataProvider(database.Context)
            .GenerateAsync([Filter("Status", "Damaged")], "Tester");

        Assert.Equal("Damaged", Assert.Single(result.Rows)["Status"]);
    }

    [Fact]
    public async Task DeletedBooksArchiveReport_ReturnsArchiveRows()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        database.Context.DeletedRecordArchives.Add(new DeletedRecordArchive
        {
            TableName = "BookCopy",
            RecordId = data.Copy.Id,
            SerializedData = "{\"AccessionNumber\":\"ACC-001\"}",
            DeletedReason = "Duplicate",
            DeletedByUserId = data.User.Id,
            DeletedAt = DateTime.UtcNow
        });
        await database.Context.SaveChangesAsync();

        var result = await new DeletedBooksArchiveReportDataProvider(database.Context)
            .GenerateAsync([], "Tester");

        Assert.Equal("BookCopy", Assert.Single(result.Rows)["EntityName"]);
    }

    [Fact]
    public async Task VisitDetailReport_ReturnsVisitRows()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        database.Context.VisitRecords.Add(new VisitRecord
        {
            VisitNumber = "VIS-001",
            OrganizationName = "University",
            VisitDate = DateTime.UtcNow,
            Department = "Library",
            Purpose = "Inspection",
            NextFollowUpDate = DateTime.UtcNow.AddDays(7),
            CreatedByUserId = data.User.Id
        });
        await database.Context.SaveChangesAsync();

        var result = await new VisitDetailReportDataProvider(database.Context)
            .GenerateAsync([], "Tester");

        Assert.Equal("Pending Follow Up", Assert.Single(result.Rows)["Status"]);
    }

    [Fact]
    public async Task AuditReport_ReturnsAuditRows()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        database.Context.AuditRecords.Add(new AuditRecord
        {
            AuditNumber = "AUD-001",
            AuditDate = DateTime.UtcNow,
            AuditType = "Internal",
            FinancialYear = "2025-26",
            ActionRequired = "Reconcile stock",
            ResponsiblePerson = "Librarian",
            CreatedByUserId = data.User.Id,
            Status = AuditStatus.Submitted
        });
        await database.Context.SaveChangesAsync();

        var result = await new AuditReportDataProvider(database.Context)
            .GenerateAsync([], "Tester");

        Assert.Equal("2025-26", Assert.Single(result.Rows)["FinancialYear"]);
    }

    [Fact]
    public async Task InventoryReport_ReturnsInventoryRows()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        database.Context.InventoryItems.Add(new InventoryItem
        {
            ItemName = "Reading Chair",
            ItemType = InventoryItemType.Chair,
            Quantity = 10,
            AvailableQuantity = 8,
            DamagedQuantity = 2,
            Location = "Reading Hall",
            Condition = "Good",
            PurchasePrice = 1000
        });
        await database.Context.SaveChangesAsync();

        var result = await new InventoryReportDataProvider(database.Context)
            .GenerateAsync([], "Tester");

        Assert.Equal(10, Assert.Single(result.Rows)["Quantity"]);
    }

    [Fact]
    public async Task NewArrivalsReport_ReturnsArrivalRows()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        database.Context.NewArrivals.Add(new NewArrival
        {
            ArrivalNumber = "ARR-001",
            MaterialType = "Book",
            Title = "New Book",
            Category = "Computing",
            DepartmentCategory = "CS",
            Quantity = 4,
            PurchaseYear = 2026,
            PurchaseMonth = 6,
            ReceivedDate = DateTime.UtcNow
        });
        await database.Context.SaveChangesAsync();

        var result = await new NewArrivalsReportDataProvider(database.Context)
            .GenerateAsync([], "Tester");

        Assert.Equal("ARR-001", Assert.Single(result.Rows)["ArrivalNumber"]);
    }

    [Fact]
    public async Task StockVerificationReport_ReturnsStockSummary()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var issued = new BookCopy
        {
            AccessionNumber = "STOCK-ISSUED",
            BookMasterId = data.Book.Id,
            CopyNumber = 2,
            AvailabilityStatus = BookStatus.Issued
        };
        database.Context.BookCopies.Add(issued);
        await database.Context.SaveChangesAsync();

        var result = await new StockVerificationReportDataProvider(database.Context)
            .GenerateAsync([], "Tester");

        Assert.Equal("2", result.SummaryItems["Total Copies"]);
        Assert.Equal("1", result.SummaryItems["Available"]);
        Assert.Equal("1", result.SummaryItems["Issued"]);
    }

    [Fact]
    public async Task PdfExporter_HandlesWideReport()
    {
        using var directory = new TemporaryReportDirectory();
        var report = CreateWideReport();

        var result = await new PdfReportExporter().ExportAsync(
            report,
            Request(ReportFormat.PDF, directory.Path));

        Assert.True(result.Succeeded);
        Assert.True(new FileInfo(result.FilePath!).Length > 500);
    }

    [Fact]
    public async Task ExcelExporter_IncludesMetadataHeadersAndAutoFilter()
    {
        using var directory = new TemporaryReportDirectory();
        var report = CreateWideReport();

        var result = await new ExcelReportExporter().ExportAsync(
            report,
            Request(ReportFormat.Excel, directory.Path));

        using var workbook = new XLWorkbook(result.FilePath!);
        var sheet = workbook.Worksheet("Report");
        Assert.Equal("Wide Report", sheet.Cell(1, 1).GetString());
        Assert.Equal("KICSIT", sheet.Cell(2, 1).GetString());
        Assert.True(sheet.AutoFilter.IsEnabled);
    }

    [Fact]
    public async Task CsvExporter_HandlesEmptyReport()
    {
        using var directory = new TemporaryReportDirectory();
        var report = CreateWideReport();
        report.Rows = [];
        report.TotalCount = 0;

        var result = await new CsvReportExporter().ExportAsync(
            report,
            Request(ReportFormat.CSV, directory.Path));

        Assert.True(result.Succeeded);
        var lines = await File.ReadAllLinesAsync(result.FilePath!);
        Assert.Single(lines);
        Assert.Contains("Column 1", lines[0]);
    }

    [Fact]
    public async Task CsvExporter_UsesConsistentDateFormatting()
    {
        using var directory = new TemporaryReportDirectory();
        var report = new ReportResult
        {
            ReportTitle = "Dates",
            InstitutionName = "KICSIT",
            Columns = [new ReportColumn { Key = "Date", Header = "Date" }],
            Rows = [new ReportRow { Values = new() { ["Date"] = new DateTime(2026, 6, 6, 14, 30, 0) } }]
        };

        var result = await new CsvReportExporter().ExportAsync(
            report,
            Request(ReportFormat.CSV, directory.Path));

        Assert.Contains("2026-06-06 14:30:00", await File.ReadAllTextAsync(result.FilePath!));
    }

    [Fact]
    public void ReportService_ResolvesAllSixteenDefinitions()
    {
        using var context = CreateUnconnectedContext();
        IReportDataProvider[] providers =
        [
            new CatalogReportDataProvider(context),
            new IssuedBooksReportDataProvider(context),
            new OverdueBooksReportDataProvider(context),
            new FineReportDataProvider(context),
            new NotificationReportDataProvider(context),
            new StudentClearanceReportDataProvider(context),
            new StudentBorrowingHistoryReportDataProvider(context),
            new FacultyBorrowingHistoryReportDataProvider(context),
            new ReservationReportDataProvider(context),
            new LostDamagedBooksReportDataProvider(context),
            new DeletedBooksArchiveReportDataProvider(context),
            new VisitDetailReportDataProvider(context),
            new AuditReportDataProvider(context),
            new InventoryReportDataProvider(context),
            new NewArrivalsReportDataProvider(context),
            new StockVerificationReportDataProvider(context)
        ];

        var definitions = new ReportService(providers).GetDefinitions();

        Assert.Equal(16, definitions.Count);
        Assert.Equal(7, definitions.Select(item => item.Category).Distinct().Count());
    }

    private static ReportFilter Filter(string key, object value)
    {
        return new ReportFilter
        {
            Key = key,
            Label = key,
            Type = ReportFilterType.Enum,
            Value = value
        };
    }

    private static ReportResult CreateWideReport()
    {
        var columns = Enumerable.Range(1, 18)
            .Select(index => new ReportColumn
            {
                Key = $"Column{index}",
                Header = $"Column {index}"
            })
            .ToList();
        var row = new ReportRow();
        foreach (var column in columns)
        {
            row[column.Key] = $"Value for {column.Header}";
        }

        return new ReportResult
        {
            ReportTitle = "Wide Report",
            InstitutionName = "KICSIT",
            GeneratedAt = DateTime.UtcNow,
            GeneratedBy = "Tester",
            AppliedFilters = ["Status: Active"],
            Columns = columns,
            Rows = [row],
            TotalCount = 1,
            SummaryItems = new Dictionary<string, string> { ["Total"] = "1" }
        };
    }

    private static ReportExportRequest Request(ReportFormat format, string path)
    {
        return new ReportExportRequest { Format = format, OutputDirectory = path };
    }

    private static KicsitLibrary.Data.KicsitLibraryDbContext CreateUnconnectedContext()
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<
                KicsitLibrary.Data.KicsitLibraryDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        return new KicsitLibrary.Data.KicsitLibraryDbContext(options);
    }

    private sealed class TemporaryReportDirectory : IDisposable
    {
        public TemporaryReportDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "KicsitLibrary.Tests",
                $"Priority5B-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
