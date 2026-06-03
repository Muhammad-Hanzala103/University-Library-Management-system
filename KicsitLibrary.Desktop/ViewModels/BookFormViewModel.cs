using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;

namespace KicsitLibrary.Desktop.ViewModels
{
    public class AuthorSelection : ObservableObject
    {
        public Author Author { get; }
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
        public AuthorSelection(Author author, bool isSelected = false)
        {
            Author = author;
            IsSelected = isSelected;
        }
    }

    public partial class BookFormViewModel : ObservableObject
    {
        private readonly ICatalogService _catalogService;
        private readonly IAuthenticationService _authService;
        private readonly BookMaster? _editingBook;

        [ObservableProperty]
        private string _windowTitle = "Add New Book";

        // Dropdown data
        [ObservableProperty]
        private ObservableCollection<Publisher> _publishers = new();

        [ObservableProperty]
        private ObservableCollection<Category> _categories = new();

        [ObservableProperty]
        private ObservableCollection<DepartmentCategory> _departmentCategories = new();

        [ObservableProperty]
        private ObservableCollection<LiteratureCategory> _literatureCategories = new();

        [ObservableProperty]
        private ObservableCollection<AuthorSelection> _authors = new();

        // Form Fields
        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _subTitle = string.Empty;

        [ObservableProperty]
        private string _edition = string.Empty;

        [ObservableProperty]
        private int _selectedPublisherId;

        [ObservableProperty]
        private string _publicationPlace = string.Empty;

        [ObservableProperty]
        private int _publicationYear = DateTime.Now.Year;

        [ObservableProperty]
        private int? _copyrightYear;

        [ObservableProperty]
        private string _series = string.Empty;

        [ObservableProperty]
        private string _language = "English";

        [ObservableProperty]
        private string _format = string.Empty;

        [ObservableProperty]
        private string _bindingType = "Hardcover";

        [ObservableProperty]
        private string _physicalDescription = string.Empty;

        [ObservableProperty]
        private string _keywords = string.Empty;

        [ObservableProperty]
        private string _notes = string.Empty;

        [ObservableProperty]
        private string _contents = string.Empty;

        [ObservableProperty]
        private string _isbn = string.Empty;

        [ObservableProperty]
        private string _issn = string.Empty;

        [ObservableProperty]
        private string _source = string.Empty;

        [ObservableProperty]
        private string _storeName = string.Empty;

        [ObservableProperty]
        private string _billNumber = string.Empty;

        [ObservableProperty]
        private string _bookCoverPath = string.Empty;

        [ObservableProperty]
        private int _selectedCategoryId;

        [ObservableProperty]
        private int _selectedDepartmentCategoryId;

        [ObservableProperty]
        private int _selectedLiteratureCategoryId;

        [ObservableProperty]
        private string _subject = string.Empty;

        [ObservableProperty]
        private string _classificationNumber = string.Empty;

        [ObservableProperty]
        private string _callNumber = string.Empty;

        [ObservableProperty]
        private string _deweyNumber = string.Empty;

        [ObservableProperty]
        private string _accessionType = "Purchase";

        [ObservableProperty]
        private DateTime _purchaseDate = DateTime.Now;

        [ObservableProperty]
        private decimal _purchasePrice;

        [ObservableProperty]
        private string _supplier = string.Empty;

        [ObservableProperty]
        private string _invoiceFilePath = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        public event Action<bool>? CloseRequest;

        public BookFormViewModel(ICatalogService catalogService, IAuthenticationService authService, BookMaster? editingBook)
        {
            _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _editingBook = editingBook;

            if (_editingBook != null)
            {
                WindowTitle = $"Edit Book: {_editingBook.Title}";
            }

            _ = InitializeFormAsync();
        }

        private async Task InitializeFormAsync()
        {
            IsBusy = true;
            try
            {
                // Fetch drop downs
                var publishersList = await _catalogService.GetAllPublishersAsync();
                var categoriesList = await _catalogService.GetAllCategoriesAsync();
                var deptsList = await _catalogService.GetAllDepartmentCategoriesAsync();
                var litsList = await _catalogService.GetAllLiteratureCategoriesAsync();
                var authorsList = await _catalogService.GetAllAuthorsAsync();

                Publishers = new ObservableCollection<Publisher>(publishersList);
                Categories = new ObservableCollection<Category>(categoriesList);
                DepartmentCategories = new ObservableCollection<DepartmentCategory>(deptsList);
                LiteratureCategories = new ObservableCollection<LiteratureCategory>(litsList);

                // Populate authors list with selection helper
                var selectedAuthorIds = _editingBook?.BookAuthors.Select(ba => ba.AuthorId).ToList() ?? new List<int>();
                Authors.Clear();
                foreach (var author in authorsList)
                {
                    Authors.Add(new AuthorSelection(author, selectedAuthorIds.Contains(author.Id)));
                }

                // If editing, map existing book properties
                if (_editingBook != null)
                {
                    Title = _editingBook.Title;
                    SubTitle = _editingBook.SubTitle ?? string.Empty;
                    Edition = _editingBook.Edition ?? string.Empty;
                    SelectedPublisherId = _editingBook.PublisherId;
                    PublicationPlace = _editingBook.PublicationPlace ?? string.Empty;
                    PublicationYear = _editingBook.PublicationYear;
                    CopyrightYear = _editingBook.CopyrightYear;
                    Series = _editingBook.Series ?? string.Empty;
                    Language = _editingBook.Language ?? "English";
                    Format = _editingBook.Format ?? string.Empty;
                    BindingType = _editingBook.BindingType ?? "Hardcover";
                    PhysicalDescription = _editingBook.PhysicalDescription ?? string.Empty;
                    Keywords = _editingBook.Keywords ?? string.Empty;
                    Notes = _editingBook.Notes ?? string.Empty;
                    Contents = _editingBook.Contents ?? string.Empty;
                    Isbn = _editingBook.ISBN ?? string.Empty;
                    Issn = _editingBook.ISSN ?? string.Empty;
                    Source = _editingBook.Source ?? string.Empty;
                    StoreName = _editingBook.StoreName ?? string.Empty;
                    BillNumber = _editingBook.BillNumber ?? string.Empty;
                    BookCoverPath = _editingBook.BookImage ?? string.Empty;
                    SelectedCategoryId = _editingBook.CategoryId;
                    SelectedDepartmentCategoryId = _editingBook.DepartmentCategoryId;
                    SelectedLiteratureCategoryId = _editingBook.LiteratureCategoryId;
                    Subject = _editingBook.Subject ?? string.Empty;
                    ClassificationNumber = _editingBook.ClassificationNumber ?? string.Empty;
                    CallNumber = _editingBook.CallNumber ?? string.Empty;
                    DeweyNumber = _editingBook.DeweyNumber ?? string.Empty;
                    AccessionType = _editingBook.AccessionType ?? "Purchase";
                    PurchaseDate = _editingBook.PurchaseDate;
                    PurchasePrice = _editingBook.PurchasePrice;
                    Supplier = _editingBook.Supplier ?? string.Empty;
                    InvoiceFilePath = _editingBook.InvoiceFile ?? string.Empty;
                }
                else
                {
                    // Set defaults for new book
                    SelectedPublisherId = Publishers.FirstOrDefault()?.Id ?? 0;
                    SelectedCategoryId = Categories.FirstOrDefault()?.Id ?? 0;
                    SelectedDepartmentCategoryId = DepartmentCategories.FirstOrDefault()?.Id ?? 0;
                    SelectedLiteratureCategoryId = LiteratureCategories.FirstOrDefault()?.Id ?? 0;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to initialize form: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void UploadCover()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image Files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png",
                Title = "Select Book Cover Image"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var securePath = SecureUploadFile(openFileDialog.FileName, "Covers");
                    BookCoverPath = securePath;
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"Upload failed: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private void UploadInvoice()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Documents (*.pdf;*.jpg;*.jpeg;*.png)|*.pdf;*.jpg;*.jpeg;*.png",
                Title = "Select Invoice Document"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var securePath = SecureUploadFile(openFileDialog.FileName, "Invoices");
                    InvoiceFilePath = securePath;
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"Upload failed: {ex.Message}";
                }
            }
        }

        private string SecureUploadFile(string sourceFilePath, string subFolder)
        {
            var fileInfo = new FileInfo(sourceFilePath);
            var maxFileSizeMb = 10; // Read from settings or use 10MB default
            var allowedSize = maxFileSizeMb * 1024 * 1024;

            if (fileInfo.Length > allowedSize)
            {
                throw new InvalidOperationException($"File size exceeds the limit of {maxFileSizeMb}MB.");
            }

            // Create target folder in local app data directory
            var appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KicsitLibrary", "Uploads", subFolder);
            if (!Directory.Exists(appDataFolder))
            {
                Directory.CreateDirectory(appDataFolder);
            }

            var uniqueFileName = $"{Guid.NewGuid()}{fileInfo.Extension}";
            var destinationPath = Path.Combine(appDataFolder, uniqueFileName);

            File.Copy(sourceFilePath, destinationPath, true);
            return destinationPath;
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            ErrorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Title))
            {
                ErrorMessage = "Title is required.";
                return;
            }

            if (SelectedPublisherId == 0)
            {
                ErrorMessage = "Publisher is required.";
                return;
            }

            if (SelectedCategoryId == 0)
            {
                ErrorMessage = "Category is required.";
                return;
            }

            if (SelectedDepartmentCategoryId == 0)
            {
                ErrorMessage = "Department classification is required.";
                return;
            }

            if (SelectedLiteratureCategoryId == 0)
            {
                ErrorMessage = "Literature classification is required.";
                return;
            }

            var selectedAuthorIds = Authors.Where(a => a.IsSelected).Select(a => a.Author.Id).ToList();
            if (!selectedAuthorIds.Any())
            {
                ErrorMessage = "At least one author must be selected.";
                return;
            }

            if (PurchasePrice < 0)
            {
                ErrorMessage = "Purchase price cannot be negative.";
                return;
            }

            IsBusy = true;
            try
            {
                var book = _editingBook ?? new BookMaster();

                book.Title = Title.Trim();
                book.SubTitle = string.IsNullOrWhiteSpace(SubTitle) ? null : SubTitle.Trim();
                book.Edition = string.IsNullOrWhiteSpace(Edition) ? null : Edition.Trim();
                book.PublisherId = SelectedPublisherId;
                book.PublicationPlace = string.IsNullOrWhiteSpace(PublicationPlace) ? null : PublicationPlace.Trim();
                book.PublicationYear = PublicationYear;
                book.CopyrightYear = CopyrightYear;
                book.Series = string.IsNullOrWhiteSpace(Series) ? null : Series.Trim();
                book.Language = string.IsNullOrWhiteSpace(Language) ? null : Language.Trim();
                book.Format = string.IsNullOrWhiteSpace(Format) ? null : Format.Trim();
                book.BindingType = string.IsNullOrWhiteSpace(BindingType) ? null : BindingType.Trim();
                book.PhysicalDescription = string.IsNullOrWhiteSpace(PhysicalDescription) ? null : PhysicalDescription.Trim();
                book.Keywords = string.IsNullOrWhiteSpace(Keywords) ? null : Keywords.Trim();
                book.Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim();
                book.Contents = string.IsNullOrWhiteSpace(Contents) ? null : Contents.Trim();
                book.ISBN = string.IsNullOrWhiteSpace(Isbn) ? null : Isbn.Trim();
                book.ISSN = string.IsNullOrWhiteSpace(Issn) ? null : Issn.Trim();
                book.Source = string.IsNullOrWhiteSpace(Source) ? null : Source.Trim();
                book.StoreName = string.IsNullOrWhiteSpace(StoreName) ? null : StoreName.Trim();
                book.BillNumber = string.IsNullOrWhiteSpace(BillNumber) ? null : BillNumber.Trim();
                book.BookImage = string.IsNullOrWhiteSpace(BookCoverPath) ? null : BookCoverPath;
                book.CategoryId = SelectedCategoryId;
                book.DepartmentCategoryId = SelectedDepartmentCategoryId;
                book.LiteratureCategoryId = SelectedLiteratureCategoryId;
                book.Subject = string.IsNullOrWhiteSpace(Subject) ? null : Subject.Trim();
                book.ClassificationNumber = string.IsNullOrWhiteSpace(ClassificationNumber) ? null : ClassificationNumber.Trim();
                book.CallNumber = string.IsNullOrWhiteSpace(CallNumber) ? null : CallNumber.Trim();
                book.DeweyNumber = string.IsNullOrWhiteSpace(DeweyNumber) ? null : DeweyNumber.Trim();
                book.AccessionType = string.IsNullOrWhiteSpace(AccessionType) ? null : AccessionType.Trim();
                book.PurchaseDate = PurchaseDate;
                book.PurchasePrice = PurchasePrice;
                book.Supplier = string.IsNullOrWhiteSpace(Supplier) ? null : Supplier.Trim();
                book.InvoiceFile = string.IsNullOrWhiteSpace(InvoiceFilePath) ? null : InvoiceFilePath;
                book.Status = BookStatus.Available;

                if (_editingBook != null)
                {
                    book.UpdatedByUserId = _authService.CurrentUser?.Id ?? 1;
                    await _catalogService.UpdateBookAsync(book, selectedAuthorIds);
                }
                else
                {
                    book.CreatedByUserId = _authService.CurrentUser?.Id ?? 1;
                    await _catalogService.AddBookAsync(book, selectedAuthorIds);
                }

                CloseRequest?.Invoke(true);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to save book catalog record: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            CloseRequest?.Invoke(false);
        }
    }
}
