using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Interfaces;

namespace KicsitLibrary.Desktop.ViewModels
{
    public partial class FinesManagementViewModel : ObservableObject
    {
        private readonly ICirculationService _circulationService;
        private readonly IAuthenticationService _authService;

        [ObservableProperty]
        private ObservableCollection<Fine> _unpaidFines = new();

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private Fine? _selectedFine;

        // Collect payment values
        [ObservableProperty] private decimal _paymentAmount;
        [ObservableProperty] private string _paymentRemarks = string.Empty;

        // Waiver values
        [ObservableProperty] private decimal _waiveAmount;
        [ObservableProperty] private string _waiverReason = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        public FinesManagementViewModel(ICirculationService circulationService, IAuthenticationService authService)
        {
            _circulationService = circulationService ?? throw new ArgumentNullException(nameof(circulationService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));

            _ = SearchFinesAsync();
        }

        [RelayCommand]
        public async Task SearchFinesAsync()
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                var results = await _circulationService.GetActiveFinesAsync(
                    string.IsNullOrWhiteSpace(SearchQuery) ? null : SearchQuery.Trim()
                );

                UnpaidFines.Clear();
                foreach (var fine in results)
                {
                    UnpaidFines.Add(fine);
                }

                SelectedFine = null;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to search fines: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        async partial void OnSelectedFineChanged(Fine? value)
        {
            if (value != null)
            {
                PaymentAmount = value.RemainingAmount;
                WaiveAmount = value.RemainingAmount;
            }
            else
            {
                PaymentAmount = 0;
                WaiveAmount = 0;
            }
            PaymentRemarks = string.Empty;
            WaiverReason = string.Empty;
        }

        [RelayCommand]
        private async Task CollectPaymentAsync()
        {
            if (SelectedFine == null)
            {
                ErrorMessage = "Please select a fine entry from the list.";
                return;
            }

            if (PaymentAmount <= 0)
            {
                ErrorMessage = "Payment amount must be greater than zero.";
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                var userId = _authService.CurrentUser?.Id ?? 1;
                await _circulationService.CollectFinePaymentAsync(
                    SelectedFine.Id,
                    PaymentAmount,
                    string.IsNullOrWhiteSpace(PaymentRemarks) ? null : PaymentRemarks.Trim(),
                    userId
                );

                MessageBox.Show($"Collected Rs. {PaymentAmount:N0} successfully against {SelectedFine.FineRecordNumber}.", "Payment Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                await SearchFinesAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Payment failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task WaiveFineAsync()
        {
            if (SelectedFine == null)
            {
                ErrorMessage = "Please select a fine entry from the list.";
                return;
            }

            if (WaiveAmount <= 0)
            {
                ErrorMessage = "Waiver amount must be greater than zero.";
                return;
            }

            if (string.IsNullOrWhiteSpace(WaiverReason))
            {
                ErrorMessage = "Waiver reason is required.";
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                var userId = _authService.CurrentUser?.Id ?? 1;
                await _circulationService.WaiveFineAsync(
                    SelectedFine.Id,
                    WaiveAmount,
                    WaiverReason.Trim(),
                    userId
                );

                MessageBox.Show($"Waived Rs. {WaiveAmount:N0} successfully against {SelectedFine.FineRecordNumber}.", "Fine Waived", MessageBoxButton.OK, MessageBoxImage.Information);
                await SearchFinesAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Waiver failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
