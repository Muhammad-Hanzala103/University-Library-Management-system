using System.Text.Json;
using KicsitLibrary.Core;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data;
using KicsitLibrary.Services.Notifications;
using KicsitLibrary.Services.Restore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Services.Backup;

public sealed class BackupRetentionService(
    KicsitLibraryDbContext context,
    IAuthenticationService authenticationService) : IBackupRetentionService
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    public async Task<BackupRetentionPreviewResult> PreviewAsync(
        AutomaticBackupSchedulerSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (!await BackupAuthorization.CanViewAsync(authenticationService))
        {
            return PreviewFailure("The current user cannot view backup retention.");
        }

        try
        {
            settings ??= new AutomaticBackupSchedulerSettings();
            var now = DateTime.UtcNow;
            var histories = await context.BackupHistories
                .AsNoTracking()
                .OrderByDescending(item => item.CreatedAt)
                .ThenByDescending(item => item.Id)
                .ToListAsync(cancellationToken);
            var successful = histories
                .Where(IsSuccessful)
                .OrderByDescending(item => item.CreatedAt)
                .ThenByDescending(item => item.Id)
                .FirstOrDefault();
            var safetyPaths = (await context.RestoreHistories
                    .AsNoTracking()
                    .Where(item => item.SafetyBackupFilePath != null)
                    .Select(item => item.SafetyBackupFilePath!)
                    .ToListAsync(cancellationToken))
                .Select(NormalizePath)
                .Where(path => path != null)
                .Cast<string>()
                .ToHashSet(PathComparer);
            var liveDatabasePath = GetLiveDatabasePath();
            var pending = await ReadPendingRestoreAsync(
                liveDatabasePath,
                cancellationToken);
            var knownRoot = await ResolveKnownBackupRootAsync(
                settings,
                cancellationToken);

            var candidates = new List<BackupRetentionCandidate>();
            for (var index = 0; index < histories.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var history = histories[index];
                var ageDays = Math.Max(0, (now.Date - history.CreatedAt.ToUniversalTime().Date).Days);
                var oldByAge = ageDays >= Math.Clamp(settings.RetentionDays, 1, 3650);
                var beyondHistoryLimit =
                    index >= Math.Clamp(settings.MaxHistoryRows, 1, 5000);
                if (!oldByAge && !beyondHistoryLimit)
                {
                    continue;
                }

                var reason = oldByAge && beyondHistoryLimit
                    ? $"Older than {settings.RetentionDays} days and exceeds the {settings.MaxHistoryRows} row limit."
                    : oldByAge
                        ? $"Older than {settings.RetentionDays} days."
                        : $"Exceeds the {settings.MaxHistoryRows} row limit.";
                var cannotDeleteReason = GetProtectionReason(
                    history,
                    successful?.Id,
                    safetyPaths,
                    pending,
                    liveDatabasePath,
                    knownRoot,
                    settings.DeletePhysicalFiles);
                candidates.Add(new BackupRetentionCandidate
                {
                    BackupHistoryId = history.Id,
                    BackupFilePath = history.BackupFilePath,
                    CompressedFilePath = history.CompressedFilePath ?? string.Empty,
                    CreatedAt = history.CreatedAt,
                    AgeDays = ageDays,
                    SizeBytes = history.BackupSizeBytes,
                    Reason = reason,
                    CanDelete = string.IsNullOrEmpty(cannotDeleteReason),
                    CannotDeleteReason = cannotDeleteReason
                });
            }

            var deletable = candidates.Where(item => item.CanDelete).ToList();
            return new BackupRetentionPreviewResult
            {
                Succeeded = true,
                CandidateCount = deletable.Count,
                TotalSizeBytes = deletable.Sum(item => item.SizeBytes),
                Candidates = candidates,
                Message =
                    $"{deletable.Count} retention candidate(s) can be soft-deleted; " +
                    $"{candidates.Count - deletable.Count} protected candidate(s) will be kept."
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return PreviewFailure(Sanitize(ex.Message));
        }
    }

    public async Task<BackupRetentionDeleteResult> ApplyAsync(
        AutomaticBackupSchedulerSettings settings,
        CancellationToken cancellationToken = default)
    {
        var user = authenticationService.CurrentUser;
        if (!await AutomaticBackupAuthorization.CanManageAsync(authenticationService))
        {
            await LogStandaloneAsync(
                "Automatic Backup Retention Blocked",
                "Outcome=Blocked;Reason=The current user cannot apply backup retention.",
                user?.Id,
                cancellationToken);
            return DeleteFailure(
                "The current user cannot apply backup retention.");
        }

        settings ??= new AutomaticBackupSchedulerSettings();
        if (!settings.RetentionEnabled)
        {
            return new BackupRetentionDeleteResult
            {
                Succeeded = true,
                Message = "Backup retention is disabled."
            };
        }

        var preview = await PreviewAsync(settings, cancellationToken);
        if (!preview.Succeeded)
        {
            return DeleteFailure(
                preview.ErrorMessage ?? preview.Message);
        }

        var result = new BackupRetentionDeleteResult();
        try
        {
            var liveDatabasePath = GetLiveDatabasePath();
            var knownRoot = await ResolveKnownBackupRootAsync(
                settings,
                cancellationToken);
            var deletedIds = new List<int>();
            var logs = new List<ActivityLog>();
            foreach (var candidate in preview.Candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!candidate.CanDelete)
                {
                    result.SkippedCount++;
                    logs.Add(CreateLog(
                        "Automatic Backup Retention Skipped",
                        candidate.BackupHistoryId,
                        $"Reason={Safe(candidate.CannotDeleteReason)}",
                        user!.Id));
                    continue;
                }

                if (settings.DeletePhysicalFiles)
                {
                    try
                    {
                        var deletion = DeleteLinkedFiles(
                            candidate,
                            knownRoot,
                            liveDatabasePath);
                        result.DeletedPhysicalFileCount += deletion.FileCount;
                        result.TotalFreedBytes += deletion.FreedBytes;
                    }
                    catch (Exception ex)
                    {
                        result.SkippedCount++;
                        logs.Add(CreateLog(
                            "Automatic Backup Retention Skipped",
                            candidate.BackupHistoryId,
                            $"Reason=Physical file deletion failed: {Safe(ex.Message)}",
                            user!.Id));
                        continue;
                    }
                }

                deletedIds.Add(candidate.BackupHistoryId);
                logs.Add(CreateLog(
                    "Automatic Backup Retention Deleted",
                    candidate.BackupHistoryId,
                    settings.DeletePhysicalFiles
                        ? "Mode=HistoryAndPhysicalFiles"
                        : "Mode=HistoryOnly",
                    user!.Id));
            }

            await using var transaction =
                await context.Database.BeginTransactionAsync(cancellationToken);
            var histories = await context.BackupHistories
                .Where(item => deletedIds.Contains(item.Id))
                .ToListAsync(cancellationToken);
            foreach (var history in histories)
            {
                history.Status = "RetentionDeleted";
                history.IsDeleted = true;
                history.DeletedAt = DateTime.UtcNow;
                history.DeletedByUserId = user!.Id;
                history.DeletedReason =
                    "Automatic backup retention policy applied.";
            }

            context.ActivityLogs.AddRange(logs);
            context.ActivityLogs.Add(new ActivityLog
            {
                Action = "Automatic Backup Retention Completed",
                UserId = user!.Id,
                IpAddress = "127.0.0.1",
                Detail =
                    $"EntityName=BackupHistory;DeletedHistory={histories.Count};" +
                    $"DeletedFiles={result.DeletedPhysicalFileCount};" +
                    $"Skipped={result.SkippedCount};FreedBytes={result.TotalFreedBytes}"
            });
            await SqliteRetryPolicy.ExecuteAsync(
                token => context.SaveChangesAsync(token),
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            result.Succeeded = true;
            result.DeletedHistoryCount = histories.Count;
            result.Message =
                $"Retention soft-deleted {histories.Count} history record(s), " +
                $"deleted {result.DeletedPhysicalFileCount} physical file(s), " +
                $"and skipped {result.SkippedCount} protected or failed candidate(s).";
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = Sanitize(ex.Message);
            result.Message = "Backup retention failed.";
            await LogStandaloneAsync(
                "Automatic Backup Retention Failed",
                $"Error={Safe(result.ErrorMessage)}",
                user?.Id,
                CancellationToken.None);
            return result;
        }
    }

    private string GetProtectionReason(
        BackupHistory history,
        int? latestSuccessfulId,
        IReadOnlySet<string> safetyPaths,
        PendingRestoreState pending,
        string liveDatabasePath,
        string knownRoot,
        bool physicalDeletionEnabled)
    {
        var backupPath = NormalizePath(history.BackupFilePath);
        var compressedPath = NormalizePath(history.CompressedFilePath);
        if (!IsSuccessful(history))
        {
            return history.VerificationStatus.Equals(
                "Failed",
                StringComparison.OrdinalIgnoreCase)
                    ? "Failed verification backups are retained for manual review."
                    : "Only completed normal backup records are eligible.";
        }
        if (history.Id == latestSuccessfulId)
        {
            return "The latest successful backup is always retained.";
        }
        if (history.VerificationStatus.Equals(
                "Failed",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Failed verification backups are retained for manual review.";
        }
        if (IsRestoreSafetyBackup(history, backupPath, safetyPaths))
        {
            return "Restore safety backups are never removed by retention.";
        }
        if (IsEmergencyRestorePath(backupPath) ||
            IsEmergencyRestorePath(compressedPath))
        {
            return "Emergency restore files are never removed by retention.";
        }
        if (pending.MetadataUnreadable)
        {
            return "Pending restore metadata could not be read safely.";
        }
        if (pending.ProtectedPaths.Contains(backupPath ?? string.Empty) ||
            pending.ProtectedPaths.Contains(compressedPath ?? string.Empty))
        {
            return "The backup is referenced by pending restore metadata.";
        }
        if (PathComparer.Equals(backupPath, liveDatabasePath) ||
            PathComparer.Equals(compressedPath, liveDatabasePath))
        {
            return "The current live database is never removed by retention.";
        }
        if (physicalDeletionEnabled)
        {
            var pathFailure = ValidateLinkedPath(
                backupPath,
                ".db",
                knownRoot,
                liveDatabasePath);
            if (!string.IsNullOrEmpty(pathFailure))
            {
                return pathFailure;
            }
            if (!string.IsNullOrWhiteSpace(compressedPath))
            {
                pathFailure = ValidateLinkedPath(
                    compressedPath,
                    ".zip",
                    knownRoot,
                    liveDatabasePath);
                if (!string.IsNullOrEmpty(pathFailure))
                {
                    return pathFailure;
                }
            }
            var metadataPath = NormalizePath(
                Path.ChangeExtension(
                    history.BackupFilePath,
                    ".metadata.json"));
            if (!string.IsNullOrWhiteSpace(metadataPath) &&
                File.Exists(metadataPath))
            {
                pathFailure = ValidateMetadataPath(
                    metadataPath,
                    knownRoot,
                    liveDatabasePath);
                if (!string.IsNullOrEmpty(pathFailure))
                {
                    return pathFailure;
                }
            }
        }

        return string.Empty;
    }

    private static bool IsSuccessful(BackupHistory history) =>
        history.Status is "Completed" or "CompletedWithWarnings";

    private static bool IsRestoreSafetyBackup(
        BackupHistory history,
        string? backupPath,
        IReadOnlySet<string> safetyPaths) =>
        (!string.IsNullOrWhiteSpace(backupPath) &&
            safetyPaths.Contains(backupPath)) ||
        (history.Reason?.StartsWith(
            "Mandatory pre-restore safety backup:",
            StringComparison.OrdinalIgnoreCase) ?? false) ||
        ContainsDirectory(backupPath, "Restore Safety");

    private static bool IsEmergencyRestorePath(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        (Path.GetFileName(path).StartsWith(
             "emergency-pre-restore-",
             StringComparison.OrdinalIgnoreCase) ||
         ContainsDirectory(path, ".kicsit-restore"));

    private static bool ContainsDirectory(string? path, string directoryName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var segments = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        return segments.Contains(directoryName, StringComparer.OrdinalIgnoreCase);
    }

    private static string ValidateLinkedPath(
        string? path,
        string requiredExtension,
        string knownRoot,
        string liveDatabasePath)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "The linked backup path is missing.";
        }
        if (!Path.GetExtension(path).Equals(
                requiredExtension,
                StringComparison.OrdinalIgnoreCase))
        {
            return $"Unsupported linked file extension: {Path.GetExtension(path)}.";
        }
        if (PathComparer.Equals(path, liveDatabasePath))
        {
            return "The current live database is never removed by retention.";
        }
        if (!IsWithinRoot(path, knownRoot))
        {
            return "The linked file is outside the configured backup folder.";
        }
        if (ContainsReparsePoint(path, knownRoot))
        {
            return "Symbolic links and reparse points are not followed for deletion.";
        }
        return string.Empty;
    }

    private static string ValidateMetadataPath(
        string? path,
        string knownRoot,
        string liveDatabasePath)
    {
        var pathFailure = ValidateLinkedPath(
            path,
            ".json",
            knownRoot,
            liveDatabasePath);
        if (!string.IsNullOrEmpty(pathFailure))
        {
            return pathFailure;
        }
        return Path.GetFileName(path!).EndsWith(
            ".metadata.json",
            StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : "Only backup metadata sidecar files can be removed.";
    }

    private static bool IsWithinRoot(string path, string root)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(root));
        var normalizedPath = Path.GetFullPath(path);
        return PathComparer.Equals(normalizedPath, normalizedRoot) ||
            normalizedPath.StartsWith(
                normalizedRoot + Path.DirectorySeparatorChar,
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal);
    }

    private static bool ContainsReparsePoint(string path, string root)
    {
        var current = File.Exists(path)
            ? new FileInfo(path).Directory
            : new DirectoryInfo(Path.GetDirectoryName(path)!);
        var normalizedRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(root));
        while (current != null)
        {
            if (current.Exists &&
                current.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                return true;
            }
            if (PathComparer.Equals(
                    Path.TrimEndingDirectorySeparator(current.FullName),
                    normalizedRoot))
            {
                break;
            }
            current = current.Parent;
        }

        return File.Exists(path) &&
            File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);
    }

    private static (int FileCount, long FreedBytes) DeleteLinkedFiles(
        BackupRetentionCandidate candidate,
        string knownRoot,
        string liveDatabasePath)
    {
        var paths = new List<(string Path, string Extension, bool IsMetadata)>
        {
            (candidate.BackupFilePath, ".db", false),
            (Path.ChangeExtension(candidate.BackupFilePath, ".metadata.json"), ".json", true)
        };
        if (!string.IsNullOrWhiteSpace(candidate.CompressedFilePath))
        {
            paths.Add((candidate.CompressedFilePath, ".zip", false));
        }

        var count = 0;
        long freed = 0;
        foreach (var item in paths
                     .Where(item => !string.IsNullOrWhiteSpace(item.Path))
                     .DistinctBy(
                         item => Path.GetFullPath(item.Path),
                         PathComparer))
        {
            var normalized = Path.GetFullPath(item.Path);
            if (!File.Exists(normalized))
            {
                continue;
            }

            var validation = item.IsMetadata
                ? ValidateMetadataPath(normalized, knownRoot, liveDatabasePath)
                : ValidateLinkedPath(
                    normalized,
                    item.Extension,
                    knownRoot,
                    liveDatabasePath);
            if (!string.IsNullOrEmpty(validation))
            {
                throw new InvalidOperationException(validation);
            }

            var size = new FileInfo(normalized).Length;
            File.Delete(normalized);
            count++;
            freed += size;
        }
        return (count, freed);
    }

    private async Task<string> ResolveKnownBackupRootAsync(
        AutomaticBackupSchedulerSettings settings,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(settings.DestinationFolder))
        {
            return Path.GetFullPath(settings.DestinationFolder.Trim());
        }

        var configured = await context.SystemSettings
            .AsNoTracking()
            .Where(item => item.Key == "BackupDefaultFolder")
            .Select(item => item.Value)
            .FirstOrDefaultAsync(cancellationToken);
        return Path.GetFullPath(
            string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.MyDocuments),
                    ProductBrand.BackupFolderName)
                : configured);
    }

    private string GetLiveDatabasePath()
    {
        var connectionString = context.Database.GetConnectionString();
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource) ||
            builder.DataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Automatic backup retention requires a file-based SQLite database.");
        }
        return Path.GetFullPath(builder.DataSource);
    }

    private static async Task<PendingRestoreState> ReadPendingRestoreAsync(
        string liveDatabasePath,
        CancellationToken cancellationToken)
    {
        var pendingPath =
            PendingRestoreProcessor.GetPendingRequestPath(liveDatabasePath);
        if (!File.Exists(pendingPath))
        {
            return new PendingRestoreState();
        }

        try
        {
            var json = await File.ReadAllTextAsync(
                pendingPath,
                cancellationToken);
            var metadata = JsonSerializer.Deserialize<PendingRestoreMetadata>(json)
                ?? throw new InvalidOperationException(
                    "Pending restore metadata is empty.");
            var protectedPaths = new[]
                {
                    metadata.OriginalBackupFilePath,
                    metadata.StagedBackupFilePath,
                    metadata.TargetDatabasePath,
                    metadata.SafetyBackupFilePath
                }
                .Select(NormalizePath)
                .Where(path => path != null)
                .Cast<string>()
                .ToHashSet(PathComparer);
            return new PendingRestoreState(protectedPaths, false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new PendingRestoreState(
                new HashSet<string>(PathComparer),
                true);
        }
    }

    private async Task LogStandaloneAsync(
        string action,
        string detail,
        int? userId,
        CancellationToken cancellationToken)
    {
        context.ActivityLogs.Add(new ActivityLog
        {
            Action = action,
            UserId = userId,
            IpAddress = "127.0.0.1",
            Detail = $"EntityName=BackupHistory;{detail}"
        });
        try
        {
            await SqliteRetryPolicy.ExecuteAsync(
                token => context.SaveChangesAsync(token),
                cancellationToken);
        }
        catch
        {
            // Authorization and retention outcomes remain authoritative.
        }
    }

    private static ActivityLog CreateLog(
        string action,
        int historyId,
        string detail,
        int userId) =>
        new()
        {
            Action = action,
            UserId = userId,
            IpAddress = "127.0.0.1",
            Detail =
                $"EntityName=BackupHistory;EntityId={historyId};{detail}"
        };

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return null;
        }
    }

    private static BackupRetentionPreviewResult PreviewFailure(string error) =>
        new()
        {
            Message = "Backup retention preview failed.",
            ErrorMessage = error
        };

    private static BackupRetentionDeleteResult DeleteFailure(string error) =>
        new()
        {
            Message = "Backup retention failed.",
            ErrorMessage = error
        };

    private static string Sanitize(string value)
    {
        var sanitized = value.ReplaceLineEndings(" ").Trim();
        return sanitized[..Math.Min(sanitized.Length, 1000)];
    }

    private static string Safe(string value) =>
        value.Replace(";", ",", StringComparison.Ordinal)
            .Replace("=", "-", StringComparison.Ordinal)
            .ReplaceLineEndings(" ");

    private sealed record PendingRestoreState(
        IReadOnlySet<string> ProtectedPaths,
        bool MetadataUnreadable)
    {
        public PendingRestoreState()
            : this(new HashSet<string>(PathComparer), false)
        {
        }
    }
}
