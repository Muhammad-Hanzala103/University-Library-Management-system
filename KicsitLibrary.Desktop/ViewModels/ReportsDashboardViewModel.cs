using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Reports.Contracts;
using KicsitLibrary.Reports.Models;

namespace KicsitLibrary.Desktop.ViewModels;

public partial class ReportsDashboardViewModel : ObservableObject
{
    private static readonly string[] CategoryOrder =
    [
        "Catalog Reports",
        "Circulation Reports",
        "Financial Reports",
        "Consumer Reports",
        "Compliance Reports",
        "Inventory Reports",
        "Notification Reports"
    ];

    private readonly IReportService _reportService;
    private readonly IReportExportService _reportExportService;
    private readonly IAuthenticationService _authenticationService;
    private readonly IReadOnlyList<ReportDefinition> _allReports;

    public ObservableCollection<ReportCategoryGroup> ReportGroups { get; } = [];
    public ObservableCollection<ReportFilterInputViewModel> FilterInputs { get; } = [];
    public ReportPreviewViewModel Preview { get; } = new();

    [ObservableProperty] private ReportDefinition? _selectedReport;
    [ObservableProperty] private string _reportSearchText = string.Empty;
    [ObservableProperty] private int _visibleReportCount;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;

    public ReportsDashboardViewModel(
        IReportService reportService,
        IReportExportService reportExportService,
        IAuthenticationService authenticationService)
    {
        _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
        _reportExportService = reportExportService ??
            throw new ArgumentNullException(nameof(reportExportService));
        _authenticationService = authenticationService ??
            throw new ArgumentNullException(nameof(authenticationService));
        _allReports = _reportService.GetDefinitions();
        RebuildReportGroups();
        SelectedReport = _allReports.FirstOrDefault();
        BuildFilterInputs();
        _ = RefreshPreviewAsync();
    }

    partial void OnReportSearchTextChanged(string value)
    {
        RebuildReportGroups();
    }

    partial void OnSelectedReportChanged(ReportDefinition? value)
    {
        BuildFilterInputs();
    }

    [RelayCommand]
    private async Task SelectReportAsync(ReportDefinition report)
    {
        SelectedReport = report;
        await RefreshPreviewAsync();
    }

    [RelayCommand]
    private async Task RefreshPreviewAsync()
    {
        if (SelectedReport == null)
        {
            ErrorMessage = "Select a report first.";
            return;
        }

        if (!TryBuildFilters(out var filters, out var validationError))
        {
            ErrorMessage = validationError;
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var result = await _reportService.GenerateAsync(
                SelectedReport.Key,
                filters,
                _authenticationService.CurrentUser?.FullName ?? "Unknown User");
            Preview.SetResult(result);
            StatusMessage = $"{result.ReportTitle}: {result.TotalCount} record(s) loaded.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unable to generate report preview: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ClearFiltersAsync()
    {
        foreach (var input in FilterInputs)
        {
            input.Clear();
        }
        await RefreshPreviewAsync();
    }

    [RelayCommand]
    private Task ExportCsvAsync() => ExportAsync(ReportFormat.CSV);

    [RelayCommand]
    private Task ExportExcelAsync() => ExportAsync(ReportFormat.Excel);

    [RelayCommand]
    private Task ExportPdfAsync() => ExportAsync(ReportFormat.PDF);

    private async Task ExportAsync(ReportFormat format)
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            if (Preview.CurrentResult == null)
            {
                await RefreshPreviewAsync();
            }

            if (Preview.CurrentResult == null)
            {
                ErrorMessage = "Report preview could not be generated.";
                return;
            }

            var result = await _reportExportService.ExportAsync(
                Preview.CurrentResult,
                new ReportExportRequest { Format = format },
                _authenticationService.CurrentUser?.Id);
            if (result.Succeeded)
            {
                StatusMessage = result.Message;
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? result.Message;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"{format} export failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RebuildReportGroups()
    {
        var matching = _allReports
            .Where(report =>
                string.IsNullOrWhiteSpace(ReportSearchText) ||
                report.Title.Contains(ReportSearchText, StringComparison.OrdinalIgnoreCase) ||
                report.Description.Contains(ReportSearchText, StringComparison.OrdinalIgnoreCase))
            .ToList();
        VisibleReportCount = matching.Count;
        ReportGroups.Clear();

        foreach (var category in CategoryOrder)
        {
            var reports = matching
                .Where(report => report.Category == category)
                .OrderBy(report => report.Title)
                .ToList();
            if (reports.Count > 0)
            {
                ReportGroups.Add(new ReportCategoryGroup(category, reports));
            }
        }
    }

    private void BuildFilterInputs()
    {
        FilterInputs.Clear();
        if (SelectedReport == null)
        {
            return;
        }

        foreach (var filter in SelectedReport.Filters)
        {
            FilterInputs.Add(new ReportFilterInputViewModel(filter));
        }
    }

    private bool TryBuildFilters(
        out IReadOnlyCollection<ReportFilter> filters,
        out string validationError)
    {
        var values = new List<ReportFilter>();
        foreach (var input in FilterInputs)
        {
            if (!input.TryCreateFilter(out var filter, out validationError))
            {
                filters = [];
                return false;
            }
            values.Add(filter);
        }

        filters = values;
        validationError = string.Empty;
        return true;
    }
}

public sealed class ReportCategoryGroup(
    string name,
    IEnumerable<ReportDefinition> reports)
{
    public string Name { get; } = name;
    public IReadOnlyList<ReportDefinition> Reports { get; } = reports.ToList();
}

public partial class ReportFilterInputViewModel : ObservableObject
{
    public ReportFilterInputViewModel(ReportFilter definition)
    {
        Definition = definition;
        Options = ["", .. definition.Options];
    }

    public ReportFilter Definition { get; }
    public string Label => Definition.Label;
    public IReadOnlyList<string> Options { get; }
    public bool IsText => Definition.Type == ReportFilterType.Text;
    public bool IsDateRange => Definition.Type == ReportFilterType.DateRange;
    public bool IsEnum => Definition.Type == ReportFilterType.Enum;
    public bool IsNumberRange => Definition.Type == ReportFilterType.NumberRange;
    public bool IsBoolean => Definition.Type == ReportFilterType.Boolean;

    [ObservableProperty] private string _textValue = string.Empty;
    [ObservableProperty] private string _secondaryTextValue = string.Empty;
    [ObservableProperty] private string _selectedOption = string.Empty;
    [ObservableProperty] private DateTime? _fromDate;
    [ObservableProperty] private DateTime? _toDate;
    [ObservableProperty] private bool _booleanValue;

    public bool TryCreateFilter(out ReportFilter filter, out string error)
    {
        object? value = null;
        object? secondaryValue = null;

        switch (Definition.Type)
        {
            case ReportFilterType.Text:
                value = TextValue;
                break;
            case ReportFilterType.Enum:
                value = SelectedOption;
                break;
            case ReportFilterType.Boolean:
                value = BooleanValue;
                break;
            case ReportFilterType.DateRange:
                if (FromDate.HasValue && ToDate.HasValue && FromDate.Value.Date > ToDate.Value.Date)
                {
                    filter = new ReportFilter();
                    error = $"{Label}: the start date cannot be after the end date.";
                    return false;
                }
                value = FromDate;
                secondaryValue = ToDate;
                break;
            case ReportFilterType.NumberRange:
                if (!TryParseOptionalDecimal(TextValue, out var minimum) ||
                    !TryParseOptionalDecimal(SecondaryTextValue, out var maximum))
                {
                    filter = new ReportFilter();
                    error = $"{Label}: enter valid numeric values.";
                    return false;
                }
                if (minimum.HasValue && maximum.HasValue && minimum > maximum)
                {
                    filter = new ReportFilter();
                    error = $"{Label}: the minimum cannot exceed the maximum.";
                    return false;
                }
                value = minimum;
                secondaryValue = maximum;
                break;
        }

        filter = new ReportFilter
        {
            Key = Definition.Key,
            Label = Definition.Label,
            Type = Definition.Type,
            Value = value,
            SecondaryValue = secondaryValue,
            Options = Definition.Options
        };
        error = string.Empty;
        return true;
    }

    public void Clear()
    {
        TextValue = string.Empty;
        SecondaryTextValue = string.Empty;
        SelectedOption = string.Empty;
        FromDate = null;
        ToDate = null;
        BooleanValue = false;
    }

    private static bool TryParseOptionalDecimal(string text, out decimal? value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = null;
            return true;
        }

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            value = parsed;
            return true;
        }

        value = null;
        return false;
    }
}
