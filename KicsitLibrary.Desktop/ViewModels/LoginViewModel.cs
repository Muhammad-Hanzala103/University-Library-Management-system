using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Interfaces;

namespace KicsitLibrary.Desktop.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly IAuthenticationService _authService;

        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        public event Action? LoginSuccess;

        public LoginViewModel(IAuthenticationService authService)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        [RelayCommand]
        private async Task LoginAsync(object? parameter)
        {
            var passwordBox = parameter as PasswordBox;
            var password = passwordBox?.Password ?? string.Empty;

            ErrorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Username))
            {
                ErrorMessage = "Username is required.";
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ErrorMessage = "Password is required.";
                return;
            }

            IsBusy = true;
            try
            {
                var user = await _authService.LoginAsync(Username, password);
                if (user != null)
                {
                    LoginSuccess?.Invoke();
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                ErrorMessage = ex.Message;
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
        private async Task MicrosoftLoginAsync()
        {
            ErrorMessage = string.Empty;
            IsBusy = true;
            await Task.Delay(1000);
            ErrorMessage = "Microsoft OAuth provider is not configured for this environment. Please sign in with your local account or contact the IT administrator.";
            IsBusy = false;
        }

        [RelayCommand]
        private async Task GoogleLoginAsync()
        {
            ErrorMessage = string.Empty;
            IsBusy = true;
            await Task.Delay(1000);
            ErrorMessage = "Google OAuth provider is not configured for this environment. Please sign in with your local account or contact the IT administrator.";
            IsBusy = false;
        }
    }
}