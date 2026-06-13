using System.Windows;
using KicsitLibrary.Desktop.ViewModels;

namespace KicsitLibrary.Desktop.Views
{
    public partial class CreateAccountWindow : Window
    {
        private readonly CreateAccountViewModel _viewModel;

        public CreateAccountWindow(CreateAccountViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;

            _viewModel.RequestCompleted += () =>
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

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
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

        private async void Submit_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.SubmitRequestAsync(PasswordInput.Password, ConfirmPasswordInput.Password);
        }
    }
}
