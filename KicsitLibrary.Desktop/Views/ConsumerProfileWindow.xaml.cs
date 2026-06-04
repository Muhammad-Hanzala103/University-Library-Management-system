using System.Windows;
using KicsitLibrary.Desktop.ViewModels;

namespace KicsitLibrary.Desktop.Views
{
    public partial class ConsumerProfileWindow : Window
    {
        public ConsumerProfileWindow(ConsumerProfileViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
