using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Helpers;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Services.Notifications
{
    public class OverdueService : IOverdueService
    {
        private static readonly string[] ReminderChannels = ["InApp", "Email"];

        private readonly KicsitLibraryDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly IActivityLogService _logService;

        public OverdueService(
            KicsitLibraryDbContext context,
            INotificationService notificationService,
            IActivityLogService logService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        public async Task<IReadOnlyList<OverdueItem>> GetOverdueItemsAsync(
            DateTime? localDate = null,
            CancellationToken cancellationToken = default)
        {
            var localToday = (localDate ?? DateTime.Now).Date;
            var overdueCutoffUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(localToday, DateTimeKind.Unspecified),
                TimeZoneInfo.Local);

            var issues = await _context.IssueRecords
                .AsNoTracking()
                .Include(ir => ir.ReceiveRecord)
                .Include(ir => ir.BookCopy)
                    .ThenInclude(bc => bc.BookMaster)
                .Include(ir => ir.Student)
                .Include(ir => ir.FacultyStaff)
                .Where(ir =>
                    ir.ReceiveRecord == null &&
                    ir.ExpectedReturnDate < overdueCutoffUtc)
                .OrderBy(ir => ir.ExpectedReturnDate)
                .ToListAsync(cancellationToken);

            if (issues.Count == 0)
            {
                return Array.Empty<OverdueItem>();
            }

            var issueIds = issues.Select(ir => ir.Id).ToList();
            var notifications = await _context.NotificationRecords
                .AsNoTracking()
                .Where(nr =>
                    nr.IssueRecordId.HasValue &&
                    issueIds.Contains(nr.IssueRecordId.Value) &&
                    nr.NotificationType == NotificationType.OverdueReminder)
                .OrderByDescending(nr => nr.CreatedAt)
                .ToListAsync(cancellationToken);
            var cooldownHours = await GetCooldownHoursAsync();

            var results = new List<OverdueItem>(issues.Count);
            foreach (var issue in issues)
            {
                var memberId = issue.MemberType == MemberType.Student
                    ? issue.StudentId ?? 0
                    : issue.FacultyStaffId ?? 0;
                var memberName = issue.MemberType == MemberType.Student
                    ? issue.Student?.Name ?? string.Empty
                    : issue.FacultyStaff?.Name ?? string.Empty;
                var memberCode = issue.MemberType == MemberType.Student
                    ? issue.Student?.RegistrationNumber ?? string.Empty
                    : issue.FacultyStaff?.PersonnelNumber ?? string.Empty;
                var memberEmail = issue.MemberType == MemberType.Student
                    ? issue.Student?.Email ?? string.Empty
                    : issue.FacultyStaff?.Email ?? string.Empty;

                var issueNotifications = notifications
                    .Where(nr => nr.IssueRecordId == issue.Id)
                    .ToList();
                var lastNotification = issueNotifications.FirstOrDefault();
                var channelEligibility = ReminderChannels
                    .Select(channel => CanCreateFromSnapshot(
                        issueNotifications.FirstOrDefault(nr => nr.Channel == channel),
                        cooldownHours,
                        DateTime.UtcNow))
                    .ToList();
                var canCreate = memberId > 0 && channelEligibility.Any(result => result.CanCreate);

                var expectedLocalDate = ToLocalDate(issue.ExpectedReturnDate);
                var daysOverdue = OverdueCalculator.CalculateOverdueDays(expectedLocalDate, localToday);

                results.Add(new OverdueItem
                {
                    IssueRecordId = issue.Id,
                    BookCopyId = issue.BookCopyId,
                    BookMasterId = issue.BookCopy.BookMasterId,
                    AccessionNumber = issue.AccessionNumber,
                    BookTitle = issue.BookCopy.BookMaster.Title,
                    MemberType = issue.MemberType,
                    MemberId = memberId,
                    MemberName = memberName,
                    MemberCode = memberCode,
                    MemberEmail = memberEmail,
                    IssueDate = issue.IssueDate,
                    ExpectedReturnDate = issue.ExpectedReturnDate,
                    DaysOverdue = daysOverdue,
                    FinePerDay = Math.Max(0, issue.FinePerDay),
                    CurrentFineAmount = OverdueCalculator.CalculateFine(daysOverdue, issue.FinePerDay),
                    LastNotificationDate = lastNotification?.CreatedAt,
                    NotificationStatus = lastNotification?.Status,
                    CanSendReminder = canCreate,
                    ReasonIfCannotSend = memberId == 0
                        ? "Borrower record is missing."
                        : canCreate
                            ? string.Empty
                            : channelEligibility.First(result => !result.CanCreate).Reason
                });
            }

            return results;
        }

        public async Task<OverdueProcessingResult> ProcessOverdueNotificationsAsync(
            int? userId = null,
            CancellationToken cancellationToken = default)
        {
            var result = new OverdueProcessingResult();
            await _logService.LogActivityAsync(
                "Overdue Processing Started",
                "Manual overdue notification processing started.",
                userId);

            try
            {
                var items = await GetOverdueItemsAsync(
                    cancellationToken: cancellationToken);
                foreach (var item in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    result.Add(await CreateReminderForItemAsync(
                        item,
                        userId,
                        cancellationToken));
                }

                result.Message =
                    $"Processed {result.ProcessedCount}; created {result.CreatedCount}; " +
                    $"skipped {result.SkippedCount}; failed {result.FailedCount}.";
                await _logService.LogActivityAsync(
                    "Overdue Processing Completed",
                    result.Message,
                    userId);
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (SqliteRetryPolicy.IsTransient(ex))
            {
                throw;
            }
            catch (Exception ex)
            {
                result.FailedCount++;
                result.Message = $"Overdue processing failed: {ex.Message}";
                await _logService.LogActivityAsync(
                    "Overdue Processing Error",
                    result.Message,
                    userId);
                return result;
            }
        }

        public async Task<OverdueProcessingResult> CreateReminderForIssueAsync(
            int issueRecordId,
            int? userId = null)
        {
            var item = (await GetOverdueItemsAsync())
                .FirstOrDefault(overdue => overdue.IssueRecordId == issueRecordId);
            if (item == null)
            {
                return new OverdueProcessingResult
                {
                    ProcessedCount = 1,
                    SkippedCount = 1,
                    Message = "The issue record is not active and overdue."
                };
            }

            return await CreateReminderForItemAsync(
                item,
                userId,
                CancellationToken.None);
        }

        public async Task<NotificationRecord?> GetLastReminderAsync(int issueRecordId)
        {
            return await _context.NotificationRecords
                .AsNoTracking()
                .Where(nr =>
                    nr.IssueRecordId == issueRecordId &&
                    nr.NotificationType == NotificationType.OverdueReminder)
                .OrderByDescending(nr => nr.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<NotificationEligibilityResult> CanCreateReminderAsync(
            int issueRecordId,
            DateTime? asOfUtc = null)
        {
            var issue = await _context.IssueRecords
                .AsNoTracking()
                .Include(ir => ir.ReceiveRecord)
                .FirstOrDefaultAsync(ir => ir.Id == issueRecordId);

            if (issue == null || issue.ReceiveRecord != null)
            {
                return new NotificationEligibilityResult
                {
                    CanCreate = false,
                    Reason = "The issue record is not active."
                };
            }

            if (ToLocalDate(issue.ExpectedReturnDate) >= DateTime.Now.Date)
            {
                return new NotificationEligibilityResult
                {
                    CanCreate = false,
                    Reason = "The issue record is not overdue."
                };
            }

            var cooldownHours = await GetCooldownHoursAsync();
            foreach (var channel in ReminderChannels)
            {
                var eligibility = await _notificationService.CanCreateNotificationAsync(
                    issueRecordId,
                    NotificationType.OverdueReminder,
                    channel,
                    cooldownHours,
                    asOfUtc);
                if (eligibility.CanCreate)
                {
                    return eligibility;
                }
            }

            return new NotificationEligibilityResult
            {
                CanCreate = false,
                Reason = "Reminder records already exist for all supported channels within the cooldown window.",
                LastNotification = await GetLastReminderAsync(issueRecordId)
            };
        }

        private async Task<OverdueProcessingResult> CreateReminderForItemAsync(
            OverdueItem item,
            int? userId,
            CancellationToken cancellationToken)
        {
            var result = new OverdueProcessingResult { ProcessedCount = 1 };
            var cooldownHours = await GetCooldownHoursAsync();

            foreach (var channel in ReminderChannels)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var eligibility = await _notificationService.CanCreateNotificationAsync(
                        item.IssueRecordId,
                        NotificationType.OverdueReminder,
                        channel,
                        cooldownHours);
                    if (!eligibility.CanCreate)
                    {
                        result.SkippedCount++;
                        await _logService.LogActivityAsync(
                            "Overdue Reminder Skipped",
                            $"{channel} reminder skipped for issue {item.IssueRecordId}: {eligibility.Reason}",
                            userId);
                        continue;
                    }

                    var missingEmail = channel == "Email" && string.IsNullOrWhiteSpace(item.MemberEmail);
                    var notification = CreateNotificationRecord(item, channel, missingEmail);
                    var created = await _notificationService.CreateNotificationAsync(
                        notification,
                        cooldownHours,
                        userId);

                    if (!created.Created)
                    {
                        result.SkippedCount++;
                        await _logService.LogActivityAsync(
                            "Overdue Reminder Skipped",
                            $"{channel} reminder skipped for issue {item.IssueRecordId}: {created.Message}",
                            userId);
                        continue;
                    }

                    result.CreatedCount++;
                    if (missingEmail)
                    {
                        result.FailedCount++;
                        await _logService.LogActivityAsync(
                            "Overdue Reminder Missing Email",
                            $"Email reminder for issue {item.IssueRecordId} was recorded as failed because the member email is missing.",
                            userId);
                    }

                    await _logService.LogActivityAsync(
                        "Overdue Reminder Created",
                        $"{channel} reminder created for issue {item.IssueRecordId} and member {item.MemberCode}.",
                        userId);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (SqliteRetryPolicy.IsTransient(ex))
                {
                    throw;
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    await _logService.LogActivityAsync(
                        "Overdue Reminder Error",
                        $"{channel} reminder failed for issue {item.IssueRecordId}: {ex.Message}",
                        userId);
                }
            }

            result.Message =
                $"Created {result.CreatedCount}; skipped {result.SkippedCount}; failed {result.FailedCount}.";
            return result;
        }

        private static NotificationRecord CreateNotificationRecord(
            OverdueItem item,
            string channel,
            bool missingEmail)
        {
            return new NotificationRecord
            {
                IssueRecordId = item.IssueRecordId,
                MemberType = item.MemberType,
                StudentId = item.MemberType == MemberType.Student ? item.MemberId : null,
                FacultyStaffId = item.MemberType == MemberType.FacultyStaff ? item.MemberId : null,
                NotificationType = NotificationType.OverdueReminder,
                Channel = channel,
                RecipientName = item.MemberName,
                RecipientCode = item.MemberCode,
                RecipientEmail = string.IsNullOrWhiteSpace(item.MemberEmail) ? null : item.MemberEmail,
                Subject = $"Overdue library item: {item.BookTitle}",
                Message =
                    $"Accession {item.AccessionNumber} was due on " +
                    $"{ToLocalDate(item.ExpectedReturnDate):dd-MMM-yyyy}. " +
                    $"It is {item.DaysOverdue} day(s) overdue with a current fine of Rs. {item.CurrentFineAmount:N0}.",
                Status = missingEmail ? NotificationStatus.Failed : NotificationStatus.Pending,
                FailureReason = missingEmail
                    ? "Member email is missing."
                    : channel == "Email"
                        ? "Email delivery pending."
                        : "In-app delivery pending."
            };
        }

        private async Task<int> GetCooldownHoursAsync()
        {
            var value = await _context.SystemSettings
                .Where(setting => setting.Key == "NotificationCooldownHours")
                .Select(setting => setting.Value)
                .FirstOrDefaultAsync();
            return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : 24;
        }

        private static NotificationEligibilityResult CanCreateFromSnapshot(
            NotificationRecord? lastNotification,
            int cooldownHours,
            DateTime nowUtc)
        {
            if (lastNotification == null)
            {
                return new NotificationEligibilityResult { CanCreate = true };
            }

            var lastCreatedUtc = EnsureUtc(lastNotification.CreatedAt);
            var sameLocalDate = lastCreatedUtc.ToLocalTime().Date == nowUtc.ToLocalTime().Date;
            var inCooldown = nowUtc - lastCreatedUtc < TimeSpan.FromHours(Math.Max(1, cooldownHours));

            return sameLocalDate || inCooldown
                ? new NotificationEligibilityResult
                {
                    CanCreate = false,
                    Reason = "A reminder already exists today or within the cooldown window.",
                    LastNotification = lastNotification
                }
                : new NotificationEligibilityResult
                {
                    CanCreate = true,
                    LastNotification = lastNotification
                };
        }

        private static DateTime ToLocalDate(DateTime value)
        {
            var utcValue = EnsureUtc(value);
            return utcValue.ToLocalTime().Date;
        }

        private static DateTime EnsureUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }
    }
}
