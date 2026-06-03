using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;

namespace KicsitLibrary.Desktop.ViewModels
{
    public partial class CopiesViewModel : ObservableObject
    {
        private readonly ICatalogService _catalogService;
        private readonly IAuthenticationService _authService;
        
        public BookMaster BookMaster { get; }

        [ObservableProperty]
        private ObservableCollection<BookCopy> _copies = new();

        [ObservableProperty]
        private ObservableCollection<Rack> _racks = new();

        [ObservableProperty]
        private ObservableCollection<Shelf> _shelves = new();

        // New Copy Fields
        [ObservableProperty]
        private string _accessionNumber = string.Empty;

        [ObservableProperty]
        private string _barcode = string.Empty;

        [ObservableProperty]
        private string _qrCode = string.Empty;

        [ObservableProperty]
        private Rack? _selectedRack;

        [ObservableProperty]
        private Shelf? _selectedShelf;

        [ObservableProperty]
        private string _rowNumber = string.Empty;

        [ObservableProperty]
        private string _customLocation = string.Empty;

        [ObservableProperty]
        private string _physicalCondition = "Normal";

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        public CopiesViewModel(ICatalogService catalogService, IAuthenticationService authService, BookMaster bookMaster)
        {
            _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            BookMaster = bookMaster ?? throw new ArgumentNullException(nameof(bookMaster));

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            IsBusy = true;
            try
            {
                var racksList = await _catalogService.GetAllRacksAsync();
                Racks = new ObservableCollection<Rack>(racksList);
                SelectedRack = Racks.FirstOrDefault();

                await LoadCopiesAsync();
                await AutoGenerateAccessionCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to initialize copies editor: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task LoadCopiesAsync()
        {
            IsBusy = true;
            try
            {
                var copiesList = await _catalogService.GetCopiesByBookIdAsync(BookMaster.Id);
                Copies.Clear();
                foreach (var copy in copiesList)
                {
                    Copies.Add(copy);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load copies: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        async partial void OnSelectedRackChanged(Rack? value)
        {
            if (value != null)
            {
                var shelvesList = await _catalogService.GetShelvesByRackIdAsync(value.Id);
                Shelves = new ObservableCollection<Shelf>(shelvesList);
                SelectedShelf = Shelves.FirstOrDefault();
            }
            else
            {
                Shelves.Clear();
                SelectedShelf = null;
            }
        }

        [RelayCommand]
        private async Task AutoGenerateAccessionAsync()
        {
            try
            {
                AccessionNumber = await _catalogService.AutoGenerateAccessionNumberAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to auto-generate accession number: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task AddCopyAsync()
        {
            ErrorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(AccessionNumber))
            {
                ErrorMessage = "Accession Number is required.";
                return;
            }

            IsBusy = true;
            try
            {
                var isDuplicate = await _catalogService.IsAccessionNumberDuplicateAsync(AccessionNumber.Trim());
                if (isDuplicate)
                {
                    ErrorMessage = $"Accession Number '{AccessionNumber}' already exists in database. Duplicate values are prohibited.";
                    IsBusy = false;
                    return;
                }

                // Determine Copy Number
                var nextCopyNumber = Copies.Any() ? Copies.Max(c => c.CopyNumber) + 1 : 1;

                // Concatenate final location detail
                var locationText = string.Empty;
                if (SelectedRack != null && SelectedShelf != null)
                {
                    locationText = $"Rack {SelectedRack.Name}, Shelf {SelectedShelf.Name}";
                    if (!string.IsNullOrWhiteSpace(RowNumber))
                    {
                        locationText += $", Row {RowNumber}";
                    }
                }
                else if (!string.IsNullOrWhiteSpace(CustomLocation))
                {
                    locationText = CustomLocation;
                }

                var newCopy = new BookCopy
                {
                    AccessionNumber = AccessionNumber.Trim(),
                    BookMasterId = BookMaster.Id,
                    CopyNumber = nextCopyNumber,
                    Barcode = string.IsNullOrWhiteSpace(Barcode) ? null : Barcode.Trim(),
                    QRCode = string.IsNullOrWhiteSpace(QRCode) ? null : QRCode.Trim(),
                    RackNumber = SelectedRack?.Name,
                    ShelfNumber = SelectedShelf?.Name,
                    RowNumber = string.IsNullOrWhiteSpace(RowNumber) ? null : RowNumber.Trim(),
                    Location = locationText,
                    PhysicalCondition = PhysicalCondition,
                    AvailabilityStatus = BookStatus.Available
                };

                await _catalogService.AddCopyAsync(newCopy);

                // Clear input fields and refresh list
                Barcode = string.Empty;
                QRCode = string.Empty;
                RowNumber = string.Empty;
                CustomLocation = string.Empty;

                await LoadCopiesAsync();
                await AutoGenerateAccessionAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to add physical copy: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task DeleteCopyAsync(BookCopy? copy)
        {
            if (copy == null) return;

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                var userId = _authService.CurrentUser?.Id ?? 1;
                await _catalogService.DeleteCopyAsync(copy.Id, "Deleted by librarian from copies view", userId);
                await LoadCopiesAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to delete physical copy: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
