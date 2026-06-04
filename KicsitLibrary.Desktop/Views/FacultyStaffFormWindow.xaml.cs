using System.Windows;
using KicsitLibrary.Desktop.ViewModels;

namespace KicsitLibrary.Desktop.Views
{
    public partial class FacultyStaffFormWindow : Window
    {
        public FacultyStaffFormWindow(FacultyStaffFormViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
