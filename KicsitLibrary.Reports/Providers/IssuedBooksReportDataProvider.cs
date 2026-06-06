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
    public sealed class IssuedBooksReportDataProvider : ReportDataProviderBase
    {
        private static readonly ReportDefinition ReportDefinition = new()
        {
            Key = ReportKeys.IssuedBooks,
            Title = "Issued Books Report",
            Description = "Active issue records and their current due status.",
            Category = "Circulation Reports",
            Columns =
            [
                Column("IssueRecordId", "Issue Record Id"),
                Column("AccessionNumber", "Accession Number"),
                Column("BookTitle", "Book Title"),
                Column("MemberType", "Member Type"),
                Column("MemberName", "Member Name"),
                Column("MemberCode", "Member Code"),
                Column("IssueDate", "Issue Date", "dd-MMM-yyyy"),
                Column("ExpectedReturnDate", "Expected Return Date", "dd-MMM-yyyy"),
                Column("FinePerDay", "Fine Per Day", "N2"),
                Column("Status", "Status")
            ],
            Filters =
            [
                Filter(ReportFilterKeys.SearchText, "Search Text", ReportFilterType.Text),
                Filter(ReportFilterKeys.MemberType, "Member Type", ReportFilterType.Enum, Enum.GetNames<MemberType>()),
                Filter(ReportFilterKeys.DateRange, "Issue Date", ReportFilterType.DateRange),
                Filter(ReportFilterKeys.OverdueOnly, "Overdue Only", ReportFilterType.Boolean)
            ]
        };

        public IssuedBooksReportDataProvider(KicsitLibraryDbContext context)
            : base(context)
        {
        }

        public override ReportDefinition Definition => ReportDefinition;

        public override async Task<ReportResult> GenerateAsync(
            IReadOnlyCollection<ReportFilter> filters,
            string generatedBy,
            CancellationToken cancellationToken = default)
        {
            var issues = await Context.IssueRecords
                .AsNoTracking()
                .Include(issue => issue.ReceiveRecord)
                .Include(issue => issue.BookCopy)
                    .ThenInclude(copy => copy.BookMaster)
                .Include(issue => issue.Student)
                .Include(issue => issue.FacultyStaff)
                .Where(issue => issue.ReceiveRecord == null)
                .OrderByDescending(issue => issue.IssueDate)
                .ToListAsync(cancellationToken);

            var search = FilterReader.Text(filters, ReportFilterKeys.SearchText);
            var memberType = FilterReader.Text(filters, ReportFilterKeys.MemberType);
            var fromDate = FilterReader.StartDate(filters, ReportFilterKeys.DateRange);
            var toDate = FilterReader.EndDate(filters, ReportFilterKeys.DateRange);
            var overdueOnly = FilterReader.Boolean(filters, ReportFilterKeys.OverdueOnly);
            var localToday = DateTime.Now.Date;

            var filtered = issues.Where(issue =>
            {
                var memberName = issue.MemberType == MemberType.Student
                    ? issue.Student?.Name
                    : issue.FacultyStaff?.Name;
                var memberCode = issue.MemberType == MemberType.Student
                    ? issue.Student?.RegistrationNumber
                    : issue.FacultyStaff?.PersonnelNumber;
                var dueDate = ToLocalDate(issue.ExpectedReturnDate);
                return Matches(search, issue.AccessionNumber, issue.BookCopy.BookMaster.Title, memberName, memberCode) &&
                    MatchesExact(memberType, issue.MemberType.ToString()) &&
                    (!fromDate.HasValue || ToLocalDate(issue.IssueDate) >= fromDate.Value.Date) &&
                    (!toDate.HasValue || ToLocalDate(issue.IssueDate) <= toDate.Value.Date) &&
                    (!overdueOnly || dueDate < localToday);
            });

            var rows = filtered.Select(issue =>
            {
                var overdue = ToLocalDate(issue.ExpectedReturnDate) < localToday;
                return Row(
                    ("IssueRecordId", issue.Id),
                    ("AccessionNumber", issue.AccessionNumber),
                    ("BookTitle", issue.BookCopy.BookMaster.Title),
                    ("MemberType", issue.MemberType.ToString()),
                    ("MemberName", issue.MemberType == MemberType.Student
                        ? issue.Student?.Name
                        : issue.FacultyStaff?.Name),
                    ("MemberCode", issue.MemberType == MemberType.Student
                        ? issue.Student?.RegistrationNumber
                        : issue.FacultyStaff?.PersonnelNumber),
                    ("IssueDate", ToLocalDate(issue.IssueDate)),
                    ("ExpectedReturnDate", ToLocalDate(issue.ExpectedReturnDate)),
                    ("FinePerDay", issue.FinePerDay),
                    ("Status", overdue ? "Overdue" : "Issued"));
            }).ToList();

            return await CreateResultAsync(
                filters,
                generatedBy,
                rows,
                new Dictionary<string, string>
                {
                    ["Active Issues"] = rows.Count.ToString(),
                    ["Overdue"] = rows.Count(row =>
                        Equals(row["Status"], "Overdue")).ToString()
                },
                cancellationToken);
        }

        private static DateTime ToLocalDate(DateTime value)
        {
            var utc = value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
            return utc.ToLocalTime().Date;
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
