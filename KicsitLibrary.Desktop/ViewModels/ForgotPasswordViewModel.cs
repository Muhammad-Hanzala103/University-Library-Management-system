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
        private string message = string.Empty;

        [ObservableProperty]
        private bool isBusy;

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
                Message = "Please enter your username or email.";
                return;
            }

            IsBusy = true;
            Message = "Processing...";

            try
            {
                // This will safely return true even if the user doesn't exist to prevent enumeration.
                await _authService.RequestPasswordResetAsync(UsernameOrEmail);
                Message = "If an account with that email exists, reset instructions have been sent.";
                
                await Task.Delay(3000);
                CloseAction?.Invoke();
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
