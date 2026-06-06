using System.IO.Compression;
using ClosedXML.Excel;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Data.Repositories;
using KicsitLibrary.Reports.Contracts;
using KicsitLibrary.Reports.Export;
using KicsitLibrary.Reports.Models;
using KicsitLibrary.Reports.Providers;
using KicsitLibrary.Reports.Services;
using KicsitLibrary.Services.Logging;
using KicsitLibrary.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Tests;

public class ReportFoundationTests
{
    [Fact]
    public async Task LibraryCatalogReport_ReturnsSeededBookData()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var provider = new CatalogReportDataProvider(database.Context);

        var result = await provider.GenerateAsync([], "Test User");

        var row = Assert.Single(result.Rows);
        Assert.Equal(data.Copy.AccessionNumber, row["AccessionNumber"]);
        Assert.Equal(data.Book.Title, row["Title"]);
        Assert.Equal(12, result.Columns.Count);
    }

    [Fact]
    public async Task IssuedBooksReport_ReturnsActiveIssueRows()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var issue = await database.AddIssueAsync(
            data,
            LocalDateToUtc(DateTime.Now.Date.AddDays(2)));
        var provider = new IssuedBooksReportDataProvider(database.Context);

        var result = await provider.GenerateAsync([], "Test User");

        var row = Assert.Single(result.Rows);
        Assert.Equal(issue.Id, row["IssueRecordId"]);
        Assert.Equal("Issued", row["Status"]);
    }

    [Fact]
    public async Task OverdueBooksReport_UsesDeterministicOverdueLogic()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var issue = await database.AddIssueAsync(
            data,
            LocalDateToUtc(DateTime.Now.Date.AddDays(-3)));
        var provider = new OverdueBooksReportDataProvider(database.Context);

        var result = await provider.GenerateAsync([], "Test User");

        var row = Assert.Single(result.Rows);
        Assert.Equal(issue.Id, row["IssueRecordId"]);
        Assert.Equal(3, row["DaysOverdue"]);
        Assert.Equal(30m, row["CurrentFine"]);
    }

    [Fact]
    public async Task FineReport_ReturnsFineRowsAndTotals()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var issue = await database.AddIssueAsync(
            data,
            LocalDateToUtc(DateTime.Now.Date.AddDays(-2)));
        database.Context.Fines.Add(new Fine
        {
            FineRecordNumber = "FINE-TEST",
            MemberType = MemberType.Student,
            StudentId = data.Student.Id,
            IssueRecordId = issue.Id,
            AccessionNumber = data.Copy.AccessionNumber,
            FineType = "Overdue",
            FinePerDay = 10,
            DaysOverdue = 2,
            FineAmount = 20,
            PaidAmount = 5,
            RemainingAmount = 15,
            PaymentStatus = FineStatus.Partial
        });
        await database.Context.SaveChangesAsync();
        var provider = new FineReportDataProvider(database.Context);

        var result = await provider.GenerateAsync([], "Test User");

        var row = Assert.Single(result.Rows);
        Assert.Equal(20m, row["FineAmount"]);
        Assert.Equal("20.00", result.SummaryItems["Total Fine"]);
        Assert.Equal("15.00", result.SummaryItems["Remaining"]);
    }

    [Fact]
    public async Task NotificationReport_ReturnsNotificationRows()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var notification = await database.AddNotificationAsync(data);
        var provider = new NotificationReportDataProvider(database.Context);

        var result = await provider.GenerateAsync([], "Test User");

        var row = Assert.Single(result.Rows);
        Assert.Equal(notification.Id, row["NotificationId"]);
        Assert.Equal("Email", row["Channel"]);
        Assert.Equal("Pending", row["Status"]);
    }

    [Fact]
    public async Task CsvExporter_CreatesFileAndIncludesHeaders()
    {
        using var directory = new TemporaryDirectory();
        var exporter = new CsvReportExporter();

        var result = await exporter.ExportAsync(
            CreateSampleReport(),
            Request(ReportFormat.CSV, directory.Path));

        Assert.True(result.Succeeded);
        Assert.True(File.Exists(result.FilePath));
        var content = await File.ReadAllTextAsync(result.FilePath!);
        Assert.Contains("Accession Number,Title", content);
        Assert.Contains("ACC-001,Test Book", content);
    }

    [Fact]
    public async Task ExcelExporter_CreatesWorkbookWithReportTitle()
    {
        using var directory = new TemporaryDirectory();
        var exporter = new ExcelReportExporter();

        var result = await exporter.ExportAsync(
            CreateSampleReport(),
            Request(ReportFormat.Excel, directory.Path));

        Assert.True(result.Succeeded);
        Assert.True(File.Exists(result.FilePath));
        using var workbook = new XLWorkbook(result.FilePath!);
        Assert.Equal(
            "Sample Report",
            workbook.Worksheet("Report").Cell(1, 1).GetString());
    }

    [Fact]
    public async Task PdfExporter_CreatesNonEmptyPdfFile()
    {
        using var directory = new TemporaryDirectory();
        var exporter = new PdfReportExporter();

        var result = await exporter.ExportAsync(
            CreateSampleReport(),
            Request(ReportFormat.PDF, directory.Path));

        Assert.True(result.Succeeded);
        Assert.True(new FileInfo(result.FilePath!).Length > 100);
        var header = new byte[4];
        await using var stream = File.OpenRead(result.FilePath!);
        await stream.ReadExactlyAsync(header);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(header));
    }

    [Fact]
    public async Task EmptyReport_ExportsGracefully()
    {
        using var directory = new TemporaryDirectory();
        var report = CreateSampleReport();
        report.Rows = [];
        report.TotalCount = 0;
        var exporter = new PdfReportExporter();

        var result = await exporter.ExportAsync(
            report,
            Request(ReportFormat.PDF, directory.Path));

        Assert.True(result.Succeeded);
        Assert.True(File.Exists(result.FilePath));
        Assert.True(new FileInfo(result.FilePath!).Length > 100);
    }

    [Fact]
    public void FileNameSanitizer_RemovesInvalidCharacters()
    {
        var result = FileNameSanitizer.Sanitize("Fine: Report/2026?.pdf");

        Assert.DoesNotContain(':', result);
        Assert.DoesNotContain('/', result);
        Assert.DoesNotContain('?', result);
        Assert.Equal("Fine_Report_2026_.pdf", result);
    }

    [Fact]
    public async Task ReportExportService_SelectsCorrectExporter()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var logService = new ActivityLogService(
            new Repository<ActivityLog>(database.Context));
        var csv = new RecordingExporter(ReportFormat.CSV);
        var pdf = new RecordingExporter(ReportFormat.PDF);
        var service = new ReportExportService([csv, pdf], logService);

        var result = await service.ExportAsync(
            CreateSampleReport(),
            new ReportExportRequest { Format = ReportFormat.PDF },
            data.User.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(0, csv.CallCount);
        Assert.Equal(1, pdf.CallCount);
        Assert.Contains(
            await database.Context.ActivityLogs.ToListAsync(),
            log => log.Action == "Report Exported");
    }

    [Fact]
    public async Task Filters_ReduceReportResultCount()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await database.AddCirculationDataAsync();
        var provider = new CatalogReportDataProvider(database.Context);
        var baseline = await provider.GenerateAsync([], "Test User");

        var filtered = await provider.GenerateAsync(
            [
                new ReportFilter
                {
                    Key = "SearchText",
                    Label = "Search Text",
                    Type = ReportFilterType.Text,
                    Value = "does-not-exist"
                }
            ],
            "Test User");

        Assert.Equal(1, baseline.TotalCount);
        Assert.Equal(0, filtered.TotalCount);
    }

    [Fact]
    public async Task Exporter_DoesNotOverwriteExistingFileByDefault()
    {
        using var directory = new TemporaryDirectory();
        var exporter = new CsvReportExporter();
        var request = Request(ReportFormat.CSV, directory.Path);
        request.FileName = "Fixed Report";

        var first = await exporter.ExportAsync(CreateSampleReport(), request);
        var second = await exporter.ExportAsync(CreateSampleReport(), request);

        Assert.NotEqual(first.FilePath, second.FilePath);
        Assert.True(File.Exists(first.FilePath));
        Assert.True(File.Exists(second.FilePath));
    }

    private static ReportResult CreateSampleReport()
    {
        return new ReportResult
        {
            ReportTitle = "Sample Report",
            InstitutionName = "KICSIT",
            GeneratedAt = DateTime.UtcNow,
            GeneratedBy = "Test User",
            Columns =
            [
                new ReportColumn
                {
                    Key = "AccessionNumber",
                    Header = "Accession Number"
                },
                new ReportColumn
                {
                    Key = "Title",
                    Header = "Title"
                }
            ],
            Rows =
            [
                new ReportRow
                {
                    Values = new Dictionary<string, object?>
                    {
                        ["AccessionNumber"] = "ACC-001",
                        ["Title"] = "Test Book"
                    }
                }
            ],
            TotalCount = 1,
            SummaryItems = new Dictionary<string, string>
            {
                ["Total Records"] = "1"
            }
        };
    }

    private static ReportExportRequest Request(
        ReportFormat format,
        string outputDirectory)
    {
        return new ReportExportRequest
        {
            Format = format,
            OutputDirectory = outputDirectory
        };
    }

    private static DateTime LocalDateToUtc(DateTime localDate)
    {
        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(localDate.AddHours(12), DateTimeKind.Unspecified),
            TimeZoneInfo.Local);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "KicsitLibrary.Tests",
                $"Reports-{Guid.NewGuid():N}");
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

    private sealed class RecordingExporter(ReportFormat format) : IReportExporter
    {
        public ReportFormat Format { get; } = format;
        public int CallCount { get; private set; }

        public Task<ReportExportResult> ExportAsync(
            ReportResult report,
            ReportExportRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new ReportExportResult
            {
                Succeeded = true,
                Format = Format,
                FilePath = "test-output",
                Message = "Exported.",
                ExportedAt = DateTime.UtcNow
            });
        }
    }
}
