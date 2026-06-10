using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using KicsitLibrary.Desktop.ViewModels;

namespace KicsitLibrary.Desktop.Views
{
    public partial class BookCatalogView : UserControl
    {
        public BookCatalogView()
        {
            InitializeComponent();
        }

        private async void ManageAuthors_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = App.AppHost?.Services.GetRequiredService<AuthorViewModel>();
            if (viewModel != null)
            {
                var window = new AuthorWindow(viewModel)
                {
                    Owner = Window.GetWindow(this)
                };
                window.ShowDialog();
                if (DataContext is BookCatalogViewModel catalogVm)
                {
                    await catalogVm.RefreshDropdownsAsync();
                }
            }
        }

        private async void ManagePublishers_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = App.AppHost?.Services.GetRequiredService<PublisherViewModel>();
            if (viewModel != null)
            {
                var window = new PublisherWindow(viewModel)
                {
                    Owner = Window.GetWindow(this)
                };
                window.ShowDialog();
                if (DataContext is BookCatalogViewModel catalogVm)
                {
                    await catalogVm.RefreshDropdownsAsync();
                }
            }
        }

        private async void ManageDepartments_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = App.AppHost?.Services.GetRequiredService<DepartmentViewModel>();
            if (viewModel != null)
            {
                var window = new DepartmentWindow(viewModel)
                {
                    Owner = Window.GetWindow(this)
                };
                window.ShowDialog();
                if (DataContext is BookCatalogViewModel catalogVm)
                {
                    await catalogVm.RefreshDropdownsAsync();
                }
            }
        }
    }
}
