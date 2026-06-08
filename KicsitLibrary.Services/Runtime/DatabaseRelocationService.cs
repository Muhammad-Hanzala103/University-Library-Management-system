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

namespace KicsitLibrary.Services.Runtime;

public sealed class DatabaseRelocationService(
    KicsitLibraryDbContext context,
    IAuthenticationService authenticationService,
    IBackupService backupService,
    IDatabaseOwnershipService ownershipService) : IDatabaseRelocationService
{
    public async Task<DatabaseRelocationPreview> PreviewRelocationAsync(
        CancellationToken cancellationToken = default)
    {
        var current = GetCurrentDatabasePath();
        var target = await GetReleaseTargetPathAsync(cancellationToken);
        var preview = await BuildPreviewAsync(current, target, cancellationToken);
        await AddActivityAsync(
            "Database Relocation Preview",
            $"Current={Safe(current)};Target={Safe(target)};CanRelocate={preview.CanRelocate}",
            cancellationToken);
        return preview;
    }

    public async Task<DatabaseRelocationPreview> ValidateRelocationTargetAsync(
        string targetDatabasePath,
        CancellationToken cancellationToken = default)
    {
        return await BuildPreviewAsync(
            GetCurrentDatabasePath(),
            targetDatabasePath,
            cancellationToken);
    }

    public async Task<DatabaseRelocationStatus> GetRelocationStatusAsync(
        CancellationToken cancellationToken = default)
    {
        var values = await ReadRuntimeSettingsAsync(cancellationToken);
        var latest = await context.DatabaseRelocationHistories.AsNoTracking()
            .OrderByDescending(item => item.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return new DatabaseRelocationStatus
        {
            CurrentDatabasePath = GetCurrentDatabasePath(),
            TargetDatabasePath = await GetReleaseTargetPathAsync(cancellationToken),
            RuntimeDataRoot = GetReleaseRoot(values),
            RuntimeStorageMode = Read(values, "RuntimeStorageMode", "Development"),
            UseReleaseDataRoot = ReadBool(values, "UseReleaseDataRoot", false),
            CanManage = CanCurrentUserManage(),
            LastStatus = latest?.Status ?? string.Empty,
            LastMessage = latest?.ErrorMessage ?? latest?.Status ?? string.Empty
        };
    }

    public async Task<IReadOnlyList<DatabaseRelocationHistoryItem>> GetRelocationHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        return await context.DatabaseRelocationHistories.AsNoTracking()
            .OrderByDescending(item => item.StartedAt)
            .ThenByDescending(item => item.Id)
            .Take(200)
            .Select(item => new DatabaseRelocationHistoryItem
            {
                DatabaseRelocationHistoryId = item.Id,
                SourceDatabasePath = item.SourceDatabasePath,
                TargetDatabasePath = item.TargetDatabasePath,
                SafetyBackupPath = item.SafetyBackupPath ?? string.Empty,
                RequestedByUserName = item.RequestedByUserName,
                StartedAt = item.StartedAt,
                FinishedAt = item.FinishedAt,
                Status = item.Status,
                Reason = item.Reason ?? string.Empty,
                ErrorMessage = item.ErrorMessage ?? string.Empty,
                RollbackPerformed = item.RollbackPerformed,
                MetadataJson = item.MetadataJson ?? string.Empty
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<DatabaseRelocationResult> RelocateDatabaseAsync(
        DatabaseRelocationRequest request,
        CancellationToken cancellationToken = default)
    {
        request ??= new DatabaseRelocationRequest();
        var startedAt = DateTime.UtcNow;
        var source = string.IsNullOrWhiteSpace(request.SourceDatabasePath)
            ? GetCurrentDatabasePath()
            : Path.GetFullPath(request.SourceDatabasePath);
        var target = string.IsNullOrWhiteSpace(request.TargetDatabasePath)
            ? await GetReleaseTargetPathAsync(cancellationToken)
            : Path.GetFullPath(request.TargetDatabasePath);
        var history = new DatabaseRelocationHistory
        {
            SourceDatabasePath = source,
            TargetDatabasePath = target,
            RequestedByUserId = authenticationService.CurrentUser?.Id ?? request.RequestedByUserId,
            RequestedByUserName = authenticationService.CurrentUser?.FullName ?? request.RequestedByUserName,
            StartedAt = startedAt,
            Status = "Started",
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim()
        };
        context.DatabaseRelocationHistories.Add(history);
        await AddActivityAsync("Database Relocation Started", $"Source={Safe(source)};Target={Safe(target)}", cancellationToken);
        await SaveAsync(cancellationToken);

        var lockResult = await ownershipService.AcquireCriticalOperationLockAsync(
            "Database Relocation",
            source,
            cancellationToken);
        if (!lockResult.Succeeded)
        {
            return await FailAsync(history, startedAt, source, target, lockResult.ErrorMessage, cancellationToken);
        }

        string safetyBackupPath = string.Empty;
        var copied = false;
        var targetExistedAtStart = false;
        string? originalTargetSnapshotPath = null;

        try
        {
            if (!CanCurrentUserManage())
            {
                return await FailAsync(history, startedAt, source, target, "The current user cannot relocate the database.", cancellationToken);
            }
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return await FailAsync(history, startedAt, source, target, "A relocation reason is required.", cancellationToken);
            }
            if (!string.Equals(request.ConfirmationText, "RELOCATE", StringComparison.Ordinal))
            {
                return await FailAsync(history, startedAt, source, target, "Type RELOCATE exactly to confirm database relocation.", cancellationToken);
            }
            if (!request.CreateSafetyBackup)
            {
                return await FailAsync(history, startedAt, source, target, "A verified safety backup is required before relocation.", cancellationToken);
            }
            if (!request.VerifyBeforeMove || !request.VerifyAfterMove)
            {
                return await FailAsync(history, startedAt, source, target, "Source and target verification are required.", cancellationToken);
            }

            var preview = await BuildPreviewAsync(source, target, cancellationToken);
            if (!preview.CanRelocate)
            {
                return await FailAsync(
                    history,
                    startedAt,
                    source,
                    target,
                    string.Join(" ", preview.BlockingReasons),
                    cancellationToken);
            }

            var sourceValidation = await RestoreSqliteUtility.ValidateAsync(source, cancellationToken);
            await AddActivityAsync(
                sourceValidation.Succeeded ? "Database Relocation Source Validation Passed" : "Database Relocation Source Validation Failed",
                $"IntegrityCheckPassed={sourceValidation.IntegrityCheckPassed};Checksum={sourceValidation.ChecksumSha256}",
                cancellationToken);
            if (!sourceValidation.Succeeded)
            {
                return await FailAsync(history, startedAt, source, target, sourceValidation.ErrorMessage ?? sourceValidation.ValidationMessage, cancellationToken);
            }

            var backup = await backupService.CreateBackupAsync(new BackupRequest
            {
                RequestedByUserId = history.RequestedByUserId,
                RequestedByUserName = history.RequestedByUserName,
                DestinationFolder = Path.Combine(await GetReleaseBackupRootAsync(cancellationToken), "Relocation Safety"),
                IncludeTimestamp = true,
                IncludeMetadataFile = true,
                VerifyAfterCreation = true,
                CompressBackup = false,
                Reason = $"Mandatory pre-relocation safety backup: {request.Reason}".Trim()
            }, cancellationToken);
            if (!backup.Succeeded)
            {
                return await FailAsync(history, startedAt, source, target, $"Safety backup failed: {backup.ErrorMessage ?? backup.Message}", cancellationToken);
            }
            safetyBackupPath = backup.BackupFilePath;
            history.SafetyBackupPath = safetyBackupPath;
            await AddActivityAsync("Database Relocation Safety Backup Created", $"SafetyBackup={Safe(safetyBackupPath)}", cancellationToken);
            await SaveAsync(cancellationToken);

            sourceValidation = await RestoreSqliteUtility.ValidateAsync(source, cancellationToken);
            if (!sourceValidation.Succeeded)
            {
                return await FailAsync(history, startedAt, source, target, sourceValidation.ErrorMessage ?? sourceValidation.ValidationMessage, cancellationToken);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            targetExistedAtStart = File.Exists(target);
            if (targetExistedAtStart)
            {
                // Snapshot existing target to enable safe rollback if overwriting fails.
                originalTargetSnapshotPath = Path.Combine(
                    Path.GetDirectoryName(target)!,

            var targetValidation = await RestoreSqliteUtility.ValidateAsync(target, cancellationToken);
            if (!targetValidation.Succeeded)
            {
                TryDelete(target);
                history.RollbackPerformed = true;
                return await FailAsync(history, startedAt, source, target, targetValidation.ErrorMessage ?? targetValidation.ValidationMessage, cancellationToken);
            }

            var settingsUpdated = false;
            if (request.EnableReleaseDataRootAfterMove)
            {
                var root = Path.GetDirectoryName(target)!;
                await UpsertSettingAsync("RuntimeDataRoot", root, "Runtime", cancellationToken);
                await UpsertSettingAsync("UseReleaseDataRoot", "True", "Runtime", cancellationToken);
                await UpsertSettingAsync("RuntimeStorageMode", "Release", "Runtime", cancellationToken);
                await UpsertSettingAsync("DatabaseFileName", Path.GetFileName(target), "Runtime", cancellationToken);
                await SaveAsync(cancellationToken);
                await UpdateTargetSettingsAsync(target, root, Path.GetFileName(target), cancellationToken);
                var postSettingsValidation = await RestoreSqliteUtility.ValidateAsync(target, cancellationToken);
                if (!postSettingsValidation.Succeeded)
                {
                    return await FailAsync(history, startedAt, source, target, postSettingsValidation.ErrorMessage ?? postSettingsValidation.ValidationMessage, cancellationToken);
                }
                settingsUpdated = true;
            }

            history.Status = "Completed";
            history.FinishedAt = DateTime.UtcNow;
            history.MetadataJson = JsonSerializer.Serialize(new
            {
                ProductName = ProductBrand.Name,
                SourceChecksumSha256 = sourceValidation.ChecksumSha256,
                TargetChecksumSha256 = targetValidation.ChecksumSha256,
                SafetyBackupPath = safetyBackupPath,
                CopyOnly = true,
                SettingsUpdated = settingsUpdated
            });
            await AddActivityAsync("Database Relocation Completed", $"SourcePreserved=True;Target={Safe(target)};SettingsUpdated={settingsUpdated}", cancellationToken);
            await SaveAsync(cancellationToken);

            return new DatabaseRelocationResult
            {
                Succeeded = true,
                SourceDatabasePath = source,
                TargetDatabasePath = target,
                SafetyBackupPath = safetyBackupPath,
                StartedAt = startedAt,
                FinishedAt = history.FinishedAt.Value,
                Copied = copied,
                Moved = false,
                SettingsUpdated = settingsUpdated,
                Message = "Database was copied to the release runtime root and verified. The source database was preserved."
            };
        }
        catch (Exception ex)
        {
            if (copied && File.Exists(target))
            {
                TryDelete(target);
                history.RollbackPerformed = true;
                await AddActivityAsync("Database Relocation Rollback Performed", $"TargetDeleted={Safe(target)}", CancellationToken.None);
            }
            return await FailAsync(history, startedAt, source, target, Sanitize(ex.Message), cancellationToken, safetyBackupPath);
        }
        finally
        {
            await ownershipService.ReleaseCriticalOperationLockAsync("Database Relocation", source);
        }
    }

    private async Task<DatabaseRelocationPreview> BuildPreviewAsync(
        string source,
        string target,
        CancellationToken cancellationToken)
    {
        source = Path.GetFullPath(source);
        target = Path.GetFullPath(target);
        var blockers = new List<string>();
        var releaseRoot = await GetReleaseDataRootAsync(cancellationToken);
        if (!File.Exists(source))
        {
            blockers.Add("Source database does not exist.");
        }
        if (!IsUnderRoot(target, releaseRoot))
        {
            blockers.Add("Target database must be under the runtime data root.");
        }
        if (IsUnderSourceCodeFolder(target))
        {
            blockers.Add("Target database must not be inside the source code folder.");
        }
        if (IsUnderProgramFiles(target))
        {
            blockers.Add("Target database must not be under Program Files.");
        }
        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add("Source and target database paths are the same.");
        }

        return new DatabaseRelocationPreview
        {
            Succeeded = blockers.Count == 0,
            CurrentDatabasePath = source,
            TargetDatabasePath = target,
            TargetExists = File.Exists(target),
            CurrentSizeBytes = File.Exists(source) ? new FileInfo(source).Length : 0,
            TargetSizeBytes = File.Exists(target) ? new FileInfo(target).Length : 0,
            SafetyBackupRequired = true,
            CanRelocate = blockers.Count == 0,
            BlockingReasons = blockers,
            Message = blockers.Count == 0
                ? "Database relocation preview passed. A safety backup and confirmation are still required."
                : "Database relocation is blocked."
        };
    }

    private string GetCurrentDatabasePath()
    {
        var dataSource = context.Database.GetDbConnection().DataSource;
        if (string.IsNullOrWhiteSpace(dataSource) ||
            dataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Database relocation requires a file-based SQLite database.");
        }
        return Path.GetFullPath(dataSource);
    }

    private async Task<string> GetReleaseTargetPathAsync(CancellationToken cancellationToken)
    {
        var settings = await ReadRuntimeSettingsAsync(cancellationToken);
        return Path.GetFullPath(Path.Combine(
            GetReleaseRoot(settings),
            Read(settings, "DatabaseFileName", "KicsitLibrary.db")));
    }

    private async Task<string> GetReleaseDataRootAsync(CancellationToken cancellationToken) =>
        GetReleaseRoot(await ReadRuntimeSettingsAsync(cancellationToken));

    private async Task<string> GetReleaseBackupRootAsync(CancellationToken cancellationToken) =>
        Path.Combine(
            await GetReleaseDataRootAsync(cancellationToken),
            "Backups");

    private async Task<Dictionary<string, string>> ReadRuntimeSettingsAsync(CancellationToken cancellationToken)
    {
        var keys = new[] { "RuntimeDataRoot", "RuntimeStorageMode", "UseReleaseDataRoot", "DatabaseFileName" };
        return await context.SystemSettings.AsNoTracking()
            .Where(item => keys.Contains(item.Key))
            .ToDictionaryAsync(item => item.Key, item => item.Value, cancellationToken);
    }

    private static string GetReleaseRoot(IReadOnlyDictionary<string, string> settings)
    {
        var root = Read(settings, "RuntimeDataRoot", string.Empty);
        return Path.GetFullPath(string.IsNullOrWhiteSpace(root)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductBrand.Name)
            : root);
    }

    private static bool IsUnderRoot(string path, string root)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var normalizedPath = Path.GetFullPath(path);
        return normalizedPath.StartsWith(
            normalizedRoot + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnderSourceCodeFolder(string path)
    {
        var current = Directory.GetCurrentDirectory();
        return IsUnderRoot(path, current) ||
            path.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnderProgramFiles(string path)
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        }.Where(item => !string.IsNullOrWhiteSpace(item));
        return roots.Any(root => IsUnderRoot(path, root));
    }

    private bool CanCurrentUserManage()
    {
        var user = authenticationService.CurrentUser;
        return user?.UserRoles.Any(userRole =>
            userRole.Role.Name is "Super Admin" or "Admin") == true;
    }

    private async Task UpsertSettingAsync(
        string key,
        string value,
        string group,
        CancellationToken cancellationToken)
    {
        var setting = await context.SystemSettings.FirstOrDefaultAsync(item => item.Key == key, cancellationToken);
        if (setting == null)
        {
            context.SystemSettings.Add(new SystemSettings { Key = key, Value = value, Group = group });
        }
        else
        {
            setting.Value = value;
            setting.Group = group;
        }
    }

    private static async Task UpdateTargetSettingsAsync(
        string targetPath,
        string runtimeRoot,
        string databaseFileName,
        CancellationToken cancellationToken)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = targetPath, Pooling = false };
        await using var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync(cancellationToken);
        foreach (var setting in new Dictionary<string, string>
        {
            ["RuntimeDataRoot"] = runtimeRoot,
            ["UseReleaseDataRoot"] = "True",
            ["RuntimeStorageMode"] = "Release",
            ["DatabaseFileName"] = databaseFileName
        })
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO SystemSettings (Key, Value, Description, \"Group\", CreatedAt, IsDeleted) " +
                "VALUES ($key, $value, '', 'Runtime', $createdAt, 0) " +
                "ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value, \"Group\" = 'Runtime';";
            command.Parameters.AddWithValue("$key", setting.Key);
            command.Parameters.AddWithValue("$value", setting.Value);
            command.Parameters.AddWithValue("$createdAt", DateTime.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task<DatabaseRelocationResult> FailAsync(
        DatabaseRelocationHistory history,
        DateTime startedAt,
        string source,
        string target,
        string error,
        CancellationToken cancellationToken,
        string safetyBackupPath = "")
    {
        history.Status = "Failed";
        history.FinishedAt = DateTime.UtcNow;
        history.ErrorMessage = Sanitize(error);
        if (!string.IsNullOrWhiteSpace(safetyBackupPath))
        {
            history.SafetyBackupPath = safetyBackupPath;
        }
        await AddActivityAsync("Database Relocation Failed", $"Error={Safe(error)}", cancellationToken);
        await SaveAsync(cancellationToken);
        return new DatabaseRelocationResult
        {
            Succeeded = false,
            SourceDatabasePath = source,
            TargetDatabasePath = target,
            SafetyBackupPath = history.SafetyBackupPath ?? string.Empty,
            StartedAt = startedAt,
            FinishedAt = history.FinishedAt.Value,
            Message = "Database relocation failed.",
            ErrorMessage = history.ErrorMessage,
            RollbackPerformed = history.RollbackPerformed
        };
    }

    private async Task AddActivityAsync(string action, string detail, CancellationToken cancellationToken)
    {
        context.ActivityLogs.Add(new ActivityLog
        {
            Action = action,
            UserId = authenticationService.CurrentUser?.Id,
            IpAddress = "127.0.0.1",
            Detail = $"EntityName=DatabaseRelocation;{detail}"
        });
        await SaveAsync(cancellationToken);
    }

    private async Task SaveAsync(CancellationToken cancellationToken) =>
        await SqliteRetryPolicy.ExecuteAsync(
            token => context.SaveChangesAsync(token),
            cancellationToken);

    private static string Read(IReadOnlyDictionary<string, string> values, string key, string fallback) =>
        values.TryGetValue(key, out var value) ? value : fallback;

    private static bool ReadBool(IReadOnlyDictionary<string, string> values, string key, bool fallback) =>
        values.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) ? parsed : fallback;

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Preserve failures in the relocation result and activity log path.
        }
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
