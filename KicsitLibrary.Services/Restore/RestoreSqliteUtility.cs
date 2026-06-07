using System.Security.Cryptography;
using KicsitLibrary.Core.Models;
using Microsoft.Data.Sqlite;

namespace KicsitLibrary.Services.Restore;

public static class RestoreSqliteUtility
{
    public static async Task<RestoreValidationResult> ValidateAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return Failure(filePath, "Backup file was not found.");
            }

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length == 0)
            {
                return Failure(filePath, "Backup file is empty.");
            }

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
            var integrityPassed =
                string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
            var requiredTables = new[] { "Users", "BookCopies", "IssueRecords", "SystemSettings" };
            await using var schemaCommand = connection.CreateCommand();
            schemaCommand.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' " +
                "AND name IN ('Users', 'BookCopies', 'IssueRecords', 'SystemSettings');";
            var detectedRequiredTables = Convert.ToInt32(
                await schemaCommand.ExecuteScalarAsync(cancellationToken));
            var schemaPassed = detectedRequiredTables == requiredTables.Length;
            var passed = integrityPassed && schemaPassed;
            return new RestoreValidationResult
            {
                Succeeded = passed,
                BackupFilePath = Path.GetFullPath(filePath),
                IntegrityCheckPassed = integrityPassed,
                ChecksumSha256 = checksum,
                FileSizeBytes = fileInfo.Length,
                ValidationMessage = passed
                    ? "SQLite integrity and KICSIT schema checks passed."
                    : "SQLite restore validation failed.",
                ErrorMessage = passed
                    ? null
                    : !integrityPassed
                        ? $"SQLite integrity_check returned: {result}"
                        : "The database does not contain the required KICSIT library tables."
            };
        }
        catch (Exception ex)
        {
            return Failure(filePath, Sanitize(ex.Message));
        }
    }

    public static async Task<int> CountRowsIfTableExistsAsync(
        SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        await using var existsCommand = connection.CreateCommand();
        existsCommand.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        existsCommand.Parameters.AddWithValue("$name", tableName);
        var exists = Convert.ToInt32(
            await existsCommand.ExecuteScalarAsync(cancellationToken)) > 0;
        if (!exists)
        {
            return 0;
        }

        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = $"SELECT COUNT(*) FROM \"{tableName}\";";
        return Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));
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

    private static RestoreValidationResult Failure(string filePath, string error) =>
        new()
        {
            BackupFilePath = filePath,
            ValidationMessage = "Backup validation failed.",
            ErrorMessage = error
        };

    private static string Sanitize(string value) =>
        value.ReplaceLineEndings(" ").Trim()[..Math.Min(value.ReplaceLineEndings(" ").Trim().Length, 1000)];
}
