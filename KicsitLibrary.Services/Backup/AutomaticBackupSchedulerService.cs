using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data;
using KicsitLibrary.Services.Notifications;
using KicsitLibrary.Services.Restore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KicsitLibrary.Services.Backup;

public sealed class AutomaticBackupSchedulerService(
    IServiceScopeFactory scopeFactory) : IAutomaticBackupSchedulerService
{
    private readonly SemaphoreSlim _runLock = new(1, 1);

    public async Task<AutomaticBackupStatus> GetSchedulerStatusAsync(
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var context =
            scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
        var authentication =
            scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
        var values = await AutomaticBackupSettingsStore.ReadValuesAsync(
            context,
            cancellationToken);
        var status = AutomaticBackupSettingsStore.CreateStatus(values);
        status.CanManage =
            await AutomaticBackupAuthorization.CanManageAsync(authentication);

        if (status.IsRunning && _runLock.CurrentCount > 0)
        {
            status.IsRunning = false;
            status.LastMessage =
                "Recovered a stale automatic backup running flag.";
            await AutomaticBackupSettingsStore.SetAsync(
                context,
                new Dictionary<string, string>
                {
                    ["AutomaticBackupIsRunning"] = "False",
                    ["AutomaticBackupLastMessage"] = status.LastMessage
                },
                authentication.CurrentUser?.Id,
                cancellationToken);
        }

        return status;
    }

    public async Task<AutomaticBackupSchedulerSettings> GetSchedulerSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var context =
            scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
        var values = await AutomaticBackupSettingsStore.ReadValuesAsync(
            context,
            cancellationToken);
        return AutomaticBackupSettingsStore.CreateSettings(values);
    }

    public async Task<AutomaticBackupSchedulerSettings> UpdateSchedulerSettingsAsync(
        AutomaticBackupSchedulerSettings settings,
        CancellationToken cancellationToken = default)
    {
        settings ??= new AutomaticBackupSchedulerSettings();
        using var scope = scopeFactory.CreateScope();
        var context =
            scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
        var authentication =
            scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
        if (!await AutomaticBackupAuthorization.CanManageAsync(authentication))
        {
            await LogAsync(
                context,
                "Automatic Backup Settings Blocked",
                "Outcome=Blocked;Reason=The current user cannot update automatic backup settings.",
                authentication.CurrentUser?.Id,
                cancellationToken);
            throw new UnauthorizedAccessException(
                "The current user cannot update automatic backup settings.");
        }

        var normalized = NormalizeSettings(settings);
        await AutomaticBackupSettingsStore.EnsureAsync(
            context,
            cancellationToken);
        await AutomaticBackupSettingsStore.SetAsync(
            context,
            new Dictionary<string, string>
            {
                ["AutomaticBackupEnabled"] = normalized.Enabled.ToString(),
                ["AutomaticBackupRunOnStartup"] =
                    normalized.RunOnStartup.ToString(),
                ["AutomaticBackupIntervalHours"] =
                    normalized.IntervalHours.ToString(),
                ["AutomaticBackupInitialDelaySeconds"] =
                    normalized.InitialDelaySeconds.ToString(),
                ["AutomaticBackupCompress"] = normalized.Compress.ToString(),
                ["AutomaticBackupVerifyAfterCreation"] =
                    normalized.VerifyAfterCreation.ToString(),
                ["AutomaticBackupDestinationFolder"] =
                    normalized.DestinationFolder,
                ["AutomaticBackupRetentionEnabled"] =
                    normalized.RetentionEnabled.ToString(),
                ["AutomaticBackupRetentionDays"] =
                    normalized.RetentionDays.ToString(),
                ["AutomaticBackupMaxHistoryRows"] =
                    normalized.MaxHistoryRows.ToString(),
                ["AutomaticBackupDeletePhysicalFiles"] =
                    normalized.DeletePhysicalFiles.ToString()
            },
            authentication.CurrentUser!.Id,
            cancellationToken);
        await LogAsync(
            context,
            "Automatic Backup Settings Updated",
            $"Enabled={normalized.Enabled};RunOnStartup={normalized.RunOnStartup};" +
            $"IntervalHours={normalized.IntervalHours};RetentionEnabled={normalized.RetentionEnabled};" +
            $"DeletePhysicalFiles={normalized.DeletePhysicalFiles}",
            authentication.CurrentUser.Id,
            cancellationToken);
        return normalized;
    }

    public Task<AutomaticBackupRunResult> RunBackupNowAsync(
        CancellationToken cancellationToken = default) =>
        RunCoreAsync(requireEnabled: false, cancellationToken);

    public Task<AutomaticBackupRunResult> RunScheduledBackupAsync(
        CancellationToken cancellationToken = default) =>
        RunCoreAsync(requireEnabled: true, cancellationToken);

    public async Task<BackupRetentionPreviewResult> PreviewRetentionAsync(
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var context =
            scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
        var retention =
            scope.ServiceProvider.GetRequiredService<IBackupRetentionService>();
        var settings = AutomaticBackupSettingsStore.CreateSettings(
            await AutomaticBackupSettingsStore.ReadValuesAsync(
                context,
                cancellationToken));
        var result = await retention.PreviewAsync(settings, cancellationToken);
        if (result.Succeeded)
        {
            await LogAsync(
                context,
                "Automatic Backup Retention Previewed",
                $"Candidates={result.CandidateCount};TotalSizeBytes={result.TotalSizeBytes}",
                scope.ServiceProvider
                    .GetRequiredService<IAuthenticationService>()
                    .CurrentUser?.Id,
                cancellationToken);
        }
        return result;
    }

    public async Task<BackupRetentionDeleteResult> ApplyRetentionAsync(
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var context =
            scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
        var retention =
            scope.ServiceProvider.GetRequiredService<IBackupRetentionService>();
        var settings = AutomaticBackupSettingsStore.CreateSettings(
            await AutomaticBackupSettingsStore.ReadValuesAsync(
                context,
                cancellationToken));
        return await retention.ApplyAsync(settings, cancellationToken);
    }

    private async Task<AutomaticBackupRunResult> RunCoreAsync(
        bool requireEnabled,
        CancellationToken cancellationToken)
    {
        var result = new AutomaticBackupRunResult
        {
            StartedAt = DateTime.UtcNow
        };
        bool lockAcquired;
        try
        {
            lockAcquired = await _runLock.WaitAsync(0, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            result.WasSkipped = true;
            result.FinishedAt = DateTime.UtcNow;
            result.Message =
                "Automatic backup was cancelled before it started.";
            result.ErrorMessage = result.Message;
            return result;
        }

        if (!lockAcquired)
        {
            result.WasSkipped = true;
            result.FinishedAt = DateTime.UtcNow;
            result.Message =
                "Automatic backup skipped because another scheduler run is active.";
            await RecordStandaloneResultAsync(
                result,
                "Automatic Backup Skipped",
                CancellationToken.None);
            return result;
        }

        using var scope = scopeFactory.CreateScope();
        var context =
            scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
        var authentication =
            scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
        var backupService =
            scope.ServiceProvider.GetRequiredService<IBackupService>();
        var retentionService =
            scope.ServiceProvider.GetRequiredService<IBackupRetentionService>();
        AutomaticBackupSchedulerSettings? settings = null;
        var runStarted = false;

        try
        {
            var values = await AutomaticBackupSettingsStore.ReadValuesAsync(
                context,
                cancellationToken);
            settings = AutomaticBackupSettingsStore.CreateSettings(values);
            if (requireEnabled && !settings.Enabled)
            {
                result.WasSkipped = true;
                result.FinishedAt = DateTime.UtcNow;
                result.Message = "Automatic backup scheduler is disabled.";
                return result;
            }
            if (!await AutomaticBackupAuthorization.CanManageAsync(authentication))
            {
                result.WasSkipped = true;
                result.FinishedAt = DateTime.UtcNow;
                result.Message =
                    "The current user cannot run automatic backups.";
                result.ErrorMessage = result.Message;
                await PersistResultAsync(
                    context,
                    result,
                    authentication.CurrentUser?.Id,
                    "Automatic Backup Blocked",
                    cancellationToken);
                return result;
            }

            var liveDatabasePath = GetLiveDatabasePath(context);
            if (File.Exists(
                    PendingRestoreProcessor.GetPendingRequestPath(
                        liveDatabasePath)))
            {
                result.WasSkipped = true;
                result.FinishedAt = DateTime.UtcNow;
                result.Message =
                    "Automatic backup skipped because a database restore is pending.";
                await PersistResultAsync(
                    context,
                    result,
                    authentication.CurrentUser!.Id,
                    "Automatic Backup Skipped",
                    cancellationToken);
                return result;
            }

            runStarted = true;
            await AutomaticBackupSettingsStore.SetAsync(
                context,
                new Dictionary<string, string>
                {
                    ["AutomaticBackupLastRunAt"] =
                        AutomaticBackupSettingsStore.FormatDate(
                            result.StartedAt),
                    ["AutomaticBackupLastMessage"] =
                        "Automatic backup is in progress.",
                    ["AutomaticBackupIsRunning"] = "True"
                },
                authentication.CurrentUser!.Id,
                cancellationToken);
            await LogAsync(
                context,
                requireEnabled
                    ? "Automatic Backup Started"
                    : "Automatic Backup Now Started",
                "Outcome=Started;Method=Microsoft.Data.Sqlite online backup API",
                authentication.CurrentUser.Id,
                cancellationToken);

            var backup = await backupService.CreateBackupAsync(
                new BackupRequest
                {
                    RequestedByUserId = authentication.CurrentUser.Id,
                    RequestedByUserName =
                        authentication.CurrentUser.FullName,
                    DestinationFolder = settings.DestinationFolder,
                    IncludeTimestamp = true,
                    IncludeMetadataFile = true,
                    VerifyAfterCreation = settings.VerifyAfterCreation,
                    CompressBackup = settings.Compress,
                    Reason = requireEnabled
                        ? "Automatic scheduled backup"
                        : "Automatic backup run manually"
                },
                cancellationToken);
            result.BackupFilePath = backup.BackupFilePath;
            result.CompressedFilePath = backup.CompressedFilePath;
            result.BackupSizeBytes = backup.BackupSizeBytes;
            result.ChecksumSha256 = backup.ChecksumSha256;
            if (!backup.Succeeded)
            {
                result.FinishedAt = DateTime.UtcNow;
                result.Message = "Automatic backup failed.";
                result.ErrorMessage =
                    backup.ErrorMessage ?? backup.Message;
                return result;
            }

            if (settings.RetentionEnabled)
            {
                var preview = await retentionService.PreviewAsync(
                    settings,
                    cancellationToken);
                result.RetentionPreviewCount = preview.CandidateCount;
                if (!preview.Succeeded)
                {
                    result.FinishedAt = DateTime.UtcNow;
                    result.Message =
                        "Backup succeeded, but retention preview failed.";
                    result.ErrorMessage =
                        preview.ErrorMessage ?? preview.Message;
                    return result;
                }

                var retention = await retentionService.ApplyAsync(
                    settings,
                    cancellationToken);
                result.RetentionDeletedCount =
                    retention.DeletedHistoryCount;
                if (!retention.Succeeded)
                {
                    result.FinishedAt = DateTime.UtcNow;
                    result.Message =
                        "Backup succeeded, but retention failed.";
                    result.ErrorMessage =
                        retention.ErrorMessage ?? retention.Message;
                    return result;
                }
            }

            result.Succeeded = true;
            result.FinishedAt = DateTime.UtcNow;
            result.Message =
                $"Automatic backup completed successfully. " +
                $"Retention previewed {result.RetentionPreviewCount} and deleted " +
                $"{result.RetentionDeletedCount} history record(s).";
            return result;
        }
        catch (OperationCanceledException)
        {
            result.FinishedAt = DateTime.UtcNow;
            result.Message = "Automatic backup was cancelled.";
            result.ErrorMessage = result.Message;
            return result;
        }
        catch (Exception ex)
        {
            result.FinishedAt = DateTime.UtcNow;
            result.Message = "Automatic backup failed.";
            result.ErrorMessage = Sanitize(ex.Message);
            return result;
        }
        finally
        {
            if (result.FinishedAt == default)
            {
                result.FinishedAt = DateTime.UtcNow;
            }

            if (runStarted)
            {
                try
                {
                    await PersistResultAsync(
                        context,
                        result,
                        authentication.CurrentUser?.Id,
                        result.Succeeded
                            ? "Automatic Backup Completed"
                            : result.WasSkipped
                                ? "Automatic Backup Skipped"
                                : "Automatic Backup Failed",
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    result.Succeeded = false;
                    result.ErrorMessage =
                        $"{result.ErrorMessage} Status persistence failed: {Sanitize(ex.Message)}"
                            .Trim();
                }
            }

            _runLock.Release();
        }
    }

    private async Task RecordStandaloneResultAsync(
        AutomaticBackupRunResult result,
        string action,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var context =
                scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
            var authentication =
                scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
            await PersistResultAsync(
                context,
                result,
                authentication.CurrentUser?.Id,
                action,
                cancellationToken);
        }
        catch
        {
            // A skipped run remains skipped even when status logging is unavailable.
        }
    }

    private static async Task PersistResultAsync(
        KicsitLibraryDbContext context,
        AutomaticBackupRunResult result,
        int? userId,
        string action,
        CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>
        {
            ["AutomaticBackupLastRunAt"] =
                AutomaticBackupSettingsStore.FormatDate(result.StartedAt),
            ["AutomaticBackupLastMessage"] = string.IsNullOrWhiteSpace(
                result.ErrorMessage)
                    ? result.Message
                    : $"{result.Message} {result.ErrorMessage}",
            ["AutomaticBackupIsRunning"] = "False"
        };
        if (result.Succeeded)
        {
            values["AutomaticBackupLastSuccessAt"] =
                AutomaticBackupSettingsStore.FormatDate(
                    result.FinishedAt);
        }
        else if (!result.WasSkipped)
        {
            values["AutomaticBackupLastFailureAt"] =
                AutomaticBackupSettingsStore.FormatDate(
                    result.FinishedAt);
        }

        await AutomaticBackupSettingsStore.SetAsync(
            context,
            values,
            userId,
            cancellationToken);
        await LogAsync(
            context,
            action,
            $"Outcome={(result.Succeeded ? "Succeeded" : result.WasSkipped ? "Skipped" : "Failed")};" +
            $"Message={Safe(result.Message)};Error={Safe(result.ErrorMessage ?? string.Empty)}",
            userId,
            cancellationToken);
    }

    private static async Task LogAsync(
        KicsitLibraryDbContext context,
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
        await SqliteRetryPolicy.ExecuteAsync(
            token => context.SaveChangesAsync(token),
            cancellationToken);
    }

    private static AutomaticBackupSchedulerSettings NormalizeSettings(
        AutomaticBackupSchedulerSettings settings) =>
        new()
        {
            Enabled = settings.Enabled,
            RunOnStartup = settings.RunOnStartup,
            IntervalHours = Math.Clamp(
                settings.IntervalHours <= 0 ? 24 : settings.IntervalHours,
                1,
                8760),
            InitialDelaySeconds = Math.Clamp(
                settings.InitialDelaySeconds <= 0
                    ? 60
                    : settings.InitialDelaySeconds,
                1,
                86400),
            Compress = settings.Compress,
            VerifyAfterCreation = settings.VerifyAfterCreation,
            DestinationFolder = string.IsNullOrWhiteSpace(
                settings.DestinationFolder)
                    ? string.Empty
                    : Path.GetFullPath(
                        settings.DestinationFolder.Trim()),
            RetentionEnabled = settings.RetentionEnabled,
            RetentionDays = Math.Clamp(
                settings.RetentionDays <= 0
                    ? 30
                    : settings.RetentionDays,
                1,
                3650),
            MaxHistoryRows = Math.Clamp(
                settings.MaxHistoryRows <= 0
                    ? 500
                    : settings.MaxHistoryRows,
                1,
                5000),
            DeletePhysicalFiles = settings.DeletePhysicalFiles
        };

    private static string GetLiveDatabasePath(
        KicsitLibraryDbContext context)
    {
        var builder = new SqliteConnectionStringBuilder(
            context.Database.GetConnectionString());
        if (string.IsNullOrWhiteSpace(builder.DataSource) ||
            builder.DataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Automatic backup requires a file-based SQLite database.");
        }
        return Path.GetFullPath(builder.DataSource);
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
