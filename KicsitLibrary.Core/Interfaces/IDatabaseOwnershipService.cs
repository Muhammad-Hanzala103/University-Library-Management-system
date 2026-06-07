using System;
using System.Threading;
using System.Threading.Tasks;
using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Core.Interfaces;

public interface IDatabaseOwnershipService
{
    Task<DatabaseOwnershipResult> AcquireApplicationInstanceLockAsync(string databasePath, CancellationToken cancellationToken = default);
    Task ReleaseApplicationInstanceLockAsync();
    Task<DatabaseOwnershipStatus> GetApplicationInstanceStatusAsync(string databasePath, CancellationToken cancellationToken = default);
    Task<CriticalOperationLockResult> AcquireCriticalOperationLockAsync(string operationName, string databasePath, CancellationToken cancellationToken = default);
    Task ReleaseCriticalOperationLockAsync(string operationName, string databasePath);
    Task<T> RunWithCriticalOperationLockAsync<T>(string operationName, string databasePath, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
    Task RunWithCriticalOperationLockAsync(string operationName, string databasePath, Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
    Task<OwnershipHealthCheckResult> GetOwnershipHealthAsync(string databasePath, CancellationToken cancellationToken = default);
    Task<int> CleanupStaleLockFilesAsync(string databasePath, CancellationToken cancellationToken = default);
}
