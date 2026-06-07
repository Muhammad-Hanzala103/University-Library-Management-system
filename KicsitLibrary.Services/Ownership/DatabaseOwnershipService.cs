using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using KicsitLibrary.Data;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Services.Ownership;

public sealed class DatabaseOwnershipService : IDatabaseOwnershipService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAuthenticationService _authenticationService;
    private Mutex? _appInstanceMutex;
    private bool _hasInstanceLock;
    private readonly ConcurrentDictionary<string, FileStream> _activeFileLocks = new();
    
    public DatabaseOwnershipService(IServiceScopeFactory scopeFactory, IAuthenticationService authenticationService)
    {
        _scopeFactory = scopeFactory;
        _authenticationService = authenticationService;
    }

    private static string GetNormalizedPathHash(string path)
    {
        var normalized = Path.GetFullPath(path).ToLowerInvariant();
        var bytes = Encoding.UTF8.GetBytes(normalized);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static string GetMutexName(string databasePath)
    {
        return $"Global\\IlmOKutub_Instance_{GetNormalizedPathHash(databasePath)}";
    }

    private static string GetLockFilePath(string databasePath)
    {
        var dbDir = Path.GetDirectoryName(Path.GetFullPath(databasePath)) ?? AppContext.BaseDirectory;
        var hash = GetNormalizedPathHash(databasePath);
        return Path.Combine(dbDir, $".ilmokutub_{hash}.lock");
    }

    private async Task LogActivityAsync(string action, string detail, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
            if (await context.Database.CanConnectAsync(cancellationToken))
            {
                var logService = scope.ServiceProvider.GetRequiredService<IActivityLogService>();
                await logService.LogActivityAsync(action, $"EntityName=Ownership;{detail}", _authenticationService.CurrentUser?.Id, "127.0.0.1");
            }
        }
        catch
        {
            // Ignore logging failures during early startup or if DB is unavailable
        }
    }

    public async Task<DatabaseOwnershipResult> AcquireApplicationInstanceLockAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        if (_hasInstanceLock)
            return new DatabaseOwnershipResult { Succeeded = true, WasAlreadyOwned = true, Message = "Already owned." };

        var mutexName = GetMutexName(databasePath);
        _appInstanceMutex = new Mutex(false, mutexName, out var createdNew);

        try
        {
            var acquired = _appInstanceMutex.WaitOne(TimeSpan.Zero);
            if (acquired)
            {
                _hasInstanceLock = true;
                await LogActivityAsync("Instance Lock Acquired", $"Mutex={mutexName}", cancellationToken);
                return new DatabaseOwnershipResult { Succeeded = true, Message = "Application instance lock acquired." };
            }
        }
        catch (AbandonedMutexException)
        {
            _hasInstanceLock = true;
            await LogActivityAsync("Instance Lock Acquired", $"Mutex={mutexName};Abandoned=True", cancellationToken);
            return new DatabaseOwnershipResult { Succeeded = true, Message = "Application instance lock acquired (abandoned)." };
        }

        await LogActivityAsync("Instance Lock Blocked", $"Mutex={mutexName}", cancellationToken);
        return new DatabaseOwnershipResult { Succeeded = false, ErrorMessage = "Another instance is already running." };
    }

    public async Task ReleaseApplicationInstanceLockAsync()
    {
        if (_hasInstanceLock && _appInstanceMutex != null)
        {
            _appInstanceMutex.ReleaseMutex();
            _hasInstanceLock = false;
            await LogActivityAsync("Instance Lock Released", "Mutex released.", CancellationToken.None);
        }
    }

    private string GetDatabasePath()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
        return Path.GetFullPath(context.Database.GetDbConnection().DataSource);
    }

    public Task<DatabaseOwnershipStatus> GetApplicationInstanceStatusAsync(CancellationToken cancellationToken = default)
    {
        var databasePath = GetDatabasePath();
        return Task.FromResult(new DatabaseOwnershipStatus
        {
            IsOwned = _hasInstanceLock,
            OwnerProcessId = Environment.ProcessId,
            OwnerMachineName = Environment.MachineName,
            OwnerUserName = Environment.UserName,
            AcquiredAt = _hasInstanceLock ? DateTime.UtcNow : null,
            LockName = GetMutexName(databasePath),
            Message = _hasInstanceLock ? "Lock held by this instance." : "Lock not held."
        });
    }

    public async Task<CriticalOperationLockResult> AcquireCriticalOperationLockAsync(string operationName, string databasePath, CancellationToken cancellationToken = default)
    {
        var lockFilePath = GetLockFilePath(databasePath);
        if (_activeFileLocks.ContainsKey(lockFilePath))
        {
            return new CriticalOperationLockResult
            {
                Succeeded = true,
                WasAlreadyOwned = true,
                OperationName = operationName,
                LockFilePath = lockFilePath,
                Message = "Lock already held by this process."
            };
        }

        int timeoutSeconds = 15;
        using (var scope = _scopeFactory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
            var timeoutSetting = await context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "CriticalOperationLockTimeoutSeconds", cancellationToken);
            if (timeoutSetting != null && int.TryParse(timeoutSetting.Value, out var t))
            {
                timeoutSeconds = t;
            }
        }

        var stopWatch = Stopwatch.StartNew();
        while (stopWatch.Elapsed.TotalSeconds < timeoutSeconds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var fs = new FileStream(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
                
                var lease = new CriticalOperationLease
                {
                    OperationName = operationName,
                    LockName = "CriticalOperation",
                    LockFilePath = lockFilePath,
                    AcquiredAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(120),
                    OwnerProcessId = Environment.ProcessId,
                    OwnerMachineName = Environment.MachineName,
                    OwnerUserName = Environment.UserName
                };

                var json = JsonSerializer.Serialize(lease, new JsonSerializerOptions { WriteIndented = true });
                var bytes = Encoding.UTF8.GetBytes(json);
                await fs.WriteAsync(bytes, cancellationToken);
                await fs.FlushAsync(cancellationToken);

                _activeFileLocks[lockFilePath] = fs;

                await LogActivityAsync("Critical Lock Acquired", $"Operation={operationName};File={Path.GetFileName(lockFilePath)}", cancellationToken);

                return new CriticalOperationLockResult
                {
                    Succeeded = true,
                    OperationName = operationName,
                    LockFilePath = lockFilePath,
                    OwnerProcessId = Environment.ProcessId,
                    AcquiredAt = DateTime.UtcNow,
                    Message = "Critical operation lock acquired."
                };
            }
            catch (IOException)
            {
                // Locked by another process
            }
            catch (UnauthorizedAccessException)
            {
                // Locked or no permission
            }

            await Task.Delay(500, cancellationToken);
        }

        await LogActivityAsync("Critical Lock Timeout", $"Operation={operationName};File={Path.GetFileName(lockFilePath)}", cancellationToken);
        return new CriticalOperationLockResult
        {
            Succeeded = false,
            OperationName = operationName,
            LockFilePath = lockFilePath,
            ErrorMessage = "Another Ilm-o-Kutub System operation is already using this database or backup folder."
        };
    }

    public async Task ReleaseCriticalOperationLockAsync(string operationName, string databasePath)
    {
        var lockFilePath = GetLockFilePath(databasePath);
        if (_activeFileLocks.TryRemove(lockFilePath, out var fs))
        {
            await fs.DisposeAsync();
            await LogActivityAsync("Critical Lock Released", $"Operation={operationName};File={Path.GetFileName(lockFilePath)}", CancellationToken.None);
        }
    }

    public async Task<T> RunWithCriticalOperationLockAsync<T>(string operationName, string databasePath, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        var lockResult = await AcquireCriticalOperationLockAsync(operationName, databasePath, cancellationToken);
        if (!lockResult.Succeeded)
        {
            throw new InvalidOperationException(lockResult.ErrorMessage);
        }

        try
        {
            return await operation(cancellationToken);
        }
        finally
        {
            await ReleaseCriticalOperationLockAsync(operationName, databasePath);
        }
    }

    public async Task RunWithCriticalOperationLockAsync(string operationName, string databasePath, Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        var lockResult = await AcquireCriticalOperationLockAsync(operationName, databasePath, cancellationToken);
        if (!lockResult.Succeeded)
        {
            throw new InvalidOperationException(lockResult.ErrorMessage);
        }

        try
        {
            await operation(cancellationToken);
        }
        finally
        {
            await ReleaseCriticalOperationLockAsync(operationName, databasePath);
        }
    }

    public async Task<OwnershipHealthCheckResult> GetOwnershipHealthAsync(CancellationToken cancellationToken = default)
    {
        var databasePath = GetDatabasePath();
        var lockFilePath = GetLockFilePath(databasePath);
        bool lockAvailable = true;
        int staleCount = 0;

        try
        {
            using var fs = new FileStream(lockFilePath, FileMode.Open, FileAccess.Read, FileShare.None);
            lockAvailable = true; 
        }
        catch (FileNotFoundException)
        {
            lockAvailable = true;
        }
        catch (IOException)
        {
            lockAvailable = false;
        }
        catch (UnauthorizedAccessException)
        {
            lockAvailable = false;
        }

        if (!lockAvailable)
        {
            // Try to read it with ReadWrite, if we can't, it's held. 
            // Wait, if it's held, maybe it's stale (process dead but file remains? No, FileShare.None with DeleteOnClose removes it when process dies).
            // But if it didn't use DeleteOnClose or crashed hard, maybe it remains? 
            // If it remains and we can't open it with FileShare.None, someone is holding it.
            // If we CAN open it, but it exists, it's stale.
            // Let's check if the file exists and we can read it.
        }

        if (File.Exists(lockFilePath) && lockAvailable && !_activeFileLocks.ContainsKey(lockFilePath))
        {
            staleCount++;
        }

        return new OwnershipHealthCheckResult
        {
            Succeeded = true,
            DatabaseLockAvailable = lockAvailable,
            BackupFolderLockAvailable = lockAvailable,
            RestoreLockAvailable = lockAvailable,
            SchedulerLockAvailable = lockAvailable,
            DetectedStaleLockFiles = staleCount,
            Message = "Health check completed."
        };
    }

    public async Task<int> CleanupStaleLockFilesAsync(bool bypassAuthorization = false, CancellationToken cancellationToken = default)
    {
        if (!bypassAuthorization)
        {
            var user = _authenticationService.CurrentUser;
            if (user == null || !user.UserRoles.Any(ur => ur.Role.Name == "Super Admin" || ur.Role.Name == "Admin" || ur.Role.RolePermissions.Any(rp => rp.Permission.Code == "MANAGE_OWNERSHIP_STATUS")))
            {
                await LogActivityAsync("Cleanup Stale Locks Denied", "Unauthorized attempt.", cancellationToken);
                throw new UnauthorizedAccessException("You do not have permission to cleanup stale locks.");
            }
        }

        var databasePath = GetDatabasePath();
        var lockFilePath = GetLockFilePath(databasePath);
        int cleaned = 0;
        if (File.Exists(lockFilePath) && !_activeFileLocks.ContainsKey(lockFilePath))
        {
            try
            {
                // Try to delete. If it throws, it's locked.
                File.Delete(lockFilePath);
                cleaned++;
                await LogActivityAsync("Cleanup Stale Locks", $"Deleted={Path.GetFileName(lockFilePath)}", cancellationToken);
            }
            catch (Exception)
            {
                // Ignore if it's still locked
            }
        }

        return cleaned;
    }

    public void Dispose()
    {
        foreach (var fs in _activeFileLocks.Values)
        {
            try { fs.Dispose(); } catch { }
        }
        _activeFileLocks.Clear();
        _appInstanceMutex?.Dispose();
    }
}
