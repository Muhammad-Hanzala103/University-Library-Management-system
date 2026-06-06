using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Data;
using KicsitLibrary.Reports.Models;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Reports.Providers
{
    public sealed class CatalogReportDataProvider : ReportDataProviderBase
    {
        private static readonly ReportDefinition ReportDefinition = new()
        {
            Key = ReportKeys.Catalog,
            Title = "Library Catalog Report",
            Description = "Physical library copies with catalog and shelf metadata.",
            Category = "Catalog Reports",
            Columns =
            [
                Column("AccessionNumber", "Accession Number"),
                Column("Title", "Title"),
                Column("Author", "Author"),
                Column("Publisher", "Publisher"),
                Column("Category", "Category"),
                Column("Department", "Department"),
                Column("LiteratureCategory", "Literature Category"),
                Column("ISBN", "ISBN"),
                Column("ISSN", "ISSN"),
                Column("Status", "Status"),
                Column("Rack", "Rack"),
                Column("Shelf", "Shelf")
            ],
            Filters =
            [
                Filter(ReportFilterKeys.SearchText, "Search Text", ReportFilterType.Text),
                Filter(ReportFilterKeys.Category, "Category", ReportFilterType.Text),
                Filter(ReportFilterKeys.Department, "Department", ReportFilterType.Text),
                Filter(ReportFilterKeys.LiteratureCategory, "Literature Category", ReportFilterType.Text),
                Filter(ReportFilterKeys.Status, "Status", ReportFilterType.Enum, Enum.GetNames<BookStatus>()),
                Filter(ReportFilterKeys.Author, "Author", ReportFilterType.Text),
                Filter(ReportFilterKeys.Publisher, "Publisher", ReportFilterType.Text)
            ]
        };

        public CatalogReportDataProvider(KicsitLibraryDbContext context)
            : base(context)
        {
        }

        public override ReportDefinition Definition => ReportDefinition;

        public override async Task<ReportResult> GenerateAsync(
            IReadOnlyCollection<ReportFilter> filters,
            string generatedBy,
            CancellationToken cancellationToken = default)
        {
            var copies = await Context.BookCopies
                .AsNoTracking()
                .Include(copy => copy.BookMaster)
                    .ThenInclude(book => book.BookAuthors)
                    .ThenInclude(bookAuthor => bookAuthor.Author)
                .Include(copy => copy.BookMaster.Publisher)
                .Include(copy => copy.BookMaster.Category)
                .Include(copy => copy.BookMaster.DepartmentCategory)
                .Include(copy => copy.BookMaster.LiteratureCategory)
                .OrderBy(copy => copy.AccessionNumber)
                .ToListAsync(cancellationToken);

            var search = FilterReader.Text(filters, ReportFilterKeys.SearchText);
            var category = FilterReader.Text(filters, ReportFilterKeys.Category);
            var department = FilterReader.Text(filters, ReportFilterKeys.Department);
            var literature = FilterReader.Text(filters, ReportFilterKeys.LiteratureCategory);
            var status = FilterReader.Text(filters, ReportFilterKeys.Status);
            var author = FilterReader.Text(filters, ReportFilterKeys.Author);
            var publisher = FilterReader.Text(filters, ReportFilterKeys.Publisher);

            var filtered = copies.Where(copy =>
                Matches(search, copy.AccessionNumber, copy.BookMaster.Title, copy.BookMaster.ISBN, copy.BookMaster.ISSN) &&
                MatchesExact(category, copy.BookMaster.Category.Name) &&
                MatchesExact(department, copy.BookMaster.DepartmentCategory.Name) &&
                MatchesExact(literature, copy.BookMaster.LiteratureCategory.Name) &&
                MatchesExact(status, copy.AvailabilityStatus.ToString()) &&
                Matches(author, copy.BookMaster.BookAuthors.Select(item => item.Author.Name).ToArray()) &&
                Matches(publisher, copy.BookMaster.Publisher.Name));

            var rows = filtered.Select(copy => Row(
                ("AccessionNumber", copy.AccessionNumber),
                ("Title", copy.BookMaster.Title),
                ("Author", string.Join(", ", copy.BookMaster.BookAuthors
                    .Select(item => item.Author.Name)
                    .OrderBy(name => name))),
                ("Publisher", copy.BookMaster.Publisher.Name),
                ("Category", copy.BookMaster.Category.Name),
                ("Department", copy.BookMaster.DepartmentCategory.Name),
                ("LiteratureCategory", copy.BookMaster.LiteratureCategory.Name),
                ("ISBN", copy.BookMaster.ISBN),
                ("ISSN", copy.BookMaster.ISSN),
                ("Status", copy.AvailabilityStatus.ToString()),
                ("Rack", copy.RackNumber),
                ("Shelf", copy.ShelfNumber))).ToList();

            return await CreateResultAsync(
                filters,
                generatedBy,
                rows,
                new Dictionary<string, string>
                {
                    ["Total Copies"] = rows.Count.ToString(),
                    ["Available"] = rows.Count(row =>
                        Equals(row["Status"], BookStatus.Available.ToString())).ToString()
                },
                cancellationToken);
        }

        private static bool Matches(string? filter, params string?[] values)
        {
            return string.IsNullOrWhiteSpace(filter) ||
                values.Any(value =>
                    value?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true);
        }

        private static bool MatchesExact(string? filter, string? value)
        {
            return string.IsNullOrWhiteSpace(filter) ||
                string.Equals(filter, value, StringComparison.OrdinalIgnoreCase);
        }
    }
}
