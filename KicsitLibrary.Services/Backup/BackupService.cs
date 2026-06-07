using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using KicsitLibrary.Core;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data;
using KicsitLibrary.Services.Notifications;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Services.Backup;

public sealed class BackupService(
    KicsitLibraryDbContext context,
    IAuthenticationService authenticationService) : IBackupService
{
    private static readonly SemaphoreSlim BackupLock = new(1, 1);

    public async Task<BackupResult> CreateBackupAsync(
        BackupRequest request,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;
        if (!await BackupAuthorization.CanManageAsync(authenticationService))
        {
            await RecordDeniedActionAsync(
                "Backup Creation Blocked",
                "The current user cannot create backups.",
                cancellationToken);
            return Failure(startedAt, "The current user cannot create backups.");
        }

        request ??= new BackupRequest();
        await BackupLock.WaitAsync(cancellationToken);
        BackupHistory? history = null;
        string backupPath = string.Empty;
        string metadataPath = string.Empty;
        string compressedPath = string.Empty;

        try
        {
            EnsureSqliteProvider();
            var user = authenticationService.CurrentUser!;
            var settings = await GetBackupSettingsAsync(cancellationToken);
            var destinationFolder = ResolveDestinationFolder(request.DestinationFolder, settings.DefaultFolder);
            Directory.CreateDirectory(destinationFolder);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var safeUserName = SanitizeFilePart(user.FullName);
            var baseName = $"Ilm-o-Kutub_Backup_{safeUserName}_{timestamp}";
            backupPath = ResolveUniquePath(destinationFolder, baseName, ".db");
            metadataPath = Path.ChangeExtension(backupPath, ".metadata.json");
            compressedPath = Path.ChangeExtension(backupPath, ".zip");

            history = new BackupHistory
            {
                BackupFileName = Path.GetFileName(backupPath),
                BackupFilePath = backupPath,
                CreatedByUserId = user.Id,
                CreatedByUserName = user.FullName,
                Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
                Status = "InProgress",
                VerificationStatus = "Pending"
            };
            context.BackupHistories.Add(history);
            AddLog("Backup Creation Started", history, $"FileName={history.BackupFileName}");
            await SaveAsync(cancellationToken);

            await CreateOnlineBackupAsync(backupPath, cancellationToken);
            var fileInfo = new FileInfo(backupPath);
            if (!fileInfo.Exists || fileInfo.Length == 0)
            {
                throw new InvalidOperationException("SQLite created an empty backup file.");
            }

            BackupVerificationResult verification;
            if (request.VerifyAfterCreation)
            {
                verification = await VerifyCoreAsync(backupPath, cancellationToken);
                ApplyVerification(history, verification);
                AddLog(
                    verification.Succeeded ? "Backup Verification Passed" : "Backup Verification Failed",
                    history,
                    $"IntegrityCheckPassed={verification.IntegrityCheckPassed};FileName={history.BackupFileName}");
                if (!verification.Succeeded)
                {
                    throw new InvalidOperationException(
                        verification.ErrorMessage ?? "Backup integrity verification failed.");
                }
            }
            else
            {
                verification = await GetChecksumOnlyAsync(backupPath, cancellationToken);
                history.ChecksumSha256 = verification.ChecksumSha256;
                history.BackupSizeBytes = verification.FileSizeBytes;
                history.VerificationStatus = "NotRequested";
            }

            var includeMetadata = request.IncludeMetadataFile || request.CompressBackup;
            var metadataJson = CreateMetadataJson(history, request, verification, startedAt);
            history.MetadataJson = metadataJson;
            if (includeMetadata)
            {
                await File.WriteAllTextAsync(metadataPath, metadataJson, cancellationToken);
            }
            else
            {
                metadataPath = string.Empty;
            }

            string? compressionWarning = null;
            if (request.CompressBackup)
            {
                try
                {
                    await CreateZipAsync(backupPath, metadataPath, compressedPath, cancellationToken);
                    history.CompressedFilePath = compressedPath;
                }
                catch (Exception ex)
                {
                    compressionWarning = SanitizeError(ex.Message);
                    TryDeleteFile(compressedPath);
                    compressedPath = string.Empty;
                    history.ErrorMessage = $"Compression failed: {compressionWarning}";
                    AddLog("Backup Compression Failed", history, $"FileName={history.BackupFileName};Error={compressionWarning}");
                }
            }
            else
            {
                compressedPath = string.Empty;
            }

            history.Status = compressionWarning == null ? "Completed" : "CompletedWithWarnings";
            history.BackupSizeBytes = new FileInfo(backupPath).Length;
            AddLog("Backup Created", history,
                $"FileName={history.BackupFileName};SizeBytes={history.BackupSizeBytes};Status={history.Status}");
            await SaveAsync(cancellationToken);

            return new BackupResult
            {
                Succeeded = true,
                BackupHistoryId = history.Id,
                BackupFilePath = backupPath,
                MetadataFilePath = metadataPath,
                CompressedFilePath = compressedPath,
                BackupSizeBytes = history.BackupSizeBytes,
                ChecksumSha256 = history.ChecksumSha256 ?? string.Empty,
                StartedAt = startedAt,
                FinishedAt = DateTime.UtcNow,
                Message = compressionWarning == null
                    ? "SQLite backup created and recorded successfully."
                    : "SQLite backup is valid, but compression failed.",
                ErrorMessage = compressionWarning
            };
        }
        catch (OperationCanceledException)
        {
            if (history != null)
            {
                await RecordFailureAsync(history, "Backup creation was cancelled.", CancellationToken.None);
            }
            TryDeleteFileIfEmpty(backupPath);
            throw;
        }
        catch (Exception ex)
        {
            var error = SanitizeError(ex.Message);
            if (history == null)
            {
                history = await CreateFailureHistoryAsync(request, backupPath, error, cancellationToken);
            }
            else
            {
                await RecordFailureAsync(history, error, cancellationToken);
            }

            TryDeleteFileIfEmpty(backupPath);
            TryDeleteFile(compressedPath);
            return new BackupResult
            {
                Succeeded = false,
                BackupHistoryId = history?.Id,
                BackupFilePath = backupPath,
                MetadataFilePath = metadataPath,
                BackupSizeBytes = File.Exists(backupPath) ? new FileInfo(backupPath).Length : 0,
                ChecksumSha256 = history?.ChecksumSha256 ?? string.Empty,
                StartedAt = startedAt,
                FinishedAt = DateTime.UtcNow,
                Message = "Backup creation failed.",
                ErrorMessage = error
            };
        }
        finally
        {
            BackupLock.Release();
        }
    }

    public async Task<BackupVerificationResult> VerifyBackupAsync(
        string filePath,
        int? backupHistoryId = null,
        CancellationToken cancellationToken = default)
    {
        if (!await BackupAuthorization.CanManageAsync(authenticationService))
        {
            await RecordDeniedActionAsync(
                "Backup Verification Blocked",
                "The current user cannot verify backups.",
                cancellationToken);
            return VerificationFailure(filePath, "The current user cannot verify backups.");
        }

        var result = await VerifyCoreAsync(filePath, cancellationToken);
        var history = backupHistoryId.HasValue
            ? await context.BackupHistories.FirstOrDefaultAsync(
                item => item.Id == backupHistoryId.Value, cancellationToken)
            : await context.BackupHistories.FirstOrDefaultAsync(
                item => item.BackupFilePath == filePath, cancellationToken);

        if (history != null)
        {
            ApplyVerification(history, result);
            if (!result.Succeeded)
            {
                history.ErrorMessage = result.ErrorMessage;
            }
            AddLog(
                result.Succeeded ? "Backup Verification Passed" : "Backup Verification Failed",
                history,
                $"IntegrityCheckPassed={result.IntegrityCheckPassed};FileName={history.BackupFileName}");
        }
        else
        {
            context.ActivityLogs.Add(new ActivityLog
            {
                Action = result.Succeeded ? "Backup Verification Passed" : "Backup Verification Failed",
                UserId = authenticationService.CurrentUser!.Id,
                IpAddress = "127.0.0.1",
                Detail = $"EntityName=BackupHistory;FileName={SanitizeDetail(Path.GetFileName(filePath))};IntegrityCheckPassed={result.IntegrityCheckPassed}"
            });
        }

        await SaveAsync(cancellationToken);
        return result;
    }

    public async Task<IReadOnlyList<BackupHistoryItem>> GetBackupHistoryAsync(
        BackupHistoryFilter filter,
        CancellationToken cancellationToken = default)
    {
        await RequireViewAsync();
        filter ??= new BackupHistoryFilter();
        var settings = await GetBackupSettingsAsync(cancellationToken);
        var query = context.BackupHistories.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var search = filter.SearchText.Trim();
            query = query.Where(item =>
                item.BackupFileName.Contains(search) ||
                item.CreatedByUserName.Contains(search) ||
                (item.Reason != null && item.Reason.Contains(search)));
        }
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(item => item.Status == filter.Status);
        }
        if (!string.IsNullOrWhiteSpace(filter.VerificationStatus))
        {
            query = query.Where(item => item.VerificationStatus == filter.VerificationStatus);
        }
        if (!string.IsNullOrWhiteSpace(filter.CreatedBy))
        {
            query = query.Where(item => item.CreatedByUserName.Contains(filter.CreatedBy));
        }
        if (filter.FromDate.HasValue)
        {
            query = query.Where(item => item.CreatedAt >= filter.FromDate.Value.Date);
        }
        if (filter.ToDate.HasValue)
        {
            query = query.Where(item => item.CreatedAt < filter.ToDate.Value.Date.AddDays(1));
        }

        var limit = Math.Clamp(
            filter.Limit <= 0 ? settings.MaxHistoryRows : filter.Limit,
            1,
            Math.Max(settings.MaxHistoryRows, 500));
        return await query
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Take(limit)
            .Select(item => Map(item))
            .ToListAsync(cancellationToken);
    }

    public async Task<BackupStatusSummary> GetBackupStatusSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        await RequireViewAsync();
        var rows = await context.BackupHistories.AsNoTracking()
            .Select(item => new
            {
                item.CreatedAt,
                item.Status,
                item.VerificationStatus,
                item.BackupSizeBytes
            })
            .ToListAsync(cancellationToken);
        var latest = rows.OrderByDescending(item => item.CreatedAt).FirstOrDefault();
        return new BackupStatusSummary
        {
            TotalBackups = rows.Count,
            SuccessfulBackups = rows.Count(item =>
                item.Status is "Completed" or "CompletedWithWarnings"),
            FailedBackups = rows.Count(item => item.Status == "Failed"),
            VerifiedBackups = rows.Count(item => item.VerificationStatus == "Passed"),
            TotalBackupSizeBytes = rows.Sum(item => item.BackupSizeBytes),
            LastBackupAt = latest?.CreatedAt,
            LastBackupStatus = latest?.Status ?? string.Empty
        };
    }

    public async Task<BackupSettings> GetBackupSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        await RequireViewAsync();
        var values = await context.SystemSettings.AsNoTracking()
            .Where(item => item.Group == "Backup")
            .ToDictionaryAsync(item => item.Key, item => item.Value, cancellationToken);
        return new BackupSettings
        {
            DefaultFolder = Read(values, "BackupDefaultFolder", string.Empty),
            CompressionEnabled = ReadBool(values, "BackupCompressionEnabled", false),
            VerifyAfterCreation = ReadBool(values, "BackupVerifyAfterCreation", true),
            RetentionDays = ReadInt(values, "BackupRetentionDays", 30, 1, 3650),
            MaxHistoryRows = ReadInt(values, "BackupMaxHistoryRows", 500, 1, 5000)
        };
    }

    public async Task<BackupResult> OpenBackupFolderAsync(
        string? folderPath = null,
        CancellationToken cancellationToken = default)
    {
        await RequireViewAsync();
        cancellationToken.ThrowIfCancellationRequested();
        var settings = await GetBackupSettingsAsync(cancellationToken);
        var folder = ResolveDestinationFolder(folderPath, settings.DefaultFolder);
        Directory.CreateDirectory(folder);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
            return new BackupResult
            {
                Succeeded = true,
                BackupFilePath = folder,
                StartedAt = DateTime.UtcNow,
                FinishedAt = DateTime.UtcNow,
                Message = "Backup folder opened."
            };
        }
        catch (Exception ex)
        {
            return Failure(DateTime.UtcNow, $"Unable to open backup folder: {SanitizeError(ex.Message)}");
        }
    }

    public async Task<BackupResult> DeleteBackupHistoryRecordAsync(
        int backupHistoryId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;
        if (!await BackupAuthorization.CanManageAsync(authenticationService))
        {
            return Failure(startedAt, "The current user cannot delete backup history.");
        }
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Failure(startedAt, "A backup history deletion reason is required.");
        }

        var history = await context.BackupHistories
            .FirstOrDefaultAsync(item => item.Id == backupHistoryId, cancellationToken);
        if (history == null)
        {
            return Failure(startedAt, "Backup history record was not found.");
        }

        history.IsDeleted = true;
        history.DeletedAt = DateTime.UtcNow;
        history.DeletedReason = reason.Trim();
        history.DeletedByUserId = authenticationService.CurrentUser!.Id;
        AddLog(
            "Backup History Deleted",
            history,
            $"FileName={history.BackupFileName};Reason={SanitizeDetail(reason)}");
        await SaveAsync(cancellationToken);
        return new BackupResult
        {
            Succeeded = true,
            BackupHistoryId = history.Id,
            BackupFilePath = history.BackupFilePath,
            StartedAt = startedAt,
            FinishedAt = DateTime.UtcNow,
            Message = "Backup history record was soft-deleted. Backup files were not deleted."
        };
    }

    private async Task CreateOnlineBackupAsync(
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var sourceConnectionString = context.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(sourceConnectionString))
        {
            throw new InvalidOperationException("The SQLite source connection string is unavailable.");
        }

        var sourceBuilder = new SqliteConnectionStringBuilder(sourceConnectionString)
        {
            Pooling = false
        };
        var destinationBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = destinationPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        };

        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var source = new SqliteConnection(sourceBuilder.ToString());
            using var destination = new SqliteConnection(destinationBuilder.ToString());
            source.Open();
            destination.Open();
            source.BackupDatabase(destination);
            cancellationToken.ThrowIfCancellationRequested();
        }, cancellationToken);
    }

    private static async Task<BackupVerificationResult> VerifyCoreAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        var verifiedAt = DateTime.UtcNow;
        try
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return VerificationFailure(filePath, "Backup file was not found.");
            }

            var fileInfo = new FileInfo(filePath);
            var checksum = await ComputeChecksumAsync(filePath, cancellationToken);
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = filePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            };
            await using var connection = new SqliteConnection(builder.ToString());
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            var result = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken));
            var passed = string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
            return new BackupVerificationResult
            {
                Succeeded = passed,
                FilePath = filePath,
                ChecksumSha256 = checksum,
                IntegrityCheckPassed = passed,
                FileSizeBytes = fileInfo.Length,
                VerifiedAt = verifiedAt,
                Message = passed
                    ? "Backup integrity check passed."
                    : "Backup integrity check failed.",
                ErrorMessage = passed ? null : $"SQLite integrity_check returned: {result}"
            };
        }
        catch (Exception ex)
        {
            return VerificationFailure(filePath, SanitizeError(ex.Message), verifiedAt);
        }
    }

    private static async Task<BackupVerificationResult> GetChecksumOnlyAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        return new BackupVerificationResult
        {
            Succeeded = true,
            FilePath = filePath,
            ChecksumSha256 = await ComputeChecksumAsync(filePath, cancellationToken),
            FileSizeBytes = new FileInfo(filePath).Length,
            VerifiedAt = DateTime.UtcNow,
            Message = "Checksum calculated; integrity verification was not requested."
        };
    }

    private static async Task<string> ComputeChecksumAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static async Task CreateZipAsync(
        string backupPath,
        string metadataPath,
        string zipPath,
        CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            archive.CreateEntryFromFile(backupPath, Path.GetFileName(backupPath), CompressionLevel.Optimal);
            archive.CreateEntryFromFile(metadataPath, Path.GetFileName(metadataPath), CompressionLevel.Optimal);
            cancellationToken.ThrowIfCancellationRequested();
        }, cancellationToken);
    }

    private async Task<BackupHistory> CreateFailureHistoryAsync(
        BackupRequest request,
        string backupPath,
        string error,
        CancellationToken cancellationToken)
    {
        var user = authenticationService.CurrentUser!;
        var history = new BackupHistory
        {
            BackupFileName = string.IsNullOrWhiteSpace(backupPath)
                ? "Backup creation failed"
                : Path.GetFileName(backupPath),
            BackupFilePath = string.IsNullOrWhiteSpace(backupPath)
                ? request.DestinationFolder
                : backupPath,
            CreatedByUserId = user.Id,
            CreatedByUserName = user.FullName,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            Status = "Failed",
            VerificationStatus = "Failed",
            ErrorMessage = error
        };
        context.BackupHistories.Add(history);
        AddLog("Backup Creation Failed", history, $"Error={SanitizeDetail(error)}");
        try
        {
            await SaveAsync(cancellationToken);
        }
        catch
        {
            // The caller still receives the original backup failure.
        }
        return history;
    }

    private async Task RecordFailureAsync(
        BackupHistory history,
        string error,
        CancellationToken cancellationToken)
    {
        history.Status = "Failed";
        history.ErrorMessage = error;
        if (history.VerificationStatus == "Pending")
        {
            history.VerificationStatus = "Failed";
        }
        AddLog("Backup Creation Failed", history, $"FileName={history.BackupFileName};Error={SanitizeDetail(error)}");
        try
        {
            await SaveAsync(cancellationToken);
        }
        catch
        {
            // The backup result retains the original error if history persistence also fails.
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken) =>
        await SqliteRetryPolicy.ExecuteAsync(
            token => context.SaveChangesAsync(token),
            cancellationToken);

    private async Task RecordDeniedActionAsync(
        string action,
        string detail,
        CancellationToken cancellationToken)
    {
        var user = authenticationService.CurrentUser;
        if (user == null)
        {
            return;
        }

        context.ActivityLogs.Add(new ActivityLog
        {
            Action = action,
            UserId = user.Id,
            IpAddress = "127.0.0.1",
            Detail = $"EntityName=BackupHistory;Outcome=Blocked;Reason={SanitizeDetail(detail)}"
        });
        try
        {
            await SaveAsync(cancellationToken);
        }
        catch
        {
            // Authorization denial remains authoritative if logging is unavailable.
        }
    }

    private void AddLog(string action, BackupHistory history, string detail) =>
        context.ActivityLogs.Add(new ActivityLog
        {
            Action = action,
            UserId = authenticationService.CurrentUser!.Id,
            IpAddress = "127.0.0.1",
            Detail =
                $"EntityName=BackupHistory;EntityId={history.Id};{detail}"
        });

    private async Task RequireViewAsync()
    {
        if (!await BackupAuthorization.CanViewAsync(authenticationService))
        {
            throw new UnauthorizedAccessException(
                "The current user cannot view backup history.");
        }
    }

    private void EnsureSqliteProvider()
    {
        if (!string.Equals(
                context.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.Sqlite",
                StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                "Priority 8A backup creation supports SQLite databases only.");
        }
    }

    private static void ApplyVerification(
        BackupHistory history,
        BackupVerificationResult verification)
    {
        history.BackupSizeBytes = verification.FileSizeBytes;
        history.ChecksumSha256 = verification.ChecksumSha256;
        history.VerifiedAt = verification.VerifiedAt;
        history.VerificationStatus = verification.Succeeded ? "Passed" : "Failed";
    }

    private static string CreateMetadataJson(
        BackupHistory history,
        BackupRequest request,
        BackupVerificationResult verification,
        DateTime startedAt) =>
        JsonSerializer.Serialize(
            new
            {
                SchemaVersion = 1,
                ProductName = ProductBrand.Name,
                history.BackupFileName,
                history.BackupSizeBytes,
                history.ChecksumSha256,
                history.CreatedByUserId,
                history.CreatedByUserName,
                history.Reason,
                BackupStartedAtUtc = startedAt,
                BackupFinishedAtUtc = DateTime.UtcNow,
                VerificationRequested = request.VerifyAfterCreation,
                verification.IntegrityCheckPassed,
                verification.VerifiedAt,
                CompressionRequested = request.CompressBackup,
                BackupMethod = "Microsoft.Data.Sqlite online backup API"
            },
            new JsonSerializerOptions { WriteIndented = true });

    private static BackupHistoryItem Map(BackupHistory item) =>
        new()
        {
            BackupHistoryId = item.Id,
            BackupFileName = item.BackupFileName,
            BackupFilePath = item.BackupFilePath,
            CompressedFilePath = item.CompressedFilePath ?? string.Empty,
            BackupSizeBytes = item.BackupSizeBytes,
            ChecksumSha256 = item.ChecksumSha256 ?? string.Empty,
            CreatedBy = item.CreatedByUserName,
            CreatedAt = item.CreatedAt,
            VerifiedAt = item.VerifiedAt,
            VerificationStatus = item.VerificationStatus,
            Reason = item.Reason ?? string.Empty,
            Status = item.Status,
            ErrorMessage = item.ErrorMessage ?? string.Empty,
            MetadataJson = item.MetadataJson ?? string.Empty
        };

    private static string ResolveDestinationFolder(
        string? requestedFolder,
        string? configuredFolder)
    {
        var folder = !string.IsNullOrWhiteSpace(requestedFolder)
            ? requestedFolder.Trim()
            : !string.IsNullOrWhiteSpace(configuredFolder)
                ? configuredFolder.Trim()
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    ProductBrand.BackupFolderName);
        return Path.GetFullPath(folder);
    }

    private static string ResolveUniquePath(
        string folder,
        string baseName,
        string extension)
    {
        var path = Path.Combine(folder, $"{baseName}{extension}");
        for (var suffix = 2; File.Exists(path); suffix++)
        {
            path = Path.Combine(folder, $"{baseName}_{suffix}{extension}");
        }
        return path;
    }

    private static string SanitizeFilePart(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var sanitized = new string(value
            .Select(character => invalid.Contains(character) || char.IsWhiteSpace(character)
                ? '_'
                : character)
            .ToArray()).Trim('_', '.');
        return string.IsNullOrWhiteSpace(sanitized)
            ? "User"
            : sanitized[..Math.Min(sanitized.Length, 40)];
    }

    private static string SanitizeError(string value)
    {
        var sanitized = value.ReplaceLineEndings(" ").Trim();
        return sanitized[..Math.Min(sanitized.Length, 1000)];
    }

    private static string SanitizeDetail(string value) =>
        value.Replace(";", ",", StringComparison.Ordinal)
            .Replace("=", "-", StringComparison.Ordinal)
            .ReplaceLineEndings(" ");

    private static string Read(
        IReadOnlyDictionary<string, string> values,
        string key,
        string fallback) =>
        values.TryGetValue(key, out var value) ? value : fallback;

    private static bool ReadBool(
        IReadOnlyDictionary<string, string> values,
        string key,
        bool fallback) =>
        values.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed)
            ? parsed
            : fallback;

    private static int ReadInt(
        IReadOnlyDictionary<string, string> values,
        string key,
        int fallback,
        int minimum,
        int maximum) =>
        values.TryGetValue(key, out var value) && int.TryParse(value, out var parsed)
            ? Math.Clamp(parsed, minimum, maximum)
            : fallback;

    private static BackupResult Failure(DateTime startedAt, string error) =>
        new()
        {
            StartedAt = startedAt,
            FinishedAt = DateTime.UtcNow,
            Message = "Backup action failed.",
            ErrorMessage = error
        };

    private static BackupVerificationResult VerificationFailure(
        string filePath,
        string error,
        DateTime? verifiedAt = null) =>
        new()
        {
            FilePath = filePath,
            VerifiedAt = verifiedAt ?? DateTime.UtcNow,
            Message = "Backup verification failed.",
            ErrorMessage = error
        };

    private static void TryDeleteFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup applies only to files created by this operation.
        }
    }

    private static void TryDeleteFileIfEmpty(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }
        try
        {
            if (new FileInfo(path).Length == 0)
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Preserve any non-empty backup for diagnostics.
        }
    }
}
