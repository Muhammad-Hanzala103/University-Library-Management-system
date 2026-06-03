using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Interfaces;

namespace KicsitLibrary.Desktop.ViewModels
{
    public partial class AuthorViewModel : ObservableObject
    {
        private readonly ICatalogService _catalogService;
        private readonly IAuthenticationService _authService;

        [ObservableProperty]
        private ObservableCollection<Author> _authors = new();

        [ObservableProperty]
        private Author? _selectedAuthor;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _alternateName = string.Empty;

        [ObservableProperty]
        private string _biography = string.Empty;

        [ObservableProperty]
        private string _language = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private bool _isEditMode;

        public AuthorViewModel(ICatalogService catalogService, IAuthenticationService authService)
        {
            _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _ = LoadAuthorsAsync();
        }

        public async Task LoadAuthorsAsync()
        {
            IsBusy = true;
            try
            {
                var authorsList = await _catalogService.GetAllAuthorsAsync();
                Authors.Clear();
                foreach (var author in authorsList)
                {
                    Authors.Add(author);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load authors: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void SelectAuthorForEdit(Author? author)
        {
            if (author == null) return;
            
            SelectedAuthor = author;
            Name = author.Name;
            AlternateName = author.AlternateName ?? string.Empty;
            Biography = author.Biography ?? string.Empty;
            Language = author.Language ?? string.Empty;
            IsEditMode = true;
            ErrorMessage = string.Empty;
        }

        [RelayCommand]
        private void ClearForm()
        {
            SelectedAuthor = null;
            Name = string.Empty;
            AlternateName = string.Empty;
            Biography = string.Empty;
            Language = string.Empty;
            IsEditMode = false;
            ErrorMessage = string.Empty;
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                ErrorMessage = "Author name is required.";
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                if (IsEditMode && SelectedAuthor != null)
                {
                    SelectedAuthor.Name = Name.Trim();
                    SelectedAuthor.AlternateName = string.IsNullOrWhiteSpace(AlternateName) ? null : AlternateName.Trim();
                    SelectedAuthor.Biography = string.IsNullOrWhiteSpace(Biography) ? null : Biography.Trim();
                    SelectedAuthor.Language = string.IsNullOrWhiteSpace(Language) ? null : Language.Trim();

                    await _catalogService.UpdateAuthorAsync(SelectedAuthor);
                }
                else
                {
                    var newAuthor = new Author
                    {
                        Name = Name.Trim(),
                        AlternateName = string.IsNullOrWhiteSpace(AlternateName) ? null : AlternateName.Trim(),
                        Biography = string.IsNullOrWhiteSpace(Biography) ? null : Biography.Trim(),
                        Language = string.IsNullOrWhiteSpace(Language) ? null : Language.Trim(),
                        ActiveStatus = true
                    };

                    await _catalogService.AddAuthorAsync(newAuthor);
                }

                ClearForm();
                await LoadAuthorsAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to save author: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task DeleteAsync(Author? author)
        {
            if (author == null) return;

            var currentUserId = _authService.CurrentUser?.Id ?? 1;

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                await _catalogService.DeleteAuthorAsync(author.Id, "Deleted via admin portal", currentUserId);
                ClearForm();
                await LoadAuthorsAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to delete author: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
