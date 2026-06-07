using System.Text.Json;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data;
using KicsitLibrary.Reports.Contracts;
using KicsitLibrary.Reports.Models;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Services.Auditing;

public sealed class ActivityLogBrowserService : IActivityLogBrowserService
{
    private readonly KicsitLibraryDbContext _context;
    private readonly IAuthenticationService _authenticationService;
    private readonly IReportExportService _reportExportService;

    public ActivityLogBrowserService(
        KicsitLibraryDbContext context,
        IAuthenticationService authenticationService,
        IReportExportService reportExportService)
    {
        _context = context;
        _authenticationService = authenticationService;
        _reportExportService = reportExportService;
    }

    public async Task<IReadOnlyList<ActivityLogListItem>> GetActivityLogsAsync(
        ActivityLogFilter filter,
        CancellationToken cancellationToken = default)
    {
        await RequireViewAsync();
        filter ??= new ActivityLogFilter();
        var query = _context.ActivityLogs.AsNoTracking().Include(item => item.User).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Action))
            query = query.Where(item => item.Action == filter.Action);
        if (!string.IsNullOrWhiteSpace(filter.EntityName))
        {
            var entityName = filter.EntityName.Trim();
            query = query.Where(item =>
                item.Detail.Contains($"EntityName={entityName}") ||
                item.Detail.Contains($"TableName={entityName}") ||
                item.Detail.Contains($"Entity={entityName}") ||
                item.Detail.Contains($"MemberType={entityName}"));
        }
        if (filter.EntityId.HasValue)
        {
            var entityId = filter.EntityId.Value.ToString();
            query = query.Where(item =>
                item.Detail.Contains($"EntityId={entityId}") ||
                item.Detail.Contains($"RecordId={entityId}") ||
                item.Detail.Contains($"MemberId={entityId}") ||
                item.Detail.Contains($"AuditRecordId={entityId}"));
        }
        if (filter.UserId.HasValue)
            query = query.Where(item => item.UserId == filter.UserId.Value);
        if (filter.FromDate.HasValue)
            query = query.Where(item => item.CreatedAt >= filter.FromDate.Value.Date.ToUniversalTime());
        if (filter.ToDate.HasValue)
            query = query.Where(item => item.CreatedAt < filter.ToDate.Value.Date.AddDays(1).ToUniversalTime());
        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var search = filter.SearchText.Trim();
            query = query.Where(item =>
                item.Action.Contains(search) ||
                item.Detail.Contains(search) ||
                (item.User != null &&
                    (item.User.Username.Contains(search) ||
                     item.User.FullName.Contains(search))));
        }

        var candidates = await query
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Take(Math.Clamp(filter.Limit <= 0 ? 500 : filter.Limit, 1, 2000))
            .ToListAsync(cancellationToken);
        return candidates.Select(Map).ToList();
    }

    public async Task<ActivityLogDetails> GetActivityLogDetailsAsync(
        int activityLogId,
        CancellationToken cancellationToken = default)
    {
        await RequireViewAsync();
        var log = await _context.ActivityLogs.AsNoTracking()
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.Id == activityLogId, cancellationToken) ??
            throw new InvalidOperationException("Activity log record was not found.");
        var item = Map(log);
        return new ActivityLogDetails
        {
            ActivityLogId = item.ActivityLogId,
            CreatedAt = item.CreatedAt,
            UserId = item.UserId,
            UserName = item.UserName,
            Username = item.Username,
            Action = item.Action,
            EntityName = item.EntityName,
            EntityId = item.EntityId,
            Description = item.Description,
            Outcome = item.Outcome,
            Source = item.Source,
            FullDetail = log.Detail,
            IpAddress = log.IpAddress ?? string.Empty,
            Metadata = ParseMetadata(log.Detail)
        };
    }

    public async Task<ActivityLogSummary> GetActivityLogSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        await RequireViewAsync();
        var todayUtc = DateTime.Today.ToUniversalTime();
        var logs = await _context.ActivityLogs.AsNoTracking()
            .Select(item => new { item.Action, item.Detail, item.UserId, item.CreatedAt })
            .ToListAsync(cancellationToken);
        return new ActivityLogSummary
        {
            TotalCount = logs.Count,
            TodayCount = logs.Count(item => item.CreatedAt >= todayUtc),
            FailureCount = logs.Count(item => IsFailure(item.Action, item.Detail)),
            DistinctUserCount = logs.Where(item => item.UserId.HasValue)
                .Select(item => item.UserId).Distinct().Count()
        };
    }

    public async Task<ActivityLogExportResult> ExportActivityLogSnapshotAsync(
        ActivityLogFilter filter,
        string format,
        CancellationToken cancellationToken = default)
    {
        var rows = await GetActivityLogsAsync(filter, cancellationToken);
        if (!Enum.TryParse<ReportFormat>(format, true, out var reportFormat))
        {
            return new ActivityLogExportResult
            {
                ErrorMessage = "Export format must be CSV, Excel, or PDF.",
                Message = "Activity log export failed."
            };
        }

        var report = new ReportResult
        {
            ReportTitle = "Activity Log Snapshot",
            InstitutionName = "KICSIT Library",
            GeneratedAt = DateTime.UtcNow,
            GeneratedBy = _authenticationService.CurrentUser?.FullName ?? "Unknown User",
            Columns =
            [
                Column("CreatedAt", "Date Time", "dd-MMM-yyyy HH:mm:ss"),
                Column("User", "User"),
                Column("Action", "Action"),
                Column("EntityName", "Entity Name"),
                Column("EntityId", "Entity Id"),
                Column("Description", "Description"),
                Column("Outcome", "Outcome"),
                Column("Source", "Source")
            ],
            Rows = rows.Select(item => new ReportRow
            {
                Values = new Dictionary<string, object?>
                {
                    ["CreatedAt"] = item.CreatedAt,
                    ["User"] = item.UserName,
                    ["Action"] = item.Action,
                    ["EntityName"] = item.EntityName,
                    ["EntityId"] = item.EntityId,
                    ["Description"] = item.Description,
                    ["Outcome"] = item.Outcome,
                    ["Source"] = item.Source
                }
            }).ToList(),
            TotalCount = rows.Count,
            SummaryItems = new Dictionary<string, string>
            {
                ["Records"] = rows.Count.ToString()
            }
        };
        var result = await _reportExportService.ExportAsync(
            report,
            new ReportExportRequest { Format = reportFormat },
            _authenticationService.CurrentUser?.Id,
            cancellationToken);
        return new ActivityLogExportResult
        {
            Succeeded = result.Succeeded,
            Message = result.Message,
            ErrorMessage = result.ErrorMessage,
            FilePath = result.FilePath
        };
    }

    public async Task<ActivityLogDeleteResult> DeleteOldLogsAsync(
        DateTime olderThanUtc,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (!AuditAuthorization.CanDeleteLogs(_authenticationService))
            return DeleteFailure("Only Super Admin or Admin can delete old activity logs.");
        if (string.IsNullOrWhiteSpace(reason))
            return DeleteFailure("A deletion reason is required.");

        var cutoff = EnsureUtc(olderThanUtc);
        var logs = await _context.ActivityLogs
            .Where(item => item.CreatedAt < cutoff)
            .ToListAsync(cancellationToken);
        if (logs.Count == 0)
            return new ActivityLogDeleteResult { Succeeded = true, Message = "No old logs matched the cutoff." };

        var user = _authenticationService.CurrentUser!;
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var log in logs)
            {
                log.IsDeleted = true;
                log.DeletedAt = DateTime.UtcNow;
                log.DeletedReason = reason.Trim();
                log.DeletedByUserId = user.Id;
            }
            _context.DeletedRecordArchives.Add(new DeletedRecordArchive
            {
                TableName = "ActivityLogs",
                RecordId = 0,
                SerializedData = JsonSerializer.Serialize(new
                {
                    Count = logs.Count,
                    CutoffUtc = cutoff,
                    FirstLogUtc = logs.Min(item => item.CreatedAt),
                    LastLogUtc = logs.Max(item => item.CreatedAt)
                }),
                DeletedByUserId = user.Id,
                DeletedAt = DateTime.UtcNow,
                DeletedReason = reason.Trim()
            });
            _context.ActivityLogs.Add(new ActivityLog
            {
                Action = "Activity Logs Archived",
                Detail = $"EntityName=ActivityLog;DeletedCount={logs.Count};CutoffUtc={cutoff:O};Reason={reason.Trim()}",
                UserId = user.Id,
                IpAddress = "127.0.0.1"
            });
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ActivityLogDeleteResult
            {
                Succeeded = true,
                DeletedCount = logs.Count,
                Message = $"{logs.Count} old activity log(s) were soft-deleted and summarized in the archive."
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return DeleteFailure(ex.Message);
        }
    }

    public async Task<IReadOnlyList<string>> GetDistinctActionsAsync(
        CancellationToken cancellationToken = default)
    {
        await RequireViewAsync();
        return await _context.ActivityLogs.AsNoTracking()
            .Select(item => item.Action).Distinct().OrderBy(item => item)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetDistinctEntityNamesAsync(
        CancellationToken cancellationToken = default)
    {
        var logs = await GetActivityLogsAsync(new ActivityLogFilter { Limit = 2000 }, cancellationToken);
        return logs.Select(item => item.EntityName)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item)
            .ToList();
    }

    public async Task<IReadOnlyList<ActivityLogUserOption>> GetDistinctUsersAsync(
        CancellationToken cancellationToken = default)
    {
        await RequireViewAsync();
        return await _context.Users.AsNoTracking()
            .Where(user => _context.ActivityLogs.Any(log => log.UserId == user.Id))
            .OrderBy(user => user.FullName)
            .Select(user => new ActivityLogUserOption
            {
                UserId = user.Id,
                DisplayName = user.FullName,
                Username = user.Username
            })
            .ToListAsync(cancellationToken);
    }

    private async Task RequireViewAsync()
    {
        if (!await AuditAuthorization.CanViewAsync(_authenticationService))
            throw new UnauthorizedAccessException("The current user cannot view activity logs.");
    }

    private static ActivityLogListItem Map(ActivityLog log)
    {
        var metadata = ParseMetadata(log.Detail);
        var entityName = Get(metadata, "EntityName", "TableName", "Entity", "MemberType");
        var entityIdText = Get(metadata, "EntityId", "RecordId", "MemberId", "AuditRecordId");
        int.TryParse(entityIdText, out var entityId);
        return new ActivityLogListItem
        {
            ActivityLogId = log.Id,
            CreatedAt = log.CreatedAt.ToLocalTime(),
            UserId = log.UserId,
            UserName = log.User?.FullName ?? "System / Unknown",
            Username = log.User?.Username ?? string.Empty,
            Action = log.Action,
            EntityName = entityName,
            EntityId = entityId > 0 ? entityId : null,
            Description = log.Detail,
            Outcome = IsFailure(log.Action, log.Detail) ? "Failure" : "Success",
            Source = string.IsNullOrWhiteSpace(log.IpAddress) ? "Application" : log.IpAddress
        };
    }

    private static IReadOnlyDictionary<string, string> ParseMetadata(string detail)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in detail.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 2 && !string.IsNullOrWhiteSpace(pair[0]))
                values[pair[0].Trim()] = pair[1].Trim();
        }
        return values;
    }

    private static string Get(IReadOnlyDictionary<string, string> values, params string[] keys)
    {
        foreach (var key in keys)
            if (values.TryGetValue(key, out var value))
                return value;
        return string.Empty;
    }

    private static bool IsFailure(string action, string detail) =>
        action.Contains("Failed", StringComparison.OrdinalIgnoreCase) ||
        action.Contains("Blocked", StringComparison.OrdinalIgnoreCase) ||
        detail.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
        detail.Contains("error", StringComparison.OrdinalIgnoreCase);

    private static ReportColumn Column(string key, string header, string? format = null) =>
        new() { Key = key, Header = header, Format = format };

    private static ActivityLogDeleteResult DeleteFailure(string error) =>
        new() { Message = "Activity log deletion failed.", ErrorMessage = error };

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
