using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Core.Interfaces;

public interface IAutomaticBackupSchedulerService
{
    Task<AutomaticBackupStatus> GetSchedulerStatusAsync(
        CancellationToken cancellationToken = default);
    Task<AutomaticBackupRunResult> RunBackupNowAsync(
        CancellationToken cancellationToken = default);
    Task<AutomaticBackupRunResult> RunScheduledBackupAsync(
        CancellationToken cancellationToken = default);
    Task<BackupRetentionPreviewResult> PreviewRetentionAsync(
        CancellationToken cancellationToken = default);
    Task<BackupRetentionDeleteResult> ApplyRetentionAsync(
        CancellationToken cancellationToken = default);
    Task<AutomaticBackupSchedulerSettings> UpdateSchedulerSettingsAsync(
        AutomaticBackupSchedulerSettings settings,
        CancellationToken cancellationToken = default);
    Task<AutomaticBackupSchedulerSettings> GetSchedulerSettingsAsync(
        CancellationToken cancellationToken = default);
}
