using System.Globalization;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data;
using KicsitLibrary.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Services.Backup;

internal static class AutomaticBackupSettingsStore
{
    public const string Group = "AutomaticBackup";

    public static readonly IReadOnlyDictionary<string, string> Defaults =
        new Dictionary<string, string>
        {
            ["AutomaticBackupEnabled"] = "False",
            ["AutomaticBackupRunOnStartup"] = "False",
            ["AutomaticBackupIntervalHours"] = "24",
            ["AutomaticBackupInitialDelaySeconds"] = "60",
            ["AutomaticBackupCompress"] = "False",
            ["AutomaticBackupVerifyAfterCreation"] = "True",
            ["AutomaticBackupDestinationFolder"] = string.Empty,
            ["AutomaticBackupRetentionEnabled"] = "False",
            ["AutomaticBackupRetentionDays"] = "30",
            ["AutomaticBackupMaxHistoryRows"] = "500",
            ["AutomaticBackupDeletePhysicalFiles"] = "False",
            ["AutomaticBackupLastRunAt"] = string.Empty,
            ["AutomaticBackupLastSuccessAt"] = string.Empty,
            ["AutomaticBackupLastFailureAt"] = string.Empty,
            ["AutomaticBackupLastMessage"] = string.Empty,
            ["AutomaticBackupIsRunning"] = "False"
        };

    public static async Task EnsureAsync(
        KicsitLibraryDbContext context,
        CancellationToken cancellationToken)
    {
        var existingKeys = await context.SystemSettings
            .Where(setting => setting.Group == Group)
            .Select(setting => setting.Key)
            .ToListAsync(cancellationToken);
        var missing = Defaults
            .Where(setting => !existingKeys.Contains(setting.Key))
            .Select(setting => new SystemSettings
            {
                Key = setting.Key,
                Value = setting.Value,
                Group = Group,
                Description = "Automatic backup scheduler setting"
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

    public static async Task<IReadOnlyDictionary<string, string>> ReadValuesAsync(
        KicsitLibraryDbContext context,
        CancellationToken cancellationToken)
    {
        await EnsureAsync(context, cancellationToken);
        return await context.SystemSettings
            .AsNoTracking()
            .Where(setting => setting.Group == Group)
            .ToDictionaryAsync(
                setting => setting.Key,
                setting => setting.Value,
                cancellationToken);
    }

    public static AutomaticBackupSchedulerSettings CreateSettings(
        IReadOnlyDictionary<string, string> values)
    {
        return new AutomaticBackupSchedulerSettings
        {
            Enabled = ReadBool(values, "AutomaticBackupEnabled", false),
            RunOnStartup = ReadBool(values, "AutomaticBackupRunOnStartup", false),
            IntervalHours = ReadInt(
                values,
                "AutomaticBackupIntervalHours",
                24,
                1,
                8760),
            InitialDelaySeconds = ReadInt(
                values,
                "AutomaticBackupInitialDelaySeconds",
                60,
                1,
                86400),
            Compress = ReadBool(values, "AutomaticBackupCompress", false),
            VerifyAfterCreation = ReadBool(
                values,
                "AutomaticBackupVerifyAfterCreation",
                true),
            DestinationFolder = Read(
                values,
                "AutomaticBackupDestinationFolder"),
            RetentionEnabled = ReadBool(
                values,
                "AutomaticBackupRetentionEnabled",
                false),
            RetentionDays = ReadInt(
                values,
                "AutomaticBackupRetentionDays",
                30,
                1,
                3650),
            MaxHistoryRows = ReadInt(
                values,
                "AutomaticBackupMaxHistoryRows",
                500,
                1,
                5000),
            DeletePhysicalFiles = ReadBool(
                values,
                "AutomaticBackupDeletePhysicalFiles",
                false)
        };
    }

    public static AutomaticBackupStatus CreateStatus(
        IReadOnlyDictionary<string, string> values)
    {
        var settings = CreateSettings(values);
        return new AutomaticBackupStatus
        {
            Enabled = settings.Enabled,
            RunOnStartup = settings.RunOnStartup,
            RetentionEnabled = settings.RetentionEnabled,
            DeletePhysicalFiles = settings.DeletePhysicalFiles,
            IntervalHours = settings.IntervalHours,
            InitialDelaySeconds = settings.InitialDelaySeconds,
            LastRunAt = ReadDate(values, "AutomaticBackupLastRunAt"),
            LastSuccessAt = ReadDate(values, "AutomaticBackupLastSuccessAt"),
            LastFailureAt = ReadDate(values, "AutomaticBackupLastFailureAt"),
            LastMessage = Read(values, "AutomaticBackupLastMessage"),
            IsRunning = ReadBool(values, "AutomaticBackupIsRunning", false)
        };
    }

    public static async Task SetAsync(
        KicsitLibraryDbContext context,
        IReadOnlyDictionary<string, string> values,
        int? userId,
        CancellationToken cancellationToken)
    {
        foreach (var value in values)
        {
            var setting = await context.SystemSettings.FirstOrDefaultAsync(
                item => item.Key == value.Key,
                cancellationToken);
            if (setting == null)
            {
                setting = new SystemSettings
                {
                    Key = value.Key,
                    Group = Group
                };
                context.SystemSettings.Add(setting);
            }

            setting.Value = value.Value;
            setting.Group = Group;
            setting.UpdatedByUserId = userId;
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await SqliteRetryPolicy.ExecuteAsync(
            token => context.SaveChangesAsync(token),
            cancellationToken);
    }

    public static string FormatDate(DateTime value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static string Read(
        IReadOnlyDictionary<string, string> values,
        string key) =>
        values.TryGetValue(key, out var value) ? value : string.Empty;

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

    private static DateTime? ReadDate(
        IReadOnlyDictionary<string, string> values,
        string key) =>
        DateTime.TryParse(
            Read(values, key),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var value)
                ? value
                : null;
}
