using KicsitLibrary.Core;
using Microsoft.Data.Sqlite;

namespace KicsitLibrary.Services.Runtime;

public static class StartupDatabasePathResolver
{
    public static string ResolveSqliteConnectionString(
        string? connectionString,
        string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("The SQLite connection string is not configured.");
        }

        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource))
        {
            throw new InvalidOperationException("The SQLite data source is not configured.");
        }

        if (builder.DataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return builder.ToString();
        }

        var configuredPath = Path.IsPathRooted(builder.DataSource)
            ? Path.GetFullPath(builder.DataSource)
            : Path.GetFullPath(Path.Combine(baseDirectory, builder.DataSource));
        var selectedPath = ResolveReleasePathIfEnabled(configuredPath);
        var databaseDirectory = Path.GetDirectoryName(selectedPath);
        if (!string.IsNullOrWhiteSpace(databaseDirectory))
        {
            Directory.CreateDirectory(databaseDirectory);
        }

        builder.DataSource = selectedPath;
        return builder.ToString();
    }

    public static string ResolveReleasePathFromSettings(
        string configuredDatabasePath,
        IReadOnlyDictionary<string, string> settings)
    {
        if (!ReadBool(settings, "UseReleaseDataRoot", false) &&
            !string.Equals(
                Read(settings, "RuntimeStorageMode", "Development"),
                "Release",
                StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFullPath(configuredDatabasePath);
        }

        var root = Read(settings, "RuntimeDataRoot", string.Empty);
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                ProductBrand.Name);
        }
        if (ContainsTraversalSegment(root))
        {
            throw new InvalidOperationException("RuntimeDataRoot must not contain path traversal segments.");
        }

        var fileName = Read(settings, "DatabaseFileName", "KicsitLibrary.db");
        if (ContainsTraversalSegment(fileName) ||
            Path.IsPathRooted(fileName) ||
            fileName.Contains(Path.DirectorySeparatorChar) ||
            fileName.Contains(Path.AltDirectorySeparatorChar) ||
            !fileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("DatabaseFileName must be a simple SQLite .db file name.");
        }

        var normalizedRoot = Path.GetFullPath(root);
        var releasePath = Path.GetFullPath(Path.Combine(normalizedRoot, fileName));
        if (!releasePath.StartsWith(
                Path.TrimEndingDirectorySeparator(normalizedRoot) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The release database path is outside the runtime data root.");
        }

        if (!File.Exists(releasePath))
        {
            throw new InvalidOperationException(
                $"Release database relocation is enabled, but the database was not found at '{releasePath}'. Disable UseReleaseDataRoot or restore the relocated database.");
        }

        return releasePath;
    }

    private static string ResolveReleasePathIfEnabled(string configuredPath)
    {
        if (!File.Exists(configuredPath))
        {
            return configuredPath;
        }

        try
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = configuredPath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            };
            using var connection = new SqliteConnection(builder.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT Key, Value FROM SystemSettings WHERE Key IN (" +
                "'UseReleaseDataRoot','RuntimeStorageMode','RuntimeDataRoot','DatabaseFileName');";
            using var reader = command.ExecuteReader();
            var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read())
            {
                settings[reader.GetString(0)] = reader.GetString(1);
            }

            return ResolveReleasePathFromSettings(configuredPath, settings);
        }
        catch (SqliteException)
        {
            return configuredPath;
        }
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
}
