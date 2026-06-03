using System.Windows;
using KicsitLibrary.Desktop.ViewModels;

namespace KicsitLibrary.Desktop.Views
{
    public partial class BookFormWindow : Window
    {
        public BookFormWindow(BookFormViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            viewModel.CloseRequest += (result) =>
            {
                DialogResult = result;
                Close();
            };
        }
    }
}
