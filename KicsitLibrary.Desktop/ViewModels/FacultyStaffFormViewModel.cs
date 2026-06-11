using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;

namespace KicsitLibrary.Desktop.ViewModels
{
    public partial class FacultyStaffFormViewModel : ObservableObject
    {
        private readonly IConsumerService _consumerService;
        private readonly FacultyStaff? _editingMember;

        [ObservableProperty]
        private string _windowTitle = "Register New Faculty/Staff";

        // Lists for combo boxes
        public ObservableCollection<string> Departments { get; } = new(KicsitLibrary.Core.Helpers.LibraryValidator.Departments);
        public ObservableCollection<FacultyType> FacultyTypes { get; } = new() 
        { 
            FacultyType.PermanentFaculty, 
            FacultyType.VisitingFaculty, 
            FacultyType.Staff, 
            FacultyType.Guest 
        };

        // Form Fields
        [ObservableProperty]
        private string _personnelNumber = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private FacultyType _selectedFacultyType = FacultyType.PermanentFaculty;

        [ObservableProperty]
        private string _selectedDepartment = "Computer Science";

        [ObservableProperty]
        private string _designation = string.Empty;

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        private string _phone = string.Empty;

        [ObservableProperty]
        private string _cnic = string.Empty;

        [ObservableProperty]
        private string _address = string.Empty;

        [ObservableProperty]
        private bool _activeStatus = true;

        [ObservableProperty]
        private DateTime _joiningDate = DateTime.Now;

        [ObservableProperty]
        private DateTime? _leavingDate;

        [ObservableProperty]
        private string _remarks = string.Empty;

        [ObservableProperty]
        private string _photoPath = string.Empty; // Store locally using PersonnelNumber

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        private bool _isFormattingCnic;

        partial void OnCnicChanged(string value)
        {
            if (_isFormattingCnic) return;
            _isFormattingCnic = true;
            try
            {
                var formatted = KicsitLibrary.Core.Helpers.LibraryValidator.FormatCnic(value);
                if (Cnic != formatted)
                {
                    Cnic = formatted;
                }
            }
            finally
            {
                _isFormattingCnic = false;
            }
        }

        public FacultyStaffFormViewModel(IConsumerService consumerService, FacultyStaff? editingMember)
        {
            _consumerService = consumerService ?? throw new ArgumentNullException(nameof(consumerService));
            _editingMember = editingMember;

            if (Departments.Count > 0)
            {
                SelectedDepartment = Departments[0];
            }

            if (_editingMember != null)
            {
                WindowTitle = $"Edit Member: {_editingMember.Name}";
                PersonnelNumber = _editingMember.PersonnelNumber;
                Name = _editingMember.Name;
                SelectedFacultyType = _editingMember.FacultyType;
                SelectedDepartment = _editingMember.Department;
                Designation = _editingMember.Designation;
                Email = _editingMember.Email;
                Phone = _editingMember.Phone;
                Cnic = KicsitLibrary.Core.Helpers.LibraryValidator.FormatCnic(_editingMember.CNIC);
                Address = _editingMember.Address;
                ActiveStatus = _editingMember.ActiveStatus;
                JoiningDate = _editingMember.JoiningDate;
                LeavingDate = _editingMember.LeavingDate;
                Remarks = _editingMember.Remarks ?? string.Empty;

                // Check local photo path
                var appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KicsitLibrary", "Photos");
                var expectedPhoto = Path.Combine(appDataDir, $"Faculty-{PersonnelNumber}.jpg");
                if (File.Exists(expectedPhoto))
                {
                    PhotoPath = expectedPhoto;
                }
            }
        }

        [RelayCommand]
        private void SelectPhoto()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image Files (*.jpg; *.jpeg; *.png; *.webp)|*.jpg;*.jpeg;*.png;*.webp",
                Title = "Select Profile Photo"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                PhotoPath = openFileDialog.FileName; // Temporary save, copy to target on save
            }
        }

        [RelayCommand]
        private async Task SaveAsync(Window? window)
        {
            if (string.IsNullOrWhiteSpace(PersonnelNumber))
            {
                ErrorMessage = "Personnel Number is required.";
                return;
            }
            if (string.IsNullOrWhiteSpace(Name))
            {
                ErrorMessage = "Name is required.";
                return;
            }
            if (string.IsNullOrWhiteSpace(SelectedDepartment) || !KicsitLibrary.Core.Helpers.LibraryValidator.Departments.Contains(SelectedDepartment))
            {
                ErrorMessage = "Invalid Department selection.";
                return;
            }
            if (string.IsNullOrWhiteSpace(Designation))
            {
                ErrorMessage = "Designation is required.";
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                // Validate duplicate Personnel No
                var isPersonnelDuplicate = await _consumerService.IsFacultyPersonnelNumberDuplicateAsync(
                    PersonnelNumber.Trim(), _editingMember?.Id);
                if (isPersonnelDuplicate)
                {
                    ErrorMessage = $"Personnel Number '{PersonnelNumber}' already exists.";
                    IsBusy = false;
                    return;
                }

                // Validate duplicate CNIC if filled
                if (!string.IsNullOrWhiteSpace(Cnic))
                {
                    if (!KicsitLibrary.Core.Helpers.LibraryValidator.IsCnicValid(Cnic))
                    {
                        ErrorMessage = "CNIC format must be #####-#######-#.";
                        IsBusy = false;
                        return;
                    }

                    var isCnicDuplicate = await _consumerService.IsFacultyCNICDuplicateAsync(
                        Cnic.Trim(), _editingMember?.Id);
                    if (isCnicDuplicate)
                    {
                        ErrorMessage = $"CNIC '{Cnic}' is already registered to another member.";
                        IsBusy = false;
                        return;
                    }
                }

                // Process photo if changed
                if (!string.IsNullOrEmpty(PhotoPath) && File.Exists(PhotoPath) && !PhotoPath.Contains("Faculty-"))
                {
                    try
                    {
                        var appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KicsitLibrary", "Photos");
                        if (!Directory.Exists(appDataDir))
                        {
                            Directory.CreateDirectory(appDataDir);
                        }

                        var destinationPath = Path.Combine(appDataDir, $"Faculty-{PersonnelNumber.Trim().ToUpperInvariant()}.jpg");
                        File.Copy(PhotoPath, destinationPath, true);
                        PhotoPath = destinationPath;
                    }
                    catch (Exception photoEx)
                    {
                        // Log warning but continue
                        Console.WriteLine($"Photo copy warning: {photoEx.Message}");
                    }
                }

                var member = _editingMember ?? new FacultyStaff();
                member.PersonnelNumber = PersonnelNumber.Trim().ToUpperInvariant();
                member.Name = Name.Trim();
                member.FacultyType = SelectedFacultyType;
                member.Department = SelectedDepartment;
                member.Designation = Designation.Trim();
                member.Email = Email.Trim();
                member.Phone = Phone.Trim();
                member.CNIC = string.IsNullOrWhiteSpace(Cnic) ? null : Cnic.Trim();
                member.Address = Address.Trim();
                member.ActiveStatus = ActiveStatus;
                member.JoiningDate = JoiningDate;
                member.LeavingDate = LeavingDate;
                member.Remarks = string.IsNullOrWhiteSpace(Remarks) ? null : Remarks.Trim();

                if (_editingMember == null)
                {
                    await _consumerService.AddFacultyStaffAsync(member);
                }
                else
                {
                    await _consumerService.UpdateFacultyStaffAsync(member);
                }

                if (window != null)
                {
                    window.DialogResult = true;
                    window.Close();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to save member: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void Cancel(Window? window)
        {
            if (window != null)
            {
                window.DialogResult = false;
                window.Close();
            }
        }
    }
}
