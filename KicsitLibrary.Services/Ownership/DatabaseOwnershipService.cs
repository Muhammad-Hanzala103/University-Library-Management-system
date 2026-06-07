using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KicsitLibrary.Services.Ownership;

public sealed class DatabaseOwnershipService : IDatabaseOwnershipService, IDisposable
{
    public const string LockTimeoutMessage =
        "Another Ilm-o-Kutub System operation is already using this database or backup folder.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAuthenticationService _authenticationService;
    private readonly ConcurrentDictionary<string, FileStream> _activeFileLocks = new();
    private static readonly ConcurrentDictionary<string, byte> HeldInstanceLocks = new();
    private Mutex? _appInstanceMutex;
    private bool _hasInstanceLock;
    private DateTime? _instanceAcquiredAt;
    private string _instanceMutexName = string.Empty;
    private string _lastOwnershipMessage = string.Empty;

    public DatabaseOwnershipService(
        IServiceScopeFactory scopeFactory,
        IAuthenticationService authenticationService)
    {
        _scopeFactory = scopeFactory;
        _authenticationService = authenticationService;
    }

    public async Task<DatabaseOwnershipResult> AcquireApplicationInstanceLockAsync(
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        if (_hasInstanceLock)
        {
            return new DatabaseOwnershipResult
            {
                Succeeded = true,
                WasAlreadyOwned = true,
                Message = "Application instance lock is already held by this process."
            };
        }

        var mutexName = GetMutexName(databasePath);
        if (HeldInstanceLocks.ContainsKey(mutexName))
        {
            _lastOwnershipMessage = "Another instance is already running.";
            await LogActivityAsync(
                "Instance Lock Blocked",
                $"Mutex={SanitizeDetail(mutexName)};Scope=InProcess",
                cancellationToken);
            return new DatabaseOwnershipResult
            {
                Succeeded = false,
                ErrorMessage = _lastOwnershipMessage
            };
        }

        _appInstanceMutex = new Mutex(false, mutexName);
        _instanceMutexName = mutexName;

        try
        {
            if (_appInstanceMutex.WaitOne(TimeSpan.Zero))
            {
                _hasInstanceLock = true;
                _instanceAcquiredAt = DateTime.UtcNow;
                HeldInstanceLocks[mutexName] = 0;
                _lastOwnershipMessage = "Application instance lock acquired.";
                await LogActivityAsync(
                    "Instance Lock Acquired",
                    $"Mutex={SanitizeDetail(mutexName)}",
                    cancellationToken);
                return new DatabaseOwnershipResult
                {
                    Succeeded = true,
                    Message = _lastOwnershipMessage
                };
            }
        }
        catch (AbandonedMutexException)
        {
            _hasInstanceLock = true;
            _instanceAcquiredAt = DateTime.UtcNow;
            HeldInstanceLocks[mutexName] = 0;
            _lastOwnershipMessage = "Application instance lock acquired after abandoned mutex.";
            await LogActivityAsync(
                "Instance Lock Acquired",
                $"Mutex={SanitizeDetail(mutexName)};Abandoned=True",
                cancellationToken);
            return new DatabaseOwnershipResult
            {
                Succeeded = true,
                Message = _lastOwnershipMessage
            };
        }

        _lastOwnershipMessage = "Another instance is already running.";
        await LogActivityAsync(
            "Instance Lock Blocked",
            $"Mutex={SanitizeDetail(mutexName)}",
            cancellationToken);
        return new DatabaseOwnershipResult
        {
            Succeeded = false,
            ErrorMessage = _lastOwnershipMessage
        };
    }

    public async Task ReleaseApplicationInstanceLockAsync()
    {
        if (_hasInstanceLock && _appInstanceMutex != null)
        {
            var mutexName = _instanceMutexName;
            try
            {
                _appInstanceMutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // The lock is already released by this process.
            }
            _hasInstanceLock = false;
            _instanceAcquiredAt = null;
            HeldInstanceLocks.TryRemove(mutexName, out _);
            _instanceMutexName = string.Empty;
            _lastOwnershipMessage = "Application instance lock released.";
            await LogActivityAsync(
                "Instance Lock Released",
                "Mutex released.",
                CancellationToken.None);
        }
    }

    public Task<DatabaseOwnershipStatus> GetApplicationInstanceStatusAsync(
        CancellationToken cancellationToken = default)
    {
        var databasePath = GetDatabasePath();
        return Task.FromResult(new DatabaseOwnershipStatus
        {
            IsOwned = _hasInstanceLock,
            OwnerProcessId = _hasInstanceLock ? Environment.ProcessId : 0,
            OwnerMachineName = _hasInstanceLock ? Environment.MachineName : string.Empty,
            OwnerUserName = _hasInstanceLock ? Environment.UserName : string.Empty,
            AcquiredAt = _instanceAcquiredAt,
            LockName = GetMutexName(databasePath),
            Message = _hasInstanceLock
                ? "Application instance lock is held by this process."
                : "Application instance lock is not held by this process."
        });
    }

    public async Task<CriticalOperationLockResult> AcquireCriticalOperationLockAsync(
        string operationName,
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        databasePath = Path.GetFullPath(databasePath);
        var domain = GetLockDomain(operationName);
        var lockFilePath = GetLockFilePath(databasePath, domain);
        if (_activeFileLocks.ContainsKey(lockFilePath))
        {
            return new CriticalOperationLockResult
            {
                Succeeded = true,
                WasAlreadyOwned = true,
                OperationName = operationName,
                LockName = domain,
                LockFilePath = lockFilePath,
                OwnerProcessId = Environment.ProcessId,
                AcquiredAt = DateTime.UtcNow,
                Message = "Critical operation lock is already held by this process."
            };
        }

        var settings = await ReadOwnershipSettingsAsync(cancellationToken);
        var timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed <= timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(lockFilePath)!);
                var stream = new FileStream(
                    lockFilePath,
                    FileMode.Create,
                    FileAccess.ReadWrite,
                    FileShare.Read,
                    4096,
                    FileOptions.WriteThrough);
                var now = DateTime.UtcNow;
                var lease = new CriticalOperationLease
                {
                    OperationName = operationName,
                    LockName = domain,
                    LockFilePath = lockFilePath,
                    AcquiredAt = now,
                    ExpiresAt = now.AddMinutes(settings.RetentionMinutes),
                    OwnerProcessId = Environment.ProcessId,
                    OwnerMachineName = Environment.MachineName,
                    OwnerUserName = Environment.UserName
                };
                var json = JsonSerializer.Serialize(lease, JsonOptions);
                var bytes = Encoding.UTF8.GetBytes(json);
                stream.SetLength(0);
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);

                _activeFileLocks[lockFilePath] = stream;
                _lastOwnershipMessage =
                    $"Critical operation lock acquired for {operationName}.";
                await LogActivityAsync(
                    "Critical Lock Acquired",
                    $"Operation={SanitizeDetail(operationName)};LockName={domain};File={Path.GetFileName(lockFilePath)}",
                    cancellationToken);

                return new CriticalOperationLockResult
                {
                    Succeeded = true,
                    OperationName = operationName,
                    LockName = domain,
                    LockFilePath = lockFilePath,
                    OwnerProcessId = Environment.ProcessId,
                    AcquiredAt = now,
                    Message = "Critical operation lock acquired."
                };
            }
            catch (IOException)
            {
                // Another process owns this lock domain.
            }
            catch (UnauthorizedAccessException)
            {
                // Treat filesystem denial the same as an unavailable lock.
            }

            if (stopwatch.Elapsed >= timeout)
            {
                break;
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(Math.Min(250, Math.Max(25, settings.TimeoutSeconds * 25))),
                cancellationToken);
        }

        _lastOwnershipMessage = LockTimeoutMessage;
        await LogActivityAsync(
            "Critical Lock Timeout",
            $"Operation={SanitizeDetail(operationName)};LockName={domain};File={Path.GetFileName(lockFilePath)}",
            cancellationToken);
        return new CriticalOperationLockResult
        {
            Succeeded = false,
            OperationName = operationName,
            LockName = domain,
            LockFilePath = lockFilePath,
            ErrorMessage = LockTimeoutMessage
        };
    }

    public async Task ReleaseCriticalOperationLockAsync(
        string operationName,
        string databasePath)
    {
        var lockFilePath = GetLockFilePath(
            Path.GetFullPath(databasePath),
            GetLockDomain(operationName));
        if (!_activeFileLocks.TryRemove(lockFilePath, out var stream))
        {
            return;
        }

        try
        {
            await stream.DisposeAsync();
            if (File.Exists(lockFilePath))
            {
                File.Delete(lockFilePath);
            }
        }
        catch
        {
            // Release is intentionally idempotent; the next health check can report leftovers.
        }

        _lastOwnershipMessage =
            $"Critical operation lock released for {operationName}.";
        await LogActivityAsync(
            "Critical Lock Released",
            $"Operation={SanitizeDetail(operationName)};File={Path.GetFileName(lockFilePath)}",
            CancellationToken.None);
    }

    public async Task<T> RunWithCriticalOperationLockAsync<T>(
        string operationName,
        string databasePath,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        var lockResult = await AcquireCriticalOperationLockAsync(
            operationName,
            databasePath,
            cancellationToken);
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

    public async Task RunWithCriticalOperationLockAsync(
        string operationName,
        string databasePath,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        var lockResult = await AcquireCriticalOperationLockAsync(
            operationName,
            databasePath,
            cancellationToken);
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

    public async Task<OwnershipHealthCheckResult> GetOwnershipHealthAsync(
        CancellationToken cancellationToken = default)
    {
        var databasePath = GetDatabasePath();
        var database = await CheckLockDomainAsync(databasePath, "database", cancellationToken);
        var backup = await CheckLockDomainAsync(databasePath, "backup", cancellationToken);
        var restore = await CheckLockDomainAsync(databasePath, "restore", cancellationToken);
        var scheduler = await CheckLockDomainAsync(databasePath, "scheduler", cancellationToken);

        return new OwnershipHealthCheckResult
        {
            Succeeded = true,
            ApplicationInstanceOwned = _hasInstanceLock,
            DatabaseLockAvailable = database.Available,
            BackupFolderLockAvailable = backup.Available,
            RestoreLockAvailable = restore.Available,
            SchedulerLockAvailable = scheduler.Available,
            DetectedStaleLockFiles =
                database.StaleCount + backup.StaleCount + restore.StaleCount + scheduler.StaleCount,
            ApplicationInstanceMessage = _hasInstanceLock
                ? "Application instance lock is held by this process."
                : "Application instance lock is not held by this process.",
            DatabaseLockMessage = database.Message,
            BackupFolderLockMessage = backup.Message,
            RestoreLockMessage = restore.Message,
            SchedulerLockMessage = scheduler.Message,
            LastOwnershipMessage = _lastOwnershipMessage,
            Message = "Ownership health check completed."
        };
    }

    public async Task<int> CleanupStaleLockFilesAsync(
        bool bypassAuthorization = false,
        CancellationToken cancellationToken = default)
    {
        if (!bypassAuthorization && !CanCleanup(_authenticationService.CurrentUser))
        {
            await LogActivityAsync(
                "Cleanup Stale Locks Denied",
                "Outcome=Blocked;Reason=Unauthorized cleanup attempt.",
                cancellationToken);
            throw new UnauthorizedAccessException(
                "You do not have permission to cleanup stale ownership lock files.");
        }

        var databasePath = GetDatabasePath();
        var settings = await ReadOwnershipSettingsAsync(cancellationToken);
        var cutoff = DateTime.UtcNow.AddMinutes(-settings.RetentionMinutes);
        var cleaned = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var lockFilePath in EnumerateLockFiles(databasePath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_activeFileLocks.ContainsKey(lockFilePath))
            {
                skipped++;
                continue;
            }

            try
            {
                var decision = await CanDeleteStaleLockAsync(
                    lockFilePath,
                    cutoff,
                    cancellationToken);
                if (!decision)
                {
                    skipped++;
                    continue;
                }

                File.Delete(lockFilePath);
                cleaned++;
            }
            catch
            {
                failed++;
            }
        }

        _lastOwnershipMessage =
            $"Stale lock cleanup completed. Cleaned={cleaned};Skipped={skipped};Failed={failed}.";
        await LogActivityAsync(
            "Cleanup Stale Locks",
            $"Cleaned={cleaned};Skipped={skipped};Failed={failed}",
            cancellationToken);
        return cleaned;
    }

    public void Dispose()
    {
        foreach (var item in _activeFileLocks.ToArray())
        {
            try
            {
                item.Value.Dispose();
                if (File.Exists(item.Key))
                {
                    File.Delete(item.Key);
                }
            }
            catch
            {
                // Best-effort cleanup during process shutdown.
            }
        }

        _activeFileLocks.Clear();
        if (!string.IsNullOrWhiteSpace(_instanceMutexName))
        {
            HeldInstanceLocks.TryRemove(_instanceMutexName, out _);
            try
            {
                if (_hasInstanceLock)
                {
                    _appInstanceMutex?.ReleaseMutex();
                }
            }
            catch
            {
                // Best-effort release during disposal.
            }
        }
        _appInstanceMutex?.Dispose();
    }

    private async Task<(bool Available, int StaleCount, string Message)> CheckLockDomainAsync(
        string databasePath,
        string domain,
        CancellationToken cancellationToken)
    {
        var path = GetLockFilePath(databasePath, domain);
        if (_activeFileLocks.ContainsKey(path))
        {
            return (false, 0, "Lock is held by this process.");
        }

        if (!File.Exists(path))
        {
            return (true, 0, "Lock is available.");
        }

        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);
            var lease = await JsonSerializer.DeserializeAsync<CriticalOperationLease>(
                stream,
                cancellationToken: cancellationToken);
            if (lease?.ExpiresAt <= DateTime.UtcNow)
            {
                return (true, 1, "Expired lock file detected.");
            }

            return (true, 0, "Lock file exists but is not active.");
        }
        catch (IOException)
        {
            return (false, 0, "Lock is held by another process.");
        }
        catch (UnauthorizedAccessException)
        {
            return (false, 0, "Lock status cannot be read because access was denied.");
        }
    }

    private async Task<bool> CanDeleteStaleLockAsync(
        string lockFilePath,
        DateTime unreadableCutoff,
        CancellationToken cancellationToken)
    {
        CriticalOperationLease? lease;
        try
        {
            lease = await TryReadLeaseAsync(lockFilePath, cancellationToken);
        }
        catch
        {
            lease = null;
        }

        if (lease != null)
        {
            return lease.ExpiresAt <= DateTime.UtcNow &&
                await IsExclusivelyLockableAsync(lockFilePath, cancellationToken);
        }

        var lastWrite = File.GetLastWriteTimeUtc(lockFilePath);
        return lastWrite <= unreadableCutoff &&
            await IsExclusivelyLockableAsync(lockFilePath, cancellationToken);
    }

    private static Task<bool> IsExclusivelyLockableAsync(
        string lockFilePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var stream = new FileStream(
                lockFilePath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);
            return Task.FromResult(true);
        }
        catch (IOException)
        {
            return Task.FromResult(false);
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult(false);
        }
    }

    private static async Task<CriticalOperationLease?> TryReadLeaseAsync(
        string lockFilePath,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            lockFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        return await JsonSerializer.DeserializeAsync<CriticalOperationLease>(
            stream,
            cancellationToken: cancellationToken);
    }

    private async Task<OwnershipSettings> ReadOwnershipSettingsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
            var values = await context.SystemSettings
                .AsNoTracking()
                .Where(item => item.Key == "CriticalOperationLockTimeoutSeconds" ||
                    item.Key == "LockFileRetentionMinutes")
                .ToDictionaryAsync(item => item.Key, item => item.Value, cancellationToken);
            return new OwnershipSettings(
                ReadInt(values, "CriticalOperationLockTimeoutSeconds", 15, 1, 300),
                ReadInt(values, "LockFileRetentionMinutes", 120, 1, 14400));
        }
        catch
        {
            return new OwnershipSettings(15, 120);
        }
    }

    private string GetDatabasePath()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
        return Path.GetFullPath(context.Database.GetDbConnection().DataSource);
    }

    private async Task LogActivityAsync(
        string action,
        string detail,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
            if (!await context.Database.CanConnectAsync(cancellationToken))
            {
                return;
            }

            var logService = scope.ServiceProvider.GetRequiredService<IActivityLogService>();
            await logService.LogActivityAsync(
                action,
                $"EntityName=Ownership;{detail}",
                _authenticationService.CurrentUser?.Id,
                "127.0.0.1");
        }
        catch
        {
            // Ownership must work during early startup even when logging is unavailable.
        }
    }

    private static bool CanCleanup(KicsitLibrary.Core.Entities.User? user) =>
        user?.UserRoles.Any(userRole =>
            userRole.Role.Name is "Super Admin" or "Admin" ||
            userRole.Role.RolePermissions.Any(rolePermission =>
                rolePermission.Permission.Code == "MANAGE_OWNERSHIP_STATUS")) == true;

    private static IEnumerable<string> EnumerateLockFiles(string databasePath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath)) ??
            AppContext.BaseDirectory;
        var hash = GetNormalizedPathHash(databasePath);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory.EnumerateFiles(directory, $".ilmokutub_{hash}*.lock");
    }

    private static string GetLockDomain(string operationName)
    {
        var operation = operationName.ToLowerInvariant();
        if (operation.Contains("restore", StringComparison.Ordinal))
        {
            return "restore";
        }
        if (operation.Contains("scheduler", StringComparison.Ordinal))
        {
            return "scheduler";
        }
        if (operation.Contains("backup", StringComparison.Ordinal) ||
            operation.Contains("retention", StringComparison.Ordinal))
        {
            return "backup";
        }

        return "database";
    }

    private static string GetMutexName(string databasePath) =>
        $"Global\\IlmOKutub_Instance_{GetNormalizedPathHash(databasePath)}";

    private static string GetLockFilePath(string databasePath, string domain)
    {
        var dbDir = Path.GetDirectoryName(Path.GetFullPath(databasePath)) ??
            AppContext.BaseDirectory;
        var hash = GetNormalizedPathHash(databasePath);
        return Path.Combine(dbDir, $".ilmokutub_{hash}_{domain}.lock");
    }

    private static string GetNormalizedPathHash(string path)
    {
        var normalized = Path.GetFullPath(path).ToLowerInvariant();
        var bytes = Encoding.UTF8.GetBytes(normalized);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static int ReadInt(
        IReadOnlyDictionary<string, string> values,
        string key,
        int fallback,
        int minimum,
        int maximum) =>
        values.TryGetValue(key, out var value) &&
            int.TryParse(value, out var parsed)
            ? Math.Clamp(parsed, minimum, maximum)
            : fallback;

    private static string SanitizeDetail(string value) =>
        value.Replace(";", ",", StringComparison.Ordinal)
            .Replace("=", "-", StringComparison.Ordinal)
            .ReplaceLineEndings(" ");

    private sealed record OwnershipSettings(
        int TimeoutSeconds,
        int RetentionMinutes);
}
