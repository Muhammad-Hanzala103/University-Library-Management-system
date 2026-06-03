using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Interfaces;

namespace KicsitLibrary.Desktop.ViewModels
{
    public partial class PublisherViewModel : ObservableObject
    {
        private readonly ICatalogService _catalogService;
        private readonly IAuthenticationService _authService;

        [ObservableProperty]
        private ObservableCollection<Publisher> _publishers = new();

        [ObservableProperty]
        private Publisher? _selectedPublisher;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _city = string.Empty;

        [ObservableProperty]
        private string _country = string.Empty;

        [ObservableProperty]
        private string _contact = string.Empty;

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        private string _website = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private bool _isEditMode;

        public PublisherViewModel(ICatalogService catalogService, IAuthenticationService authService)
        {
            _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _ = LoadPublishersAsync();
        }

        public async Task LoadPublishersAsync()
        {
            IsBusy = true;
            try
            {
                var publishersList = await _catalogService.GetAllPublishersAsync();
                Publishers.Clear();
                foreach (var publisher in publishersList)
                {
                    Publishers.Add(publisher);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load publishers: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void SelectPublisherForEdit(Publisher? publisher)
        {
            if (publisher == null) return;
            
            SelectedPublisher = publisher;
            Name = publisher.Name;
            City = publisher.City ?? string.Empty;
            Country = publisher.Country ?? string.Empty;
            Contact = publisher.Contact ?? string.Empty;
            Email = publisher.Email ?? string.Empty;
            Website = publisher.Website ?? string.Empty;
            IsEditMode = true;
            ErrorMessage = string.Empty;
        }

        [RelayCommand]
        private void ClearForm()
        {
            SelectedPublisher = null;
            Name = string.Empty;
            City = string.Empty;
            Country = string.Empty;
            Contact = string.Empty;
            Email = string.Empty;
            Website = string.Empty;
            IsEditMode = false;
            ErrorMessage = string.Empty;
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                ErrorMessage = "Publisher name is required.";
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                if (IsEditMode && SelectedPublisher != null)
                {
                    SelectedPublisher.Name = Name.Trim();
                    SelectedPublisher.City = string.IsNullOrWhiteSpace(City) ? null : City.Trim();
                    SelectedPublisher.Country = string.IsNullOrWhiteSpace(Country) ? null : Country.Trim();
                    SelectedPublisher.Contact = string.IsNullOrWhiteSpace(Contact) ? null : Contact.Trim();
                    SelectedPublisher.Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();
                    SelectedPublisher.Website = string.IsNullOrWhiteSpace(Website) ? null : Website.Trim();

                    await _catalogService.UpdatePublisherAsync(SelectedPublisher);
                }
                else
                {
                    var newPublisher = new Publisher
                    {
                        Name = Name.Trim(),
                        City = string.IsNullOrWhiteSpace(City) ? null : City.Trim(),
                        Country = string.IsNullOrWhiteSpace(Country) ? null : Country.Trim(),
                        Contact = string.IsNullOrWhiteSpace(Contact) ? null : Contact.Trim(),
                        Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
                        Website = string.IsNullOrWhiteSpace(Website) ? null : Website.Trim(),
                        ActiveStatus = true
                    };

                    await _catalogService.AddPublisherAsync(newPublisher);
                }

                ClearForm();
                await LoadPublishersAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to save publisher: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task DeleteAsync(Publisher? publisher)
        {
            if (publisher == null) return;

            var currentUserId = _authService.CurrentUser?.Id ?? 1;

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                await _catalogService.DeletePublisherAsync(publisher.Id, "Deleted via admin portal", currentUserId);
                ClearForm();
                await LoadPublishersAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to delete publisher: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
