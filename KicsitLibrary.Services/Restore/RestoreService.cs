using System.Text.Json;
using KicsitLibrary.Core;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data;
using KicsitLibrary.Services.Notifications;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Services.Restore;

public sealed class RestoreService(
    KicsitLibraryDbContext context,
    IAuthenticationService authenticationService,
    IBackupService backupService,
    IDatabaseOwnershipService ownershipService) : IRestoreService
{
    private static readonly SemaphoreSlim RestoreLock = new(1, 1);

    public async Task<RestorePreviewResult> PreviewRestoreAsync(
        string backupFilePath,
        CancellationToken cancellationToken = default)
    {
        await RequireViewAsync();
        var validation = await ValidateBackupForRestoreAsync(backupFilePath, cancellationToken);
        if (!validation.Succeeded)
        {
            await AddActivityAsync(
                "Restore Preview Failed",
                $"FileName={Safe(Path.GetFileName(backupFilePath))};Error={Safe(validation.ErrorMessage ?? validation.ValidationMessage)}",
                cancellationToken);
            return new RestorePreviewResult
            {
                BackupFilePath = backupFilePath,
                ErrorMessage = validation.ErrorMessage,
                Message = "Restore preview failed."
            };
        }

        try
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = backupFilePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            };
            await using var connection = new SqliteConnection(builder.ToString());
            await connection.OpenAsync(cancellationToken);
            await using var tablesCommand = connection.CreateCommand();
            tablesCommand.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';";
            var result = new RestorePreviewResult
            {
                Succeeded = true,
                BackupFilePath = Path.GetFullPath(backupFilePath),
                BackupSizeBytes = validation.FileSizeBytes,
                ChecksumSha256 = validation.ChecksumSha256,
                IntegrityCheckPassed = validation.IntegrityCheckPassed,
                BackupCreatedAt = File.GetCreationTimeUtc(backupFilePath),
                DetectedTablesCount = Convert.ToInt32(
                    await tablesCommand.ExecuteScalarAsync(cancellationToken)),
                DetectedUserCount = await RestoreSqliteUtility.CountRowsIfTableExistsAsync(
                    connection, "Users", cancellationToken),
                DetectedBookCopyCount = await RestoreSqliteUtility.CountRowsIfTableExistsAsync(
                    connection, "BookCopies", cancellationToken),
                DetectedIssueRecordCount = await RestoreSqliteUtility.CountRowsIfTableExistsAsync(
                    connection, "IssueRecords", cancellationToken),
                DetectedBackupHistoryRecord =
                    await RestoreSqliteUtility.CountRowsIfTableExistsAsync(
                        connection, "BackupHistories", cancellationToken) > 0,
                Message = "Backup preview and SQLite integrity validation passed."
            };
            await AddActivityAsync(
                "Restore Preview Completed",
                $"FileName={Safe(Path.GetFileName(backupFilePath))};Tables={result.DetectedTablesCount}",
                cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            await AddActivityAsync(
                "Restore Preview Failed",
                $"FileName={Safe(Path.GetFileName(backupFilePath))};Error={Safe(ex.Message)}",
                cancellationToken);
            return new RestorePreviewResult
            {
                BackupFilePath = backupFilePath,
                Message = "Restore preview failed.",
                ErrorMessage = Sanitize(ex.Message)
            };
        }
    }

    public async Task<RestoreValidationResult> ValidateBackupForRestoreAsync(
        string backupFilePath,
        CancellationToken cancellationToken = default)
    {
        await RequireViewAsync();
        var result = await RestoreSqliteUtility.ValidateAsync(backupFilePath, cancellationToken);
        await AddActivityAsync(
            result.Succeeded ? "Restore Validation Passed" : "Restore Validation Failed",
            $"FileName={Safe(Path.GetFileName(backupFilePath))};IntegrityCheckPassed={result.IntegrityCheckPassed};Error={Safe(result.ErrorMessage ?? string.Empty)}",
            cancellationToken);
        return result;
    }

    public async Task<RestoreResult> RestoreFromBackupAsync(
        RestoreRequest request,
        CancellationToken cancellationToken = default)
    {
        request ??= new RestoreRequest();
        var startedAt = DateTime.UtcNow;
        var targetPath = GetDatabasePath();
        var lockResult = await ownershipService.AcquireCriticalOperationLockAsync("Restore Staging", targetPath, cancellationToken);
        if (!lockResult.Succeeded)
        {
            return new RestoreResult
            {
                Succeeded = false,
                Message = "Restore staging failed.",
                ErrorMessage = lockResult.ErrorMessage,
                StartedAt = startedAt,
                FinishedAt = DateTime.UtcNow
            };
        }

        var semaphoreHeld = false;
        RestoreHistory? history = null;

        try
        {
            await RestoreLock.WaitAsync(cancellationToken);
            semaphoreHeld = true;
            EnsureSqliteProvider();
            var user = authenticationService.CurrentUser;
            history = new RestoreHistory
            {
                BackupFilePath = request.BackupFilePath,
                RestoredDatabasePath = targetPath,
                RequestedByUserId = user?.Id ?? request.RequestedByUserId,
                RequestedByUserName = user?.FullName ?? request.RequestedByUserName,
                StartedAt = startedAt,
                Status = "Started",
                Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim()
            };
            context.RestoreHistories.Add(history);
            AddLog("Restore Attempt Started", history, $"FileName={Safe(Path.GetFileName(request.BackupFilePath))}");
            await SaveAsync(cancellationToken);

            if (!await RestoreAuthorization.CanManageAsync(authenticationService))
            {
                return await FailAsync(history, "The current user cannot restore databases.", cancellationToken);
            }
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return await FailAsync(history, "A restore reason is required.", cancellationToken);
            }
            if (request.RequireConfirmationText &&
                !string.Equals(request.ConfirmationText, "RESTORE", StringComparison.Ordinal))
            {
                return await FailAsync(history, "Type RESTORE exactly to confirm the restore.", cancellationToken);
            }
            if (!request.CreateSafetyBackup)
            {
                return await FailAsync(history, "A safety backup is mandatory before restore.", cancellationToken);
            }
            if (!request.VerifyBeforeRestore)
            {
                return await FailAsync(history, "Pre-restore verification is mandatory.", cancellationToken);
            }
            if (request.AllowRestoreWhileAppRunning)
            {
                return await FailAsync(
                    history,
                    "Direct restore while the application is running is not supported.",
                    cancellationToken);
            }

            var validation = await RestoreSqliteUtility.ValidateAsync(
                request.BackupFilePath, cancellationToken);
            if (!validation.Succeeded)
            {
                return await FailAsync(
                    history,
                    validation.ErrorMessage ?? validation.ValidationMessage,
                    cancellationToken);
            }

            history.ChecksumSha256 = validation.ChecksumSha256;
            AddLog("Restore Validation Passed", history,
                $"ChecksumSha256={validation.ChecksumSha256};SizeBytes={validation.FileSizeBytes}");
            await SaveAsync(cancellationToken);

            var safetyBackup = await CreateSafetyBackupBeforeRestoreAsync(request, cancellationToken);
            if (!safetyBackup.Succeeded)
            {
                return await FailAsync(
                    history,
                    $"Safety backup failed: {safetyBackup.ErrorMessage ?? safetyBackup.Message}",
                    cancellationToken);
            }

            history.SafetyBackupFilePath = safetyBackup.BackupFilePath;
            AddLog("Restore Safety Backup Created", history,
                $"SafetyBackupFilePath={Safe(safetyBackup.BackupFilePath)}");
            await SaveAsync(cancellationToken);

            var pendingPath = PendingRestoreProcessor.GetPendingRequestPath(targetPath);
            if (File.Exists(pendingPath))
            {
                return await FailAsync(
                    history,
                    "Another restore is already pending application restart.",
                    cancellationToken);
            }

            var stagingDirectory = Path.GetDirectoryName(pendingPath)!;
            Directory.CreateDirectory(stagingDirectory);
            var stagedPath = Path.Combine(
                stagingDirectory,
                $"staged-restore-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.db");
            File.Copy(request.BackupFilePath, stagedPath, overwrite: false);
            var stagedValidation = await RestoreSqliteUtility.ValidateAsync(stagedPath, cancellationToken);
            if (!stagedValidation.Succeeded ||
                !string.Equals(
                    stagedValidation.ChecksumSha256,
                    validation.ChecksumSha256,
                    StringComparison.Ordinal))
            {
                return await FailAsync(
                    history,
                    "The staged backup failed integrity or checksum verification.",
                    cancellationToken);
            }

            var metadata = new PendingRestoreMetadata
            {
                ProductName = ProductBrand.Name,
                RestoreHistoryId = history.Id,
                OriginalBackupFilePath = Path.GetFullPath(request.BackupFilePath),
                StagedBackupFilePath = stagedPath,
                TargetDatabasePath = targetPath,
                SafetyBackupFilePath = safetyBackup.BackupFilePath,
                ChecksumSha256 = validation.ChecksumSha256,
                RequestedByUserId = history.RequestedByUserId,
                RequestedByUserName = history.RequestedByUserName,
                Reason = request.Reason.Trim(),
                RequestedAt = startedAt,
                VerifyAfterRestore = request.VerifyAfterRestore
            };
            var json = JsonSerializer.Serialize(
                metadata,
                new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(pendingPath, json, cancellationToken);

            history.Status = "PendingRestart";
            history.MetadataJson = json;
            AddLog("Restore Staged", history,
                $"PendingRequest={Safe(pendingPath)};StagedFile={Safe(stagedPath)}");
            await SaveAsync(cancellationToken);
            return new RestoreResult
            {
                Succeeded = true,
                RestoreHistoryId = history.Id,
                BackupFilePath = request.BackupFilePath,
                SafetyBackupFilePath = safetyBackup.BackupFilePath,
                RestoredDatabasePath = targetPath,
                StartedAt = startedAt,
                FinishedAt = DateTime.UtcNow,
                RequiresApplicationRestart = true,
                Message = "Restore was verified and staged. Restart the application to apply it."
            };
        }
        catch (OperationCanceledException)
        {
            if (history != null)
            {
                await FailAsync(history, "Restore staging was cancelled.", CancellationToken.None);
            }
            throw;
        }
        catch (Exception ex)
        {
            if (history != null)
            {
                return await FailAsync(history, Sanitize(ex.Message), cancellationToken);
            }
            return Failure(request.BackupFilePath, startedAt, Sanitize(ex.Message));
        }
        finally
        {
            if (semaphoreHeld)
            {
                RestoreLock.Release();
            }
            await ownershipService.ReleaseCriticalOperationLockAsync("Restore Staging", targetPath);
        }
    }

    public async Task<IReadOnlyList<RestoreHistoryItem>> GetRestoreHistoryAsync(
        RestoreHistoryFilter filter,
        CancellationToken cancellationToken = default)
    {
        await RequireViewAsync();
        filter ??= new RestoreHistoryFilter();
        var query = context.RestoreHistories.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var search = filter.SearchText.Trim();
            query = query.Where(item =>
                item.BackupFilePath.Contains(search) ||
                item.RequestedByUserName.Contains(search) ||
                (item.Reason != null && item.Reason.Contains(search)));
        }
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(item => item.Status == filter.Status);
        }
        if (filter.FromDate.HasValue)
        {
            query = query.Where(item => item.StartedAt >= filter.FromDate.Value.Date);
        }
        if (filter.ToDate.HasValue)
        {
            query = query.Where(item => item.StartedAt < filter.ToDate.Value.Date.AddDays(1));
        }

        return await query
            .OrderByDescending(item => item.StartedAt)
            .ThenByDescending(item => item.Id)
            .Take(Math.Clamp(filter.Limit, 1, 5000))
            .Select(item => new RestoreHistoryItem
            {
                RestoreHistoryId = item.Id,
                BackupFilePath = item.BackupFilePath,
                SafetyBackupFilePath = item.SafetyBackupFilePath ?? string.Empty,
                RestoredDatabasePath = item.RestoredDatabasePath,
                RequestedByUserName = item.RequestedByUserName,
                CreatedAt = item.StartedAt,
                FinishedAt = item.FinishedAt,
                Status = item.Status,
                Reason = item.Reason ?? string.Empty,
                ErrorMessage = item.ErrorMessage ?? string.Empty,
                RolledBack = item.RolledBack,
                ChecksumSha256 = item.ChecksumSha256 ?? string.Empty,
                MetadataJson = item.MetadataJson ?? string.Empty
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<RestoreStatusSummary> GetRestoreStatusSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        await RequireViewAsync();
        var rows = await context.RestoreHistories.AsNoTracking()
            .Select(item => new { item.StartedAt, item.Status, item.RolledBack })
            .ToListAsync(cancellationToken);
        var latest = rows.OrderByDescending(item => item.StartedAt).FirstOrDefault();
        return new RestoreStatusSummary
        {
            TotalRestores = rows.Count,
            PendingRestarts = rows.Count(item => item.Status == "PendingRestart"),
            SuccessfulRestores = rows.Count(item => item.Status == "Completed"),
            FailedRestores = rows.Count(item => item.Status == "Failed"),
            RolledBackRestores = rows.Count(item => item.RolledBack),
            LastRestoreAt = latest?.StartedAt,
            LastRestoreStatus = latest?.Status ?? string.Empty
        };
    }

    public async Task<BackupResult> CreateSafetyBackupBeforeRestoreAsync(
        RestoreRequest request,
        CancellationToken cancellationToken = default)
    {
        var settings = await backupService.GetBackupSettingsAsync(cancellationToken);
        var root = string.IsNullOrWhiteSpace(settings.DefaultFolder)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                ProductBrand.BackupFolderName)
            : settings.DefaultFolder;
        var safetyFolder = Path.Combine(root, "Restore Safety");
        return await backupService.CreateBackupAsync(new BackupRequest
        {
            RequestedByUserId = authenticationService.CurrentUser?.Id ?? request.RequestedByUserId,
            RequestedByUserName =
                authenticationService.CurrentUser?.FullName ?? request.RequestedByUserName,
            DestinationFolder = safetyFolder,
            IncludeTimestamp = true,
            IncludeMetadataFile = true,
            VerifyAfterCreation = true,
            CompressBackup = false,
            Reason = $"Mandatory pre-restore safety backup: {request.Reason}".Trim()
        }, cancellationToken);
    }

    private async Task<RestoreResult> FailAsync(
        RestoreHistory history,
        string error,
        CancellationToken cancellationToken)
    {
        history.Status = "Failed";
        history.FinishedAt = DateTime.UtcNow;
        history.ErrorMessage = Sanitize(error);
        AddLog("Restore Failed", history, $"Error={Safe(error)}");
        await SaveAsync(cancellationToken);
        return new RestoreResult
        {
            RestoreHistoryId = history.Id,
            BackupFilePath = history.BackupFilePath,
            SafetyBackupFilePath = history.SafetyBackupFilePath ?? string.Empty,
            RestoredDatabasePath = history.RestoredDatabasePath,
            StartedAt = history.StartedAt,
            FinishedAt = history.FinishedAt.Value,
            Message = "Restore failed.",
            ErrorMessage = history.ErrorMessage
        };
    }

    private async Task RequireViewAsync()
    {
        if (!await RestoreAuthorization.CanViewAsync(authenticationService))
        {
            throw new UnauthorizedAccessException(
                "The current user cannot view restore information.");
        }
    }

    private void EnsureSqliteProvider()
    {
        if (!string.Equals(
                context.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.Sqlite",
                StringComparison.Ordinal))
        {
            throw new NotSupportedException("Priority 8B restore supports SQLite databases only.");
        }
    }

    private string GetDatabasePath()
    {
        var connectionString = context.Database.GetConnectionString();
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource) ||
            builder.DataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Restore requires a file-based SQLite database.");
        }
        return Path.GetFullPath(builder.DataSource);
    }

    private async Task AddActivityAsync(
        string action,
        string detail,
        CancellationToken cancellationToken)
    {
        context.ActivityLogs.Add(new ActivityLog
        {
            Action = action,
            UserId = authenticationService.CurrentUser?.Id,
            IpAddress = "127.0.0.1",
            Detail = $"EntityName=RestoreHistory;{detail}"
        });
        await SaveAsync(cancellationToken);
    }

    private void AddLog(string action, RestoreHistory history, string detail) =>
        context.ActivityLogs.Add(new ActivityLog
        {
            Action = action,
            UserId = authenticationService.CurrentUser?.Id,
            IpAddress = "127.0.0.1",
            Detail = $"EntityName=RestoreHistory;EntityId={history.Id};{detail}"
        });

    private async Task SaveAsync(CancellationToken cancellationToken) =>
        await SqliteRetryPolicy.ExecuteAsync(
            token => context.SaveChangesAsync(token),
            cancellationToken);

    private static RestoreResult Failure(
        string backupFilePath,
        DateTime startedAt,
        string error) =>
        new()
        {
            BackupFilePath = backupFilePath,
            StartedAt = startedAt,
            FinishedAt = DateTime.UtcNow,
            Message = "Restore failed.",
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
}
