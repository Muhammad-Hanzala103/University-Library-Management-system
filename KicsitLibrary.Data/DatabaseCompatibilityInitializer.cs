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

        private static readonly IReadOnlyDictionary<string, string> StudentClearanceColumns =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ClearedByUserId"] = "INTEGER NULL"
            };

        private static readonly IReadOnlyDictionary<string, string> FacultyClearanceColumns =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ClearanceStatus"] = "TEXT NOT NULL DEFAULT 'NotCleared'",
                ["ClearanceDate"] = "TEXT NULL",
                ["ClearanceRemarks"] = "TEXT NULL",
                ["ClearedByUserId"] = "INTEGER NULL"
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
                await AddMissingColumnsAsync(connection, "NotificationRecords", NotificationColumns);
                await AddMissingColumnsAsync(connection, "Students", StudentClearanceColumns);
                await AddMissingColumnsAsync(connection, "FacultyStaff", FacultyClearanceColumns);
                await context.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE IF NOT EXISTS "StockVerificationSessions" (
                        "Id" INTEGER NOT NULL CONSTRAINT "PK_StockVerificationSessions" PRIMARY KEY AUTOINCREMENT,
                        "SessionNumber" TEXT NOT NULL,
                        "StartedAt" TEXT NOT NULL,
                        "CompletedAt" TEXT NULL,
                        "Status" TEXT NOT NULL,
                        "StartedByUserId" INTEGER NOT NULL,
                        "CompletedByUserId" INTEGER NULL,
                        "Remarks" TEXT NULL,
                        "CreatedAt" TEXT NOT NULL,
                        "UpdatedAt" TEXT NULL,
                        "IsDeleted" INTEGER NOT NULL DEFAULT 0,
                        "DeletedAt" TEXT NULL,
                        "DeletedReason" TEXT NULL,
                        "DeletedByUserId" INTEGER NULL
                    );
                    """);
                await context.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE IF NOT EXISTS "StockVerificationEntries" (
                        "Id" INTEGER NOT NULL CONSTRAINT "PK_StockVerificationEntries" PRIMARY KEY AUTOINCREMENT,
                        "SessionId" INTEGER NOT NULL,
                        "BookCopyId" INTEGER NOT NULL,
                        "ExpectedStatus" TEXT NOT NULL,
                        "ActualStatus" TEXT NULL,
                        "VerificationRemarks" TEXT NULL,
                        "VerifiedAt" TEXT NULL,
                        "VerifiedByUserId" INTEGER NULL,
                        "IsMismatch" INTEGER NOT NULL DEFAULT 0,
                        "IsReconciled" INTEGER NOT NULL DEFAULT 0,
                        "ReconciledAt" TEXT NULL,
                        "ReconciledByUserId" INTEGER NULL,
                        "ReconciliationReason" TEXT NULL,
                        "CreatedAt" TEXT NOT NULL,
                        "UpdatedAt" TEXT NULL,
                        "IsDeleted" INTEGER NOT NULL DEFAULT 0,
                        "DeletedAt" TEXT NULL,
                        "DeletedReason" TEXT NULL,
                        "DeletedByUserId" INTEGER NULL
                    );
                    """);
                await context.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE IF NOT EXISTS "BackupHistories" (
                        "Id" INTEGER NOT NULL CONSTRAINT "PK_BackupHistories" PRIMARY KEY AUTOINCREMENT,
                        "BackupFileName" TEXT NOT NULL,
                        "BackupFilePath" TEXT NOT NULL,
                        "CompressedFilePath" TEXT NULL,
                        "BackupSizeBytes" INTEGER NOT NULL DEFAULT 0,
                        "ChecksumSha256" TEXT NULL,
                        "CreatedByUserId" INTEGER NOT NULL,
                        "CreatedByUserName" TEXT NOT NULL,
                        "VerifiedAt" TEXT NULL,
                        "VerificationStatus" TEXT NOT NULL DEFAULT 'Pending',
                        "Reason" TEXT NULL,
                        "Status" TEXT NOT NULL DEFAULT 'InProgress',
                        "ErrorMessage" TEXT NULL,
                        "MetadataJson" TEXT NULL,
                        "CreatedAt" TEXT NOT NULL,
                        "UpdatedAt" TEXT NULL,
                        "IsDeleted" INTEGER NOT NULL DEFAULT 0,
                        "DeletedAt" TEXT NULL,
                        "DeletedReason" TEXT NULL,
                        "DeletedByUserId" INTEGER NULL
                    );
                    """);
                await context.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE IF NOT EXISTS "RestoreHistories" (
                        "Id" INTEGER NOT NULL CONSTRAINT "PK_RestoreHistories" PRIMARY KEY AUTOINCREMENT,
                        "BackupFilePath" TEXT NOT NULL,
                        "SafetyBackupFilePath" TEXT NULL,
                        "RestoredDatabasePath" TEXT NOT NULL,
                        "RequestedByUserId" INTEGER NOT NULL,
                        "RequestedByUserName" TEXT NOT NULL,
                        "StartedAt" TEXT NOT NULL,
                        "FinishedAt" TEXT NULL,
                        "Status" TEXT NOT NULL DEFAULT 'Started',
                        "Reason" TEXT NULL,
                        "ErrorMessage" TEXT NULL,
                        "RolledBack" INTEGER NOT NULL DEFAULT 0,
                        "ChecksumSha256" TEXT NULL,
                        "MetadataJson" TEXT NULL,
                        "CreatedAt" TEXT NOT NULL,
                        "UpdatedAt" TEXT NULL,
                        "IsDeleted" INTEGER NOT NULL DEFAULT 0,
                        "DeletedAt" TEXT NULL,
                        "DeletedReason" TEXT NULL,
                        "DeletedByUserId" INTEGER NULL
                    );
                    """);

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
                await context.Database.ExecuteSqlRawAsync(
                    "CREATE INDEX IF NOT EXISTS \"IX_Students_ClearanceStatus\" " +
                    "ON \"Students\" (\"ClearanceStatus\");");
                await context.Database.ExecuteSqlRawAsync(
                    "CREATE INDEX IF NOT EXISTS \"IX_FacultyStaff_ClearanceStatus\" " +
                    "ON \"FacultyStaff\" (\"ClearanceStatus\");");
                await context.Database.ExecuteSqlRawAsync(
                    "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_StockVerificationSessions_SessionNumber\" ON \"StockVerificationSessions\" (\"SessionNumber\");");
                await context.Database.ExecuteSqlRawAsync(
                    "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_StockVerificationEntries_SessionId_BookCopyId\" ON \"StockVerificationEntries\" (\"SessionId\", \"BookCopyId\");");
                await context.Database.ExecuteSqlRawAsync(
                    "CREATE INDEX IF NOT EXISTS \"IX_BackupHistories_CreatedAt\" ON \"BackupHistories\" (\"CreatedAt\");");
                await context.Database.ExecuteSqlRawAsync(
                    "CREATE INDEX IF NOT EXISTS \"IX_BackupHistories_Status\" ON \"BackupHistories\" (\"Status\");");
                await context.Database.ExecuteSqlRawAsync(
                    "CREATE INDEX IF NOT EXISTS \"IX_BackupHistories_CreatedByUserName\" ON \"BackupHistories\" (\"CreatedByUserName\");");
                await context.Database.ExecuteSqlRawAsync(
                    "CREATE INDEX IF NOT EXISTS \"IX_RestoreHistories_StartedAt\" ON \"RestoreHistories\" (\"StartedAt\");");
                await context.Database.ExecuteSqlRawAsync(
                    "CREATE INDEX IF NOT EXISTS \"IX_RestoreHistories_Status\" ON \"RestoreHistories\" (\"Status\");");
                await context.Database.ExecuteSqlRawAsync(
                    "CREATE INDEX IF NOT EXISTS \"IX_RestoreHistories_RequestedByUserName\" ON \"RestoreHistories\" (\"RequestedByUserName\");");
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private static async Task AddMissingColumnsAsync(
            System.Data.Common.DbConnection connection,
            string tableName,
            IReadOnlyDictionary<string, string> columns)
        {
            var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = $"PRAGMA table_info('{tableName}');";
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    existingColumns.Add(reader.GetString(1));
                }
            }

            foreach (var column in columns)
            {
                if (existingColumns.Contains(column.Key))
                {
                    continue;
                }

                await using var alterCommand = connection.CreateCommand();
                alterCommand.CommandText =
                    $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{column.Key}\" {column.Value};";
                await alterCommand.ExecuteNonQueryAsync();
            }
        }
    }
}
