using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Core.Interfaces
{
    public interface INotificationService
    {
        Task<NotificationCreateResult> CreateNotificationAsync(
            NotificationRecord notification,
            int cooldownHours = 24,
            int? userId = null);

        Task<IReadOnlyList<NotificationRecord>> GetNotificationsAsync();
        Task<IReadOnlyList<NotificationRecord>> GetPendingEmailNotificationsAsync();
        Task<NotificationDeliveryResult> SendNotificationAsync(int notificationId, int? userId = null);
        Task<NotificationBatchDeliveryResult> SendPendingEmailNotificationsAsync(int? userId = null);
        Task<NotificationDeliveryResult> RetryNotificationRecordAsync(int notificationId, int? userId = null);
        Task<EmailSettingsValidationResult> ValidateEmailSettingsAsync();
        Task<NotificationRecord> MarkAsReadAsync(int notificationId, int? userId = null);

        Task<NotificationRecord?> GetLastNotificationForIssueAsync(
            int issueRecordId,
            NotificationType notificationType,
            string channel);

        Task<NotificationEligibilityResult> CanCreateNotificationAsync(
            int issueRecordId,
            NotificationType notificationType,
            string channel,
            int cooldownHours = 24,
            DateTime? asOfUtc = null);
    }
}
