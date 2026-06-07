using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Core.Interfaces;

public interface IBackupRetentionService
{
    Task<BackupRetentionPreviewResult> PreviewAsync(
        AutomaticBackupSchedulerSettings settings,
        CancellationToken cancellationToken = default);
    Task<BackupRetentionDeleteResult> ApplyAsync(
        AutomaticBackupSchedulerSettings settings,
        CancellationToken cancellationToken = default);
}
