using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace KicsitLibrary.Desktop.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private object? _currentView;

        [ObservableProperty]
        private string _title = "Dashboard";

        [ObservableProperty]
        private string _currentUserName = "Administrator";

        [ObservableProperty]
        private string _currentUserRole = "Super Admin";

        public MainViewModel()
        {
            NavigateToDashboard();
        }

        [RelayCommand]
        private void NavigateToDashboard()
        {
            Title = "Dashboard";
        }

        [RelayCommand]
        private void NavigateToCatalog()
        {
            Title = "Book Catalog";
        }

        [RelayCommand]
        private void NavigateToIssue()
        {
            Title = "Issue Material";
        }

        [RelayCommand]
        private void NavigateToReceive()
        {
            Title = "Receive Material";
        }

        [RelayCommand]
        private void NavigateToFines()
        {
            Title = "Fines Management";
        }

        [RelayCommand]
        private void NavigateToOverdue()
        {
            Title = "Overdue Reminders";
        }

        [RelayCommand]
        private void NavigateToStudents()
        {
            Title = "Students Management";
        }

        [RelayCommand]
        private void NavigateToFaculty()
        {
            Title = "Faculty & Staff";
        }

        [RelayCommand]
        private void NavigateToVisits()
        {
            Title = "Visit Records";
        }

        [RelayCommand]
        private void NavigateToAudit()
        {
            Title = "Audit Records";
        }

        [RelayCommand]
        private void NavigateToInventory()
        {
            Title = "Inventory Management";
        }

        [RelayCommand]
        private void NavigateToReports()
        {
            Title = "Reports & Analytics";
        }

        [RelayCommand]
        private void NavigateToSettings()
        {
            Title = "System Settings";
        }
    }
}
