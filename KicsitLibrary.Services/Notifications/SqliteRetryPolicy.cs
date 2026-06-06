using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Services.Notifications
{
    internal static class SqliteRetryPolicy
    {
        private const int MaxAttempts = 3;

        public static async Task ExecuteAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken)
        {
            await ExecuteAsync(
                async token =>
                {
                    await operation(token);
                    return true;
                },
                cancellationToken);
        }

        public static async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken)
        {
            for (var attempt = 1; ; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    return await operation(cancellationToken);
                }
                catch (Exception ex) when (IsTransient(ex) && attempt < MaxAttempts)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), cancellationToken);
                }
            }
        }

        public static bool IsTransient(Exception exception)
        {
            if (exception is SqliteException sqliteException)
            {
                return sqliteException.SqliteErrorCode is 5 or 6;
            }

            return exception is DbUpdateException { InnerException: not null } updateException
                ? IsTransient(updateException.InnerException!)
                : exception.InnerException != null && IsTransient(exception.InnerException);
        }
    }
}
