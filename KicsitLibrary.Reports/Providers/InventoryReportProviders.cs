using KicsitLibrary.Core.Enums;
using KicsitLibrary.Data;
using KicsitLibrary.Reports.Models;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Reports.Providers;

public sealed class InventoryReportDataProvider(KicsitLibraryDbContext context)
    : ReportDataProviderBase(context)
{
    private static readonly ReportDefinition ReportDefinition = new()
    {
        Key = ReportKeys.Inventory,
        Title = "Inventory Report",
        Description = "Library equipment quantities, condition, location, and value.",
        Category = "Inventory Reports",
        Columns =
        [
            Column("ItemName", "Item Name"),
            Column("ItemType", "Item Type"),
            Column("Quantity", "Quantity"),
            Column("AvailableQuantity", "Available Quantity"),
            Column("DamagedQuantity", "Damaged Quantity"),
            Column("Location", "Location"),
            Column("Condition", "Condition"),
            Column("PurchaseDate", "Purchase Date", "dd-MMM-yyyy"),
            Column("PurchasePrice", "Purchase Price", "N2"),
            Column("Supplier", "Supplier"),
            Column("LastUpdatedDate", "Last Updated Date", "dd-MMM-yyyy")
        ],
        Filters =
        [
            Filter(ReportFilterKeys.SearchText, "Search Text", ReportFilterType.Text),
            Filter(ReportFilterKeys.ItemType, "Item Type", ReportFilterType.Enum, Enum.GetNames<InventoryItemType>()),
            Filter(ReportFilterKeys.Condition, "Condition", ReportFilterType.Text),
            Filter(ReportFilterKeys.Location, "Location", ReportFilterType.Text),
            Filter(ReportFilterKeys.DateRange, "Purchase Date", ReportFilterType.DateRange)
        ]
    };

    public override ReportDefinition Definition => ReportDefinition;

    public override async Task<ReportResult> GenerateAsync(
        IReadOnlyCollection<ReportFilter> filters,
        string generatedBy,
        CancellationToken cancellationToken = default)
    {
        var items = await Context.InventoryItems.AsNoTracking()
            .OrderBy(item => item.ItemName)
            .ToListAsync(cancellationToken);
        var search = FilterReader.Text(filters, ReportFilterKeys.SearchText);
        var type = FilterReader.Text(filters, ReportFilterKeys.ItemType);
        var condition = FilterReader.Text(filters, ReportFilterKeys.Condition);
        var location = FilterReader.Text(filters, ReportFilterKeys.Location);
        var fromDate = FilterReader.StartDate(filters, ReportFilterKeys.DateRange);
        var toDate = FilterReader.EndDate(filters, ReportFilterKeys.DateRange);

        var rows = items.Where(item =>
            TextMatches(search, item.ItemName, item.Supplier, item.Remarks) &&
            ExactMatches(type, item.ItemType.ToString()) &&
            TextMatches(condition, item.Condition) &&
            TextMatches(location, item.Location) &&
            (!fromDate.HasValue || AsLocalDate(item.PurchaseDate) >= fromDate.Value.Date) &&
            (!toDate.HasValue || AsLocalDate(item.PurchaseDate) <= toDate.Value.Date))
        .Select(item => Row(
            ("ItemName", item.ItemName),
            ("ItemType", item.ItemType.ToString()),
            ("Quantity", item.Quantity),
            ("AvailableQuantity", item.AvailableQuantity),
            ("DamagedQuantity", item.DamagedQuantity),
            ("Location", item.Location),
            ("Condition", item.Condition),
            ("PurchaseDate", AsLocalDate(item.PurchaseDate)),
            ("PurchasePrice", item.PurchasePrice),
            ("Supplier", item.Supplier),
            ("LastUpdatedDate", AsLocalDate(item.LastUpdatedDate))))
        .ToList();

        return await CreateResultAsync(filters, generatedBy, rows,
            new Dictionary<string, string>
            {
                ["Inventory Lines"] = rows.Count.ToString(),
                ["Total Quantity"] = rows.Sum(row => Convert.ToInt32(row["Quantity"])).ToString(),
                ["Available Quantity"] = rows.Sum(row => Convert.ToInt32(row["AvailableQuantity"])).ToString(),
                ["Damaged Quantity"] = rows.Sum(row => Convert.ToInt32(row["DamagedQuantity"])).ToString()
            }, cancellationToken);
    }
}

public sealed class NewArrivalsReportDataProvider(KicsitLibraryDbContext context)
    : ReportDataProviderBase(context)
{
    private static readonly ReportDefinition ReportDefinition = new()
    {
        Key = ReportKeys.NewArrivals,
        Title = "New Arrivals Report",
        Description = "Recently received library materials and purchasing details.",
        Category = "Catalog Reports",
        Columns =
        [
            Column("ArrivalNumber", "Arrival Number"),
            Column("MaterialType", "Material Type"),
            Column("Title", "Title"),
            Column("Category", "Category"),
            Column("DepartmentCategory", "Department Category"),
            Column("Quantity", "Quantity"),
            Column("PurchaseYear", "Purchase Year"),
            Column("PurchaseMonth", "Purchase Month"),
            Column("Supplier", "Supplier"),
            Column("InvoiceNumber", "Invoice Number"),
            Column("ReceivedDate", "Received Date", "dd-MMM-yyyy")
        ],
        Filters =
        [
            Filter(ReportFilterKeys.SearchText, "Search Text", ReportFilterType.Text),
            Filter(ReportFilterKeys.MaterialType, "Material Type", ReportFilterType.Text),
            Filter(ReportFilterKeys.Category, "Category", ReportFilterType.Text),
            Filter(ReportFilterKeys.Department, "Department", ReportFilterType.Text),
            Filter(ReportFilterKeys.PurchaseYear, "Purchase Year", ReportFilterType.NumberRange),
            Filter(ReportFilterKeys.ReceivedDateRange, "Received Date", ReportFilterType.DateRange)
        ]
    };

    public override ReportDefinition Definition => ReportDefinition;

    public override async Task<ReportResult> GenerateAsync(
        IReadOnlyCollection<ReportFilter> filters,
        string generatedBy,
        CancellationToken cancellationToken = default)
    {
        var arrivals = await Context.NewArrivals.AsNoTracking()
            .OrderByDescending(item => item.ReceivedDate)
            .ToListAsync(cancellationToken);
        var search = FilterReader.Text(filters, ReportFilterKeys.SearchText);
        var materialType = FilterReader.Text(filters, ReportFilterKeys.MaterialType);
        var category = FilterReader.Text(filters, ReportFilterKeys.Category);
        var department = FilterReader.Text(filters, ReportFilterKeys.Department);
        var minYear = FilterReader.MinimumInteger(filters, ReportFilterKeys.PurchaseYear);
        var maxYear = FilterReader.MaximumInteger(filters, ReportFilterKeys.PurchaseYear);
        var fromDate = FilterReader.StartDate(filters, ReportFilterKeys.ReceivedDateRange);
        var toDate = FilterReader.EndDate(filters, ReportFilterKeys.ReceivedDateRange);

        var rows = arrivals.Where(item =>
            TextMatches(search, item.ArrivalNumber, item.Title, item.Supplier, item.InvoiceNumber) &&
            TextMatches(materialType, item.MaterialType) &&
            TextMatches(category, item.Category) &&
            TextMatches(department, item.DepartmentCategory) &&
            (!minYear.HasValue || item.PurchaseYear >= minYear.Value) &&
            (!maxYear.HasValue || item.PurchaseYear <= maxYear.Value) &&
            (!fromDate.HasValue || AsLocalDate(item.ReceivedDate) >= fromDate.Value.Date) &&
            (!toDate.HasValue || AsLocalDate(item.ReceivedDate) <= toDate.Value.Date))
        .Select(item => Row(
            ("ArrivalNumber", item.ArrivalNumber),
            ("MaterialType", item.MaterialType),
            ("Title", item.Title),
            ("Category", item.Category),
            ("DepartmentCategory", item.DepartmentCategory),
            ("Quantity", item.Quantity),
            ("PurchaseYear", item.PurchaseYear),
            ("PurchaseMonth", item.PurchaseMonth),
            ("Supplier", item.Supplier),
            ("InvoiceNumber", item.InvoiceNumber),
            ("ReceivedDate", AsLocalDate(item.ReceivedDate))))
        .ToList();

        return await CreateResultAsync(filters, generatedBy, rows,
            new Dictionary<string, string>
            {
                ["Arrival Records"] = rows.Count.ToString(),
                ["Total Quantity"] = rows.Sum(row => Convert.ToInt32(row["Quantity"])).ToString()
            }, cancellationToken);
    }
}

public sealed class StockVerificationReportDataProvider(KicsitLibraryDbContext context)
    : ReportDataProviderBase(context)
{
    private static readonly ReportDefinition ReportDefinition = new()
    {
        Key = ReportKeys.StockVerification,
        Title = "Stock Verification Report",
        Description = "Latest physical stock-verification results and reconciliation state.",
        Category = "Inventory Reports",
        Columns =
        [
            Column("AccessionNumber", "Accession Number"),
            Column("BookTitle", "Book Title"),
            Column("Category", "Category"),
            Column("Department", "Department"),
            Column("Rack", "Rack"),
            Column("Shelf", "Shelf"),
            Column("ExpectedStatus", "Expected Status"),
            Column("ActualStatus", "Actual Status"),
            Column("LastIssueDate", "Last Issue Date", "dd-MMM-yyyy"),
            Column("LastReceiveDate", "Last Receive Date", "dd-MMM-yyyy"),
            Column("VerificationRemarks", "Verification Remarks")
        ],
        Filters =
        [
            Filter(ReportFilterKeys.SearchText, "Search Text", ReportFilterType.Text),
            Filter(ReportFilterKeys.Category, "Category", ReportFilterType.Text),
            Filter(ReportFilterKeys.Department, "Department", ReportFilterType.Text),
            Filter(ReportFilterKeys.Status, "Status", ReportFilterType.Enum, Enum.GetNames<BookStatus>()),
            Filter(ReportFilterKeys.Rack, "Rack", ReportFilterType.Text),
            Filter(ReportFilterKeys.Shelf, "Shelf", ReportFilterType.Text)
        ]
    };

    public override ReportDefinition Definition => ReportDefinition;

    public override async Task<ReportResult> GenerateAsync(
        IReadOnlyCollection<ReportFilter> filters,
        string generatedBy,
        CancellationToken cancellationToken = default)
    {
        var latestSessionId = await Context.StockVerificationSessions.AsNoTracking()
            .OrderByDescending(item => item.StartedAt)
            .Select(item => (int?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var verification = latestSessionId.HasValue
            ? await Context.StockVerificationEntries.AsNoTracking()
                .Where(item => item.SessionId == latestSessionId.Value)
                .ToDictionaryAsync(item => item.BookCopyId, cancellationToken)
            : [];
        var copies = await Context.BookCopies.IgnoreQueryFilters().AsNoTracking()
            .Include(copy => copy.BookMaster).ThenInclude(book => book.Category)
            .Include(copy => copy.BookMaster.DepartmentCategory)
            .OrderBy(copy => copy.AccessionNumber)
            .ToListAsync(cancellationToken);
        var search = FilterReader.Text(filters, ReportFilterKeys.SearchText);
        var category = FilterReader.Text(filters, ReportFilterKeys.Category);
        var department = FilterReader.Text(filters, ReportFilterKeys.Department);
        var status = FilterReader.Text(filters, ReportFilterKeys.Status);
        var rack = FilterReader.Text(filters, ReportFilterKeys.Rack);
        var shelf = FilterReader.Text(filters, ReportFilterKeys.Shelf);

        var rows = copies.Select(copy =>
        {
            var currentStatus = copy.IsDeleted ? BookStatus.Deleted : copy.AvailabilityStatus;
            verification.TryGetValue(copy.Id, out var result);
            return new { Copy = copy, Status = currentStatus, Result = result };
        })
        .Where(item =>
            TextMatches(search, item.Copy.AccessionNumber, item.Copy.BookMaster.Title) &&
            ExactMatches(category, item.Copy.BookMaster.Category?.Name) &&
            ExactMatches(department, item.Copy.BookMaster.DepartmentCategory?.Name) &&
            ExactMatches(status, item.Status.ToString()) &&
            TextMatches(rack, item.Copy.RackNumber) &&
            TextMatches(shelf, item.Copy.ShelfNumber))
        .Select(item => Row(
            ("AccessionNumber", item.Copy.AccessionNumber),
            ("BookTitle", item.Copy.BookMaster.Title),
            ("Category", item.Copy.BookMaster.Category?.Name ?? string.Empty),
            ("Department", item.Copy.BookMaster.DepartmentCategory?.Name ?? string.Empty),
            ("Rack", item.Copy.RackNumber),
            ("Shelf", item.Copy.ShelfNumber),
            ("ExpectedStatus", item.Status.ToString()),
            ("ActualStatus", item.Result?.ActualStatus?.ToString() ?? "Unverified"),
            ("LastIssueDate", item.Copy.LastIssuedDate?.ToLocalTime()),
            ("LastReceiveDate", item.Copy.LastReceivedDate?.ToLocalTime()),
            ("VerificationRemarks", item.Result?.VerificationRemarks ?? "Physical verification pending")))
        .ToList();

        var summary = new Dictionary<string, string>
        {
            ["Total Copies"] = rows.Count.ToString(),
            ["Matched"] = verification.Values.Count(item => item.ActualStatus.HasValue && !item.IsMismatch).ToString(),
            ["Mismatched"] = verification.Values.Count(item => item.ActualStatus.HasValue && item.IsMismatch).ToString(),
            ["Unverified"] = verification.Values.Count(item => !item.ActualStatus.HasValue).ToString()
        };
        foreach (var value in new[]
                 {
                     BookStatus.Available, BookStatus.Issued, BookStatus.Reserved,
                     BookStatus.Lost, BookStatus.Damaged, BookStatus.Deleted
                 })
        {
            summary[value.ToString()] = rows.Count(row =>
                Equals(row["ExpectedStatus"], value.ToString())).ToString();
        }

        return await CreateResultAsync(filters, generatedBy, rows, summary, cancellationToken);
    }
}
