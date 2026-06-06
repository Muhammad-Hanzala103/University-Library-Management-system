using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Services.Notifications
{
    public class NotificationService : INotificationService
    {
        private readonly KicsitLibraryDbContext _context;
        private readonly IActivityLogService _logService;

        public NotificationService(
            KicsitLibraryDbContext context,
            IActivityLogService logService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        public async Task<NotificationCreateResult> CreateNotificationAsync(
            NotificationRecord notification,
            int cooldownHours = 24,
            int? userId = null)
        {
            ArgumentNullException.ThrowIfNull(notification);

            if (!notification.IssueRecordId.HasValue)
            {
                throw new InvalidOperationException("An issue record is required for notification deduplication.");
            }

            var eligibility = await CanCreateNotificationAsync(
                notification.IssueRecordId.Value,
                notification.NotificationType,
                notification.Channel,
                cooldownHours);

            if (!eligibility.CanCreate)
            {
                return new NotificationCreateResult
                {
                    Created = false,
                    Message = eligibility.Reason,
                    Notification = eligibility.LastNotification!
                };
            }

            var nowUtc = DateTime.UtcNow;
            notification.Channel = NormalizeChannel(notification.Channel);
            notification.DeduplicationKey = BuildDeduplicationKey(
                notification.IssueRecordId.Value,
                notification.NotificationType,
                notification.Channel,
                nowUtc);

            try
            {
                await _context.NotificationRecords.AddAsync(notification);
                await _context.SaveChangesAsync();
                await _logService.LogActivityAsync(
                    "Notification Created",
                    $"{notification.Channel} {notification.NotificationType} notification created for issue {notification.IssueRecordId}.",
                    userId);

                return new NotificationCreateResult
                {
                    Created = true,
                    Message = "Notification record created.",
                    Notification = notification
                };
            }
            catch (DbUpdateException)
            {
                _context.Entry(notification).State = EntityState.Detached;
                var existing = await _context.NotificationRecords
                    .FirstOrDefaultAsync(nr => nr.DeduplicationKey == notification.DeduplicationKey);

                if (existing != null)
                {
                    return new NotificationCreateResult
                    {
                        Created = false,
                        Message = "A notification already exists in the current deduplication window.",
                        Notification = existing
                    };
                }

                throw;
            }
        }

        public async Task<IReadOnlyList<NotificationRecord>> GetNotificationsAsync()
        {
            return await _context.NotificationRecords
                .Include(nr => nr.IssueRecord)
                .OrderByDescending(nr => nr.CreatedAt)
                .ToListAsync();
        }

        public async Task<NotificationRecord> RetryNotificationRecordAsync(
            int notificationId,
            int? userId = null)
        {
            var notification = await _context.NotificationRecords
                .FirstOrDefaultAsync(nr => nr.Id == notificationId);
            if (notification == null)
            {
                throw new InvalidOperationException("Notification record not found.");
            }

            var maxRetryCount = await GetIntegerSettingAsync("MaxNotificationRetryCount", 3);
            if (notification.RetryCount >= maxRetryCount)
            {
                notification.Status = NotificationStatus.Failed;
                notification.FailureReason = $"Maximum retry count of {maxRetryCount} reached.";
            }
            else
            {
                notification.RetryCount++;
                notification.LastAttemptAt = DateTime.UtcNow;

                if (notification.Channel.Equals("Email", StringComparison.OrdinalIgnoreCase) &&
                    string.IsNullOrWhiteSpace(notification.RecipientEmail))
                {
                    notification.Status = NotificationStatus.Failed;
                    notification.FailureReason = "Member email is missing.";
                }
                else
                {
                    notification.Status = NotificationStatus.Pending;
                    notification.FailureReason = notification.Channel.Equals(
                        "Email",
                        StringComparison.OrdinalIgnoreCase)
                        ? "Email delivery pending."
                        : "In-app delivery pending.";
                }
            }

            await _context.SaveChangesAsync();
            await _logService.LogActivityAsync(
                "Notification Retry",
                $"Notification {notification.Id} retry updated to attempt {notification.RetryCount}; no external delivery was attempted.",
                userId);

            return notification;
        }

        public async Task<NotificationRecord> MarkAsReadAsync(
            int notificationId,
            int? userId = null)
        {
            var notification = await _context.NotificationRecords
                .FirstOrDefaultAsync(nr => nr.Id == notificationId);
            if (notification == null)
            {
                throw new InvalidOperationException("Notification record not found.");
            }

            notification.ReadAt ??= DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await _logService.LogActivityAsync(
                "Notification Read",
                $"Notification {notification.Id} marked as read.",
                userId);

            return notification;
        }

        public async Task<NotificationRecord?> GetLastNotificationForIssueAsync(
            int issueRecordId,
            NotificationType notificationType,
            string channel)
        {
            var normalizedChannel = NormalizeChannel(channel);
            return await _context.NotificationRecords
                .Where(nr =>
                    nr.IssueRecordId == issueRecordId &&
                    nr.NotificationType == notificationType &&
                    nr.Channel == normalizedChannel)
                .OrderByDescending(nr => nr.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<NotificationEligibilityResult> CanCreateNotificationAsync(
            int issueRecordId,
            NotificationType notificationType,
            string channel,
            int cooldownHours = 24,
            DateTime? asOfUtc = null)
        {
            var nowUtc = EnsureUtc(asOfUtc ?? DateTime.UtcNow);
            var normalizedChannel = NormalizeChannel(channel);
            var lastNotification = await GetLastNotificationForIssueAsync(
                issueRecordId,
                notificationType,
                normalizedChannel);

            if (lastNotification == null)
            {
                return new NotificationEligibilityResult { CanCreate = true };
            }

            var lastCreatedUtc = EnsureUtc(lastNotification.CreatedAt);
            var sameLocalDate = lastCreatedUtc.ToLocalTime().Date == nowUtc.ToLocalTime().Date;
            var inCooldown = nowUtc - lastCreatedUtc < TimeSpan.FromHours(Math.Max(1, cooldownHours));

            if (sameLocalDate || inCooldown)
            {
                return new NotificationEligibilityResult
                {
                    CanCreate = false,
                    Reason = sameLocalDate
                        ? "A reminder already exists for this issue and channel today."
                        : $"A reminder already exists within the {Math.Max(1, cooldownHours)} hour cooldown.",
                    LastNotification = lastNotification
                };
            }

            return new NotificationEligibilityResult
            {
                CanCreate = true,
                LastNotification = lastNotification
            };
        }

        private async Task<int> GetIntegerSettingAsync(string key, int defaultValue)
        {
            var value = await _context.SystemSettings
                .Where(setting => setting.Key == key)
                .Select(setting => setting.Value)
                .FirstOrDefaultAsync();
            return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : defaultValue;
        }

        private static string NormalizeChannel(string channel)
        {
            if (channel.Equals("InApp", StringComparison.OrdinalIgnoreCase))
            {
                return "InApp";
            }

            if (channel.Equals("WhatsApp", StringComparison.OrdinalIgnoreCase))
            {
                return "WhatsApp";
            }

            return "Email";
        }

        private static string BuildDeduplicationKey(
            int issueRecordId,
            NotificationType notificationType,
            string channel,
            DateTime nowUtc)
        {
            return $"{issueRecordId}:{notificationType}:{channel}:{nowUtc.ToLocalTime():yyyyMMdd}";
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
