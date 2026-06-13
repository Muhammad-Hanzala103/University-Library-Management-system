using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Interfaces;

namespace KicsitLibrary.Desktop.ViewModels
{
    public partial class ForgotPasswordViewModel : ObservableObject
    {
        private readonly IAuthenticationService _authService;

        [ObservableProperty]
        private string usernameOrEmail = string.Empty;

        [ObservableProperty]
        private string resetCode = string.Empty;

        [ObservableProperty]
        private string newPassword = string.Empty;

        [ObservableProperty]
        private string confirmPassword = string.Empty;

        [ObservableProperty]
        private string message = string.Empty;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private bool isCodeSent;

        public Action? CloseAction { get; set; }

        public ForgotPasswordViewModel(IAuthenticationService authService)
        {
            _authService = authService;
        }

        [RelayCommand]
        private async Task RequestResetAsync()
        {
            if (string.IsNullOrWhiteSpace(UsernameOrEmail))
            {
                Message = "Please enter your username, email, or phone.";
                return;
            }

            IsBusy = true;
            Message = "Processing...";

            try
            {
                var result = await _authService.RequestPasswordResetAsync(UsernameOrEmail);
                
                IsCodeSent = true;
                Message = result.Message;
            }
            catch (Exception ex)
            {
                Message = $"An error occurred: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ResetPasswordAsync()
        {
            if (string.IsNullOrWhiteSpace(ResetCode))
            {
                Message = "Please enter the reset code.";
                return;
            }

            if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword != ConfirmPassword)
            {
                Message = "Passwords do not match.";
                return;
            }

            IsBusy = true;
            Message = "Processing...";

            try
            {
                var success = await _authService.ResetPasswordAsync(UsernameOrEmail, ResetCode, NewPassword);
                
                if (success)
                {
                    Message = "Password successfully reset.";
                    await Task.Delay(2000);
                    CloseAction?.Invoke();
                }
                else
                {
                    Message = "Invalid or expired reset code.";
                }
            }
            catch (Exception ex)
            {
                Message = $"An error occurred: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
