using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Desktop.Helpers;

namespace KicsitLibrary.Desktop.ViewModels
{
    public partial class ConsumerProfileViewModel : ObservableObject
    {
        private readonly IConsumerService _consumerService;
        private readonly int _memberId;
        private readonly MemberType _memberType;

        // Details Display
        [ObservableProperty] private string _memberName = string.Empty;
        [ObservableProperty] private string _identifierLabel = string.Empty;
        [ObservableProperty] private string _identifierValue = string.Empty;
        [ObservableProperty] private string _department = string.Empty;
        [ObservableProperty] private string _memberTypeDisplay = string.Empty;
        [ObservableProperty] private string _programOrDesignationLabel = string.Empty;
        [ObservableProperty] private string _programOrDesignationValue = string.Empty;
        [ObservableProperty] private string _clearanceDisplay = string.Empty;
        [ObservableProperty] private string _email = string.Empty;
        [ObservableProperty] private string _phone = string.Empty;
        [ObservableProperty] private string _cnic = string.Empty;
        [ObservableProperty] private string _address = string.Empty;
        [ObservableProperty] private string _photoPath = string.Empty;
        [ObservableProperty] private string _statusDisplay = "Active";

        // Lists
        [ObservableProperty] private ObservableCollection<IssueRecord> _activeIssues = new();
        [ObservableProperty] private ObservableCollection<IssueRecord> _returnHistory = new();
        [ObservableProperty] private ObservableCollection<Fine> _fines = new();
        [ObservableProperty] private ObservableCollection<Reservation> _reservations = new();

        // Vector Images
        [ObservableProperty] private DrawingImage? _barcodeImage;
        [ObservableProperty] private DrawingImage? _qrCodeImage;

        [ObservableProperty] private string _errorMessage = string.Empty;
        [ObservableProperty] private bool _isBusy;

        public ConsumerProfileViewModel(IConsumerService consumerService, int memberId, MemberType memberType)
        {
            _consumerService = consumerService ?? throw new ArgumentNullException(nameof(consumerService));
            _memberId = memberId;
            _memberType = memberType;

            _ = LoadProfileAsync();
        }

        private async Task LoadProfileAsync()
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                string identifier = string.Empty;

                if (_memberType == MemberType.Student)
                {
                    var student = await _consumerService.GetStudentByIdAsync(_memberId);
                    if (student != null)
                    {
                        MemberName = student.Name;
                        IdentifierLabel = "Registration No";
                        IdentifierValue = student.RegistrationNumber;
                        identifier = student.RegistrationNumber;
                        Department = student.Department;
                        MemberTypeDisplay = "Student";
                        ProgramOrDesignationLabel = "Program / Batch";
                        ProgramOrDesignationValue = $"{student.Program} ({student.Batch})";
                        ClearanceDisplay = student.ClearanceStatus.ToString();
                        Email = student.Email;
                        Phone = student.Phone;
                        Cnic = string.IsNullOrWhiteSpace(student.CNIC) ? "N/A" : KicsitLibrary.Core.Helpers.LibraryValidator.FormatCnic(student.CNIC);
                        Address = student.Address;
                        PhotoPath = student.PhotoPath ?? string.Empty;
                        StatusDisplay = student.ActiveStatus ? "Active" : "Inactive";
                    }
                }
                else
                {
                    var fs = await _consumerService.GetFacultyStaffByIdAsync(_memberId);
                    if (fs != null)
                    {
                        MemberName = fs.Name;
                        IdentifierLabel = "Personnel No";
                        IdentifierValue = fs.PersonnelNumber;
                        identifier = fs.PersonnelNumber;
                        Department = fs.Department;
                        MemberTypeDisplay = fs.FacultyType.ToString();
                        ProgramOrDesignationLabel = "Designation";
                        ProgramOrDesignationValue = fs.Designation;
                        ClearanceDisplay = fs.ActiveStatus ? "Cleared" : "Suspended";
                        Email = fs.Email;
                        Phone = fs.Phone;
                        Cnic = string.IsNullOrWhiteSpace(fs.CNIC) ? "N/A" : KicsitLibrary.Core.Helpers.LibraryValidator.FormatCnic(fs.CNIC);
                        Address = fs.Address;
                        StatusDisplay = fs.ActiveStatus ? "Active" : "Inactive";

                        // Load photo from local path
                        var appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KicsitLibrary", "Photos");
                        var expectedPhoto = Path.Combine(appDataDir, $"Faculty-{fs.PersonnelNumber}.jpg");
                        if (File.Exists(expectedPhoto))
                        {
                            PhotoPath = expectedPhoto;
                        }
                    }
                }

                // Generate vector codes
                if (!string.IsNullOrEmpty(identifier))
                {
                    BarcodeImage = BarcodeGenerator.GenerateCode39(identifier);
                    QrCodeImage = QrCodeGenerator.GenerateQRCode(identifier);
                }

                // Load History
                var issues = await _consumerService.GetBorrowingHistoryAsync(_memberId, _memberType);
                ActiveIssues.Clear();
                ReturnHistory.Clear();
                foreach (var issue in issues)
                {
                    if (issue.ReceiveRecord == null)
                    {
                        ActiveIssues.Add(issue);
                    }
                    else
                    {
                        ReturnHistory.Add(issue);
                    }
                }

                var fineRecords = await _consumerService.GetFineHistoryAsync(_memberId, _memberType);
                Fines.Clear();
                foreach (var fine in fineRecords)
                {
                    Fines.Add(fine);
                }

                var reservationRecords = await _consumerService.GetReservationHistoryAsync(_memberId, _memberType);
                Reservations.Clear();
                foreach (var res in reservationRecords)
                {
                    Reservations.Add(res);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load consumer profile: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
