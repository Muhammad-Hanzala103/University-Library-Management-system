using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Core.Interfaces;

public interface IActivityLogBrowserService
{
    Task<IReadOnlyList<ActivityLogListItem>> GetActivityLogsAsync(
        ActivityLogFilter filter,
        CancellationToken cancellationToken = default);
    Task<ActivityLogDetails> GetActivityLogDetailsAsync(
        int activityLogId,
        CancellationToken cancellationToken = default);
    Task<ActivityLogSummary> GetActivityLogSummaryAsync(
        CancellationToken cancellationToken = default);
    Task<ActivityLogExportResult> ExportActivityLogSnapshotAsync(
        ActivityLogFilter filter,
        string format,
        CancellationToken cancellationToken = default);
    Task<ActivityLogDeleteResult> DeleteOldLogsAsync(
        DateTime olderThanUtc,
        string reason,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetDistinctActionsAsync(
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetDistinctEntityNamesAsync(
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ActivityLogUserOption>> GetDistinctUsersAsync(
        CancellationToken cancellationToken = default);
}
