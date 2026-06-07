using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Core.Interfaces;

public interface IBackupService
{
    Task<BackupResult> CreateBackupAsync(
        BackupRequest request,
        CancellationToken cancellationToken = default);
    Task<BackupVerificationResult> VerifyBackupAsync(
        string filePath,
        int? backupHistoryId = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BackupHistoryItem>> GetBackupHistoryAsync(
        BackupHistoryFilter filter,
        CancellationToken cancellationToken = default);
    Task<BackupStatusSummary> GetBackupStatusSummaryAsync(
        CancellationToken cancellationToken = default);
    Task<BackupSettings> GetBackupSettingsAsync(
        CancellationToken cancellationToken = default);
    Task<BackupResult> OpenBackupFolderAsync(
        string? folderPath = null,
        CancellationToken cancellationToken = default);
    Task<BackupResult> DeleteBackupHistoryRecordAsync(
        int backupHistoryId,
        string reason,
        CancellationToken cancellationToken = default);
}
