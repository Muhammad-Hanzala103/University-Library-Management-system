using System;
using System.Collections.Generic;

namespace KicsitLibrary.Reports.Models
{
    public enum ReportFormat
    {
        CSV,
        Excel,
        PDF
    }

    public enum ReportFilterType
    {
        Text,
        DateRange,
        Enum,
        NumberRange,
        Boolean
    }

    public sealed class ReportDefinition
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public IReadOnlyList<ReportColumn> Columns { get; set; } = [];
        public IReadOnlyList<ReportFilter> Filters { get; set; } = [];
    }

    public sealed class ReportColumn
    {
        public string Key { get; set; } = string.Empty;
        public string Header { get; set; } = string.Empty;
        public string? Format { get; set; }
    }

    public sealed class ReportFilter
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public ReportFilterType Type { get; set; }
        public object? Value { get; set; }
        public object? SecondaryValue { get; set; }
        public IReadOnlyList<string> Options { get; set; } = [];
    }

    public sealed class ReportRow
    {
        public Dictionary<string, object?> Values { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);

        public object? this[string key]
        {
            get => Values.TryGetValue(key, out var value) ? value : null;
            set => Values[key] = value;
        }
    }

    public sealed class ReportResult
    {
        public string ReportTitle { get; set; } = string.Empty;
        public string InstitutionName { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public string GeneratedBy { get; set; } = string.Empty;
        public IReadOnlyList<string> AppliedFilters { get; set; } = [];
        public IReadOnlyList<ReportColumn> Columns { get; set; } = [];
        public IReadOnlyList<ReportRow> Rows { get; set; } = [];
        public int TotalCount { get; set; }
        public IReadOnlyDictionary<string, string> SummaryItems { get; set; } =
            new Dictionary<string, string>();
    }

    public sealed class ReportExportRequest
    {
        public ReportFormat Format { get; set; }
        public string? OutputDirectory { get; set; }
        public string? FileName { get; set; }
        public bool Overwrite { get; set; }
    }

    public sealed class ReportExportResult
    {
        public bool Succeeded { get; set; }
        public string? FilePath { get; set; }
        public ReportFormat Format { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public DateTime ExportedAt { get; set; }
    }
}
