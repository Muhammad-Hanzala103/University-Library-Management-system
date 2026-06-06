using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Desktop.Services;

namespace KicsitLibrary.Desktop.ViewModels
{
    public partial class OverdueRemindersViewModel : ObservableObject
    {
        private readonly IOverdueService _overdueService;
        private readonly IOverdueSchedulerService _schedulerService;
        private readonly INotificationService _notificationService;
        private readonly IAuthenticationService _authenticationService;
        private readonly IActivityLogService _logService;
        private readonly IRecordDetailsService _recordDetailsService;
        private List<OverdueItem> _allItems = [];

        public IReadOnlyList<string> MemberTypeOptions { get; } = ["All", "Student", "FacultyStaff"];
        public IReadOnlyList<string> NotificationStatusOptions { get; } = ["All", "None", "Pending", "Failed", "Sent"];

        [ObservableProperty] private ObservableCollection<OverdueItem> _overdueItems = new();
        [ObservableProperty] private OverdueItem? _selectedItem;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _selectedMemberType = "All";
        [ObservableProperty] private string _minimumDaysOverdue = string.Empty;
        [ObservableProperty] private string _maximumDaysOverdue = string.Empty;
        [ObservableProperty] private string _minimumFine = string.Empty;
        [ObservableProperty] private string _maximumFine = string.Empty;
        [ObservableProperty] private string _selectedNotificationStatus = "All";
        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private string _errorMessage = string.Empty;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private bool _schedulerEnabled;
        [ObservableProperty] private bool _schedulerRunOnStartup;
        [ObservableProperty] private int _schedulerIntervalMinutes;
        [ObservableProperty] private bool _schedulerSendPendingEmails;
        [ObservableProperty] private DateTime? _schedulerLastRunAt;
        [ObservableProperty] private DateTime? _schedulerLastSuccessAt;
        [ObservableProperty] private DateTime? _schedulerLastFailureAt;
        [ObservableProperty] private string _schedulerLastMessage = string.Empty;
        [ObservableProperty] private bool _schedulerIsRunning;

        public OverdueRemindersViewModel(
            IOverdueService overdueService,
            IOverdueSchedulerService schedulerService,
            INotificationService notificationService,
            IAuthenticationService authenticationService,
            IActivityLogService logService,
            IRecordDetailsService recordDetailsService)
        {
            _overdueService = overdueService ?? throw new ArgumentNullException(nameof(overdueService));
            _schedulerService = schedulerService ?? throw new ArgumentNullException(nameof(schedulerService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _recordDetailsService = recordDetailsService ?? throw new ArgumentNullException(nameof(recordDetailsService));
            _ = RefreshAsync();
        }

        partial void OnSearchTextChanged(string value) => ApplyFilters();
        partial void OnSelectedMemberTypeChanged(string value) => ApplyFilters();
        partial void OnMinimumDaysOverdueChanged(string value) => ApplyFilters();
        partial void OnMaximumDaysOverdueChanged(string value) => ApplyFilters();
        partial void OnMinimumFineChanged(string value) => ApplyFilters();
        partial void OnMaximumFineChanged(string value) => ApplyFilters();
        partial void OnSelectedNotificationStatusChanged(string value) => ApplyFilters();

        [RelayCommand]
        public async Task RefreshAsync()
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                _allItems = (await _overdueService.GetOverdueItemsAsync()).ToList();
                ApplyFilters();
                await LoadSchedulerStatusAsync();
                StatusMessage = $"Loaded {_allItems.Count} active overdue item(s).";
                await _logService.LogActivityAsync(
                    "Overdue View Refreshed",
                    StatusMessage,
                    CurrentUserId);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Unable to load overdue items: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task RunOverdueCheckAsync()
        {
            await ProcessAllAsync("Overdue check", scheduledRun: false);
        }

        [RelayCommand]
        private async Task SendRemindersForAllEligibleAsync()
        {
            await ProcessAllAsync("Eligible reminder processing", scheduledRun: false);
        }

        [RelayCommand]
        private async Task RunSchedulerNowAsync()
        {
            await ProcessAllAsync("Scheduler run", scheduledRun: true);
        }

        [RelayCommand]
        private async Task RefreshSchedulerStatusAsync()
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                await LoadSchedulerStatusAsync();
                StatusMessage = "Scheduler status refreshed.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Unable to refresh scheduler status: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SendReminderForSelectedAsync()
        {
            if (SelectedItem == null)
            {
                ErrorMessage = "Select an overdue item first.";
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                var result = await _overdueService.CreateReminderForIssueAsync(
                    SelectedItem.IssueRecordId,
                    CurrentUserId);
                StatusMessage = result.Message;
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Reminder processing failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ClearFiltersAsync()
        {
            SearchText = string.Empty;
            SelectedMemberType = "All";
            MinimumDaysOverdue = string.Empty;
            MaximumDaysOverdue = string.Empty;
            MinimumFine = string.Empty;
            MaximumFine = string.Empty;
            SelectedNotificationStatus = "All";
            ApplyFilters();
            await _logService.LogActivityAsync(
                "Overdue Filters Cleared",
                "Overdue reminder filters were reset.",
                CurrentUserId);
        }

        [RelayCommand]
        private void OpenMemberProfile()
        {
            if (SelectedItem == null)
            {
                ErrorMessage = "Select an overdue item first.";
                return;
            }

            _recordDetailsService.OpenMemberProfile(SelectedItem.MemberId, SelectedItem.MemberType);
        }

        [RelayCommand]
        private async Task OpenBookDetailsAsync()
        {
            if (SelectedItem == null)
            {
                ErrorMessage = "Select an overdue item first.";
                return;
            }

            try
            {
                await _recordDetailsService.OpenBookDetailsAsync(SelectedItem.BookMasterId);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Unable to open book details: {ex.Message}";
            }
        }

        private async Task ProcessAllAsync(
            string actionLabel,
            bool scheduledRun)
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                var result = scheduledRun
                    ? await _schedulerService.RunAsync(CurrentUserId)
                    : await _schedulerService.RunManualOverdueCheckAsync(CurrentUserId);
                var pendingEmails = await _notificationService.GetPendingEmailNotificationsAsync();
                await RefreshAsync();
                StatusMessage =
                    $"{actionLabel}: {result.Message} " +
                    $"{pendingEmails.Count} email record(s) are pending manual delivery in Notification Center.";
                if (!result.Succeeded && !result.WasSkipped)
                {
                    ErrorMessage = result.FailureReason ?? result.Message;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"{actionLabel} failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadSchedulerStatusAsync()
        {
            var status = await _schedulerService.GetStatusAsync();
            SchedulerEnabled = status.Enabled;
            SchedulerRunOnStartup = status.RunOnStartup;
            SchedulerIntervalMinutes = status.IntervalMinutes;
            SchedulerSendPendingEmails = status.SendPendingEmails;
            SchedulerLastRunAt = status.LastRunAt?.ToLocalTime();
            SchedulerLastSuccessAt = status.LastSuccessAt?.ToLocalTime();
            SchedulerLastFailureAt = status.LastFailureAt?.ToLocalTime();
            SchedulerLastMessage = status.LastMessage;
            SchedulerIsRunning = status.IsRunning;
        }

        private void ApplyFilters()
        {
            IEnumerable<OverdueItem> query = _allItems;
            var search = SearchText.Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(item =>
                    item.AccessionNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    item.BookTitle.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    item.MemberName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    item.MemberCode.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    item.MemberEmail.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            if (SelectedMemberType != "All")
            {
                query = query.Where(item => item.MemberType.ToString() == SelectedMemberType);
            }

            if (int.TryParse(MinimumDaysOverdue, out var minimumDays))
            {
                query = query.Where(item => item.DaysOverdue >= minimumDays);
            }

            if (int.TryParse(MaximumDaysOverdue, out var maximumDays))
            {
                query = query.Where(item => item.DaysOverdue <= maximumDays);
            }

            if (decimal.TryParse(MinimumFine, out var minimumFine))
            {
                query = query.Where(item => item.CurrentFineAmount >= minimumFine);
            }

            if (decimal.TryParse(MaximumFine, out var maximumFine))
            {
                query = query.Where(item => item.CurrentFineAmount <= maximumFine);
            }

            if (SelectedNotificationStatus == "None")
            {
                query = query.Where(item => item.NotificationStatus == null);
            }
            else if (SelectedNotificationStatus != "All")
            {
                query = query.Where(item => item.NotificationStatus?.ToString() == SelectedNotificationStatus);
            }

            OverdueItems = new ObservableCollection<OverdueItem>(query);
        }

        private int? CurrentUserId => _authenticationService.CurrentUser?.Id;
    }
}
