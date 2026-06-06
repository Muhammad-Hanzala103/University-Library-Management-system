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
    public sealed class NotificationReportDataProvider : ReportDataProviderBase
    {
        private static readonly ReportDefinition ReportDefinition = new()
        {
            Key = ReportKeys.Notifications,
            Title = "Notification Report",
            Description = "Notification delivery state, retries, and failures.",
            Category = "Notification Reports",
            Columns =
            [
                Column("NotificationId", "Notification Id"),
                Column("MemberType", "Member Type"),
                Column("RecipientName", "Recipient Name"),
                Column("RecipientCode", "Recipient Code"),
                Column("RecipientEmail", "Recipient Email"),
                Column("NotificationType", "Notification Type"),
                Column("Channel", "Channel"),
                Column("Status", "Status"),
                Column("RetryCount", "Retry Count"),
                Column("CreatedAt", "Created At", "dd-MMM-yyyy HH:mm"),
                Column("SentAt", "Sent At", "dd-MMM-yyyy HH:mm"),
                Column("FailureReason", "Failure Reason")
            ],
            Filters =
            [
                Filter(ReportFilterKeys.SearchText, "Search Text", ReportFilterType.Text),
                Filter(ReportFilterKeys.Channel, "Channel", ReportFilterType.Enum, "InApp", "Email", "WhatsApp"),
                Filter(ReportFilterKeys.NotificationType, "Notification Type", ReportFilterType.Enum, Enum.GetNames<NotificationType>()),
                Filter(ReportFilterKeys.Status, "Status", ReportFilterType.Enum, Enum.GetNames<NotificationStatus>()),
                Filter(ReportFilterKeys.MemberType, "Member Type", ReportFilterType.Enum, Enum.GetNames<MemberType>()),
                Filter(ReportFilterKeys.DateRange, "Created Date", ReportFilterType.DateRange)
            ]
        };

        public NotificationReportDataProvider(KicsitLibraryDbContext context)
            : base(context)
        {
        }

        public override ReportDefinition Definition => ReportDefinition;

        public override async Task<ReportResult> GenerateAsync(
            IReadOnlyCollection<ReportFilter> filters,
            string generatedBy,
            CancellationToken cancellationToken = default)
        {
            var notifications = await Context.NotificationRecords
                .AsNoTracking()
                .OrderByDescending(notification => notification.CreatedAt)
                .ToListAsync(cancellationToken);

            var search = FilterReader.Text(filters, ReportFilterKeys.SearchText);
            var channel = FilterReader.Text(filters, ReportFilterKeys.Channel);
            var notificationType = FilterReader.Text(filters, ReportFilterKeys.NotificationType);
            var status = FilterReader.Text(filters, ReportFilterKeys.Status);
            var memberType = FilterReader.Text(filters, ReportFilterKeys.MemberType);
            var fromDate = FilterReader.StartDate(filters, ReportFilterKeys.DateRange);
            var toDate = FilterReader.EndDate(filters, ReportFilterKeys.DateRange);

            var rows = notifications.Where(notification =>
                    Matches(
                        search,
                        notification.RecipientName,
                        notification.RecipientCode,
                        notification.RecipientEmail,
                        notification.Subject,
                        notification.FailureReason) &&
                    MatchesExact(channel, notification.Channel) &&
                    MatchesExact(notificationType, notification.NotificationType.ToString()) &&
                    MatchesExact(status, notification.Status.ToString()) &&
                    MatchesExact(memberType, notification.MemberType.ToString()) &&
                    (!fromDate.HasValue || notification.CreatedAt.ToLocalTime().Date >= fromDate.Value.Date) &&
                    (!toDate.HasValue || notification.CreatedAt.ToLocalTime().Date <= toDate.Value.Date))
                .Select(notification => Row(
                    ("NotificationId", notification.Id),
                    ("MemberType", notification.MemberType.ToString()),
                    ("RecipientName", notification.RecipientName),
                    ("RecipientCode", notification.RecipientCode),
                    ("RecipientEmail", notification.RecipientEmail),
                    ("NotificationType", notification.NotificationType.ToString()),
                    ("Channel", notification.Channel),
                    ("Status", notification.Status.ToString()),
                    ("RetryCount", notification.RetryCount),
                    ("CreatedAt", notification.CreatedAt.ToLocalTime()),
                    ("SentAt", notification.SentAt?.ToLocalTime()),
                    ("FailureReason", notification.FailureReason)))
                .ToList();

            return await CreateResultAsync(
                filters,
                generatedBy,
                rows,
                new Dictionary<string, string>
                {
                    ["Notification Records"] = rows.Count.ToString(),
                    ["Pending"] = Count(rows, NotificationStatus.Pending),
                    ["Sent"] = Count(rows, NotificationStatus.Sent),
                    ["Failed"] = Count(rows, NotificationStatus.Failed)
                },
                cancellationToken);
        }

        private static string Count(
            IEnumerable<ReportRow> rows,
            NotificationStatus status)
        {
            return rows.Count(row =>
                Equals(row["Status"], status.ToString())).ToString();
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
