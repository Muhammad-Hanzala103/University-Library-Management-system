using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Desktop.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly IDashboardService _dashboardService;
        private readonly INavigationService _navigationService;

        [ObservableProperty]
        private DashboardStats _stats = new();

        [ObservableProperty]
        private bool _isBusy;

        public DashboardViewModel(IDashboardService dashboardService, INavigationService navigationService)
        {
            _dashboardService = dashboardService ?? throw new ArgumentNullException(nameof(dashboardService));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            
            // Execute async call safely in constructor
            _ = LoadStatsAsync();
        }

        [RelayCommand]
        public async Task LoadStatsAsync()
        {
            IsBusy = true;
            try
            {
                Stats = await _dashboardService.GetDashboardStatsAsync();
            }
            catch (Exception)
            {
                // Soft fail (falls back to default stats model)
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void QuickIssue() => _navigationService.NavigateTo("Issue Material");

        [RelayCommand]
        private void QuickReceive() => _navigationService.NavigateTo("Receive Material");

        [RelayCommand]
        private void QuickCatalog() => _navigationService.NavigateTo("Book Catalog");

        [RelayCommand]
        private void QuickStudents() => _navigationService.NavigateTo("Students");

        [RelayCommand]
        private void QuickFaculty() => _navigationService.NavigateTo("Faculty and Staff");

        [RelayCommand]
        private void QuickOverdue() => _navigationService.NavigateTo("Overdue Reminders");

        [RelayCommand]
        private void QuickFines() => _navigationService.NavigateTo("Fines");

        [RelayCommand]
        private void QuickSettings() => _navigationService.NavigateTo("Settings");
    }
}
