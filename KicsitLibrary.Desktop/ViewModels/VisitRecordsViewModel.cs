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
    public partial class VisitRecordsViewModel : ObservableObject
    {
        private readonly IConsumerService _consumerService;
        private readonly IAuthenticationService _authService;

        [ObservableProperty]
        private ObservableCollection<VisitRecord> _visitRecords = new();

        // Search Filter Properties
        [ObservableProperty]
        private string _searchOrganization = string.Empty;

        [ObservableProperty]
        private DateTime? _searchDate;

        [ObservableProperty]
        private string _searchPurpose = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        public VisitRecordsViewModel(IConsumerService consumerService, IAuthenticationService authService)
        {
            _consumerService = consumerService ?? throw new ArgumentNullException(nameof(consumerService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));

            _ = SearchAsync();
        }

        [RelayCommand]
        public async Task SearchAsync()
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                var results = await _consumerService.SearchVisitRecordsAsync(
                    string.IsNullOrWhiteSpace(SearchOrganization) ? null : SearchOrganization.Trim(),
                    SearchDate,
                    string.IsNullOrWhiteSpace(SearchPurpose) ? null : SearchPurpose.Trim()
                );

                VisitRecords.Clear();
                foreach (var rec in results)
                {
                    VisitRecords.Add(rec);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load visit records: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ClearFiltersAsync()
        {
            SearchOrganization = string.Empty;
            SearchDate = null;
            SearchPurpose = string.Empty;

            await SearchAsync();
        }

        [RelayCommand]
        private void AddVisit()
        {
            var vm = new VisitRecordFormViewModel(_consumerService, _authService, null);
            var window = new Views.VisitRecordWindow(vm);
            if (window.ShowDialog() == true)
            {
                _ = SearchAsync();
            }
        }

        [RelayCommand]
        private void EditVisit(VisitRecord? record)
        {
            if (record == null) return;

            var vm = new VisitRecordFormViewModel(_consumerService, _authService, record);
            var window = new Views.VisitRecordWindow(vm);
            if (window.ShowDialog() == true)
            {
                _ = SearchAsync();
            }
        }

        [RelayCommand]
        private async Task DeleteVisitAsync(VisitRecord? record)
        {
            if (record == null) return;

            var confirm = MessageBox.Show($"Are you sure you want to delete visit record '{record.VisitNumber}' for '{record.OrganizationName}'?",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                IsBusy = true;
                try
                {
                    var userId = _authService.CurrentUser?.Id ?? 1;
                    await _consumerService.DeleteVisitRecordAsync(record.Id, "Deleted by user from UI list", userId);
                    await SearchAsync();
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"Failed to delete visit record: {ex.Message}";
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }
    }
}
