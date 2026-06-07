using System;

namespace KicsitLibrary.Core.Models;

public sealed class DatabaseOwnershipStatus
{
    public bool IsOwned { get; set; }
    public int OwnerProcessId { get; set; }
    public string OwnerMachineName { get; set; } = string.Empty;
    public string OwnerUserName { get; set; } = string.Empty;
    public DateTime? AcquiredAt { get; set; }
    public string LockName { get; set; } = string.Empty;
    public string LockFilePath { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class DatabaseOwnershipResult
{
    public bool Succeeded { get; set; }
    public bool WasAlreadyOwned { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

public sealed class CriticalOperationLockResult
{
    public bool Succeeded { get; set; }
    public bool WasAlreadyOwned { get; set; }
    public string OperationName { get; set; } = string.Empty;
    public int? OwnerProcessId { get; set; }
    public string LockName { get; set; } = string.Empty;
    public string LockFilePath { get; set; } = string.Empty;
    public DateTime? AcquiredAt { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

public sealed class CriticalOperationLease
{
    public string OperationName { get; set; } = string.Empty;
    public string LockName { get; set; } = string.Empty;
    public string LockFilePath { get; set; } = string.Empty;
    public DateTime AcquiredAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int OwnerProcessId { get; set; }
    public string OwnerMachineName { get; set; } = string.Empty;
    public string OwnerUserName { get; set; } = string.Empty;
}

public sealed class BackupFolderOwnershipStatus
{
    public bool IsOwned { get; set; }
    public int OwnerProcessId { get; set; }
    public string OwnerMachineName { get; set; } = string.Empty;
    public string OwnerUserName { get; set; } = string.Empty;
    public DateTime? AcquiredAt { get; set; }
    public string LockName { get; set; } = string.Empty;
    public string LockFilePath { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class OwnershipHealthCheckResult
{
    public bool Succeeded { get; set; }
    public bool ApplicationInstanceOwned { get; set; }
    public bool DatabaseLockAvailable { get; set; }
    public bool BackupFolderLockAvailable { get; set; }
    public bool RestoreLockAvailable { get; set; }
    public bool SchedulerLockAvailable { get; set; }
    public int DetectedStaleLockFiles { get; set; }
    public string ApplicationInstanceMessage { get; set; } = string.Empty;
    public string DatabaseLockMessage { get; set; } = string.Empty;
    public string BackupFolderLockMessage { get; set; } = string.Empty;
    public string RestoreLockMessage { get; set; } = string.Empty;
    public string SchedulerLockMessage { get; set; } = string.Empty;
    public string LastOwnershipMessage { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}
