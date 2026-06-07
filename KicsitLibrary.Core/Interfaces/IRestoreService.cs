using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Core.Interfaces;

public interface IRestoreService
{
    Task<RestorePreviewResult> PreviewRestoreAsync(
        string backupFilePath,
        CancellationToken cancellationToken = default);
    Task<RestoreValidationResult> ValidateBackupForRestoreAsync(
        string backupFilePath,
        CancellationToken cancellationToken = default);
    Task<RestoreResult> RestoreFromBackupAsync(
        RestoreRequest request,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RestoreHistoryItem>> GetRestoreHistoryAsync(
        RestoreHistoryFilter filter,
        CancellationToken cancellationToken = default);
    Task<RestoreStatusSummary> GetRestoreStatusSummaryAsync(
        CancellationToken cancellationToken = default);
    Task<BackupResult> CreateSafetyBackupBeforeRestoreAsync(
        RestoreRequest request,
        CancellationToken cancellationToken = default);
}
