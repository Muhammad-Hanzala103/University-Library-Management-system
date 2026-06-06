using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Core.Interfaces
{
    public interface IOverdueService
    {
        Task<IReadOnlyList<OverdueItem>> GetOverdueItemsAsync(
            DateTime? localDate = null,
            CancellationToken cancellationToken = default);

        Task<OverdueProcessingResult> ProcessOverdueNotificationsAsync(
            int? userId = null,
            CancellationToken cancellationToken = default);
        Task<OverdueProcessingResult> CreateReminderForIssueAsync(int issueRecordId, int? userId = null);
        Task<NotificationRecord?> GetLastReminderAsync(int issueRecordId);
        Task<NotificationEligibilityResult> CanCreateReminderAsync(int issueRecordId, DateTime? asOfUtc = null);
    }
}
