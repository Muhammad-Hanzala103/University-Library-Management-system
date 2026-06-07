using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Desktop.Services;

namespace KicsitLibrary.Desktop.ViewModels;

public partial class ActivityLogsViewModel : ObservableObject
{
    private readonly IActivityLogBrowserService _service;
    private readonly IAuditDialogService _dialogService;

    [ObservableProperty] private ObservableCollection<ActivityLogListItem> _logs = [];
    [ObservableProperty] private ObservableCollection<string> _actions = [string.Empty];
    [ObservableProperty] private ObservableCollection<string> _entityNames = [string.Empty];
    [ObservableProperty] private ObservableCollection<ActivityLogUserOption> _users = [];
    [ObservableProperty] private ActivityLogListItem? _selectedLog;
    [ObservableProperty] private ActivityLogUserOption? _selectedUser;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedAction = string.Empty;
    [ObservableProperty] private string _selectedEntityName = string.Empty;
    [ObservableProperty] private string _entityId = string.Empty;
    [ObservableProperty] private DateTime? _fromDate;
    [ObservableProperty] private DateTime? _toDate;
    [ObservableProperty] private ActivityLogSummary _summary = new();
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public ActivityLogsViewModel(
        IActivityLogBrowserService service,
        IAuditDialogService dialogService)
    {
        _service = service;
        _dialogService = dialogService;
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            int.TryParse(EntityId, out var entityId);
            var filter = BuildFilter(entityId > 0 ? entityId : null);
            Logs = new ObservableCollection<ActivityLogListItem>(
                await _service.GetActivityLogsAsync(filter));
            Summary = await _service.GetActivityLogSummaryAsync();
            Actions = new ObservableCollection<string>(
                [string.Empty, .. await _service.GetDistinctActionsAsync()]);
            EntityNames = new ObservableCollection<string>(
                [string.Empty, .. await _service.GetDistinctEntityNamesAsync()]);
            Users = new ObservableCollection<ActivityLogUserOption>(
                await _service.GetDistinctUsersAsync());
            StatusMessage = $"{Logs.Count} activity log(s) loaded.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unable to load activity logs: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ClearFiltersAsync()
    {
        SearchText = string.Empty;
        SelectedAction = string.Empty;
        SelectedEntityName = string.Empty;
        EntityId = string.Empty;
        SelectedUser = null;
        FromDate = null;
        ToDate = null;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task OpenDetailsAsync()
    {
        if (SelectedLog == null)
        {
            StatusMessage = "Select an activity log first.";
            return;
        }
        await _dialogService.ShowActivityLogDetailsAsync(SelectedLog.ActivityLogId);
    }

    [RelayCommand]
    private Task ExportCsvAsync() => ExportAsync("CSV");

    [RelayCommand]
    private Task ExportExcelAsync() => ExportAsync("Excel");

    [RelayCommand]
    private Task ExportPdfAsync() => ExportAsync("PDF");

    private async Task ExportAsync(string format)
    {
        IsBusy = true;
        try
        {
            int.TryParse(EntityId, out var entityId);
            var result = await _service.ExportActivityLogSnapshotAsync(
                BuildFilter(entityId > 0 ? entityId : null),
                format);
            StatusMessage = result.Succeeded
                ? result.Message
                : result.ErrorMessage ?? result.Message;
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

    private ActivityLogFilter BuildFilter(int? entityId) =>
        new()
        {
            SearchText = SearchText,
            Action = SelectedAction,
            EntityName = SelectedEntityName,
            EntityId = entityId,
            UserId = SelectedUser?.UserId,
            FromDate = FromDate,
            ToDate = ToDate,
            Limit = 500
        };
}
