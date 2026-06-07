using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Desktop.Services;

namespace KicsitLibrary.Desktop.ViewModels;

public partial class BackupManagementViewModel(
    IBackupService backupService,
    IAutomaticBackupSchedulerService automaticBackupSchedulerService,
    IAuthenticationService authenticationService,
    IBackupDialogService dialogService,
    IRestoreDialogService restoreDialogService) : ObservableObject
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
    [ObservableProperty] private bool _automaticBackupEnabled;
    [ObservableProperty] private bool _automaticBackupRunOnStartup;
    [ObservableProperty] private int _automaticBackupIntervalHours = 24;
    [ObservableProperty] private int _automaticBackupInitialDelaySeconds = 60;
    [ObservableProperty] private bool _automaticBackupCompress;
    [ObservableProperty] private bool _automaticBackupVerifyAfterCreation = true;
    [ObservableProperty] private string _automaticBackupDestinationFolder = string.Empty;
    [ObservableProperty] private bool _automaticBackupRetentionEnabled;
    [ObservableProperty] private int _automaticBackupRetentionDays = 30;
    [ObservableProperty] private int _automaticBackupMaxHistoryRows = 500;
    [ObservableProperty] private bool _automaticBackupDeletePhysicalFiles;
    [ObservableProperty] private DateTime? _automaticBackupLastRunAt;
    [ObservableProperty] private DateTime? _automaticBackupLastSuccessAt;
    [ObservableProperty] private DateTime? _automaticBackupLastFailureAt;
    [ObservableProperty] private string _automaticBackupLastMessage = string.Empty;
    [ObservableProperty] private bool _automaticBackupIsRunning;
    [ObservableProperty] private bool _canManageAutomaticBackups;
    [ObservableProperty] private ObservableCollection<BackupRetentionCandidate> _retentionCandidates = [];
    [ObservableProperty] private int _retentionCandidateCount;
    [ObservableProperty] private long _retentionCandidateBytes;

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
                        ProductBrand.BackupFolderName)
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
            await LoadAutomaticBackupAsync();
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
    private async Task RestoreSelectedAsync()
    {
        if (SelectedBackup == null)
        {
            StatusMessage = "Select a backup first.";
            return;
        }

        await restoreDialogService.ShowRestorePreviewAsync(SelectedBackup.BackupFilePath);
        StatusMessage = "Restore preview closed. Refresh Restore to view any staged attempt.";
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

    [RelayCommand]
    private async Task RefreshSchedulerStatusAsync()
    {
        IsBusy = true;
        try
        {
            await LoadAutomaticBackupAsync();
            StatusMessage = "Automatic backup settings and status refreshed.";
        }
        catch (Exception ex)
        {
            StatusMessage =
                $"Unable to refresh automatic backup status: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveSchedulerSettingsAsync()
    {
        if (!CanManageAutomaticBackups)
        {
            StatusMessage =
                "The current user cannot update automatic backup settings.";
            return;
        }

        IsBusy = true;
        try
        {
            var saved =
                await automaticBackupSchedulerService.UpdateSchedulerSettingsAsync(
                    CreateAutomaticSettings());
            ApplyAutomaticSettings(saved);
            StatusMessage = "Automatic backup settings saved.";
            await LoadAutomaticStatusAsync();
        }
        catch (Exception ex)
        {
            StatusMessage =
                $"Unable to save automatic backup settings: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RunAutomaticBackupNowAsync()
    {
        if (!CanManageAutomaticBackups)
        {
            StatusMessage =
                "The current user cannot run automatic backups.";
            return;
        }

        IsBusy = true;
        try
        {
            var result =
                await automaticBackupSchedulerService.RunBackupNowAsync();
            StatusMessage = result.Succeeded
                ? $"{result.Message} {result.BackupFilePath}"
                : result.ErrorMessage ?? result.Message;
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Automatic backup failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PreviewRetentionAsync()
    {
        IsBusy = true;
        try
        {
            var result =
                await automaticBackupSchedulerService.PreviewRetentionAsync();
            RetentionCandidates =
                new ObservableCollection<BackupRetentionCandidate>(
                    result.Candidates);
            RetentionCandidateCount = result.CandidateCount;
            RetentionCandidateBytes = result.TotalSizeBytes;
            StatusMessage = result.Succeeded
                ? result.Message
                : result.ErrorMessage ?? result.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Retention preview failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ApplyRetentionAsync()
    {
        if (!CanManageAutomaticBackups)
        {
            StatusMessage =
                "The current user cannot apply backup retention.";
            return;
        }
        if (!AutomaticBackupRetentionEnabled)
        {
            StatusMessage = "Enable and save retention before applying it.";
            return;
        }

        if (RetentionCandidates.Count == 0)
        {
            await PreviewRetentionAsync();
        }
        if (AutomaticBackupDeletePhysicalFiles &&
            !dialogService.ConfirmPhysicalRetentionDeletion(
                RetentionCandidateCount,
                RetentionCandidateBytes))
        {
            StatusMessage = "Physical backup deletion was cancelled.";
            return;
        }

        IsBusy = true;
        try
        {
            var result =
                await automaticBackupSchedulerService.ApplyRetentionAsync();
            StatusMessage = result.Succeeded
                ? result.Message
                : result.ErrorMessage ?? result.Message;
            await RefreshAsync();
            RetentionCandidates = [];
            RetentionCandidateCount = 0;
            RetentionCandidateBytes = 0;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Retention failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenAutomaticBackupFolderAsync()
    {
        var result = await backupService.OpenBackupFolderAsync(
            AutomaticBackupDestinationFolder);
        StatusMessage = result.Succeeded
            ? result.Message
            : result.ErrorMessage ?? result.Message;
    }

    private async Task LoadAutomaticBackupAsync()
    {
        var settings =
            await automaticBackupSchedulerService.GetSchedulerSettingsAsync();
        ApplyAutomaticSettings(settings);
        await LoadAutomaticStatusAsync();
    }

    private async Task LoadAutomaticStatusAsync()
    {
        var status =
            await automaticBackupSchedulerService.GetSchedulerStatusAsync();
        AutomaticBackupLastRunAt = status.LastRunAt;
        AutomaticBackupLastSuccessAt = status.LastSuccessAt;
        AutomaticBackupLastFailureAt = status.LastFailureAt;
        AutomaticBackupLastMessage = status.LastMessage;
        AutomaticBackupIsRunning = status.IsRunning;
        CanManageAutomaticBackups = status.CanManage;
    }

    private AutomaticBackupSchedulerSettings CreateAutomaticSettings() =>
        new()
        {
            Enabled = AutomaticBackupEnabled,
            RunOnStartup = AutomaticBackupRunOnStartup,
            IntervalHours = AutomaticBackupIntervalHours,
            InitialDelaySeconds =
                AutomaticBackupInitialDelaySeconds,
            Compress = AutomaticBackupCompress,
            VerifyAfterCreation =
                AutomaticBackupVerifyAfterCreation,
            DestinationFolder =
                AutomaticBackupDestinationFolder,
            RetentionEnabled =
                AutomaticBackupRetentionEnabled,
            RetentionDays =
                AutomaticBackupRetentionDays,
            MaxHistoryRows =
                AutomaticBackupMaxHistoryRows,
            DeletePhysicalFiles =
                AutomaticBackupDeletePhysicalFiles
        };

    private void ApplyAutomaticSettings(
        AutomaticBackupSchedulerSettings settings)
    {
        AutomaticBackupEnabled = settings.Enabled;
        AutomaticBackupRunOnStartup = settings.RunOnStartup;
        AutomaticBackupIntervalHours = settings.IntervalHours;
        AutomaticBackupInitialDelaySeconds =
            settings.InitialDelaySeconds;
        AutomaticBackupCompress = settings.Compress;
        AutomaticBackupVerifyAfterCreation =
            settings.VerifyAfterCreation;
        AutomaticBackupDestinationFolder =
            settings.DestinationFolder;
        AutomaticBackupRetentionEnabled =
            settings.RetentionEnabled;
        AutomaticBackupRetentionDays =
            settings.RetentionDays;
        AutomaticBackupMaxHistoryRows =
            settings.MaxHistoryRows;
        AutomaticBackupDeletePhysicalFiles =
            settings.DeletePhysicalFiles;
    }
}
