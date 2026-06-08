using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Core.Interfaces;

public interface IDatabaseRelocationService
{
    Task<DatabaseRelocationPreview> PreviewRelocationAsync(
        CancellationToken cancellationToken = default);

    Task<DatabaseRelocationResult> RelocateDatabaseAsync(
        DatabaseRelocationRequest request,
        CancellationToken cancellationToken = default);

    Task<DatabaseRelocationStatus> GetRelocationStatusAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DatabaseRelocationHistoryItem>> GetRelocationHistoryAsync(
        CancellationToken cancellationToken = default);

    Task<DatabaseRelocationPreview> ValidateRelocationTargetAsync(
        string targetDatabasePath,
        CancellationToken cancellationToken = default);
}
