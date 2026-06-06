using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KicsitLibrary.Services.Notifications
{
    public sealed class OverdueSchedulerService : IOverdueSchedulerService
    {
        private const string SettingsGroup = "Scheduler";
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly SemaphoreSlim _runLock = new(1, 1);

        private static readonly IReadOnlyDictionary<string, string> DefaultSettings =
            new Dictionary<string, string>
            {
                ["OverdueSchedulerEnabled"] = "False",
                ["OverdueSchedulerRunOnStartup"] = "False",
                ["OverdueSchedulerIntervalMinutes"] = "60",
                ["OverdueSchedulerInitialDelaySeconds"] = "30",
                ["OverdueSchedulerSendPendingEmails"] = "False",
                ["OverdueSchedulerMaxRunMinutes"] = "10",
                ["OverdueSchedulerLastRunAt"] = string.Empty,
                ["OverdueSchedulerLastSuccessAt"] = string.Empty,
                ["OverdueSchedulerLastFailureAt"] = string.Empty,
                ["OverdueSchedulerLastMessage"] = string.Empty,
                ["OverdueSchedulerIsRunning"] = "False"
            };

        public OverdueSchedulerService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        }

        public async Task<OverdueSchedulerStatus> GetStatusAsync(
            CancellationToken cancellationToken = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
            await EnsureSettingsAsync(context, cancellationToken);

            var values = await context.SystemSettings
                .AsNoTracking()
                .Where(setting => setting.Group == SettingsGroup)
                .ToDictionaryAsync(
                    setting => setting.Key,
                    setting => setting.Value,
                    cancellationToken);
            var status = CreateStatus(values);

            if (status.IsRunning && _runLock.CurrentCount > 0)
            {
                await SetSettingsAsync(
                    context,
                    new Dictionary<string, string>
                    {
                        ["OverdueSchedulerIsRunning"] = "False",
                        ["OverdueSchedulerLastMessage"] =
                            "Recovered a stale scheduler running flag."
                    },
                    cancellationToken);
                status.IsRunning = false;
                status.LastMessage = "Recovered a stale scheduler running flag.";
            }

            return status;
        }

        public Task<OverdueSchedulerRunResult> RunAsync(
            int? userId = null,
            CancellationToken cancellationToken = default)
        {
            return RunCoreAsync(
                requireEnabled: true,
                allowAutomaticEmail: true,
                userId,
                cancellationToken);
        }

        public Task<OverdueSchedulerRunResult> RunManualOverdueCheckAsync(
            int? userId = null,
            CancellationToken cancellationToken = default)
        {
            return RunCoreAsync(
                requireEnabled: false,
                allowAutomaticEmail: false,
                userId,
                cancellationToken);
        }

        private async Task<OverdueSchedulerRunResult> RunCoreAsync(
            bool requireEnabled,
            bool allowAutomaticEmail,
            int? userId,
            CancellationToken cancellationToken)
        {
            var result = new OverdueSchedulerRunResult
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
                result.FinishedAt = DateTime.UtcNow;
                result.WasSkipped = true;
                result.Message = "Scheduler run was cancelled before it started.";
                result.FailureReason = result.Message;
                await RecordStandaloneResultAsync(result, userId);
                return result;
            }

            if (!lockAcquired)
            {
                result.FinishedAt = DateTime.UtcNow;
                result.WasSkipped = true;
                result.Message = "Scheduler run skipped because another run is active.";
                await RecordStandaloneResultAsync(result, userId);
                return result;
            }

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
            var overdueService = scope.ServiceProvider.GetRequiredService<IOverdueService>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var logService = scope.ServiceProvider.GetRequiredService<IActivityLogService>();
            OverdueSchedulerStatus? settings = null;

            try
            {
                await EnsureSettingsAsync(context, cancellationToken);
                settings = await ReadStatusAsync(context, cancellationToken);

                if (requireEnabled && !settings.Enabled)
                {
                    result.FinishedAt = DateTime.UtcNow;
                    result.WasSkipped = true;
                    result.Message = "Scheduler is disabled.";
                    return result;
                }

                await SetSettingsAsync(
                    context,
                    new Dictionary<string, string>
                    {
                        ["OverdueSchedulerLastRunAt"] = FormatDate(result.StartedAt),
                        ["OverdueSchedulerLastMessage"] = "Scheduler run is in progress.",
                        ["OverdueSchedulerIsRunning"] = "True"
                    },
                    cancellationToken);
                await LogWithRetryAsync(
                    logService,
                    requireEnabled ? "Overdue Scheduler Started" : "Manual Overdue Check Started",
                    "Overdue notification processing started.",
                    userId,
                    cancellationToken);

                using var timeoutSource =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutSource.CancelAfter(TimeSpan.FromMinutes(settings.MaxRunMinutes));
                var runToken = timeoutSource.Token;

                var overdueResult = await SqliteRetryPolicy.ExecuteAsync(
                    token => overdueService.ProcessOverdueNotificationsAsync(userId, token),
                    runToken);
                result.ProcessedCount = overdueResult.ProcessedCount;
                result.CreatedCount = overdueResult.CreatedCount;
                result.SkippedCount = overdueResult.SkippedCount;
                result.FailedCount = overdueResult.FailedCount;

                if (allowAutomaticEmail && settings.SendPendingEmails)
                {
                    var emailResult = await SqliteRetryPolicy.ExecuteAsync(
                        token => notificationService.SendPendingEmailNotificationsAsync(
                            userId,
                            token),
                        runToken);
                    result.EmailsAttempted =
                        emailResult.SentCount + emailResult.FailedCount;
                    result.EmailsSent = emailResult.SentCount;
                    result.EmailsFailed =
                        emailResult.FailedCount + emailResult.SkippedCount;
                    result.FailedCount += result.EmailsFailed;
                }

                result.Succeeded = result.FailedCount == 0;
                result.FinishedAt = DateTime.UtcNow;
                result.Message = BuildSummary(result, settings.SendPendingEmails && allowAutomaticEmail);
                if (!result.Succeeded)
                {
                    result.FailureReason =
                        "One or more overdue or email records could not be processed.";
                }
            }
            catch (OperationCanceledException)
            {
                result.FinishedAt = DateTime.UtcNow;
                result.Succeeded = false;
                result.FailureReason = cancellationToken.IsCancellationRequested
                    ? "Scheduler run was cancelled."
                    : $"Scheduler run exceeded the {settings?.MaxRunMinutes ?? 10} minute limit.";
                result.Message = result.FailureReason;
            }
            catch (Exception ex)
            {
                result.FinishedAt = DateTime.UtcNow;
                result.Succeeded = false;
                result.FailureReason = ex.Message;
                result.Message = $"Scheduler run failed: {ex.Message}";
            }
            finally
            {
                if (result.FinishedAt == default)
                {
                    result.FinishedAt = DateTime.UtcNow;
                }

                try
                {
                    await PersistResultAsync(context, logService, result, userId);
                }
                catch (Exception ex)
                {
                    result.Succeeded = false;
                    result.FailureReason = AppendFailure(
                        result.FailureReason,
                        $"Scheduler status persistence failed: {ex.Message}");
                    result.Message = AppendFailure(result.Message, result.FailureReason);
                }

                _runLock.Release();
            }

            return result;
        }

        private async Task RecordStandaloneResultAsync(
            OverdueSchedulerRunResult result,
            int? userId)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
                var logService = scope.ServiceProvider.GetRequiredService<IActivityLogService>();
                await EnsureSettingsAsync(context, CancellationToken.None);
                await SetSettingsAsync(
                    context,
                    new Dictionary<string, string>
                    {
                        ["OverdueSchedulerLastRunAt"] = FormatDate(result.StartedAt),
                        ["OverdueSchedulerLastMessage"] = result.Message
                    },
                    CancellationToken.None);
                await LogWithRetryAsync(
                    logService,
                    "Overdue Scheduler Skipped",
                    result.Message,
                    userId,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                result.FailureReason = AppendFailure(
                    result.FailureReason,
                    $"Scheduler skip status could not be saved: {ex.Message}");
            }
        }

        private static async Task PersistResultAsync(
            KicsitLibraryDbContext context,
            IActivityLogService logService,
            OverdueSchedulerRunResult result,
            int? userId)
        {
            var values = new Dictionary<string, string>
            {
                ["OverdueSchedulerLastRunAt"] = FormatDate(result.StartedAt),
                ["OverdueSchedulerLastMessage"] = result.Message,
                ["OverdueSchedulerIsRunning"] = "False"
            };

            if (result.Succeeded)
            {
                values["OverdueSchedulerLastSuccessAt"] = FormatDate(result.FinishedAt);
            }
            else if (!result.WasSkipped ||
                     !result.Message.Equals("Scheduler is disabled.", StringComparison.Ordinal))
            {
                values["OverdueSchedulerLastFailureAt"] = FormatDate(result.FinishedAt);
            }

            await SetSettingsAsync(context, values, CancellationToken.None);
            await LogWithRetryAsync(
                logService,
                result.Succeeded
                    ? "Overdue Scheduler Completed"
                    : result.WasSkipped
                        ? "Overdue Scheduler Skipped"
                        : "Overdue Scheduler Failed",
                result.Message,
                userId,
                CancellationToken.None);
        }

        private static async Task EnsureSettingsAsync(
            KicsitLibraryDbContext context,
            CancellationToken cancellationToken)
        {
            var existingKeys = await context.SystemSettings
                .Where(setting => setting.Group == SettingsGroup)
                .Select(setting => setting.Key)
                .ToListAsync(cancellationToken);
            var missing = DefaultSettings
                .Where(setting => !existingKeys.Contains(setting.Key))
                .Select(setting => new SystemSettings
                {
                    Key = setting.Key,
                    Value = setting.Value,
                    Group = SettingsGroup,
                    Description = "Overdue scheduler setting"
                })
                .ToList();

            if (missing.Count == 0)
            {
                return;
            }

            context.SystemSettings.AddRange(missing);
            await SqliteRetryPolicy.ExecuteAsync(
                token => context.SaveChangesAsync(token),
                cancellationToken);
        }

        private static async Task<OverdueSchedulerStatus> ReadStatusAsync(
            KicsitLibraryDbContext context,
            CancellationToken cancellationToken)
        {
            var values = await context.SystemSettings
                .AsNoTracking()
                .Where(setting => setting.Group == SettingsGroup)
                .ToDictionaryAsync(
                    setting => setting.Key,
                    setting => setting.Value,
                    cancellationToken);
            return CreateStatus(values);
        }

        private static OverdueSchedulerStatus CreateStatus(
            IReadOnlyDictionary<string, string> values)
        {
            return new OverdueSchedulerStatus
            {
                Enabled = ParseBoolean(values, "OverdueSchedulerEnabled"),
                RunOnStartup = ParseBoolean(values, "OverdueSchedulerRunOnStartup"),
                IntervalMinutes = ParsePositiveInteger(
                    values,
                    "OverdueSchedulerIntervalMinutes",
                    60),
                InitialDelaySeconds = ParsePositiveInteger(
                    values,
                    "OverdueSchedulerInitialDelaySeconds",
                    30),
                SendPendingEmails = ParseBoolean(
                    values,
                    "OverdueSchedulerSendPendingEmails"),
                MaxRunMinutes = ParsePositiveInteger(
                    values,
                    "OverdueSchedulerMaxRunMinutes",
                    10),
                LastRunAt = ParseDate(values, "OverdueSchedulerLastRunAt"),
                LastSuccessAt = ParseDate(values, "OverdueSchedulerLastSuccessAt"),
                LastFailureAt = ParseDate(values, "OverdueSchedulerLastFailureAt"),
                LastMessage = Get(values, "OverdueSchedulerLastMessage"),
                IsRunning = ParseBoolean(values, "OverdueSchedulerIsRunning")
            };
        }

        private static async Task SetSettingsAsync(
            KicsitLibraryDbContext context,
            IReadOnlyDictionary<string, string> values,
            CancellationToken cancellationToken)
        {
            foreach (var value in values)
            {
                var setting = await context.SystemSettings
                    .FirstOrDefaultAsync(
                        item => item.Key == value.Key,
                        cancellationToken);
                if (setting == null)
                {
                    setting = new SystemSettings
                    {
                        Key = value.Key,
                        Group = SettingsGroup
                    };
                    context.SystemSettings.Add(setting);
                }

                setting.Value = value.Value;
                setting.UpdatedAt = DateTime.UtcNow;
            }

            await SqliteRetryPolicy.ExecuteAsync(
                token => context.SaveChangesAsync(token),
                cancellationToken);
        }

        private static Task LogWithRetryAsync(
            IActivityLogService logService,
            string action,
            string detail,
            int? userId,
            CancellationToken cancellationToken)
        {
            return SqliteRetryPolicy.ExecuteAsync(
                _ => logService.LogActivityAsync(action, detail, userId),
                cancellationToken);
        }

        private static string BuildSummary(
            OverdueSchedulerRunResult result,
            bool emailDeliveryEnabled)
        {
            var emailSummary = emailDeliveryEnabled
                ? $" Emails attempted {result.EmailsAttempted}; sent {result.EmailsSent}; failed or blocked {result.EmailsFailed}."
                : " Automatic email delivery was disabled.";
            return
                $"Processed {result.ProcessedCount}; created {result.CreatedCount}; " +
                $"skipped {result.SkippedCount}; failed {result.FailedCount}." +
                emailSummary;
        }

        private static string Get(
            IReadOnlyDictionary<string, string> values,
            string key)
        {
            return values.TryGetValue(key, out var value) ? value : string.Empty;
        }

        private static bool ParseBoolean(
            IReadOnlyDictionary<string, string> values,
            string key)
        {
            return bool.TryParse(Get(values, key), out var value) && value;
        }

        private static int ParsePositiveInteger(
            IReadOnlyDictionary<string, string> values,
            string key,
            int fallback)
        {
            return int.TryParse(Get(values, key), out var value) && value > 0
                ? value
                : fallback;
        }

        private static DateTime? ParseDate(
            IReadOnlyDictionary<string, string> values,
            string key)
        {
            return DateTime.TryParse(
                Get(values, key),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var value)
                ? value
                : null;
        }

        private static string FormatDate(DateTime value)
        {
            return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        }

        private static string AppendFailure(string? existing, string? additional)
        {
            if (string.IsNullOrWhiteSpace(existing))
            {
                return additional ?? string.Empty;
            }

            return string.IsNullOrWhiteSpace(additional)
                ? existing
                : $"{existing} {additional}";
        }
    }
}
