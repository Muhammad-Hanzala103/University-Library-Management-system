using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        private readonly IEmailTransport _emailTransport;
        private readonly IEmailSettingsService _emailSettingsService;

        public NotificationService(
            KicsitLibraryDbContext context,
            IActivityLogService logService,
            IEmailTransport emailTransport,
            IEmailSettingsService emailSettingsService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _emailTransport = emailTransport ?? throw new ArgumentNullException(nameof(emailTransport));
            _emailSettingsService = emailSettingsService ?? throw new ArgumentNullException(nameof(emailSettingsService));
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

        public async Task<IReadOnlyList<NotificationRecord>> GetPendingEmailNotificationsAsync()
        {
            return await _context.NotificationRecords
                .Where(notification =>
                    notification.Channel == "Email" &&
                    notification.Status == NotificationStatus.Pending)
                .OrderBy(notification => notification.CreatedAt)
                .ToListAsync();
        }

        public async Task<NotificationDeliveryResult> SendNotificationAsync(
            int notificationId,
            int? userId = null)
        {
            var notification = await _context.NotificationRecords
                .FirstOrDefaultAsync(record => record.Id == notificationId);
            if (notification == null)
            {
                return new NotificationDeliveryResult
                {
                    NotificationId = notificationId,
                    Message = "Notification record not found."
                };
            }

            return await DeliverEmailAsync(notification, userId);
        }

        public async Task<NotificationBatchDeliveryResult> SendPendingEmailNotificationsAsync(
            int? userId = null)
        {
            var result = new NotificationBatchDeliveryResult();
            var pendingIds = await _context.NotificationRecords
                .Where(notification =>
                    notification.Channel == "Email" &&
                    notification.Status == NotificationStatus.Pending)
                .OrderBy(notification => notification.CreatedAt)
                .Select(notification => notification.Id)
                .ToListAsync();

            foreach (var notificationId in pendingIds)
            {
                var delivery = await SendNotificationAsync(notificationId, userId);
                result.ProcessedCount++;
                if (delivery.Succeeded)
                {
                    result.SentCount++;
                }
                else if (delivery.Attempted)
                {
                    result.FailedCount++;
                }
                else
                {
                    result.SkippedCount++;
                }
            }

            result.Message =
                $"Processed {result.ProcessedCount}; sent {result.SentCount}; " +
                $"failed {result.FailedCount}; skipped {result.SkippedCount}.";
            await _logService.LogActivityAsync(
                "Pending Email Processing Completed",
                result.Message,
                userId);

            return result;
        }

        public async Task<NotificationDeliveryResult> RetryNotificationRecordAsync(
            int notificationId,
            int? userId = null)
        {
            var notification = await _context.NotificationRecords
                .FirstOrDefaultAsync(nr => nr.Id == notificationId);
            if (notification == null)
            {
                return new NotificationDeliveryResult
                {
                    NotificationId = notificationId,
                    Message = "Notification record not found."
                };
            }

            if (!notification.Channel.Equals("Email", StringComparison.OrdinalIgnoreCase))
            {
                return await RecordBlockedDeliveryAsync(
                    notification,
                    "Only email notification records can be retried through SMTP.",
                    userId,
                    keepPending: notification.Status == NotificationStatus.Pending);
            }

            return await DeliverEmailAsync(notification, userId);
        }

        public Task<EmailSettingsValidationResult> ValidateEmailSettingsAsync()
        {
            return _emailSettingsService.ValidateAsync();
        }

        private async Task<NotificationDeliveryResult> DeliverEmailAsync(
            NotificationRecord notification,
            int? userId)
        {
            if (!notification.Channel.Equals("Email", StringComparison.OrdinalIgnoreCase))
            {
                return await RecordBlockedDeliveryAsync(
                    notification,
                    "Only email notification records can be sent through SMTP.",
                    userId,
                    keepPending: notification.Status == NotificationStatus.Pending);
            }

            if (notification.Status == NotificationStatus.Sent)
            {
                return await RecordBlockedDeliveryAsync(
                    notification,
                    "Notification has already been sent.",
                    userId,
                    keepPending: false);
            }

            if (string.IsNullOrWhiteSpace(notification.RecipientEmail))
            {
                return await RecordBlockedDeliveryAsync(
                    notification,
                    "Recipient email is missing.",
                    userId,
                    keepPending: false);
            }

            var validation = await _emailSettingsService.ValidateAsync();
            if (!validation.IsEnabled)
            {
                return await RecordBlockedDeliveryAsync(
                    notification,
                    validation.Message,
                    userId,
                    keepPending: true);
            }

            if (!validation.IsValid)
            {
                return await RecordBlockedDeliveryAsync(
                    notification,
                    validation.Message,
                    userId,
                    keepPending: false);
            }

            var options = validation.Options;
            if (notification.RetryCount >= options.MaxNotificationRetryCount)
            {
                return await RecordBlockedDeliveryAsync(
                    notification,
                    $"Maximum retry count of {options.MaxNotificationRetryCount} reached.",
                    userId,
                    keepPending: false);
            }

            notification.RetryCount++;
            notification.LastAttemptAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            EmailSendResult sendResult;
            try
            {
                sendResult = await _emailTransport.SendAsync(
                    new EmailMessage
                    {
                        ToEmail = notification.RecipientEmail,
                        ToName = notification.RecipientName,
                        Subject = notification.Subject,
                        PlainTextBody = notification.Message,
                        FromEmail = options.FromEmail,
                        FromName = options.FromName
                    },
                    options,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                sendResult = new EmailSendResult
                {
                    Succeeded = false,
                    FailureReason = ex.Message
                };
            }

            if (sendResult.Succeeded)
            {
                notification.Status = NotificationStatus.Sent;
                notification.SentAt = sendResult.SentAt ?? DateTime.UtcNow;
                notification.FailureReason = null;
            }
            else
            {
                notification.Status = NotificationStatus.Failed;
                notification.SentAt = null;
                notification.FailureReason = SanitizeFailureReason(
                    string.IsNullOrWhiteSpace(sendResult.FailureReason)
                        ? "Email transport failed without a reason."
                        : sendResult.FailureReason,
                    options.Password);
            }

            await _context.SaveChangesAsync();
            var activityDetail = sendResult.Succeeded
                ? $"Email notification {notification.Id} sent to {notification.RecipientCode}."
                : $"Email notification {notification.Id} failed for {notification.RecipientCode}: {notification.FailureReason}";
            await _logService.LogActivityAsync(
                sendResult.Succeeded ? "Email Delivery Succeeded" : "Email Delivery Failed",
                activityDetail,
                userId);

            return new NotificationDeliveryResult
            {
                NotificationId = notification.Id,
                Succeeded = sendResult.Succeeded,
                Attempted = true,
                Message = sendResult.Succeeded
                    ? "Email sent successfully."
                    : notification.FailureReason ?? "Email delivery failed.",
                Notification = notification
            };
        }

        private async Task<NotificationDeliveryResult> RecordBlockedDeliveryAsync(
            NotificationRecord notification,
            string reason,
            int? userId,
            bool keepPending)
        {
            notification.Status = keepPending
                ? NotificationStatus.Pending
                : NotificationStatus.Failed;
            notification.FailureReason = reason;
            await _context.SaveChangesAsync();
            await _logService.LogActivityAsync(
                "Email Delivery Blocked",
                $"Notification {notification.Id} was not sent: {reason}",
                userId);

            return new NotificationDeliveryResult
            {
                NotificationId = notification.Id,
                Succeeded = false,
                Attempted = false,
                Message = reason,
                Notification = notification
            };
        }

        private static string SanitizeFailureReason(string reason, string password)
        {
            return string.IsNullOrEmpty(password)
                ? reason
                : reason.Replace(password, "[REDACTED]", StringComparison.Ordinal);
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
