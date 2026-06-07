namespace KicsitLibrary.Core.Models;

public sealed class ActivityLogFilter
{
    public string? SearchText { get; set; }
    public string? Action { get; set; }
    public string? EntityName { get; set; }
    public int? EntityId { get; set; }
    public int? UserId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Limit { get; set; } = 500;
}

public class ActivityLogListItem
{
    public int ActivityLogId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public int? EntityId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}

public sealed class ActivityLogDetails : ActivityLogListItem
{
    public string FullDetail { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, string> Metadata { get; set; } =
        new Dictionary<string, string>();
}

public sealed class ActivityLogSummary
{
    public int TotalCount { get; set; }
    public int TodayCount { get; set; }
    public int FailureCount { get; set; }
    public int DistinctUserCount { get; set; }
}

public sealed class ActivityLogUserOption
{
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
}

public sealed class ActivityLogExportResult
{
    public bool Succeeded { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string? FilePath { get; set; }
}

public sealed class ActivityLogDeleteResult
{
    public bool Succeeded { get; set; }
    public int DeletedCount { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}
