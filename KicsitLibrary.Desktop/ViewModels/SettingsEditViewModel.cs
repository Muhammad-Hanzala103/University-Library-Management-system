using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Desktop.ViewModels
{
    public partial class SettingsEditViewModel : ObservableObject
    {
        private readonly ISettingsManagementService _settingsService;
        private readonly IAuthenticationService _authService;

        [ObservableProperty]
        private string key = string.Empty;

        [ObservableProperty]
        private string displayName = string.Empty;

        [ObservableProperty]
        private string category = string.Empty;

        [ObservableProperty]
        private string? description;

        [ObservableProperty]
        private string currentValue = string.Empty;

        [ObservableProperty]
        private string currentValueMasked = string.Empty;

        [ObservableProperty]
        private string newValue = string.Empty;

        [ObservableProperty]
        private string? reason;

        [ObservableProperty]
        private string dataType = "String";

        [ObservableProperty]
        private bool isSensitive;

        [ObservableProperty]
        private bool requiresRestart;

        [ObservableProperty]
        private string? validationMessage;

        [ObservableProperty]
        private bool isValidationError;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string? statusMessage;

        [ObservableProperty]
        private string defaultValue = string.Empty;

        public SettingsEditViewModel(
            ISettingsManagementService settingsService,
            IAuthenticationService authService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        public void LoadSetting(SettingsItemView setting)
        {
            Key = setting.Key;
            DisplayName = setting.DisplayName;
            Category = setting.Category;
            Description = setting.Description;
            CurrentValue = setting.Value;
            CurrentValueMasked = setting.MaskedValue;
            NewValue = setting.Value;
            DataType = setting.DataType;
            IsSensitive = setting.IsSensitive;
            RequiresRestart = setting.RequiresRestart;
            DefaultValue = setting.DefaultValue;
            ValidationMessage = null;
            IsValidationError = false;
            StatusMessage = null;
        }

        [RelayCommand]
        private async Task ValidateAsync()
        {
            try
            {
                IsBusy = true;
                var result = await _settingsService.ValidateSettingAsync(Key, NewValue);

                if (!result.IsValid)
                {
                    ValidationMessage = result.ErrorMessage;
                    IsValidationError = true;
                }
                else
                {
                    ValidationMessage = result.WarningMessage ?? "Value is valid";
                    IsValidationError = false;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void ResetToDefault()
        {
            NewValue = DefaultValue;
            ValidationMessage = null;
            IsValidationError = false;
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            try
            {
                IsBusy = true;
                StatusMessage = "Updating setting...";

                var result = await _settingsService.UpdateSettingAsync(
                    new SettingsUpdateRequest
                    {
                        Key = Key,
                        NewValue = NewValue,
                        RequestedByUserId = _authService.CurrentUser?.Id,
                        Reason = Reason
                    });

                if (result.Succeeded)
                {
                    StatusMessage = result.Message;
                    SettingUpdated?.Invoke(this, EventArgs.Empty);
                    await Task.Delay(1000); // Show success message briefly
                    Close?.Invoke(this, true);
                }
                else
                {
                    ValidationMessage = result.ErrorMessage;
                    IsValidationError = true;
                    StatusMessage = null;
                }
            }
            catch (Exception ex)
            {
                ValidationMessage = $"Error: {ex.Message}";
                IsValidationError = true;
                StatusMessage = null;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            Close?.Invoke(this, false);
        }

        public event EventHandler? SettingUpdated;
        public event EventHandler<bool>? Close;
    }
}
