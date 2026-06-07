using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Interfaces;

namespace KicsitLibrary.Desktop.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly INavigationService _navigationService;
        private readonly IAuthenticationService _authService;
        private IServiceScope? _currentScope;

        [ObservableProperty]
        private object? _currentView;

        [ObservableProperty]
        private string _title = "Dashboard";

        [ObservableProperty]
        private string _currentUserName = "Administrator";

        [ObservableProperty]
        private string _currentUserRole = "Super Admin";

        public MainViewModel(INavigationService navigationService, IAuthenticationService authService)
        {
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));

            _navigationService.NavigationChanged += OnNavigationChanged;

            LoadUserDetails();

            Title = _navigationService.CurrentViewName;
            
            // Force load the initial dashboard view
            OnNavigationChanged("Dashboard");
        }

        private void LoadUserDetails()
        {
            if (_authService.CurrentUser != null)
            {
                CurrentUserName = _authService.CurrentUser.FullName;
                
                var userRole = _authService.CurrentUser.UserRoles.FirstOrDefault();
                CurrentUserRole = userRole?.Role?.Name ?? "Read Only Viewer";
            }
        }

        private void OnNavigationChanged(string viewName)
        {
            Title = viewName;
            
            _currentScope?.Dispose();
            _currentScope = null;
            
            switch (viewName)
            {
                case "Dashboard":
                    _currentScope = App.AppHost?.Services.CreateScope();
                    CurrentView = _currentScope?.ServiceProvider.GetService<DashboardViewModel>();
                    break;
                case "Book Catalog":
                    _currentScope = App.AppHost?.Services.CreateScope();
                    CurrentView = _currentScope?.ServiceProvider.GetService<BookCatalogViewModel>();
                    break;
                case "Students Management":
                    _currentScope = App.AppHost?.Services.CreateScope();
                    CurrentView = _currentScope?.ServiceProvider.GetService<StudentsManagementViewModel>();
                    break;
                case "Faculty & Staff":
                    _currentScope = App.AppHost?.Services.CreateScope();
                    CurrentView = _currentScope?.ServiceProvider.GetService<FacultyStaffManagementViewModel>();
                    break;
                case "Visit Records":
                    _currentScope = App.AppHost?.Services.CreateScope();
                    CurrentView = _currentScope?.ServiceProvider.GetService<VisitRecordsViewModel>();
                    break;
                case "Issue Material":
                    _currentScope = App.AppHost?.Services.CreateScope();
                    CurrentView = _currentScope?.ServiceProvider.GetService<IssueMaterialViewModel>();
                    break;
                case "Receive Material":
                    _currentScope = App.AppHost?.Services.CreateScope();
                    CurrentView = _currentScope?.ServiceProvider.GetService<ReceiveMaterialViewModel>();
                    break;
                case "Fines Management":
                    _currentScope = App.AppHost?.Services.CreateScope();
                    CurrentView = _currentScope?.ServiceProvider.GetService<FinesManagementViewModel>();
                    break;
                case "Overdue Reminders":
                    _currentScope = App.AppHost?.Services.CreateScope();
                    CurrentView = _currentScope?.ServiceProvider.GetService<OverdueRemindersViewModel>();
                    break;
                case "Notification Center":
                    _currentScope = App.AppHost?.Services.CreateScope();
                    CurrentView = _currentScope?.ServiceProvider.GetService<NotificationCenterViewModel>();
                    break;
                case "Reports & Analytics":
                    _currentScope = App.AppHost?.Services.CreateScope();
                    CurrentView = _currentScope?.ServiceProvider.GetService<ReportsDashboardViewModel>();
                    break;
                case "Clearance":
                    _currentScope = App.AppHost?.Services.CreateScope();
                    CurrentView = _currentScope?.ServiceProvider.GetService<ClearanceDashboardViewModel>();
                    break;
                case "Reservations":
                    _currentScope = App.AppHost?.Services.CreateScope();
                    CurrentView = _currentScope?.ServiceProvider.GetService<ReservationManagementViewModel>();
                    if (CurrentView is ReservationManagementViewModel reservations)
                        _ = reservations.RefreshAsync();
                    break;
                case "Activity Logs":
                    _currentScope = App.AppHost?.Services.CreateScope();
                    CurrentView = _currentScope?.ServiceProvider.GetService<ActivityLogsViewModel>();
                    if (CurrentView is ActivityLogsViewModel activityLogs)
                        _ = activityLogs.RefreshAsync();
                    break;
                case "Audit Records":
                    _currentScope = App.AppHost?.Services.CreateScope();
                    CurrentView = _currentScope?.ServiceProvider.GetService<AuditRecordsViewModel>();
                    if (CurrentView is AuditRecordsViewModel auditRecords)
                        _ = auditRecords.RefreshAsync();
                    break;
                default:
                    CurrentView = null;
                    break;
            }
        }

        [RelayCommand]
        private void NavigateToDashboard() => _navigationService.NavigateTo("Dashboard");

        [RelayCommand]
        private void NavigateToCatalog() => _navigationService.NavigateTo("Book Catalog");

        [RelayCommand]
        private void NavigateToIssue() => _navigationService.NavigateTo("Issue Material");

        [RelayCommand]
        private void NavigateToReceive() => _navigationService.NavigateTo("Receive Material");

        [RelayCommand]
        private void NavigateToFines() => _navigationService.NavigateTo("Fines Management");

        [RelayCommand]
        private void NavigateToOverdue() => _navigationService.NavigateTo("Overdue Reminders");

        [RelayCommand]
        private void NavigateToNotifications() => _navigationService.NavigateTo("Notification Center");

        [RelayCommand]
        private void NavigateToStudents() => _navigationService.NavigateTo("Students Management");

        [RelayCommand]
        private void NavigateToFaculty() => _navigationService.NavigateTo("Faculty & Staff");

        [RelayCommand]
        private void NavigateToVisits() => _navigationService.NavigateTo("Visit Records");

        [RelayCommand]
        private void NavigateToAudit() => _navigationService.NavigateTo("Audit Records");

        [RelayCommand]
        private void NavigateToActivityLogs() => _navigationService.NavigateTo("Activity Logs");

        [RelayCommand]
        private void NavigateToInventory() => _navigationService.NavigateTo("Inventory Management");

        [RelayCommand]
        private void NavigateToReports() => _navigationService.NavigateTo("Reports & Analytics");

        [RelayCommand]
        private void NavigateToClearance() => _navigationService.NavigateTo("Clearance");

        [RelayCommand]
        private void NavigateToReservations() => _navigationService.NavigateTo("Reservations");

        [RelayCommand]
        private void NavigateToSettings() => _navigationService.NavigateTo("System Settings");

        [RelayCommand]
        private async Task LogoutAsync()
        {
            await _authService.LogoutAsync();
            
            var loginWindow = App.AppHost?.Services.GetRequiredService<LoginWindow>();
            var activeWindow = App.Current.MainWindow;
            
            if (activeWindow != null && loginWindow != null)
            {
                activeWindow.Hide();

                if (loginWindow.ShowDialog() == true)
                {
                    LoadUserDetails();
                    _navigationService.NavigateTo("Dashboard");
                    activeWindow.Show();
                }
                else
                {
                    App.Current.Shutdown();
                }
            }
        }
    }
}
