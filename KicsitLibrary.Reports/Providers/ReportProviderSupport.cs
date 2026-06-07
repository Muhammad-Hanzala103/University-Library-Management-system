using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KicsitLibrary.Core;
using KicsitLibrary.Data;
using KicsitLibrary.Reports.Contracts;
using KicsitLibrary.Reports.Models;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Reports.Providers
{
    internal static class ReportKeys
    {
        public const string Catalog = "catalog";
        public const string IssuedBooks = "issued-books";
        public const string OverdueBooks = "overdue-books";
        public const string Fines = "fines";
        public const string Notifications = "notifications";
        public const string StudentClearance = "student-clearance";
        public const string StudentBorrowingHistory = "student-borrowing-history";
        public const string FacultyBorrowingHistory = "faculty-borrowing-history";
        public const string Reservations = "reservations";
        public const string LostDamagedBooks = "lost-damaged-books";
        public const string DeletedBooksArchive = "deleted-books-archive";
        public const string VisitDetail = "visit-detail";
        public const string Audit = "audit";
        public const string Inventory = "inventory";
        public const string NewArrivals = "new-arrivals";
        public const string StockVerification = "stock-verification";
    }

    internal static class ReportFilterKeys
    {
        public const string SearchText = "SearchText";
        public const string Category = "Category";
        public const string Department = "Department";
        public const string LiteratureCategory = "LiteratureCategory";
        public const string Status = "Status";
        public const string Author = "Author";
        public const string Publisher = "Publisher";
        public const string MemberType = "MemberType";
        public const string DateRange = "DateRange";
        public const string OverdueOnly = "OverdueOnly";
        public const string DaysOverdue = "DaysOverdue";
        public const string FineAmount = "FineAmount";
        public const string PaymentStatus = "PaymentStatus";
        public const string Channel = "Channel";
        public const string NotificationType = "NotificationType";
        public const string Program = "Program";
        public const string Batch = "Batch";
        public const string ClearanceStatus = "ClearanceStatus";
        public const string PendingOnly = "PendingOnly";
        public const string RegistrationNumber = "RegistrationNumber";
        public const string PersonnelNumber = "PersonnelNumber";
        public const string FacultyType = "FacultyType";
        public const string ExpiredOnly = "ExpiredOnly";
        public const string EntityName = "EntityName";
        public const string DeletedBy = "DeletedBy";
        public const string Organization = "Organization";
        public const string PendingFollowUpOnly = "PendingFollowUpOnly";
        public const string AuditType = "AuditType";
        public const string FinancialYear = "FinancialYear";
        public const string PendingActionOnly = "PendingActionOnly";
        public const string ItemType = "ItemType";
        public const string Condition = "Condition";
        public const string Location = "Location";
        public const string MaterialType = "MaterialType";
        public const string PurchaseYear = "PurchaseYear";
        public const string ReceivedDateRange = "ReceivedDateRange";
        public const string Rack = "Rack";
        public const string Shelf = "Shelf";
    }

    public abstract class ReportDataProviderBase : IReportDataProvider
    {
        protected ReportDataProviderBase(KicsitLibraryDbContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        protected KicsitLibraryDbContext Context { get; }
        public abstract ReportDefinition Definition { get; }

        public abstract Task<ReportResult> GenerateAsync(
            IReadOnlyCollection<ReportFilter> filters,
            string generatedBy,
            CancellationToken cancellationToken = default);

        protected async Task<ReportResult> CreateResultAsync(
            IReadOnlyCollection<ReportFilter> filters,
            string generatedBy,
            IReadOnlyList<ReportRow> rows,
            IReadOnlyDictionary<string, string>? summary = null,
            CancellationToken cancellationToken = default)
        {
            var institutionName = await Context.SystemSettings
                .AsNoTracking()
                .Where(setting => setting.Key == "InstituteName")
                .Select(setting => setting.Value)
                .FirstOrDefaultAsync(cancellationToken) ??
                ProductBrand.InstitutionName;

            return new ReportResult
            {
                ReportTitle = Definition.Title,
                InstitutionName = institutionName,
                GeneratedAt = DateTime.UtcNow,
                GeneratedBy = string.IsNullOrWhiteSpace(generatedBy)
                    ? "Unknown User"
                    : generatedBy,
                AppliedFilters = FilterReader.Describe(filters),
                Columns = Definition.Columns,
                Rows = rows,
                TotalCount = rows.Count,
                SummaryItems = summary ??
                    new Dictionary<string, string>
                    {
                        ["Total Records"] = rows.Count.ToString(
                            CultureInfo.InvariantCulture)
                    }
            };
        }

        protected static ReportRow Row(params (string Key, object? Value)[] values)
        {
            var row = new ReportRow();
            foreach (var value in values)
            {
                row[value.Key] = value.Value;
            }
            return row;
        }

        protected static ReportColumn Column(
            string key,
            string header,
            string? format = null)
        {
            return new ReportColumn
            {
                Key = key,
                Header = header,
                Format = format
            };
        }

        protected static ReportFilter Filter(
            string key,
            string label,
            ReportFilterType type,
            params string[] options)
        {
            return new ReportFilter
            {
                Key = key,
                Label = label,
                Type = type,
                Options = options
            };
        }

        protected static bool TextMatches(string? filter, params string?[] values)
        {
            return string.IsNullOrWhiteSpace(filter) ||
                values.Any(value =>
                    value?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true);
        }

        protected static bool ExactMatches(string? filter, string? value)
        {
            return string.IsNullOrWhiteSpace(filter) ||
                string.Equals(filter, value, StringComparison.OrdinalIgnoreCase);
        }

        protected static DateTime AsLocalDate(DateTime value)
        {
            var utc = value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
            return utc.ToLocalTime().Date;
        }
    }

    internal static class FilterReader
    {
        public static string? Text(
            IReadOnlyCollection<ReportFilter> filters,
            string key)
        {
            var value = Find(filters, key)?.Value?.ToString()?.Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        public static bool Boolean(
            IReadOnlyCollection<ReportFilter> filters,
            string key)
        {
            var value = Find(filters, key)?.Value;
            return value is bool boolean
                ? boolean
                : bool.TryParse(value?.ToString(), out var parsed) && parsed;
        }

        public static DateTime? StartDate(
            IReadOnlyCollection<ReportFilter> filters,
            string key)
        {
            return ToDate(Find(filters, key)?.Value);
        }

        public static DateTime? EndDate(
            IReadOnlyCollection<ReportFilter> filters,
            string key)
        {
            return ToDate(Find(filters, key)?.SecondaryValue);
        }

        public static decimal? MinimumDecimal(
            IReadOnlyCollection<ReportFilter> filters,
            string key)
        {
            return ToDecimal(Find(filters, key)?.Value);
        }

        public static decimal? MaximumDecimal(
            IReadOnlyCollection<ReportFilter> filters,
            string key)
        {
            return ToDecimal(Find(filters, key)?.SecondaryValue);
        }

        public static int? MinimumInteger(
            IReadOnlyCollection<ReportFilter> filters,
            string key)
        {
            return ToInteger(Find(filters, key)?.Value);
        }

        public static int? MaximumInteger(
            IReadOnlyCollection<ReportFilter> filters,
            string key)
        {
            return ToInteger(Find(filters, key)?.SecondaryValue);
        }

        public static IReadOnlyList<string> Describe(
            IReadOnlyCollection<ReportFilter> filters)
        {
            var descriptions = new List<string>();
            foreach (var filter in filters)
            {
                var first = Format(filter.Value);
                var second = Format(filter.SecondaryValue);
                if (string.IsNullOrWhiteSpace(first) &&
                    string.IsNullOrWhiteSpace(second))
                {
                    continue;
                }

                descriptions.Add(string.IsNullOrWhiteSpace(second)
                    ? $"{filter.Label}: {first}"
                    : $"{filter.Label}: {first} to {second}");
            }
            return descriptions;
        }

        private static ReportFilter? Find(
            IEnumerable<ReportFilter> filters,
            string key)
        {
            return filters.FirstOrDefault(filter =>
                filter.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        }

        private static DateTime? ToDate(object? value)
        {
            return value is DateTime date
                ? date
                : DateTime.TryParse(
                    value?.ToString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal,
                    out var parsed)
                    ? parsed
                    : null;
        }

        private static decimal? ToDecimal(object? value)
        {
            return value is decimal decimalValue
                ? decimalValue
                : decimal.TryParse(
                    value?.ToString(),
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var parsed)
                    ? parsed
                    : null;
        }

        private static int? ToInteger(object? value)
        {
            return value is int integer
                ? integer
                : int.TryParse(
                    value?.ToString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var parsed)
                    ? parsed
                    : null;
        }

        private static string Format(object? value)
        {
            return value switch
            {
                null => string.Empty,
                DateTime date => date.ToString("dd-MMM-yyyy"),
                bool boolean => boolean ? "Yes" : "No",
                _ => value.ToString() ?? string.Empty
            };
        }
    }
}
