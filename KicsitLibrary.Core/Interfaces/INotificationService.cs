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
        Task<NotificationRecord> RetryNotificationRecordAsync(int notificationId, int? userId = null);
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
