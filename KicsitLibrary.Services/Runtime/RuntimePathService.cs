using KicsitLibrary.Core;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Data;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Services.Runtime;

public sealed class RuntimePathService(KicsitLibraryDbContext context) : IRuntimePathService
{
    private const string DevelopmentMode = "Development";
    private const string ReleaseMode = "Release";

    public async Task<string> GetDataRootAsync(CancellationToken cancellationToken = default)
    {
        var settings = await ReadSettingsAsync(cancellationToken);
        return ResolveDataRoot(settings);
    }

    public async Task<string> GetDatabasePathAsync(CancellationToken cancellationToken = default)
    {
        var settings = await ReadSettingsAsync(cancellationToken);
        var fileName = ReadFileName(settings.DatabaseFileName, "KicsitLibrary.db");
        var root = ShouldUseReleaseRoot(settings)
            ? ResolveDataRoot(settings)
            : AppContext.BaseDirectory;
        return EnsureUnderRoot(root, Path.Combine(root, fileName));
    }

    public async Task<string> GetDocumentStorageRootAsync(CancellationToken cancellationToken = default)
    {
        var settings = await ReadSettingsAsync(cancellationToken);
        if (!ShouldUseReleaseRoot(settings))
        {
            return Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                ProductBrand.Name,
                "Documents"));
        }

        return CombineRuntimeFolder(settings, settings.DocumentsFolderName, "Documents");
    }

    public async Task<string> GetBackupRootAsync(CancellationToken cancellationToken = default)
    {
        var settings = await ReadSettingsAsync(cancellationToken);
        if (!ShouldUseReleaseRoot(settings))
        {
            return Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                ProductBrand.BackupFolderName));
        }

        return CombineRuntimeFolder(settings, settings.BackupsFolderName, "Backups");
    }

    public async Task<string> GetRestoreStagingRootAsync(CancellationToken cancellationToken = default)
    {
        var settings = await ReadSettingsAsync(cancellationToken);
        return ShouldUseReleaseRoot(settings)
            ? CombineRuntimeFolder(settings, settings.RestoreStagingFolderName, "RestoreStaging")
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".kicsit-restore"));
    }

    public async Task<string> GetReportExportRootAsync(CancellationToken cancellationToken = default)
    {
        var settings = await ReadSettingsAsync(cancellationToken);
        if (!ShouldUseReleaseRoot(settings))
        {
            return Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                ProductBrand.ReportFolderName));
        }

        return CombineRuntimeFolder(settings, settings.ReportsFolderName, "Reports");
    }

    public async Task<string> GetCertificateRootAsync(CancellationToken cancellationToken = default)
    {
        var settings = await ReadSettingsAsync(cancellationToken);
        if (!ShouldUseReleaseRoot(settings))
        {
            return Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                ProductBrand.CertificateFolderName));
        }

        return CombineRuntimeFolder(settings, settings.CertificatesFolderName, "Certificates");
    }

    public async Task<string> GetLogsRootAsync(CancellationToken cancellationToken = default)
    {
        var settings = await ReadSettingsAsync(cancellationToken);
        return CombineRuntimeFolder(settings, settings.LogsFolderName, "Logs");
    }

    public async Task<string> GetTempRootAsync(CancellationToken cancellationToken = default)
    {
        var settings = await ReadSettingsAsync(cancellationToken);
        return CombineRuntimeFolder(settings, settings.TempFolderName, "Temp");
    }

    public async Task<string> GetLockRootAsync(CancellationToken cancellationToken = default)
    {
        var settings = await ReadSettingsAsync(cancellationToken);
        return CombineRuntimeFolder(settings, settings.LocksFolderName, "Locks");
    }

    public async Task EnsureRuntimeFoldersAsync(CancellationToken cancellationToken = default)
    {
        var folders = new[]
        {
            await GetDataRootAsync(cancellationToken),
            await GetDocumentStorageRootAsync(cancellationToken),
            await GetBackupRootAsync(cancellationToken),
            await GetRestoreStagingRootAsync(cancellationToken),
            await GetReportExportRootAsync(cancellationToken),
            await GetCertificateRootAsync(cancellationToken),
            await GetLogsRootAsync(cancellationToken),
            await GetTempRootAsync(cancellationToken),
            await GetLockRootAsync(cancellationToken)
        };

        foreach (var folder in folders.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(folder);
        }
    }

    private async Task<RuntimePathSettings> ReadSettingsAsync(CancellationToken cancellationToken)
    {
        var keys = new[]
        {
            "RuntimeDataRoot",
            "RuntimeStorageMode",
            "UseReleaseDataRoot",
            "DatabaseFileName",
            "DocumentsFolderName",
            "BackupsFolderName",
            "ReportsFolderName",
            "CertificatesFolderName",
            "RestoreStagingFolderName",
            "LogsFolderName",
            "TempFolderName",
            "LocksFolderName"
        };
        var values = await context.SystemSettings.AsNoTracking()
            .Where(setting => keys.Contains(setting.Key))
            .ToDictionaryAsync(setting => setting.Key, setting => setting.Value, cancellationToken);

        return new RuntimePathSettings(
            Read(values, "RuntimeDataRoot", string.Empty),
            Read(values, "RuntimeStorageMode", DevelopmentMode),
            ReadBool(values, "UseReleaseDataRoot", false),
            Read(values, "DatabaseFileName", "KicsitLibrary.db"),
            Read(values, "DocumentsFolderName", "Documents"),
            Read(values, "BackupsFolderName", "Backups"),
            Read(values, "ReportsFolderName", "Reports"),
            Read(values, "CertificatesFolderName", "Certificates"),
            Read(values, "RestoreStagingFolderName", "RestoreStaging"),
            Read(values, "LogsFolderName", "Logs"),
            Read(values, "TempFolderName", "Temp"),
            Read(values, "LocksFolderName", "Locks"));
    }

    private static string ResolveDataRoot(RuntimePathSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.RuntimeDataRoot))
        {
            return NormalizeRoot(settings.RuntimeDataRoot);
        }

        if (ShouldUseReleaseRoot(settings))
        {
            return Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                ProductBrand.Name));
        }

        return Path.GetFullPath(AppContext.BaseDirectory);
    }

    private static string CombineRuntimeFolder(
        RuntimePathSettings settings,
        string configuredFolderName,
        string fallbackFolderName)
    {
        var root = ResolveDataRoot(settings);
        var folderName = ReadPathSegment(configuredFolderName, fallbackFolderName);
        return EnsureUnderRoot(root, Path.Combine(root, folderName));
    }

    private static bool ShouldUseReleaseRoot(RuntimePathSettings settings) =>
        settings.UseReleaseDataRoot ||
        string.Equals(settings.RuntimeStorageMode, ReleaseMode, StringComparison.OrdinalIgnoreCase) ||
        !string.IsNullOrWhiteSpace(settings.RuntimeDataRoot);

    private static string NormalizeRoot(string root)
    {
        if (ContainsTraversalSegment(root))
        {
            throw new InvalidOperationException("RuntimeDataRoot must not contain path traversal segments.");
        }

        return Path.GetFullPath(root.Trim());
    }

    private static string EnsureUnderRoot(string root, string path)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var normalizedPath = Path.GetFullPath(path);
        if (!normalizedPath.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase) &&
            !normalizedPath.StartsWith(
                normalizedRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Resolved runtime path is outside the runtime data root.");
        }

        return normalizedPath;
    }

    private static string ReadPathSegment(string value, string fallback)
    {
        var segment = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        if (ContainsTraversalSegment(segment) ||
            Path.IsPathRooted(segment) ||
            segment.Contains(Path.DirectorySeparatorChar) ||
            segment.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new InvalidOperationException("Runtime folder names must be simple folder names.");
        }

        if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidOperationException("Runtime folder names contain invalid characters.");
        }

        return segment;
    }

    private static string ReadFileName(string value, string fallback)
    {
        var fileName = ReadPathSegment(value, fallback);
        if (!fileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("DatabaseFileName must be a SQLite .db file name.");
        }

        return fileName;
    }

    private static bool ContainsTraversalSegment(string value)
    {
        var parts = value.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        return parts.Any(part => part == "..");
    }

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

    private sealed record RuntimePathSettings(
        string RuntimeDataRoot,
        string RuntimeStorageMode,
        bool UseReleaseDataRoot,
        string DatabaseFileName,
        string DocumentsFolderName,
        string BackupsFolderName,
        string ReportsFolderName,
        string CertificatesFolderName,
        string RestoreStagingFolderName,
        string LogsFolderName,
        string TempFolderName,
        string LocksFolderName);
}
