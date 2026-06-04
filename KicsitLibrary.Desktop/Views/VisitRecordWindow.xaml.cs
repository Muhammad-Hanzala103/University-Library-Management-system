using System.Windows;
using KicsitLibrary.Desktop.ViewModels;

namespace KicsitLibrary.Desktop.Views
{
    public partial class VisitRecordWindow : Window
    {
        public VisitRecordWindow(VisitRecordFormViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
