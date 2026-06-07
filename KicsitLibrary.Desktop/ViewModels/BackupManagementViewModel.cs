using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Desktop.Services;

namespace KicsitLibrary.Desktop.ViewModels;

public partial class BackupManagementViewModel(
    IBackupService backupService,
    IAuthenticationService authenticationService,
    IBackupDialogService dialogService) : ObservableObject
{
    public IReadOnlyList<string> StatusOptions { get; } =
        ["", "InProgress", "Completed", "CompletedWithWarnings", "Failed"];
    public IReadOnlyList<string> VerificationOptions { get; } =
        ["", "Pending", "Passed", "Failed", "NotRequested"];

    [ObservableProperty] private ObservableCollection<BackupHistoryItem> _backupHistory = [];
    [ObservableProperty] private BackupHistoryItem? _selectedBackup;
    [ObservableProperty] private BackupStatusSummary _summary = new();
    [ObservableProperty] private string _destinationFolder = string.Empty;
    [ObservableProperty] private string _reason = string.Empty;
    [ObservableProperty] private bool _verifyAfterCreation = true;
    [ObservableProperty] private bool _compressBackup;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedStatus = string.Empty;
    [ObservableProperty] private string _selectedVerificationStatus = string.Empty;
    [ObservableProperty] private DateTime? _fromDate;
    [ObservableProperty] private DateTime? _toDate;
    [ObservableProperty] private string _createdBy = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var settings = await backupService.GetBackupSettingsAsync();
            if (string.IsNullOrWhiteSpace(DestinationFolder))
            {
                DestinationFolder = string.IsNullOrWhiteSpace(settings.DefaultFolder)
                    ? Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "KICSIT Library Backups")
                    : settings.DefaultFolder;
                VerifyAfterCreation = settings.VerifyAfterCreation;
                CompressBackup = settings.CompressionEnabled;
            }

            BackupHistory = new ObservableCollection<BackupHistoryItem>(
                await backupService.GetBackupHistoryAsync(new BackupHistoryFilter
                {
                    SearchText = SearchText,
                    Status = SelectedStatus,
                    VerificationStatus = SelectedVerificationStatus,
                    FromDate = FromDate,
                    ToDate = ToDate,
                    CreatedBy = CreatedBy
                }));
            Summary = await backupService.GetBackupStatusSummaryAsync();
            StatusMessage = $"{BackupHistory.Count} backup history record(s) loaded.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unable to load backup history: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        IsBusy = true;
        try
        {
            var user = authenticationService.CurrentUser;
            var result = await backupService.CreateBackupAsync(new BackupRequest
            {
                RequestedByUserId = user?.Id ?? 0,
                RequestedByUserName = user?.FullName ?? string.Empty,
                DestinationFolder = DestinationFolder,
                IncludeTimestamp = true,
                IncludeMetadataFile = true,
                VerifyAfterCreation = VerifyAfterCreation,
                CompressBackup = CompressBackup,
                Reason = Reason
            });
            StatusMessage = result.Succeeded
                ? $"{result.Message} {result.BackupFilePath}"
                : result.ErrorMessage ?? result.Message;
            if (result.Succeeded)
            {
                Reason = string.Empty;
            }
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Backup creation failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task VerifySelectedAsync()
    {
        if (SelectedBackup == null)
        {
            StatusMessage = "Select a backup first.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await backupService.VerifyBackupAsync(
                SelectedBackup.BackupFilePath,
                SelectedBackup.BackupHistoryId);
            StatusMessage = result.Succeeded
                ? result.Message
                : result.ErrorMessage ?? result.Message;
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Backup verification failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenBackupFolderAsync()
    {
        var folder = SelectedBackup == null
            ? DestinationFolder
            : Path.GetDirectoryName(SelectedBackup.BackupFilePath);
        var result = await backupService.OpenBackupFolderAsync(folder);
        StatusMessage = result.Succeeded
            ? result.Message
            : result.ErrorMessage ?? result.Message;
    }

    [RelayCommand]
    private async Task ViewDetailsAsync()
    {
        if (SelectedBackup == null)
        {
            StatusMessage = "Select a backup first.";
            return;
        }
        await dialogService.ShowBackupDetailsAsync(SelectedBackup);
    }

    [RelayCommand]
    private async Task ClearFiltersAsync()
    {
        SearchText = string.Empty;
        SelectedStatus = string.Empty;
        SelectedVerificationStatus = string.Empty;
        FromDate = null;
        ToDate = null;
        CreatedBy = string.Empty;
        await RefreshAsync();
    }
}
