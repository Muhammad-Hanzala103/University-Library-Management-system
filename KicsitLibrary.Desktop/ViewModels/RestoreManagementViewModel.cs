using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Desktop.Services;

namespace KicsitLibrary.Desktop.ViewModels;

public partial class RestoreManagementViewModel(
    IRestoreService restoreService,
    IRestoreDialogService dialogService) : ObservableObject
{
    public IReadOnlyList<string> StatusOptions { get; } =
        ["", "Started", "PendingRestart", "Completed", "Failed", "RolledBack", "CriticalFailure"];

    [ObservableProperty] private ObservableCollection<RestoreHistoryItem> _restoreHistory = [];
    [ObservableProperty] private RestoreHistoryItem? _selectedRestore;
    [ObservableProperty] private RestoreStatusSummary _summary = new();
    [ObservableProperty] private string _backupFilePath = string.Empty;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedStatus = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            RestoreHistory = new ObservableCollection<RestoreHistoryItem>(
                await restoreService.GetRestoreHistoryAsync(new RestoreHistoryFilter
                {
                    SearchText = SearchText,
                    Status = SelectedStatus
                }));
            Summary = await restoreService.GetRestoreStatusSummaryAsync();
            StatusMessage = $"{RestoreHistory.Count} restore history record(s) loaded.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unable to load restore history: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PreviewBackupAsync()
    {
        if (string.IsNullOrWhiteSpace(BackupFilePath))
        {
            StatusMessage = "Enter a backup file path first.";
            return;
        }
        await dialogService.ShowRestorePreviewAsync(BackupFilePath);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task ValidateBackupAsync()
    {
        IsBusy = true;
        try
        {
            var result = await restoreService.ValidateBackupForRestoreAsync(BackupFilePath);
            StatusMessage = result.Succeeded
                ? $"{result.ValidationMessage} SHA-256: {result.ChecksumSha256}"
                : result.ErrorMessage ?? result.ValidationMessage;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Validation failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ViewHistoryDetailsAsync()
    {
        if (SelectedRestore == null)
        {
            StatusMessage = "Select a restore history row first.";
            return;
        }
        await dialogService.ShowRestoreHistoryDetailsAsync(SelectedRestore);
    }
}
