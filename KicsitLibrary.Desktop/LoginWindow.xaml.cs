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

        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
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
    }
}
