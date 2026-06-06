using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Helpers;
using KicsitLibrary.Data;
using KicsitLibrary.Reports.Models;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Reports.Providers
{
    public sealed class OverdueBooksReportDataProvider : ReportDataProviderBase
    {
        private static readonly ReportDefinition ReportDefinition = new()
        {
            Key = ReportKeys.OverdueBooks,
            Title = "Overdue Books Report",
            Description = "Active overdue issues with current fine and reminder status.",
            Columns =
            [
                Column("IssueRecordId", "Issue Record Id"),
                Column("AccessionNumber", "Accession Number"),
                Column("BookTitle", "Book Title"),
                Column("MemberType", "Member Type"),
                Column("MemberName", "Member Name"),
                Column("MemberCode", "Member Code"),
                Column("ExpectedReturnDate", "Expected Return Date", "dd-MMM-yyyy"),
                Column("DaysOverdue", "Days Overdue"),
                Column("CurrentFine", "Current Fine", "N2"),
                Column("LastNotificationDate", "Last Notification Date", "dd-MMM-yyyy HH:mm"),
                Column("NotificationStatus", "Notification Status")
            ],
            Filters =
            [
                Filter(ReportFilterKeys.SearchText, "Search Text", ReportFilterType.Text),
                Filter(ReportFilterKeys.MemberType, "Member Type", ReportFilterType.Enum, Enum.GetNames<MemberType>()),
                Filter(ReportFilterKeys.DaysOverdue, "Days Overdue", ReportFilterType.NumberRange),
                Filter(ReportFilterKeys.FineAmount, "Current Fine", ReportFilterType.NumberRange)
            ]
        };

        public OverdueBooksReportDataProvider(KicsitLibraryDbContext context)
            : base(context)
        {
        }

        public override ReportDefinition Definition => ReportDefinition;

        public override async Task<ReportResult> GenerateAsync(
            IReadOnlyCollection<ReportFilter> filters,
            string generatedBy,
            CancellationToken cancellationToken = default)
        {
            var localToday = DateTime.Now.Date;
            var cutoffUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(localToday, DateTimeKind.Unspecified),
                TimeZoneInfo.Local);
            var issues = await Context.IssueRecords
                .AsNoTracking()
                .Include(issue => issue.ReceiveRecord)
                .Include(issue => issue.BookCopy)
                    .ThenInclude(copy => copy.BookMaster)
                .Include(issue => issue.Student)
                .Include(issue => issue.FacultyStaff)
                .Where(issue =>
                    issue.ReceiveRecord == null &&
                    issue.ExpectedReturnDate < cutoffUtc)
                .OrderBy(issue => issue.ExpectedReturnDate)
                .ToListAsync(cancellationToken);

            var issueIds = issues.Select(issue => issue.Id).ToList();
            var notifications = await Context.NotificationRecords
                .AsNoTracking()
                .Where(notification =>
                    notification.IssueRecordId.HasValue &&
                    issueIds.Contains(notification.IssueRecordId.Value) &&
                    notification.NotificationType == NotificationType.OverdueReminder)
                .OrderByDescending(notification => notification.CreatedAt)
                .ToListAsync(cancellationToken);

            var search = FilterReader.Text(filters, ReportFilterKeys.SearchText);
            var memberType = FilterReader.Text(filters, ReportFilterKeys.MemberType);
            var minimumDays = FilterReader.MinimumInteger(filters, ReportFilterKeys.DaysOverdue);
            var maximumDays = FilterReader.MaximumInteger(filters, ReportFilterKeys.DaysOverdue);
            var minimumFine = FilterReader.MinimumDecimal(filters, ReportFilterKeys.FineAmount);
            var maximumFine = FilterReader.MaximumDecimal(filters, ReportFilterKeys.FineAmount);

            var rows = issues.Select(issue =>
            {
                var memberName = issue.MemberType == MemberType.Student
                    ? issue.Student?.Name
                    : issue.FacultyStaff?.Name;
                var memberCode = issue.MemberType == MemberType.Student
                    ? issue.Student?.RegistrationNumber
                    : issue.FacultyStaff?.PersonnelNumber;
                var dueDate = ToLocalDate(issue.ExpectedReturnDate);
                var daysOverdue = OverdueCalculator.CalculateOverdueDays(
                    dueDate,
                    localToday);
                var currentFine = OverdueCalculator.CalculateFine(
                    daysOverdue,
                    issue.FinePerDay);
                var lastNotification = notifications
                    .FirstOrDefault(item => item.IssueRecordId == issue.Id);

                return new
                {
                    Issue = issue,
                    MemberName = memberName,
                    MemberCode = memberCode,
                    DueDate = dueDate,
                    DaysOverdue = daysOverdue,
                    CurrentFine = currentFine,
                    LastNotification = lastNotification
                };
            })
            .Where(item =>
                Matches(
                    search,
                    item.Issue.AccessionNumber,
                    item.Issue.BookCopy.BookMaster.Title,
                    item.MemberName,
                    item.MemberCode) &&
                MatchesExact(memberType, item.Issue.MemberType.ToString()) &&
                (!minimumDays.HasValue || item.DaysOverdue >= minimumDays.Value) &&
                (!maximumDays.HasValue || item.DaysOverdue <= maximumDays.Value) &&
                (!minimumFine.HasValue || item.CurrentFine >= minimumFine.Value) &&
                (!maximumFine.HasValue || item.CurrentFine <= maximumFine.Value))
            .Select(item => Row(
                ("IssueRecordId", item.Issue.Id),
                ("AccessionNumber", item.Issue.AccessionNumber),
                ("BookTitle", item.Issue.BookCopy.BookMaster.Title),
                ("MemberType", item.Issue.MemberType.ToString()),
                ("MemberName", item.MemberName),
                ("MemberCode", item.MemberCode),
                ("ExpectedReturnDate", item.DueDate),
                ("DaysOverdue", item.DaysOverdue),
                ("CurrentFine", item.CurrentFine),
                ("LastNotificationDate", item.LastNotification?.CreatedAt.ToLocalTime()),
                ("NotificationStatus", item.LastNotification?.Status.ToString() ?? "None")))
            .ToList();

            return await CreateResultAsync(
                filters,
                generatedBy,
                rows,
                new Dictionary<string, string>
                {
                    ["Overdue Issues"] = rows.Count.ToString(),
                    ["Current Fine Total"] = rows
                        .Sum(row => Convert.ToDecimal(row["CurrentFine"] ?? 0))
                        .ToString("N2")
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
