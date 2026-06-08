using System.Text.Json;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Services.Restore;

public static class PendingRestoreProcessor
{
    private const string WorkingDirectoryName = ".kicsit-restore";
    private const string PendingFileName = "pending-restore.json";
    private const string ResultFileName = "restore-result.json";

    public static string GetPendingRequestPath(string databasePath) =>
        Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(databasePath))!,
            WorkingDirectoryName,
            PendingFileName);

    public static string GetPendingRequestPath(
        string databasePath,
        string? restoreStagingRoot) =>
        string.IsNullOrWhiteSpace(restoreStagingRoot)
            ? GetPendingRequestPath(databasePath)
            : Path.Combine(Path.GetFullPath(restoreStagingRoot), PendingFileName);

    public static string GetResultPath(string databasePath) =>
        Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(databasePath))!,
            WorkingDirectoryName,
            ResultFileName);

    public static string GetResultPath(
        string databasePath,
        string? restoreStagingRoot) =>
        string.IsNullOrWhiteSpace(restoreStagingRoot)
            ? GetResultPath(databasePath)
            : Path.Combine(Path.GetFullPath(restoreStagingRoot), ResultFileName);

    public static async Task<PendingRestoreResult?> ApplyPendingRestoreAsync(
        string databasePath,
        Func<CancellationToken, Task>? afterReplacement = null,
        CancellationToken cancellationToken = default)
    {
        databasePath = Path.GetFullPath(databasePath);
        var pendingPath = GetPendingRequestPath(databasePath);
        if (!File.Exists(pendingPath))
        {
            return null;
        }

        PendingRestoreMetadata metadata;
        try
        {
            var json = await File.ReadAllTextAsync(pendingPath, cancellationToken);
            metadata = JsonSerializer.Deserialize<PendingRestoreMetadata>(json)
                ?? throw new InvalidOperationException("Pending restore metadata is invalid.");
        }
        catch (Exception ex)
        {
            var invalidResult = new PendingRestoreResult
            {
                Status = "Failed",
                ErrorMessage = Sanitize(ex.Message),
                FinishedAt = DateTime.UtcNow
            };
            await WriteResultAsync(databasePath, invalidResult, cancellationToken);
            File.Delete(pendingPath);
            return invalidResult;
        }

        var result = new PendingRestoreResult
        {
            Request = metadata,
            FinishedAt = DateTime.UtcNow
        };
        string emergencyPath = string.Empty;
        var replacementOccurred = false;

        try
        {
            if (!string.Equals(
                    Path.GetFullPath(metadata.TargetDatabasePath),
                    databasePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Pending restore target does not match the configured database.");
            }

            var stagedValidation = await RestoreSqliteUtility.ValidateAsync(
                metadata.StagedBackupFilePath, cancellationToken);
            if (!stagedValidation.Succeeded ||
                !string.Equals(
                    stagedValidation.ChecksumSha256,
                    metadata.ChecksumSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    stagedValidation.ErrorMessage ??
                    "Pending restore checksum verification failed.");
            }

            var workingDirectory = Path.GetDirectoryName(pendingPath)!;
            Directory.CreateDirectory(workingDirectory);
            emergencyPath = Path.Combine(
                workingDirectory,
                $"emergency-pre-restore-{DateTime.UtcNow:yyyyMMddHHmmssfff}.db");
            if (File.Exists(databasePath))
            {
                File.Copy(databasePath, emergencyPath, overwrite: false);
                var emergencyValidation = await RestoreSqliteUtility.ValidateAsync(
                    emergencyPath, cancellationToken);
                if (!emergencyValidation.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Emergency copy of the current database failed integrity verification.");
                }
            }

            var replacementPath = Path.Combine(
                workingDirectory,
                $"replacement-{Guid.NewGuid():N}.db");
            File.Copy(metadata.StagedBackupFilePath, replacementPath, overwrite: false);
            var replacementValidation = await RestoreSqliteUtility.ValidateAsync(
                replacementPath, cancellationToken);
            if (!replacementValidation.Succeeded)
            {
                throw new InvalidOperationException(
                    "Prepared replacement failed SQLite integrity verification.");
            }

            if (File.Exists(databasePath))
            {
                File.Replace(replacementPath, databasePath, null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(replacementPath, databasePath);
            }
            replacementOccurred = true;

            if (afterReplacement != null)
            {
                await afterReplacement(cancellationToken);
            }

            if (metadata.VerifyAfterRestore)
            {
                var postValidation = await RestoreSqliteUtility.ValidateAsync(
                    databasePath, cancellationToken);
                if (!postValidation.Succeeded)
                {
                    throw new InvalidOperationException(
                        postValidation.ErrorMessage ??
                        "Post-restore SQLite integrity verification failed.");
                }
            }

            result.Succeeded = true;
            result.Status = "Completed";
            result.EmergencyBackupFilePath = emergencyPath;
            result.FinishedAt = DateTime.UtcNow;
            await WriteResultAsync(databasePath, result, cancellationToken);
            File.Delete(pendingPath);
            return result;
        }
        catch (Exception ex)
        {
            result.Status = "Failed";
            result.ErrorMessage = Sanitize(ex.Message);
            result.EmergencyBackupFilePath = emergencyPath;

            if (replacementOccurred)
            {
                try
                {
                    var rollbackPath = File.Exists(emergencyPath)
                        ? emergencyPath
                        : metadata.SafetyBackupFilePath;
                    if (string.IsNullOrWhiteSpace(rollbackPath) || !File.Exists(rollbackPath))
                    {
                        throw new InvalidOperationException("No valid rollback database is available.");
                    }

                    File.Copy(rollbackPath, databasePath, overwrite: true);
                    var rollbackValidation = await RestoreSqliteUtility.ValidateAsync(
                        databasePath, cancellationToken);
                    if (!rollbackValidation.Succeeded)
                    {
                        throw new InvalidOperationException(
                            "Rollback database failed SQLite integrity verification.");
                    }
                    result.RolledBack = true;
                    result.Status = "RolledBack";
                }
                catch (Exception rollbackException)
                {
                    result.ErrorMessage =
                        $"{result.ErrorMessage} Rollback failed: {Sanitize(rollbackException.Message)}";
                    result.Status = "CriticalFailure";
                }
            }

            result.FinishedAt = DateTime.UtcNow;
            await WriteResultAsync(databasePath, result, cancellationToken);
            if (result.Status != "CriticalFailure")
            {
                File.Delete(pendingPath);
            }
            return result;
        }
    }

    public static async Task ImportResultAsync(
        KicsitLibraryDbContext context,
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        var resultPath = GetResultPath(databasePath);
        if (!File.Exists(resultPath))
        {
            return;
        }

        var json = await File.ReadAllTextAsync(resultPath, cancellationToken);
        var result = JsonSerializer.Deserialize<PendingRestoreResult>(json)
            ?? throw new InvalidOperationException("Restore result metadata is invalid.");
        var request = result.Request;
        var userExists = request.RequestedByUserId > 0 &&
            await context.Users.AnyAsync(
                item => item.Id == request.RequestedByUserId,
                cancellationToken);
        context.RestoreHistories.Add(new RestoreHistory
        {
            BackupFilePath = request.OriginalBackupFilePath,
            SafetyBackupFilePath = request.SafetyBackupFilePath,
            RestoredDatabasePath = request.TargetDatabasePath,
            RequestedByUserId = request.RequestedByUserId,
            RequestedByUserName = request.RequestedByUserName,
            StartedAt = request.RequestedAt,
            FinishedAt = result.FinishedAt,
            Status = result.Status,
            Reason = request.Reason,
            ErrorMessage = string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? null
                : result.ErrorMessage,
            RolledBack = result.RolledBack,
            ChecksumSha256 = request.ChecksumSha256,
            MetadataJson = json
        });
        context.ActivityLogs.Add(new ActivityLog
        {
            Action = result.Succeeded
                ? "Restore Applied at Startup"
                : result.RolledBack
                    ? "Restore Rolled Back"
                    : "Restore Startup Failed",
            UserId = userExists ? request.RequestedByUserId : null,
            IpAddress = "127.0.0.1",
            Detail =
                $"EntityName=RestoreHistory;Status={result.Status};BackupFile={Safe(Path.GetFileName(request.OriginalBackupFilePath))};EmergencyBackup={Safe(result.EmergencyBackupFilePath)}"
        });
        await context.SaveChangesAsync(cancellationToken);
        File.Delete(resultPath);
    }

    private static async Task WriteResultAsync(
        string databasePath,
        PendingRestoreResult result,
        CancellationToken cancellationToken)
    {
        var resultPath = GetResultPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(resultPath)!);
        await File.WriteAllTextAsync(
            resultPath,
            JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
    }

    private static string Sanitize(string value)
    {
        var sanitized = value.ReplaceLineEndings(" ").Trim();
        return sanitized[..Math.Min(sanitized.Length, 1000)];
    }

    private static string Safe(string value) =>
        value.Replace(";", ",", StringComparison.Ordinal)
            .Replace("=", "-", StringComparison.Ordinal)
            .ReplaceLineEndings(" ");
}
