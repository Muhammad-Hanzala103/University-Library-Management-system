using System.Windows;
using KicsitLibrary.Desktop.ViewModels;

namespace KicsitLibrary.Desktop.Views
{
    public partial class ChangePasswordWindow : Window
    {
        public ChangePasswordWindow(ChangePasswordViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.Close += (s, result) =>
            {
                if (result)
                    DialogResult = true;
                Close();
            };
        }
    }
}
