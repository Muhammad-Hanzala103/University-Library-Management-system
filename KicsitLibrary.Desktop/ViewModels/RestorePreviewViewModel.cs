using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Desktop.ViewModels;

public partial class RestorePreviewViewModel(
    IRestoreService restoreService,
    IAuthenticationService authenticationService) : ObservableObject
{
    [ObservableProperty] private RestorePreviewResult _preview = new();
    [ObservableProperty] private RestoreHistoryItem? _history;
    [ObservableProperty] private bool _isHistoryMode;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreCommand))]
    private bool _canManageRestore;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreCommand))]
    private bool _validationPassed;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreCommand))]
    private string _reason = string.Empty;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreCommand))]
    private string _confirmationText = string.Empty;
    [ObservableProperty] private bool _createSafetyBackup = true;
    [ObservableProperty] private bool _verifyBeforeRestore = true;
    [ObservableProperty] private bool _verifyAfterRestore = true;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public bool CanRestore =>
        !IsHistoryMode &&
        CanManageRestore &&
        !IsBusy &&
        ValidationPassed &&
        !string.IsNullOrWhiteSpace(Reason) &&
        string.Equals(ConfirmationText, "RESTORE", StringComparison.Ordinal) &&
        CreateSafetyBackup &&
        VerifyBeforeRestore;

    partial void OnIsBusyChanged(bool value) => RestoreCommand.NotifyCanExecuteChanged();
    partial void OnCreateSafetyBackupChanged(bool value) => RestoreCommand.NotifyCanExecuteChanged();
    partial void OnVerifyBeforeRestoreChanged(bool value) => RestoreCommand.NotifyCanExecuteChanged();

    public async Task LoadAsync(string backupFilePath)
    {
        IsHistoryMode = false;
        IsBusy = true;
        try
        {
            var user = authenticationService.CurrentUser;
            var roles = user?.UserRoles
                .Select(item => item.Role?.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
            CanManageRestore = user != null &&
                (roles.Contains("Super Admin") ||
                 roles.Contains("Admin") ||
                 await authenticationService.VerifyUserPermissionAsync(
                     user.Id,
                     "MANAGE_RESTORES"));
            Preview = await restoreService.PreviewRestoreAsync(backupFilePath);
            ValidationPassed = Preview.Succeeded && Preview.IntegrityCheckPassed;
            StatusMessage = Preview.Succeeded
                ? CanManageRestore
                    ? Preview.Message
                    : $"{Preview.Message} Your role can view restore information but cannot stage a restore."
                : Preview.ErrorMessage ?? Preview.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void LoadHistory(RestoreHistoryItem item)
    {
        IsHistoryMode = true;
        History = item;
        Preview = new RestorePreviewResult
        {
            BackupFilePath = item.BackupFilePath,
            ChecksumSha256 = item.ChecksumSha256,
            Message = $"Restore status: {item.Status}"
        };
        Reason = item.Reason;
        StatusMessage = string.IsNullOrWhiteSpace(item.ErrorMessage)
            ? item.Status
            : item.ErrorMessage;
    }

    [RelayCommand]
    private async Task ValidateAsync()
    {
        IsBusy = true;
        try
        {
            var result = await restoreService.ValidateBackupForRestoreAsync(
                Preview.BackupFilePath);
            ValidationPassed = result.Succeeded && result.IntegrityCheckPassed;
            if (result.Succeeded)
            {
                Preview.ChecksumSha256 = result.ChecksumSha256;
                Preview.BackupSizeBytes = result.FileSizeBytes;
                Preview.IntegrityCheckPassed = result.IntegrityCheckPassed;
                OnPropertyChanged(nameof(Preview));
            }
            StatusMessage = result.Succeeded
                ? result.ValidationMessage
                : result.ErrorMessage ?? result.ValidationMessage;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRestore))]
    private async Task RestoreAsync()
    {
        IsBusy = true;
        try
        {
            var user = authenticationService.CurrentUser;
            var result = await restoreService.RestoreFromBackupAsync(new RestoreRequest
            {
                BackupFilePath = Preview.BackupFilePath,
                RequestedByUserId = user?.Id ?? 0,
                RequestedByUserName = user?.FullName ?? string.Empty,
                Reason = Reason,
                CreateSafetyBackup = CreateSafetyBackup,
                VerifyBeforeRestore = VerifyBeforeRestore,
                VerifyAfterRestore = VerifyAfterRestore,
                RequireConfirmationText = true,
                ConfirmationText = ConfirmationText,
                AllowRestoreWhileAppRunning = false
            });
            StatusMessage = result.Succeeded
                ? $"{result.Message} Safety backup: {result.SafetyBackupFilePath}"
                : result.ErrorMessage ?? result.Message;
            if (result.Succeeded)
            {
                ValidationPassed = false;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
