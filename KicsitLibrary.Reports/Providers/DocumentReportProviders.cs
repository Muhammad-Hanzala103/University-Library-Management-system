using KicsitLibrary.Data;
using KicsitLibrary.Reports.Models;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Reports.Providers;

public abstract class DocumentReportDataProviderBase(KicsitLibraryDbContext context)
    : ReportDataProviderBase(context)
{
    protected async Task<ReportResult> GenerateDocumentsAsync(
        string documentType,
        IReadOnlyCollection<ReportFilter> filters,
        string generatedBy,
        CancellationToken cancellationToken)
    {
        var documents = await Context.DocumentUploads.AsNoTracking()
            .Where(item => item.DocumentType == documentType)
            .OrderByDescending(item => item.UploadDate)
            .ToListAsync(cancellationToken);
        var search = FilterReader.Text(filters, ReportFilterKeys.SearchText);
        var uploadedBy = FilterReader.Text(filters, ReportFilterKeys.UploadedBy);
        var fromDate = FilterReader.StartDate(filters, ReportFilterKeys.DateRange);
        var toDate = FilterReader.EndDate(filters, ReportFilterKeys.DateRange);
        var expiredOnly = FilterReader.Boolean(filters, ReportFilterKeys.ExpiredOnly);
        var missingOnly = FilterReader.Boolean(filters, ReportFilterKeys.MissingFileOnly);

        var rows = documents
            .Where(item =>
                TextMatches(search, item.DocumentTitle, item.OriginalFileName, item.Description, item.Remarks) &&
                TextMatches(uploadedBy, item.UploadedBy) &&
                (!fromDate.HasValue || AsLocalDate(item.UploadDate) >= fromDate.Value.Date) &&
                (!toDate.HasValue || AsLocalDate(item.UploadDate) <= toDate.Value.Date) &&
                (!expiredOnly || (item.ExpiryDate.HasValue && item.ExpiryDate.Value.Date < DateTime.UtcNow.Date)) &&
                (!missingOnly || !File.Exists(string.IsNullOrWhiteSpace(item.StoredFilePath) ? item.FilePath : item.StoredFilePath)))
            .Select(item => Row(
                ("DocumentId", item.Id),
                ("Title", item.DocumentTitle),
                ("Type", item.DocumentType),
                ("Version", item.VersionNumber),
                ("OriginalFileName", item.OriginalFileName),
                ("SizeBytes", item.FileSizeBytes),
                ("UploadedBy", item.UploadedBy),
                ("UploadDate", AsLocalDate(item.UploadDate)),
                ("ExpiryDate", item.ExpiryDate?.ToLocalTime().Date),
                ("Status", item.ActiveStatus && !item.IsDeleted
                    ? File.Exists(string.IsNullOrWhiteSpace(item.StoredFilePath) ? item.FilePath : item.StoredFilePath)
                        ? "Active"
                        : "Missing File"
                    : "Inactive"),
                ("RelatedEntity", string.IsNullOrWhiteSpace(item.RelatedEntityType)
                    ? string.Empty
                    : $"{item.RelatedEntityType} {item.RelatedEntityId}")))
            .ToList();

        return await CreateResultAsync(filters, generatedBy, rows,
            new Dictionary<string, string>
            {
                ["Documents"] = rows.Count.ToString(),
                ["Missing Files"] = rows.Count(row => Equals(row["Status"], "Missing File")).ToString()
            }, cancellationToken);
    }

    protected static IReadOnlyList<ReportColumn> DocumentColumns() =>
    [
        Column("DocumentId", "Document Id"),
        Column("Title", "Title"),
        Column("Type", "Type"),
        Column("Version", "Version"),
        Column("OriginalFileName", "Original File"),
        Column("SizeBytes", "Size", "N0"),
        Column("UploadedBy", "Uploaded By"),
        Column("UploadDate", "Upload Date", "dd-MMM-yyyy"),
        Column("ExpiryDate", "Expiry Date", "dd-MMM-yyyy"),
        Column("Status", "Status"),
        Column("RelatedEntity", "Related Entity")
    ];

    protected static IReadOnlyList<ReportFilter> DocumentFilters() =>
    [
        Filter(ReportFilterKeys.SearchText, "Search Text", ReportFilterType.Text),
        Filter(ReportFilterKeys.UploadedBy, "Uploaded By", ReportFilterType.Text),
        Filter(ReportFilterKeys.DateRange, "Upload Date", ReportFilterType.DateRange),
        Filter(ReportFilterKeys.ExpiredOnly, "Expired Only", ReportFilterType.Boolean),
        Filter(ReportFilterKeys.MissingFileOnly, "Missing File Only", ReportFilterType.Boolean)
    ];
}

public sealed class SopDocumentsReportDataProvider(KicsitLibraryDbContext context)
    : DocumentReportDataProviderBase(context)
{
    private static readonly ReportDefinition ReportDefinition = new()
    {
        Key = ReportKeys.SopDocuments,
        Title = "SOP Documents Report",
        Description = "Uploaded library standard operating procedure documents.",
        Category = "Compliance Reports",
        Columns = DocumentColumns(),
        Filters = DocumentFilters()
    };

    public override ReportDefinition Definition => ReportDefinition;

    public override Task<ReportResult> GenerateAsync(
        IReadOnlyCollection<ReportFilter> filters,
        string generatedBy,
        CancellationToken cancellationToken = default) =>
        GenerateDocumentsAsync("LibrarySop", filters, generatedBy, cancellationToken);
}

public sealed class NationalLibraryRatesDocumentsReportDataProvider(KicsitLibraryDbContext context)
    : DocumentReportDataProviderBase(context)
{
    private static readonly ReportDefinition ReportDefinition = new()
    {
        Key = ReportKeys.NationalLibraryRatesDocuments,
        Title = "National Library Rates Documents Report",
        Description = "Uploaded national library rates documents.",
        Category = "Compliance Reports",
        Columns = DocumentColumns(),
        Filters = DocumentFilters()
    };

    public override ReportDefinition Definition => ReportDefinition;

    public override Task<ReportResult> GenerateAsync(
        IReadOnlyCollection<ReportFilter> filters,
        string generatedBy,
        CancellationToken cancellationToken = default) =>
        GenerateDocumentsAsync("NationalLibraryRates", filters, generatedBy, cancellationToken);
}
