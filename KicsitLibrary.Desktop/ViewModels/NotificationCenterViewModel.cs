using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Interfaces;

namespace KicsitLibrary.Desktop.ViewModels
{
    public partial class NotificationCenterViewModel : ObservableObject
    {
        private readonly INotificationService _notificationService;
        private readonly IAuthenticationService _authenticationService;
        private readonly IActivityLogService _logService;
        private List<NotificationRecord> _allNotifications = [];

        public IReadOnlyList<string> ChannelOptions { get; } = ["All", "InApp", "Email", "WhatsApp"];
        public IReadOnlyList<string> NotificationTypeOptions { get; } =
            ["All", "BeforeDueDateReminder", "DueDateReminder", "OverdueReminder", "FinePendingReminder", "ReservationAvailableReminder", "ClearancePendingReminder"];
        public IReadOnlyList<string> StatusOptions { get; } = ["All", "Pending", "Failed", "Sent"];
        public IReadOnlyList<string> MemberTypeOptions { get; } = ["All", "Student", "FacultyStaff"];

        [ObservableProperty] private ObservableCollection<NotificationRecord> _notifications = new();
        [ObservableProperty] private NotificationRecord? _selectedNotification;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _selectedChannel = "All";
        [ObservableProperty] private string _selectedNotificationType = "All";
        [ObservableProperty] private string _selectedStatus = "All";
        [ObservableProperty] private string _selectedMemberType = "All";
        [ObservableProperty] private DateTime? _fromDate;
        [ObservableProperty] private DateTime? _toDate;
        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private string _errorMessage = string.Empty;
        [ObservableProperty] private bool _isBusy;

        public NotificationCenterViewModel(
            INotificationService notificationService,
            IAuthenticationService authenticationService,
            IActivityLogService logService)
        {
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _ = RefreshAsync();
        }

        partial void OnSearchTextChanged(string value) => ApplyFilters();
        partial void OnSelectedChannelChanged(string value) => ApplyFilters();
        partial void OnSelectedNotificationTypeChanged(string value) => ApplyFilters();
        partial void OnSelectedStatusChanged(string value) => ApplyFilters();
        partial void OnSelectedMemberTypeChanged(string value) => ApplyFilters();
        partial void OnFromDateChanged(DateTime? value) => ApplyFilters();
        partial void OnToDateChanged(DateTime? value) => ApplyFilters();

        [RelayCommand]
        public async Task RefreshAsync()
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                _allNotifications = (await _notificationService.GetNotificationsAsync()).ToList();
                ApplyFilters();
                StatusMessage = $"Loaded {_allNotifications.Count} notification record(s).";
                await _logService.LogActivityAsync(
                    "Notification Center Refreshed",
                    StatusMessage,
                    CurrentUserId);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Unable to load notifications: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task RetrySelectedAsync()
        {
            if (SelectedNotification == null)
            {
                ErrorMessage = "Select a notification record first.";
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                var updated = await _notificationService.RetryNotificationRecordAsync(
                    SelectedNotification.Id,
                    CurrentUserId);
                StatusMessage =
                    $"Retry recorded for notification {updated.Id}. No external delivery was attempted.";
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Retry failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task MarkSelectedAsReadAsync()
        {
            if (SelectedNotification == null)
            {
                ErrorMessage = "Select a notification record first.";
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                var updated = await _notificationService.MarkAsReadAsync(
                    SelectedNotification.Id,
                    CurrentUserId);
                StatusMessage = $"Notification {updated.Id} marked as read.";
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Unable to mark notification as read: {ex.Message}";
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
            SelectedChannel = "All";
            SelectedNotificationType = "All";
            SelectedStatus = "All";
            SelectedMemberType = "All";
            FromDate = null;
            ToDate = null;
            ApplyFilters();
            await _logService.LogActivityAsync(
                "Notification Filters Cleared",
                "Notification center filters were reset.",
                CurrentUserId);
        }

        private void ApplyFilters()
        {
            IEnumerable<NotificationRecord> query = _allNotifications;
            var search = SearchText.Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(notification =>
                    notification.RecipientName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    notification.RecipientCode.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (notification.RecipientEmail?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    notification.Subject.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            if (SelectedChannel != "All")
            {
                query = query.Where(notification => notification.Channel == SelectedChannel);
            }

            if (SelectedNotificationType != "All")
            {
                query = query.Where(notification =>
                    notification.NotificationType.ToString() == SelectedNotificationType);
            }

            if (SelectedStatus != "All")
            {
                query = query.Where(notification => notification.Status.ToString() == SelectedStatus);
            }

            if (SelectedMemberType != "All")
            {
                query = query.Where(notification =>
                    notification.MemberType.ToString() == SelectedMemberType);
            }

            if (FromDate.HasValue)
            {
                query = query.Where(notification => notification.CreatedAt.Date >= FromDate.Value.Date);
            }

            if (ToDate.HasValue)
            {
                query = query.Where(notification => notification.CreatedAt.Date <= ToDate.Value.Date);
            }

            Notifications = new ObservableCollection<NotificationRecord>(query);
        }

        private int? CurrentUserId => _authenticationService.CurrentUser?.Id;
    }
}
