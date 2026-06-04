using System.Windows;
using KicsitLibrary.Desktop.ViewModels;

namespace KicsitLibrary.Desktop.Views
{
    public partial class StudentFormWindow : Window
    {
        public StudentFormWindow(StudentFormViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
