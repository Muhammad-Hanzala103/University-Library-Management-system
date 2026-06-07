using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Data;

namespace KicsitLibrary.Desktop.ViewModels
{
    public partial class ReceiveMaterialViewModel : ObservableObject
    {
        private readonly ICirculationService _circulationService;
        private readonly IAuthenticationService _authService;
        private readonly IReservationService _reservationService;
        private readonly KicsitLibraryDbContext _context;

        [ObservableProperty] private string _accessionNumber = string.Empty;
        [ObservableProperty] private bool _isIssueFound;

        // Details Display
        [ObservableProperty] private string _bookTitle = string.Empty;
        [ObservableProperty] private string _borrowerName = string.Empty;
        [ObservableProperty] private string _borrowerTypeDisplay = string.Empty;
        [ObservableProperty] private string _issueDateDisplay = string.Empty;
        [ObservableProperty] private string _dueDateDisplay = string.Empty;
        [ObservableProperty] private int _overdueDays;
        [ObservableProperty] private decimal _calculatedFine;

        // Return Inputs
        [ObservableProperty] private string _selectedCondition = "Normal";
        [ObservableProperty] private decimal _paidAmount;
        [ObservableProperty] private string _waiverReason = string.Empty;
        [ObservableProperty] private string _remarks = string.Empty;

        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private bool _isBusy;

        private IssueRecord? _loadedIssue;

        public ReceiveMaterialViewModel(
            ICirculationService circulationService,
            IAuthenticationService authService,
            IReservationService reservationService,
            KicsitLibraryDbContext context)
        {
            _circulationService = circulationService ?? throw new ArgumentNullException(nameof(circulationService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _reservationService = reservationService ?? throw new ArgumentNullException(nameof(reservationService));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        [RelayCommand]
        private async Task LoadDetailsAsync()
        {
            StatusMessage = string.Empty;
            IsIssueFound = false;
            _loadedIssue = null;
            BookTitle = string.Empty;
            BorrowerName = string.Empty;
            CalculatedFine = 0;
            OverdueDays = 0;
            PaidAmount = 0;
            WaiverReason = string.Empty;
            Remarks = string.Empty;

            if (string.IsNullOrWhiteSpace(AccessionNumber))
            {
                StatusMessage = "Please enter or scan the material Accession Number.";
                return;
            }

            IsBusy = true;
            try
            {
                // Find active issue record
                var issue = await _context.IssueRecords
                    .Include(ir => ir.BookCopy).ThenInclude(bc => bc.BookMaster)
                    .Include(ir => ir.Student)
                    .Include(ir => ir.FacultyStaff)
                    .Include(ir => ir.ReceiveRecord)
                    .FirstOrDefaultAsync(ir => ir.AccessionNumber == AccessionNumber.Trim() && ir.ReceiveRecord == null && !ir.IsDeleted);

                if (issue != null)
                {
                    _loadedIssue = issue;
                    BookTitle = issue.BookCopy.BookMaster.Title;
                    BorrowerName = issue.MemberType == MemberType.Student ? (issue.Student?.Name ?? "Student") : (issue.FacultyStaff?.Name ?? "Faculty/Staff");
                    BorrowerTypeDisplay = issue.MemberType.ToString();
                    IssueDateDisplay = issue.IssueDate.ToString("dd-MMM-yyyy");
                    DueDateDisplay = issue.ExpectedReturnDate.ToString("dd-MMM-yyyy");

                    if (DateTime.UtcNow > issue.ExpectedReturnDate)
                    {
                        OverdueDays = (int)(DateTime.UtcNow - issue.ExpectedReturnDate).TotalDays;
                        CalculatedFine = OverdueDays * issue.FinePerDay;
                    }
                    else
                    {
                        OverdueDays = 0;
                        CalculatedFine = 0;
                    }

                    IsIssueFound = true;
                    UpdateCalculatedFine();
                }
                else
                {
                    StatusMessage = $"No active check-out record found for book '{AccessionNumber.Trim()}'.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load active loan details: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        async partial void OnSelectedConditionChanged(string value)
        {
            UpdateCalculatedFine();
        }

        private void UpdateCalculatedFine()
        {
            if (_loadedIssue == null) return;

            decimal baseFine = 0;
            if (DateTime.UtcNow > _loadedIssue.ExpectedReturnDate)
            {
                var days = (int)(DateTime.UtcNow - _loadedIssue.ExpectedReturnDate).TotalDays;
                baseFine = days * _loadedIssue.FinePerDay;
            }

            if (SelectedCondition == "Lost" || SelectedCondition == "Damaged")
            {
                var bookPrice = _loadedIssue.BookCopy.BookMaster.PurchasePrice;
                CalculatedFine = baseFine + bookPrice + 200; // Book Price + Rs.200 surcharge
            }
            else
            {
                CalculatedFine = baseFine;
            }

            PaidAmount = CalculatedFine; // Default paid amount to total fine
        }

        [RelayCommand]
        private async Task ConfirmReturnAsync()
        {
            if (!IsIssueFound || _loadedIssue == null)
            {
                StatusMessage = "No active borrowing record loaded.";
                return;
            }

            IsBusy = true;
            try
            {
                var userId = _authService.CurrentUser?.Id ?? 1;
                var bookMasterId = _loadedIssue.BookCopy.BookMasterId;
                var record = await _circulationService.ReceiveBookAsync(
                    AccessionNumber.Trim(),
                    SelectedCondition,
                    PaidAmount,
                    string.IsNullOrWhiteSpace(WaiverReason) ? null : WaiverReason.Trim(),
                    Remarks.Trim(),
                    userId
                );

                var availability = await _reservationService.MarkReservationAvailableAsync(bookMasterId);
                StatusMessage = availability.Succeeded
                    ? $"Material check-in successful. Reservation queue updated. Transaction ID {record.Id}."
                    : $"Material check-in successful. Transaction ID {record.Id}. {availability.ErrorMessage}";
                ClearAll();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Check-in failed: {ex.Message}";
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
            IsIssueFound = false;
            _loadedIssue = null;
            BookTitle = string.Empty;
            BorrowerName = string.Empty;
            BorrowerTypeDisplay = string.Empty;
            IssueDateDisplay = string.Empty;
            DueDateDisplay = string.Empty;
            OverdueDays = 0;
            CalculatedFine = 0;

            SelectedCondition = "Normal";
            PaidAmount = 0;
            WaiverReason = string.Empty;
            Remarks = string.Empty;
        }
    }
}
