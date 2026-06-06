using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Data
{
    public static class DatabaseCompatibilityInitializer
    {
        private static readonly IReadOnlyDictionary<string, string> NotificationColumns =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["IssueRecordId"] = "INTEGER NULL",
                ["RecipientName"] = "TEXT NOT NULL DEFAULT ''",
                ["RecipientCode"] = "TEXT NOT NULL DEFAULT ''",
                ["RecipientEmail"] = "TEXT NULL",
                ["RetryCount"] = "INTEGER NOT NULL DEFAULT 0",
                ["LastAttemptAt"] = "TEXT NULL",
                ["ReadAt"] = "TEXT NULL",
                ["DeduplicationKey"] = "TEXT NULL"
            };

        public static async Task ApplyAsync(KicsitLibraryDbContext context)
        {
            if (!string.Equals(
                    context.Database.ProviderName,
                    "Microsoft.EntityFrameworkCore.Sqlite",
                    StringComparison.Ordinal))
            {
                return;
            }

            var connection = context.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                await connection.OpenAsync();
            }

            try
            {
                var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                await using (var command = connection.CreateCommand())
                {
                    command.CommandText = "PRAGMA table_info('NotificationRecords');";
                    await using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        existingColumns.Add(reader.GetString(1));
                    }
                }

                foreach (var column in NotificationColumns)
                {
                    if (existingColumns.Contains(column.Key))
                    {
                        continue;
                    }

                    await using var alterCommand = connection.CreateCommand();
                    alterCommand.CommandText =
                        $"ALTER TABLE \"NotificationRecords\" ADD COLUMN \"{column.Key}\" {column.Value};";
                    await alterCommand.ExecuteNonQueryAsync();
                }

                await context.Database.ExecuteSqlRawAsync(
                    "CREATE INDEX IF NOT EXISTS \"IX_NotificationRecords_IssueRecordId\" " +
                    "ON \"NotificationRecords\" (\"IssueRecordId\");");
                await context.Database.ExecuteSqlRawAsync(
                    "CREATE INDEX IF NOT EXISTS \"IX_NotificationRecords_CreatedAt\" " +
                    "ON \"NotificationRecords\" (\"CreatedAt\");");
                await context.Database.ExecuteSqlRawAsync(
                    "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_NotificationRecords_DeduplicationKey\" " +
                    "ON \"NotificationRecords\" (\"DeduplicationKey\") " +
                    "WHERE \"DeduplicationKey\" IS NOT NULL;");
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }
    }
}
