using System;
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
        public System.Collections.ObjectModel.ObservableCollection<Student> StudentSuggestions { get; } = new();
        [ObservableProperty] private Student? _selectedStudentSuggestion;

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
            IsMemberFound = false;
            IsEligible = false;
            _foundMemberId = null;

            if (string.IsNullOrWhiteSpace(AccessionNumber))
            {
                StatusMessage = "Please enter or scan a book Accession Number.";
                return;
            }

            if (string.IsNullOrWhiteSpace(MemberIdentifier))
            {
                StatusMessage = "Please enter or scan a Member Registration / Personnel Number.";
                return;
            }

            IsBusy = true;
            try
            {
                // 1. Search Book Copy
                var copy = await _circulationService.GetCopyDetailsForCirculationAsync(AccessionNumber.Trim());
                if (copy != null)
                {
                    LoadedCopy = copy;
                    BookTitle = copy.BookMaster.Title;
                    BookAuthorDisplay = copy.BookMaster.Subject ?? "General Subject";
                    BookStatusDisplay = copy.AvailabilityStatus.ToString();
                    IsBookFound = true;
                }
                else
                {
                    BookTitle = "Material not found in catalog.";
                    BookAuthorDisplay = string.Empty;
                    BookStatusDisplay = "Unavailable";
                }

                // 2. Search Member
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
                        MemberPhotoPath = string.Empty; // Local fallback checked in UI
                    }
                }
                else
                {
                    MemberName = "Member record not found.";
                    MemberDetailDisplay = string.Empty;
                    MemberDepartment = string.Empty;
                    MemberStatusDisplay = "Inactive";
                    MemberPhotoPath = string.Empty;
                }

                // 3. Check Eligibility if both found
                if (IsBookFound && IsMemberFound && _foundMemberId.HasValue)
                {
                    var result = await _circulationService.CheckMemberEligibilityAsync(_foundMemberId.Value, SelectedMemberType);
                    CurrentIssuedCount = result.CurrentIssuedCount;
                    MaxAllowedLimit = result.MaxAllowedLimit;
                    PendingFinesTotal = result.PendingFinesTotal;
                    HasActiveOverdue = result.HasActiveOverdue;
                    EligibilityMessage = result.EligibilityMessage;

                    if (LoadedCopy?.AvailabilityStatus != BookStatus.Available)
                    {
                        EligibilityMessage = $"Material copy is not available (Status: {LoadedCopy?.AvailabilityStatus})";
                        IsEligible = false;
                    }
                    else
                    {
                        IsEligible = result.EligibilityMessage == "Eligible";
                    }
                }
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
        private async Task ConfirmIssueAsync()
        {
            if (!IsEligible || !_foundMemberId.HasValue || LoadedCopy == null)
            {
                StatusMessage = "Cannot issue material. Validation checks failed.";
                return;
            }

            IsBusy = true;
            try
            {
                var userId = _authService.CurrentUser?.Id ?? 1;
                var record = await _circulationService.IssueBookAsync(
                    LoadedCopy.AccessionNumber,
                    _foundMemberId.Value,
                    SelectedMemberType,
                    userId
                );

                StatusMessage = $"Material checkout successful! Due date: {record.ExpectedReturnDate:dd-MMM-yyyy}.";
                
                // Clear fields for next checkout
                AccessionNumber = string.Empty;
                IsEligible = false;
                IsBookFound = false;
                LoadedCopy = null;
                BookTitle = "No Book Loaded";
                BookAuthorDisplay = string.Empty;
                BookStatusDisplay = string.Empty;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to issue material: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void ClearAll()
        {
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
