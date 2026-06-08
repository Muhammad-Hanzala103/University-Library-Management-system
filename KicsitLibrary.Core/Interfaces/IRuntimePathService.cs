namespace KicsitLibrary.Core.Interfaces;

public interface IRuntimePathService
{
    Task<string> GetDataRootAsync(CancellationToken cancellationToken = default);

    Task<string> GetDatabasePathAsync(CancellationToken cancellationToken = default);

    Task<string> GetDocumentStorageRootAsync(CancellationToken cancellationToken = default);

    Task<string> GetBackupRootAsync(CancellationToken cancellationToken = default);

    Task<string> GetRestoreStagingRootAsync(CancellationToken cancellationToken = default);

    Task<string> GetReportExportRootAsync(CancellationToken cancellationToken = default);

    Task<string> GetCertificateRootAsync(CancellationToken cancellationToken = default);

    Task<string> GetLogsRootAsync(CancellationToken cancellationToken = default);

    Task<string> GetTempRootAsync(CancellationToken cancellationToken = default);

    Task<string> GetLockRootAsync(CancellationToken cancellationToken = default);

    Task EnsureRuntimeFoldersAsync(CancellationToken cancellationToken = default);
}
