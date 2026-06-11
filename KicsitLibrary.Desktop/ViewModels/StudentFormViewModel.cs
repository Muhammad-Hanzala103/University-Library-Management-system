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
    public partial class StudentFormViewModel : ObservableObject
    {
        private readonly IConsumerService _consumerService;
        private readonly ICatalogService _catalogService;
        private readonly Student? _editingStudent;

        [ObservableProperty]
        private string _windowTitle = "Register New Student";

        // Lists for combo boxes
        public ObservableCollection<string> Programs { get; } = new(KicsitLibrary.Core.Helpers.LibraryValidator.Programs);
        public ObservableCollection<string> Departments { get; } = new();
        public ObservableCollection<string> Batches { get; } = new() { "2022", "2023", "2024", "2025", "2026" };

        // Form Fields
        [ObservableProperty]
        private string _registrationNumber = string.Empty;

        [ObservableProperty]
        private string _admissionNumber = string.Empty;

        [ObservableProperty]
        private string _rollNumber = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _fatherName = string.Empty;

        [ObservableProperty]
        private string _selectedProgram = "BSCS";

        [ObservableProperty]
        private string _selectedDepartment = "Computer Science";

        [ObservableProperty]
        private string _selectedBatch = "2024";

        [ObservableProperty]
        private string _semester = "1st";

        [ObservableProperty]
        private string _session = "Morning";

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        private string _phone = string.Empty;

        [ObservableProperty]
        private string _cnic = string.Empty;

        [ObservableProperty]
        private string _address = string.Empty;

        [ObservableProperty]
        private string _photoPath = string.Empty;

        [ObservableProperty]
        private int _pageNumber;

        [ObservableProperty]
        private int _registerNumber;

        [ObservableProperty]
        private bool _activeStatus = true;

        [ObservableProperty]
        private ClearanceStatus _clearanceStatus = ClearanceStatus.NotCleared;

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

        public StudentFormViewModel(IConsumerService consumerService, ICatalogService catalogService, Student? editingStudent)
        {
            _consumerService = consumerService ?? throw new ArgumentNullException(nameof(consumerService));
            _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
            _editingStudent = editingStudent;

            LoadDepartments();

            if (_editingStudent != null)
            {
                WindowTitle = $"Edit Student: {_editingStudent.Name}";
                RegistrationNumber = _editingStudent.RegistrationNumber;
                AdmissionNumber = _editingStudent.AdmissionNumber;
                RollNumber = _editingStudent.RollNumber;
                Name = _editingStudent.Name;
                FatherName = _editingStudent.FatherName;
                SelectedProgram = _editingStudent.Program;
                SelectedDepartment = _editingStudent.Department;
                SelectedBatch = _editingStudent.Batch;
                Semester = _editingStudent.Semester;
                Session = _editingStudent.Session;
                Email = _editingStudent.Email;
                Phone = _editingStudent.Phone;
                Cnic = KicsitLibrary.Core.Helpers.LibraryValidator.FormatCnic(_editingStudent.CNIC);
                Address = _editingStudent.Address;
                PhotoPath = _editingStudent.PhotoPath ?? string.Empty;
                PageNumber = _editingStudent.PageNumber;
                RegisterNumber = _editingStudent.RegisterNumber;
                ActiveStatus = _editingStudent.ActiveStatus;
                ClearanceStatus = _editingStudent.ClearanceStatus;
            }
        }

        private void LoadDepartments()
        {
            Departments.Clear();
            foreach (var d in KicsitLibrary.Core.Helpers.LibraryValidator.Departments)
            {
                Departments.Add(d);
            }

            if (_editingStudent != null)
            {
                SelectedDepartment = _editingStudent.Department;
            }
            else if (Departments.Count > 0)
            {
                SelectedDepartment = Departments[0];
            }
        }

        [RelayCommand]
        private void SelectPhoto()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image Files (*.jpg; *.jpeg; *.png; *.webp)|*.jpg;*.jpeg;*.png;*.webp",
                Title = "Select Student Profile Photo"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Copy to app profile directory or set absolute path
                    var appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KicsitLibrary", "Photos");
                    if (!Directory.Exists(appDataDir))
                    {
                        Directory.CreateDirectory(appDataDir);
                    }

                    var fileExtension = Path.GetExtension(openFileDialog.FileName);
                    var newFileName = $"{Guid.NewGuid()}{fileExtension}";
                    var destinationPath = Path.Combine(appDataDir, newFileName);

                    File.Copy(openFileDialog.FileName, destinationPath, true);
                    PhotoPath = destinationPath;
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"Failed to copy photo: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private async Task SaveAsync(Window? window)
        {
            if (string.IsNullOrWhiteSpace(RegistrationNumber))
            {
                ErrorMessage = "Registration Number is required.";
                return;
            }
            if (!KicsitLibrary.Core.Helpers.LibraryValidator.IsRegistrationNumberValid(RegistrationNumber))
            {
                ErrorMessage = "Registration Number must contain numbers only.";
                return;
            }
            if (string.IsNullOrWhiteSpace(SelectedProgram) || !KicsitLibrary.Core.Helpers.LibraryValidator.Programs.Contains(SelectedProgram))
            {
                ErrorMessage = "Invalid Program selection.";
                return;
            }
            if (string.IsNullOrWhiteSpace(SelectedDepartment) || !KicsitLibrary.Core.Helpers.LibraryValidator.Departments.Contains(SelectedDepartment))
            {
                ErrorMessage = "Invalid Department selection.";
                return;
            }
            if (string.IsNullOrWhiteSpace(Name))
            {
                ErrorMessage = "Student Name is required.";
                return;
            }
            if (string.IsNullOrWhiteSpace(RollNumber))
            {
                ErrorMessage = "Roll Number is required.";
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                // Validate duplicate Reg No
                var isRegDuplicate = await _consumerService.IsStudentRegistrationNumberDuplicateAsync(
                    RegistrationNumber.Trim(), _editingStudent?.Id);
                if (isRegDuplicate)
                {
                    ErrorMessage = $"Registration Number '{RegistrationNumber}' already exists.";
                    IsBusy = false;
                    return;
                }

                // Validate duplicate CNIC if filled
                if (!string.IsNullOrWhiteSpace(Cnic))
                {
                    var cnicRegex = new System.Text.RegularExpressions.Regex(@"^\d{5}-\d{7}-\d{1}$");
                    if (!cnicRegex.IsMatch(Cnic.Trim()))
                    {
                        ErrorMessage = "CNIC format must be #####-#######-#.";
                        IsBusy = false;
                        return;
                    }

                    var isCnicDuplicate = await _consumerService.IsStudentCNICDuplicateAsync(
                        Cnic.Trim(), _editingStudent?.Id);
                    if (isCnicDuplicate)
                    {
                        ErrorMessage = $"CNIC '{Cnic}' is already registered to another member.";
                        IsBusy = false;
                        return;
                    }
                }

                // Validate email format and duplicates if filled
                if (!string.IsNullOrWhiteSpace(Email))
                {
                    var emailRegex = new System.Text.RegularExpressions.Regex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$");
                    if (!emailRegex.IsMatch(Email.Trim()))
                    {
                        ErrorMessage = "Email format is invalid.";
                        IsBusy = false;
                        return;
                    }

                    var isEmailDuplicate = await _consumerService.IsStudentEmailDuplicateAsync(
                        Email.Trim(), _editingStudent?.Id);
                    if (isEmailDuplicate)
                    {
                        ErrorMessage = $"Email '{Email}' is already registered to another student.";
                        IsBusy = false;
                        return;
                    }
                }

                // Validate phone format if filled
                if (!string.IsNullOrWhiteSpace(Phone))
                {
                    var phoneRegex = new System.Text.RegularExpressions.Regex(@"^\+?[0-9\s\-]{7,15}$");
                    if (!phoneRegex.IsMatch(Phone.Trim()))
                    {
                        ErrorMessage = "Phone number is invalid. Must be 7-15 digits.";
                        IsBusy = false;
                        return;
                    }
                }

                var student = _editingStudent ?? new Student();
                student.RegistrationNumber = RegistrationNumber.Trim().ToUpperInvariant();
                student.AdmissionNumber = AdmissionNumber.Trim();
                student.RollNumber = RollNumber.Trim().ToUpperInvariant();
                student.Name = Name.Trim();
                student.FatherName = FatherName.Trim();
                student.Program = SelectedProgram;
                student.Department = SelectedDepartment;
                student.Batch = SelectedBatch;
                student.Semester = Semester.Trim();
                student.Session = Session.Trim();
                student.Email = Email.Trim();
                student.Phone = Phone.Trim();
                student.CNIC = string.IsNullOrWhiteSpace(Cnic) ? null : Cnic.Trim();
                student.Address = Address.Trim();
                student.PhotoPath = string.IsNullOrWhiteSpace(PhotoPath) ? null : PhotoPath;
                student.PageNumber = PageNumber;
                student.RegisterNumber = RegisterNumber;
                student.ActiveStatus = ActiveStatus;
                student.ClearanceStatus = ClearanceStatus;

                if (_editingStudent == null)
                {
                    await _consumerService.AddStudentAsync(student);
                }
                else
                {
                    await _consumerService.UpdateStudentAsync(student);
                }

                if (window != null)
                {
                    window.DialogResult = true;
                    window.Close();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to save student: {ex.Message}";
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
