using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Interfaces;

namespace KicsitLibrary.Desktop.ViewModels
{
    public partial class ChangePasswordViewModel : ObservableObject
    {
        private readonly IAuthenticationService _authService;

        [ObservableProperty]
        private string _currentPassword = string.Empty;

        [ObservableProperty]
        private string _newPassword = string.Empty;

        [ObservableProperty]
        private string _confirmPassword = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private string _successMessage = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        public event EventHandler<bool>? Close;

        public ChangePasswordViewModel(IAuthenticationService authService)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        [RelayCommand]
        private async Task ChangePasswordAsync()
        {
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(CurrentPassword))
            {
                ErrorMessage = "Current password is required.";
                return;
            }

            if (string.IsNullOrWhiteSpace(NewPassword))
            {
                ErrorMessage = "New password is required.";
                return;
            }

            if (NewPassword.Length < 6)
            {
                ErrorMessage = "New password must be at least 6 characters long.";
                return;
            }

            if (NewPassword != ConfirmPassword)
            {
                ErrorMessage = "New password and confirmation do not match.";
                return;
            }

            if (_authService.CurrentUser == null)
            {
                ErrorMessage = "No active user session found.";
                return;
            }

            IsBusy = true;
            try
            {
                bool success = await _authService.ChangePasswordAsync(_authService.CurrentUser.Id, CurrentPassword, NewPassword);
                if (success)
                    SuccessMessage = "Password changed successfully!";
                else
                    ErrorMessage = "Failed to change password. Please verify your current password.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"An error occurred: {ex.Message}";
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
    }
}
