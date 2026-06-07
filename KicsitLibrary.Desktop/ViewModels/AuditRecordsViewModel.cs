using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Desktop.Services;
using KicsitLibrary.Reports.Contracts;
using KicsitLibrary.Reports.Models;

namespace KicsitLibrary.Desktop.ViewModels;

public partial class AuditRecordsViewModel : ObservableObject
{
    private readonly IAuditRecordService _service;
    private readonly IAuditDialogService _dialogService;
    private readonly IReportService _reportService;
    private readonly IReportExportService _reportExportService;
    private readonly IAuthenticationService _authenticationService;

    public IReadOnlyList<string> AuditTypes { get; } =
        ["", "Internal Audit", "External Audit", "Financial Audit", "Stock Audit",
         "HEC Audit", "PEC Audit", "QEC Audit", "NCEAC Audit", "Other"];
    public IReadOnlyList<string> StatusOptions { get; } =
        ["", .. Enum.GetNames<AuditStatus>()];
    public IReadOnlyList<AuditStatus> StatusValues { get; } = Enum.GetValues<AuditStatus>();

    [ObservableProperty] private ObservableCollection<AuditRecordListItem> _auditRecords = [];
    [ObservableProperty] private AuditRecordListItem? _selectedAuditRecord;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedAuditType = string.Empty;
    [ObservableProperty] private string _selectedStatus = string.Empty;
    [ObservableProperty] private string _financialYear = string.Empty;
    [ObservableProperty] private DateTime? _fromDate;
    [ObservableProperty] private DateTime? _toDate;
    [ObservableProperty] private bool _pendingActionOnly;
    [ObservableProperty] private AuditStatus _newStatus = AuditStatus.Submitted;
    [ObservableProperty] private string _actionRemarks = string.Empty;
    [ObservableProperty] private AuditStatusSummary _summary = new();
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public AuditRecordsViewModel(
        IAuditRecordService service,
        IAuditDialogService dialogService,
        IReportService reportService,
        IReportExportService reportExportService,
        IAuthenticationService authenticationService)
    {
        _service = service;
        _dialogService = dialogService;
        _reportService = reportService;
        _reportExportService = reportExportService;
        _authenticationService = authenticationService;
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            AuditStatus? status = Enum.TryParse<AuditStatus>(SelectedStatus, out var parsed)
                ? parsed : null;
            AuditRecords = new ObservableCollection<AuditRecordListItem>(
                await _service.GetAuditRecordsAsync(new AuditRecordFilter
                {
                    SearchText = SearchText,
                    AuditType = SelectedAuditType,
                    Status = status,
                    FinancialYear = FinancialYear,
                    FromDate = FromDate,
                    ToDate = ToDate,
                    PendingActionOnly = PendingActionOnly
                }));
            Summary = await _service.GetAuditStatusSummaryAsync();
            StatusMessage = $"{AuditRecords.Count} audit record(s) loaded.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unable to load audit records: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (await _dialogService.ShowAuditFormAsync())
            await RefreshAsync();
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        if (SelectedAuditRecord == null)
        {
            StatusMessage = "Select an audit record first.";
            return;
        }
        if (await _dialogService.ShowAuditFormAsync(SelectedAuditRecord.AuditRecordId))
            await RefreshAsync();
    }

    [RelayCommand]
    private async Task ViewDetailsAsync()
    {
        if (SelectedAuditRecord == null)
        {
            StatusMessage = "Select an audit record first.";
            return;
        }
        await _dialogService.ShowAuditDetailsAsync(SelectedAuditRecord.AuditRecordId);
    }

    [RelayCommand]
    private async Task ChangeStatusAsync()
    {
        if (SelectedAuditRecord == null)
        {
            StatusMessage = "Select an audit record first.";
            return;
        }
        var result = await _service.ChangeAuditStatusAsync(
            SelectedAuditRecord.AuditRecordId,
            NewStatus,
            ActionRemarks);
        StatusMessage = result.Succeeded ? result.Message : result.ErrorMessage ?? result.Message;
        if (result.Succeeded)
            await RefreshAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedAuditRecord == null)
        {
            StatusMessage = "Select an audit record first.";
            return;
        }
        var result = await _service.DeleteAuditRecordAsync(
            SelectedAuditRecord.AuditRecordId,
            ActionRemarks);
        StatusMessage = result.Succeeded ? result.Message : result.ErrorMessage ?? result.Message;
        if (result.Succeeded)
            await RefreshAsync();
    }

    [RelayCommand]
    private async Task ClearFiltersAsync()
    {
        SearchText = string.Empty;
        SelectedAuditType = string.Empty;
        SelectedStatus = string.Empty;
        FinancialYear = string.Empty;
        FromDate = null;
        ToDate = null;
        PendingActionOnly = false;
        await RefreshAsync();
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
        try
        {
            var filters = new List<ReportFilter>
            {
                Filter("SearchText", "Search Text", ReportFilterType.Text, SearchText),
                Filter("AuditType", "Audit Type", ReportFilterType.Text, SelectedAuditType),
                Filter("Status", "Status", ReportFilterType.Enum, SelectedStatus),
                Filter("FinancialYear", "Financial Year", ReportFilterType.Text, FinancialYear),
                new()
                {
                    Key = "DateRange",
                    Label = "Audit Date",
                    Type = ReportFilterType.DateRange,
                    Value = FromDate,
                    SecondaryValue = ToDate
                },
                Filter("PendingActionOnly", "Pending Action Only", ReportFilterType.Boolean, PendingActionOnly)
            };
            var report = await _reportService.GenerateAsync(
                "audit",
                filters,
                _authenticationService.CurrentUser?.FullName ?? "Unknown User");
            var result = await _reportExportService.ExportAsync(
                report,
                new ReportExportRequest { Format = format },
                _authenticationService.CurrentUser?.Id);
            StatusMessage = result.Succeeded ? result.Message : result.ErrorMessage ?? result.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = $"{format} export failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static ReportFilter Filter(
        string key,
        string label,
        ReportFilterType type,
        object? value) =>
        new() { Key = key, Label = label, Type = type, Value = value };
}
