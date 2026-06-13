using System.Windows;
using KicsitLibrary.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace KicsitLibrary.Desktop
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        public LoginWindow(LoginViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            viewModel.LoginSuccess += () =>
            {
                DialogResult = true;
                Close();
            };
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Do not drag when clicking inside textboxes/passwordboxes
            if (e.OriginalSource is System.Windows.FrameworkElement fe && 
                (fe is System.Windows.Controls.TextBox || fe is System.Windows.Controls.PasswordBox || fe is System.Windows.Controls.Button || fe is System.Windows.Controls.ComboBox))
            {
                return;
            }

            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void ForgotPassword_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = App.AppHost?.Services.GetRequiredService<ViewModels.ForgotPasswordViewModel>();
            if (viewModel != null)
            {
                var forgotPasswordWindow = new Views.ForgotPasswordWindow(viewModel)
                {
                    Owner = this
                };
                forgotPasswordWindow.ShowDialog();
            }
        }
        private void CreateAccount_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = App.AppHost?.Services.GetRequiredService<ViewModels.CreateAccountViewModel>();
            if (viewModel != null)
            {
                var createAccountWindow = new Views.CreateAccountWindow(viewModel)
                {
                    Owner = this
                };
                createAccountWindow.ShowDialog();
            }
        }
    }
}
