using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Desktop.Views;

namespace KicsitLibrary.Desktop.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly INavigationService _navigationService;
        private readonly IAuthenticationService _authService;
        private readonly IHintService _hintService;
        private IServiceScope? _currentScope;

        [ObservableProperty]
        private object? _currentView;

        [ObservableProperty]
        private string _title = "Dashboard";

        [ObservableProperty]
        private string _currentUserName = "Administrator";

        [ObservableProperty]
        private string _currentUserRole = "Super Admin";

        public bool ShowHelpfulHints
        {
            get => _hintService.ShowHelpfulHints;
            set
            {
                if (_hintService.ShowHelpfulHints == value)
                {
                    return;
                }

                _hintService.ShowHelpfulHints = value;
                OnPropertyChanged();
            }
        }

        public MainViewModel(
            INavigationService navigationService,
            IAuthenticationService authService,
            IHintService hintService)
        {
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _hintService = hintService ?? throw new ArgumentNullException(nameof(hintService));

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
                case "Students":
                    _currentScope = App.AppHost?.Services.CreateScope();
                    CurrentView = _currentScope?.ServiceProvider.GetService<StudentsManagementViewModel>();
                    break;
                case "Faculty and Staff":
                    _currentScope = App.AppHost?.Services.CreateScope();
                    CurrentView = _currentScope?.ServiceProvider.GetService<FacultyStaffManagementViewModel>();
                    break;
                case "Visits":
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
                case "Fines":
                    _currentScope = App.AppHost?.Services.CreateScope();
                    CurrentView = _currentScope?.ServiceProvider.GetService<FinesManagementViewModel>();
                    break;
                case "Overdue Reminders":
                    _currentScope = App.AppHost?.Services.CreateScope();
                    CurrentView = _currentScope?.ServiceProvider.GetService<OverdueRemindersViewModel>();
                    break;
                case "Notifications":
                    _currentScope = App.AppHost?.Services.CreateScope();
                    CurrentView = _currentScope?.ServiceProvider.GetService<NotificationCenterViewModel>();
                    break;
                case "Reports":
                    _currentScope = App.AppHost?.Services.CreateScope();
                    CurrentView = _currentScope?.ServiceProvider.GetService<ReportsDashboardViewModel>();
                    break;
                case "Library Clearance":
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
                case "Inventory":
                    _currentScope = App.AppHost?.Services.CreateScope();
                    CurrentView = _currentScope?.ServiceProvider.GetService<InventoryManagementViewModel>();
                    if (CurrentView is InventoryManagementViewModel inventory) _ = inventory.RefreshAsync();
                    break;
                case "Stock Verification":
                    _currentScope = App.AppHost?.Services.CreateScope();
                    CurrentView = _currentScope?.ServiceProvider.GetService<StockVerificationViewModel>();
                    if (CurrentView is StockVerificationViewModel stock) _ = stock.RefreshAsync();
                    break;
                case "Backup":
                    _currentScope = App.AppHost?.Services.CreateScope();
                    CurrentView = _currentScope?.ServiceProvider.GetService<BackupManagementViewModel>();
                    if (CurrentView is BackupManagementViewModel backups) _ = backups.RefreshAsync();
                    break;
                case "Restore":
                    _currentScope = App.AppHost?.Services.CreateScope();
                    CurrentView = _currentScope?.ServiceProvider.GetService<RestoreManagementViewModel>();
                    if (CurrentView is RestoreManagementViewModel restores) _ = restores.RefreshAsync();
                    break;
                case "Documents":
                    _currentScope = App.AppHost?.Services.CreateScope();
                    CurrentView = _currentScope?.ServiceProvider.GetService<DocumentManagementViewModel>();
                    if (CurrentView is DocumentManagementViewModel documents) _ = documents.RefreshAsync();
                    break;
                case "Settings":
                    _currentScope = App.AppHost?.Services.CreateScope();
                    CurrentView = _currentScope?.ServiceProvider.GetService<SettingsManagementViewModel>();
                    if (CurrentView is SettingsManagementViewModel settings) _ = settings.InitializeAsync();
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
        private void NavigateToFines() => _navigationService.NavigateTo("Fines");

        [RelayCommand]
        private void NavigateToOverdue() => _navigationService.NavigateTo("Overdue Reminders");

        [RelayCommand]
        private void NavigateToNotifications() => _navigationService.NavigateTo("Notifications");

        [RelayCommand]
        private void NavigateToStudents() => _navigationService.NavigateTo("Students");

        [RelayCommand]
        private void NavigateToFaculty() => _navigationService.NavigateTo("Faculty and Staff");

        [RelayCommand]
        private void NavigateToVisits() => _navigationService.NavigateTo("Visits");

        [RelayCommand]
        private void NavigateToAudit() => _navigationService.NavigateTo("Audit Records");

        [RelayCommand]
        private void NavigateToActivityLogs() => _navigationService.NavigateTo("Activity Logs");

        [RelayCommand]
        private void NavigateToInventory() => _navigationService.NavigateTo("Inventory");

        [RelayCommand]
        private void NavigateToStockVerification() => _navigationService.NavigateTo("Stock Verification");

        [RelayCommand]
        private void NavigateToBackups() => _navigationService.NavigateTo("Backup");

        [RelayCommand]
        private void NavigateToRestores() => _navigationService.NavigateTo("Restore");

        [RelayCommand]
        private void NavigateToDocuments() => _navigationService.NavigateTo("Documents");

        [RelayCommand]
        private void NavigateToReports() => _navigationService.NavigateTo("Reports");

        [RelayCommand]
        private void NavigateToClearance() => _navigationService.NavigateTo("Library Clearance");

        [RelayCommand]
        private void NavigateToReservations() => _navigationService.NavigateTo("Reservations");

        [RelayCommand]
        private void NavigateToSettings() => _navigationService.NavigateTo("Settings");

        [RelayCommand]
        private void ChangePassword()
        {
            var activeWindow = App.Current.MainWindow;
            var changePasswordWindow = App.AppHost?.Services.GetRequiredService<ChangePasswordWindow>();
            if (changePasswordWindow != null)
            {
                changePasswordWindow.Owner = activeWindow;
                changePasswordWindow.ShowDialog();
            }
        }

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
