using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Interfaces;

namespace KicsitLibrary.Desktop.ViewModels
{
    public partial class BookCatalogViewModel : ObservableObject
    {
        private readonly ICatalogService _catalogService;
        private readonly IAuthenticationService _authService;

        [ObservableProperty]
        private ObservableCollection<BookMaster> _books = new();

        // Lists for filter dropdowns
        [ObservableProperty]
        private ObservableCollection<Author> _authors = new();

        [ObservableProperty]
        private ObservableCollection<Category> _categories = new();

        [ObservableProperty]
        private ObservableCollection<DepartmentCategory> _departmentCategories = new();

        [ObservableProperty]
        private ObservableCollection<Publisher> _publishers = new();

        // Search Filter properties
        [ObservableProperty]
        private string _searchTitle = string.Empty;

        [ObservableProperty]
        private int? _selectedAuthorId;

        [ObservableProperty]
        private int? _selectedCategoryId;

        [ObservableProperty]
        private int? _selectedDepartmentCategoryId;

        [ObservableProperty]
        private int? _selectedPublisherId;

        [ObservableProperty]
        private string _searchISBN = string.Empty;

        [ObservableProperty]
        private string _searchAccessionNumber = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        public BookCatalogViewModel(ICatalogService catalogService, IAuthenticationService authService)
        {
            _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            
            _ = InitializeFiltersAndSearchAsync();
        }

        private async Task InitializeFiltersAndSearchAsync()
        {
            IsBusy = true;
            try
            {
                // Load filters concurrently
                var authorsTask = _catalogService.GetAllAuthorsAsync();
                var categoriesTask = _catalogService.GetAllCategoriesAsync();
                var deptsTask = _catalogService.GetAllDepartmentCategoriesAsync();
                var publishersTask = _catalogService.GetAllPublishersAsync();

                await Task.WhenAll(authorsTask, categoriesTask, deptsTask, publishersTask);

                Authors = new ObservableCollection<Author>(authorsTask.Result);
                Categories = new ObservableCollection<Category>(categoriesTask.Result);
                DepartmentCategories = new ObservableCollection<DepartmentCategory>(deptsTask.Result);
                Publishers = new ObservableCollection<Publisher>(publishersTask.Result);

                // Run initial search
                await SearchAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to initialize catalog: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task RefreshDropdownsAsync()
        {
            try
            {
                var authorsList = await _catalogService.GetAllAuthorsAsync();
                var categoriesList = await _catalogService.GetAllCategoriesAsync();
                var deptsList = await _catalogService.GetAllDepartmentCategoriesAsync();
                var publishersList = await _catalogService.GetAllPublishersAsync();

                Authors = new ObservableCollection<Author>(authorsList);
                Categories = new ObservableCollection<Category>(categoriesList);
                DepartmentCategories = new ObservableCollection<DepartmentCategory>(deptsList);
                Publishers = new ObservableCollection<Publisher>(publishersList);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to refresh filters: {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task SearchAsync()
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                var results = await _catalogService.SearchBooksAsync(
                    string.IsNullOrWhiteSpace(SearchTitle) ? null : SearchTitle.Trim(),
                    SelectedAuthorId,
                    SelectedCategoryId,
                    SelectedDepartmentCategoryId,
                    SelectedPublisherId,
                    string.IsNullOrWhiteSpace(SearchISBN) ? null : SearchISBN.Trim(),
                    string.IsNullOrWhiteSpace(SearchAccessionNumber) ? null : SearchAccessionNumber.Trim()
                );

                Books.Clear();
                foreach (var book in results)
                {
                    Books.Add(book);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to search catalog: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ClearFiltersAsync()
        {
            SearchTitle = string.Empty;
            SelectedAuthorId = null;
            SelectedCategoryId = null;
            SelectedDepartmentCategoryId = null;
            SelectedPublisherId = null;
            SearchISBN = string.Empty;
            SearchAccessionNumber = string.Empty;

            await SearchAsync();
        }

        [RelayCommand]
        private void AddBook()
        {
            var vm = new BookFormViewModel(_catalogService, _authService, null);
            var window = new Views.BookFormWindow(vm);
            
            if (window.ShowDialog() == true)
            {
                _ = SearchAsync();
            }
        }

        [RelayCommand]
        private void EditBook(BookMaster? book)
        {
            if (book == null) return;

            var vm = new BookFormViewModel(_catalogService, _authService, book);
            var window = new Views.BookFormWindow(vm);

            if (window.ShowDialog() == true)
            {
                _ = SearchAsync();
            }
        }

        [RelayCommand]
        private async Task DeleteBookAsync(BookMaster? book)
        {
            if (book == null) return;

            var confirmResult = MessageBox.Show($"Are you sure you want to delete '{book.Title}'? This will soft-delete all physical copies and hold records.", 
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirmResult == MessageBoxResult.Yes)
            {
                IsBusy = true;
                try
                {
                    var userId = _authService.CurrentUser?.Id ?? 1;
                    await _catalogService.DeleteBookAsync(book.Id, "Deleted by user from UI catalog", userId);
                    await SearchAsync();
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"Failed to delete book: {ex.Message}";
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        [RelayCommand]
        private void ManageCopies(BookMaster? book)
        {
            if (book == null) return;

            var vm = new CopiesViewModel(_catalogService, _authService, book);
            var window = new Views.CopiesWindow(vm);
            window.ShowDialog();
            
            // Refresh counts
            _ = SearchAsync();
        }
    }
}
