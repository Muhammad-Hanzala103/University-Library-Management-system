using System;
using System.Collections.ObjectModel;
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

        // Summary Totals
        [ObservableProperty] private decimal _totalFine;
        [ObservableProperty] private decimal _totalPaidAmount;

        // Return Inputs
        [ObservableProperty] private string _waiverReason = string.Empty;
        [ObservableProperty] private string _remarks = string.Empty;
        [ObservableProperty] private string _paymentMode = "Pay Now";
        public ObservableCollection<string> PaymentModes { get; } = new() { "Pay Now", "Pay Later", "Waive" };

        public bool IsPayNowVisible => PaymentMode == "Pay Now";
        public bool IsWaiverVisible => PaymentMode == "Waive";

        public ObservableCollection<PendingReturnItemViewModel> PendingReturns { get; } = new();

        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private bool _isBusy;

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

        partial void OnPaymentModeChanged(string value)
        {
            OnPropertyChanged(nameof(IsPayNowVisible));
            OnPropertyChanged(nameof(IsWaiverVisible));
        }

        [RelayCommand]
        private async Task LoadDetailsAsync()
        {
            StatusMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(AccessionNumber))
            {
                StatusMessage = "Please enter or scan the material Accession Number.";
                return;
            }

            var cleanAccession = AccessionNumber.Trim();
            if (PendingReturns.Any(item => item.AccessionNumber.Equals(cleanAccession, StringComparison.OrdinalIgnoreCase)))
            {
                StatusMessage = $"Book '{cleanAccession}' is already added to the return list.";
                AccessionNumber = string.Empty;
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
                    .FirstOrDefaultAsync(ir => ir.AccessionNumber == cleanAccession && ir.ReceiveRecord == null && !ir.IsDeleted);

                if (issue != null)
                {
                    var item = new PendingReturnItemViewModel(issue, UpdateTotals);
                    PendingReturns.Add(item);
                    IsIssueFound = PendingReturns.Count > 0;
                    UpdateTotals();
                    AccessionNumber = string.Empty;
                }
                else
                {
                    StatusMessage = $"No active check-out record found for book '{cleanAccession}'.";
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

        private void UpdateTotals()
        {
            TotalFine = PendingReturns.Sum(item => item.CalculatedFine);
            TotalPaidAmount = TotalFine; // Default paid amount to total fine
        }

        [RelayCommand]
        private void RemoveItem(PendingReturnItemViewModel item)
        {
            if (item == null) return;
            PendingReturns.Remove(item);
            IsIssueFound = PendingReturns.Count > 0;
            UpdateTotals();
            StatusMessage = string.Empty;
        }

        [RelayCommand]
        private async Task ConfirmReturnAsync()
        {
            if (PendingReturns.Count == 0)
            {
                StatusMessage = "No books in the return list to process.";
                return;
            }

            string? finalWaiverReason = null;
            if (PaymentMode == "Waive" && TotalFine > 0)
            {
                if (string.IsNullOrWhiteSpace(WaiverReason))
                {
                    StatusMessage = "Waiver Reason is required when waiving fine.";
                    return;
                }
                var currentUserId = _authService.CurrentUser?.Id ?? 1;
                var isAuthorized = await _authService.VerifyUserPermissionAsync(currentUserId, "MANAGE_FINES");
                if (!isAuthorized)
                {
                    StatusMessage = "Authorization failed. Only users with MANAGE_FINES permission can waive fines.";
                    return;
                }
                finalWaiverReason = WaiverReason.Trim();
            }

            IsBusy = true;
            try
            {
                var userId = _authService.CurrentUser?.Id ?? 1;
                int successfulReturns = 0;

                foreach (var item in PendingReturns.ToList())
                {
                    decimal itemPaidAmount = 0;
                    if (PaymentMode == "Pay Now")
                    {
                        itemPaidAmount = item.CalculatedFine;
                    }

                    var bookMasterId = item.Issue.BookCopy.BookMasterId;
                    
                    var record = await _circulationService.ReceiveBookAsync(
                        item.AccessionNumber,
                        item.SelectedCondition,
                        itemPaidAmount,
                        finalWaiverReason,
                        Remarks.Trim(),
                        userId
                    );

                    await _reservationService.MarkReservationAvailableAsync(bookMasterId);
                    successfulReturns++;
                }

                StatusMessage = $"{successfulReturns} book(s) returned successfully";
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
        private void PrintReturnSlip()
        {
            if (PendingReturns.Count == 0)
            {
                StatusMessage = "Return list is empty. Add items first.";
                return;
            }

            var slip = new System.Text.StringBuilder();
            slip.AppendLine($"========================================");
            slip.AppendLine($"         ILM-O-KUTUB RETURN SLIP        ");
            slip.AppendLine($"========================================");
            slip.AppendLine($"Date: {DateTime.Now:dd-MMM-yyyy HH:mm}");
            slip.AppendLine($"Staff: {_authService.CurrentUser?.FullName ?? "Administrator"}");
            slip.AppendLine($"Payment Mode: {PaymentMode}");
            if (PaymentMode == "Waive")
            {
                slip.AppendLine($"Waiver Reason: {WaiverReason}");
            }
            slip.AppendLine($"----------------------------------------");
            foreach (var item in PendingReturns)
            {
                slip.AppendLine($"Accession: {item.AccessionNumber}");
                slip.AppendLine($"Title: {item.BookTitle}");
                slip.AppendLine($"Borrower: {item.BorrowerName} ({item.BorrowerTypeDisplay})");
                slip.AppendLine($"Condition: {item.SelectedCondition}");
                slip.AppendLine($"Fine: Rs. {item.CalculatedFine:N0}");
                slip.AppendLine($"----------------------------------------");
            }
            slip.AppendLine($"Total Fine: Rs. {TotalFine:N0}");
            if (PaymentMode == "Pay Now")
            {
                slip.AppendLine($"Total Paid: Rs. {TotalPaidAmount:N0}");
            }
            else
            {
                slip.AppendLine($"Total Paid: Rs. 0");
            }
            slip.AppendLine($"========================================");
            
            StatusMessage = slip.ToString();
        }

        [RelayCommand]
        private void ClearAll()
        {
            AccessionNumber = string.Empty;
            IsIssueFound = false;
            PendingReturns.Clear();
            TotalFine = 0;
            TotalPaidAmount = 0;
            WaiverReason = string.Empty;
            Remarks = string.Empty;
            PaymentMode = "Pay Now";
        }
    }
}
