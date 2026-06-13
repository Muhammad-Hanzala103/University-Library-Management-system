using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;

namespace KicsitLibrary.Desktop.ViewModels
{
    public partial class IssueMaterialViewModel : ObservableObject
    {
        private readonly ICirculationService _circulationService;
        private readonly IConsumerService _consumerService;
        private readonly IAuthenticationService _authService;

        [ObservableProperty] private string _accessionNumber = string.Empty;
        [ObservableProperty] private string _memberIdentifier = string.Empty;
        [ObservableProperty] private MemberType _selectedMemberType = MemberType.Student;

        [ObservableProperty] private bool _showSuggestions;
        public ObservableCollection<Student> StudentSuggestions { get; } = new();
        [ObservableProperty] private Student? _selectedStudentSuggestion;
        public ObservableCollection<IssueRecord> ActiveIssues { get; } = new();

        // Advanced checkout cart
        public ObservableCollection<BookCopy> Cart { get; } = new();
        [ObservableProperty] private bool _autoAddOnScan = true;

        // Found flags
        [ObservableProperty] private bool _isBookFound;
        [ObservableProperty] private bool _isMemberFound;
        [ObservableProperty] private bool _isEligible;

        // Details Cards
        [ObservableProperty] private BookCopy? _loadedCopy;
        [ObservableProperty] private string _bookTitle = "No Book Loaded";
        [ObservableProperty] private string _bookAuthorDisplay = string.Empty;
        [ObservableProperty] private string _bookStatusDisplay = string.Empty;

        [ObservableProperty] private string _memberName = "No Member Loaded";
        [ObservableProperty] private string _memberDetailDisplay = string.Empty;
        [ObservableProperty] private string _memberDepartment = string.Empty;
        [ObservableProperty] private string _memberStatusDisplay = string.Empty;
        [ObservableProperty] private string _memberPhotoPath = string.Empty;

        // Eligibility details
        [ObservableProperty] private int _currentIssuedCount;
        [ObservableProperty] private int _maxAllowedLimit;
        [ObservableProperty] private decimal _pendingFinesTotal;
        [ObservableProperty] private bool _hasActiveOverdue;
        [ObservableProperty] private string _eligibilityMessage = string.Empty;

        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private bool _isBusy;

        private int? _foundMemberId;

        public IssueMaterialViewModel(ICirculationService circulationService, IConsumerService consumerService, IAuthenticationService authService)
        {
            _circulationService = circulationService ?? throw new ArgumentNullException(nameof(circulationService));
            _consumerService = consumerService ?? throw new ArgumentNullException(nameof(consumerService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        [RelayCommand]
        private async Task ValidateDetailsAsync()
        {
            StatusMessage = string.Empty;
            IsBookFound = false;
            IsEligible = false;

            if (string.IsNullOrWhiteSpace(AccessionNumber) && string.IsNullOrWhiteSpace(MemberIdentifier))
            {
                StatusMessage = "Please scan or enter details first.";
                return;
            }

            IsBusy = true;
            try
            {
                // 1. Search Member (if specified and not loaded or changing)
                if (!string.IsNullOrWhiteSpace(MemberIdentifier))
                {
                    var memberObj = await _circulationService.GetMemberDetailsAsync(MemberIdentifier.Trim(), SelectedMemberType);
                    if (memberObj != null)
                    {
                        IsMemberFound = true;
                        if (SelectedMemberType == MemberType.Student && memberObj is Student student)
                        {
                            _foundMemberId = student.Id;
                            MemberName = student.Name;
                            MemberDetailDisplay = $"{student.Program} - Batch {student.Batch}";
                            MemberDepartment = student.Department;
                            MemberStatusDisplay = student.ActiveStatus ? "Active" : "Inactive";
                            MemberPhotoPath = student.PhotoPath ?? string.Empty;
                        }
                        else if (SelectedMemberType == MemberType.FacultyStaff && memberObj is FacultyStaff fs)
                        {
                            _foundMemberId = fs.Id;
                            MemberName = fs.Name;
                            MemberDetailDisplay = $"{fs.Designation} ({fs.FacultyType})";
                            MemberDepartment = fs.Department;
                            MemberStatusDisplay = fs.ActiveStatus ? "Active" : "Inactive";
                            MemberPhotoPath = string.Empty;
                        }

                        // Load Active Issues & Core Eligibility for the member
                        if (_foundMemberId.HasValue)
                        {
                            var issues = await _circulationService.GetActiveIssuesByMemberAsync(_foundMemberId.Value, SelectedMemberType);
                            ActiveIssues.Clear();
                            foreach (var issue in issues)
                            {
                                ActiveIssues.Add(issue);
                            }

                            var eligibilityResult = await _circulationService.CheckMemberEligibilityAsync(_foundMemberId.Value, SelectedMemberType);
                            CurrentIssuedCount = eligibilityResult.CurrentIssuedCount;
                            MaxAllowedLimit = eligibilityResult.MaxAllowedLimit;
                            PendingFinesTotal = eligibilityResult.PendingFinesTotal;
                            HasActiveOverdue = eligibilityResult.HasActiveOverdue;
                        }
                    }
                    else
                    {
                        MemberName = "Member record not found.";
                        MemberDetailDisplay = string.Empty;
                        MemberDepartment = string.Empty;
                        MemberStatusDisplay = "Inactive";
                        MemberPhotoPath = string.Empty;
                        IsMemberFound = false;
                        _foundMemberId = null;
                        ActiveIssues.Clear();
                    }
                }

                // 2. Search Book Copy (if specified)
                if (!string.IsNullOrWhiteSpace(AccessionNumber))
                {
                    var copy = await _circulationService.GetCopyDetailsForCirculationAsync(AccessionNumber.Trim());
                    if (copy != null)
                    {
                        LoadedCopy = copy;
                        BookTitle = copy.BookMaster.Title;
                        BookAuthorDisplay = copy.BookMaster.Subject ?? "General Subject";
                        BookStatusDisplay = copy.AvailabilityStatus.ToString();
                        IsBookFound = true;

                        if (AutoAddOnScan)
                        {
                            if (Cart.Any(c => c.AccessionNumber == copy.AccessionNumber))
                            {
                                StatusMessage = $"Book copy '{copy.AccessionNumber}' is already in the cart.";
                            }
                            else if (copy.AvailabilityStatus != BookStatus.Available)
                            {
                                StatusMessage = $"Book copy '{copy.AccessionNumber}' is not available (Status: {copy.AvailabilityStatus}).";
                            }
                            else
                            {
                                Cart.Add(copy);
                                StatusMessage = $"Added '{copy.BookMaster.Title}' to checkout cart.";
                                AccessionNumber = string.Empty;
                                LoadedCopy = null;
                                IsBookFound = false;
                            }
                        }
                    }
                    else
                    {
                        BookTitle = "Material not found in catalog.";
                        BookAuthorDisplay = string.Empty;
                        BookStatusDisplay = "Unavailable";
                        LoadedCopy = null;
                    }
                }

                RecalculateEligibility();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Validation failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void AddToCart()
        {
            if (LoadedCopy == null)
            {
                StatusMessage = "No book copy loaded to add to cart.";
                return;
            }

            if (Cart.Any(c => c.AccessionNumber == LoadedCopy.AccessionNumber))
            {
                StatusMessage = $"Book copy '{LoadedCopy.AccessionNumber}' is already in the cart.";
                return;
            }

            if (LoadedCopy.AvailabilityStatus != BookStatus.Available)
            {
                StatusMessage = $"Book copy '{LoadedCopy.AccessionNumber}' is not available.";
                return;
            }

            Cart.Add(LoadedCopy);
            StatusMessage = $"Added '{LoadedCopy.BookMaster.Title}' to cart.";
            
            AccessionNumber = string.Empty;
            LoadedCopy = null;
            IsBookFound = false;

            RecalculateEligibility();
        }

        [RelayCommand]
        private void RemoveFromCart(BookCopy copy)
        {
            if (copy == null) return;
            Cart.Remove(copy);
            StatusMessage = $"Removed '{copy.BookMaster.Title}' from cart.";
            RecalculateEligibility();
        }

        [RelayCommand]
        private void ClearCart()
        {
            Cart.Clear();
            StatusMessage = "Cart cleared.";
            RecalculateEligibility();
        }

        private void RecalculateEligibility()
        {
            if (!IsMemberFound || !_foundMemberId.HasValue)
            {
                IsEligible = false;
                EligibilityMessage = "Validate member registration first.";
                return;
            }

            var remainingLimit = MaxAllowedLimit - CurrentIssuedCount;
            if (Cart.Count == 0)
            {
                IsEligible = false;
                EligibilityMessage = "Add books to the cart to checkout.";
                return;
            }

            if (Cart.Count > remainingLimit)
            {
                IsEligible = false;
                EligibilityMessage = $"Limit exceeded: Member can borrow {remainingLimit} more books, but cart has {Cart.Count} items.";
                return;
            }

            if (HasActiveOverdue)
            {
                IsEligible = false;
                EligibilityMessage = "Blocked: Member has active overdue books.";
                return;
            }

            if (PendingFinesTotal > 0)
            {
                IsEligible = false;
                EligibilityMessage = $"Blocked: Member has Rs. {PendingFinesTotal:N0} outstanding fines.";
                return;
            }

            IsEligible = true;
            EligibilityMessage = $"Eligible. Ready to checkout {Cart.Count} item(s).";
        }

        [RelayCommand]
        private async Task ConfirmIssueAsync()
        {
            if (!IsEligible || !_foundMemberId.HasValue || Cart.Count == 0)
            {
                StatusMessage = "Cannot issue material. Validation checks failed.";
                return;
            }

            IsBusy = true;
            try
            {
                var userId = _authService.CurrentUser?.Id ?? 1;
                var succeededCount = 0;
                var failedCount = 0;
                var errorMessages = new System.Collections.Generic.List<string>();
                var receiptBuilder = new System.Text.StringBuilder();

                receiptBuilder.AppendLine("========================================");
                receiptBuilder.AppendLine("       ILM-O-KUTUB LIBRARY SYSTEM       ");
                receiptBuilder.AppendLine("             CHECKOUT SLIP              ");
                receiptBuilder.AppendLine("========================================");
                receiptBuilder.AppendLine($"Date: {DateTime.Now:dd-MMM-yyyy HH:mm:ss}");
                receiptBuilder.AppendLine($"Member: {MemberName}");
                receiptBuilder.AppendLine($"ID: {MemberIdentifier}");
                receiptBuilder.AppendLine($"Category: {SelectedMemberType}");
                receiptBuilder.AppendLine("----------------------------------------");
                receiptBuilder.AppendLine("Issued Materials:");

                var copiesToIssue = Cart.ToList();

                foreach (var copy in copiesToIssue)
                {
                    try
                    {
                        var record = await _circulationService.IssueBookAsync(
                            copy.AccessionNumber,
                            _foundMemberId.Value,
                            SelectedMemberType,
                            userId
                        );
                        succeededCount++;
                        receiptBuilder.AppendLine($"- {copy.BookMaster.Title}");
                        receiptBuilder.AppendLine($"  Acc No: {copy.AccessionNumber}  Due: {record.ExpectedReturnDate:dd-MMM-yyyy}");
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        errorMessages.Add($"{copy.AccessionNumber}: {ex.Message}");
                    }
                }

                receiptBuilder.AppendLine("----------------------------------------");
                receiptBuilder.AppendLine($"Total Issued: {succeededCount}");
                if (failedCount > 0)
                {
                    receiptBuilder.AppendLine($"Failed: {failedCount}");
                }
                receiptBuilder.AppendLine("========================================");
                receiptBuilder.AppendLine("Please return books on or before due date.");
                receiptBuilder.AppendLine("     Thank you for using our library!   ");
                receiptBuilder.AppendLine("========================================");

                if (failedCount == 0)
                {
                    StatusMessage = $"Successfully checked out {succeededCount} book(s)!\n\nReceipt:\n{receiptBuilder}";
                }
                else
                {
                    StatusMessage = $"Checkout completed with errors. Succeeded: {succeededCount}, Failed: {failedCount}.\nErrors:\n{string.Join("\n", errorMessages)}\n\nReceipt:\n{receiptBuilder}";
                }

                Cart.Clear();
                AccessionNumber = string.Empty;
                IsEligible = false;
                IsBookFound = false;
                LoadedCopy = null;
                BookTitle = "No Book Loaded";
                BookAuthorDisplay = string.Empty;
                BookStatusDisplay = string.Empty;

                if (_foundMemberId.HasValue)
                {
                    var issues = await _circulationService.GetActiveIssuesByMemberAsync(_foundMemberId.Value, SelectedMemberType);
                    ActiveIssues.Clear();
                    foreach (var issue in issues)
                    {
                        ActiveIssues.Add(issue);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to issue materials: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void ReserveInstead()
        {
            if (!IsBookFound || LoadedCopy == null)
            {
                StatusMessage = "No book loaded to reserve.";
                return;
            }
            StatusMessage = $"Navigation to Reservation module with book '{LoadedCopy.BookMaster.Title}' is triggered.";
        }

        [RelayCommand]
        private void PrintSlip()
        {
            if (ActiveIssues.Count == 0)
            {
                StatusMessage = "No active checkout records to print.";
                return;
            }

            var receiptBuilder = new System.Text.StringBuilder();
            receiptBuilder.AppendLine("========================================");
            receiptBuilder.AppendLine("       ILM-O-KUTUB LIBRARY SYSTEM       ");
            receiptBuilder.AppendLine("          ACTIVE ISSUES RECEIPT         ");
            receiptBuilder.AppendLine("========================================");
            receiptBuilder.AppendLine($"Date: {DateTime.Now:dd-MMM-yyyy HH:mm:ss}");
            receiptBuilder.AppendLine($"Member: {MemberName}");
            receiptBuilder.AppendLine($"ID: {MemberIdentifier}");
            receiptBuilder.AppendLine("----------------------------------------");
            receiptBuilder.AppendLine("Current Active Issues:");
            foreach (var issue in ActiveIssues)
            {
                receiptBuilder.AppendLine($"- Acc No: {issue.AccessionNumber}");
                receiptBuilder.AppendLine($"  Issued: {issue.IssueDate:dd-MMM-yyyy}  Due: {issue.ExpectedReturnDate:dd-MMM-yyyy}");
            }
            receiptBuilder.AppendLine("========================================");

            StatusMessage = receiptBuilder.ToString();
        }

        [RelayCommand]
        private void ClearAll()
        {
            Cart.Clear();
            AccessionNumber = string.Empty;
            MemberIdentifier = string.Empty;
            SelectedMemberType = MemberType.Student;

            IsBookFound = false;
            IsMemberFound = false;
            IsEligible = false;
            _foundMemberId = null;

            LoadedCopy = null;
            BookTitle = "No Book Loaded";
            BookAuthorDisplay = string.Empty;
            BookStatusDisplay = string.Empty;

            MemberName = "No Member Loaded";
            MemberDetailDisplay = string.Empty;
            MemberDepartment = string.Empty;
            MemberStatusDisplay = string.Empty;
            MemberPhotoPath = string.Empty;

            EligibilityMessage = string.Empty;
            StatusMessage = string.Empty;
            ActiveIssues.Clear();
        }

        partial void OnSelectedStudentSuggestionChanged(Student? value)
        {
            if (value != null)
            {
                MemberIdentifier = value.RegistrationNumber;
                _foundMemberId = value.Id;
                MemberName = value.Name;
                MemberDetailDisplay = $"{value.Program} - Batch {value.Batch}";
                MemberDepartment = value.Department;
                MemberStatusDisplay = value.ActiveStatus ? "Active" : "Inactive";
                MemberPhotoPath = value.PhotoPath ?? string.Empty;
                IsMemberFound = true;
                ShowSuggestions = false;
                _ = ValidateDetailsAsync();
            }
        }

        partial void OnMemberIdentifierChanged(string value)
        {
            if (SelectedStudentSuggestion != null && SelectedStudentSuggestion.RegistrationNumber == value)
            {
                return;
            }
            if (SelectedMemberType == MemberType.Student && !string.IsNullOrWhiteSpace(value))
            {
                var trimmed = value.Trim().ToLowerInvariant();
                if (trimmed.Length >= 2)
                {
                    _ = LoadSuggestionsAsync(trimmed);
                    return;
                }
            }
            ShowSuggestions = false;
        }

        private async Task LoadSuggestionsAsync(string query)
        {
            try
            {
                var matches = await _consumerService.SearchStudentsAsync(query, null, null, null, null, null);
                var list = matches.Take(10).ToList();
                
                StudentSuggestions.Clear();
                foreach (var s in list)
                {
                    StudentSuggestions.Add(s);
                }
                ShowSuggestions = StudentSuggestions.Count > 0;
            }
            catch
            {
                ShowSuggestions = false;
            }
        }
    }
}
