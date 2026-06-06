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
    public sealed class FineReportDataProvider : ReportDataProviderBase
    {
        private static readonly ReportDefinition ReportDefinition = new()
        {
            Key = ReportKeys.Fines,
            Title = "Fine Report",
            Description = "Fine balances, payments, and member details.",
            Columns =
            [
                Column("FineId", "Fine Id"),
                Column("AccessionNumber", "Accession Number"),
                Column("BookTitle", "Book Title"),
                Column("MemberType", "Member Type"),
                Column("MemberName", "Member Name"),
                Column("MemberCode", "Member Code"),
                Column("FineType", "Fine Type"),
                Column("FineAmount", "Fine Amount", "N2"),
                Column("PaidAmount", "Paid Amount", "N2"),
                Column("RemainingAmount", "Remaining Amount", "N2"),
                Column("PaymentStatus", "Payment Status"),
                Column("CreatedAt", "Created At", "dd-MMM-yyyy HH:mm")
            ],
            Filters =
            [
                Filter(ReportFilterKeys.SearchText, "Search Text", ReportFilterType.Text),
                Filter(ReportFilterKeys.MemberType, "Member Type", ReportFilterType.Enum, Enum.GetNames<MemberType>()),
                Filter(ReportFilterKeys.PaymentStatus, "Payment Status", ReportFilterType.Enum, Enum.GetNames<FineStatus>()),
                Filter(ReportFilterKeys.DateRange, "Created Date", ReportFilterType.DateRange),
                Filter(ReportFilterKeys.FineAmount, "Fine Amount", ReportFilterType.NumberRange)
            ]
        };

        public FineReportDataProvider(KicsitLibraryDbContext context)
            : base(context)
        {
        }

        public override ReportDefinition Definition => ReportDefinition;

        public override async Task<ReportResult> GenerateAsync(
            IReadOnlyCollection<ReportFilter> filters,
            string generatedBy,
            CancellationToken cancellationToken = default)
        {
            var fines = await Context.Fines
                .AsNoTracking()
                .Include(fine => fine.Student)
                .Include(fine => fine.FacultyStaff)
                .Include(fine => fine.IssueRecord)
                    .ThenInclude(issue => issue.BookCopy)
                    .ThenInclude(copy => copy.BookMaster)
                .OrderByDescending(fine => fine.CreatedAt)
                .ToListAsync(cancellationToken);

            var search = FilterReader.Text(filters, ReportFilterKeys.SearchText);
            var memberType = FilterReader.Text(filters, ReportFilterKeys.MemberType);
            var paymentStatus = FilterReader.Text(filters, ReportFilterKeys.PaymentStatus);
            var fromDate = FilterReader.StartDate(filters, ReportFilterKeys.DateRange);
            var toDate = FilterReader.EndDate(filters, ReportFilterKeys.DateRange);
            var minimumAmount = FilterReader.MinimumDecimal(filters, ReportFilterKeys.FineAmount);
            var maximumAmount = FilterReader.MaximumDecimal(filters, ReportFilterKeys.FineAmount);

            var rows = fines.Where(fine =>
            {
                var memberName = fine.MemberType == MemberType.Student
                    ? fine.Student?.Name
                    : fine.FacultyStaff?.Name;
                var memberCode = fine.MemberType == MemberType.Student
                    ? fine.Student?.RegistrationNumber
                    : fine.FacultyStaff?.PersonnelNumber;
                return Matches(
                        search,
                        fine.AccessionNumber,
                        fine.FineRecordNumber,
                        fine.IssueRecord.BookCopy.BookMaster.Title,
                        memberName,
                        memberCode) &&
                    MatchesExact(memberType, fine.MemberType.ToString()) &&
                    MatchesExact(paymentStatus, fine.PaymentStatus.ToString()) &&
                    (!fromDate.HasValue || fine.CreatedAt.ToLocalTime().Date >= fromDate.Value.Date) &&
                    (!toDate.HasValue || fine.CreatedAt.ToLocalTime().Date <= toDate.Value.Date) &&
                    (!minimumAmount.HasValue || fine.FineAmount >= minimumAmount.Value) &&
                    (!maximumAmount.HasValue || fine.FineAmount <= maximumAmount.Value);
            })
            .Select(fine => Row(
                ("FineId", fine.Id),
                ("AccessionNumber", fine.AccessionNumber),
                ("BookTitle", fine.IssueRecord.BookCopy.BookMaster.Title),
                ("MemberType", fine.MemberType.ToString()),
                ("MemberName", fine.MemberType == MemberType.Student
                    ? fine.Student?.Name
                    : fine.FacultyStaff?.Name),
                ("MemberCode", fine.MemberType == MemberType.Student
                    ? fine.Student?.RegistrationNumber
                    : fine.FacultyStaff?.PersonnelNumber),
                ("FineType", fine.FineType),
                ("FineAmount", fine.FineAmount),
                ("PaidAmount", fine.PaidAmount),
                ("RemainingAmount", fine.RemainingAmount),
                ("PaymentStatus", fine.PaymentStatus.ToString()),
                ("CreatedAt", fine.CreatedAt.ToLocalTime())))
            .ToList();

            return await CreateResultAsync(
                filters,
                generatedBy,
                rows,
                new Dictionary<string, string>
                {
                    ["Fine Records"] = rows.Count.ToString(),
                    ["Total Fine"] = Sum(rows, "FineAmount").ToString("N2"),
                    ["Paid"] = Sum(rows, "PaidAmount").ToString("N2"),
                    ["Remaining"] = Sum(rows, "RemainingAmount").ToString("N2")
                },
                cancellationToken);
        }

        private static decimal Sum(IEnumerable<ReportRow> rows, string key)
        {
            return rows.Sum(row => Convert.ToDecimal(row[key] ?? 0));
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
