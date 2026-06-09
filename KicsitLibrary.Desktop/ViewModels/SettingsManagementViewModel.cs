using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Desktop.Services;
using MessageBox = System.Windows.MessageBox;

namespace KicsitLibrary.Desktop.ViewModels
{
    public partial class SettingsManagementViewModel : ObservableObject
    {
        private readonly ISettingsManagementService _settingsService;
        private readonly IAuthenticationService _authService;
        private readonly IActivityLogService _activityLogService;
        private readonly ISettingsDialogService _dialogService;

        [ObservableProperty]
        private ObservableCollection<SettingsCategoryItem> categories = new();

        [ObservableProperty]
        private ObservableCollection<SettingsItemView> visibleSettings = new();

        [ObservableProperty]
        private SettingsItemView? selectedSetting;

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private string selectedCategory = string.Empty;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string? statusMessage;

        [ObservableProperty]
        private bool canEditSettings;

        [ObservableProperty]
        private bool canExport;

        public SettingsManagementViewModel(
            ISettingsManagementService settingsService,
            IAuthenticationService authService,
            IActivityLogService activityLogService,
            ISettingsDialogService dialogService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _activityLogService = activityLogService ?? throw new ArgumentNullException(nameof(activityLogService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            CheckAuthorization();
        }

        public async Task InitializeAsync()
        {
            await LoadCategoriesAsync();
        }

        private void CheckAuthorization()
        {
            if (_authService.CurrentUser == null)
            {
                CanEditSettings = false;
                CanExport = false;
                return;
            }

            // Super Admin can always manage
            if (_authService.CurrentUser.UserRoles.Any(ur => ur.Role.Name == "Super Admin"))
            {
                CanEditSettings = true;
                CanExport = true;
                return;
            }

            // Check for MANAGE_SYSTEM permission (Admin)
            CanEditSettings = _authService.CurrentUser.UserRoles
                .SelectMany(ur => ur.Role.RolePermissions)
                .Any(rp => rp.Permission.Code == "MANAGE_SYSTEM");

            // Auditor can view and export
            CanExport = CanEditSettings || _authService.CurrentUser.UserRoles.Any(ur => ur.Role.Name == "Auditor");
        }

        [RelayCommand]
        private async Task LoadCategoriesAsync()
        {
            try
            {
                IsBusy = true;
                StatusMessage = "Loading settings...";

                var cats = await _settingsService.GetSettingsCategoriesAsync();
                Categories.Clear();
                foreach (var cat in cats)
                {
                    Categories.Add(cat);
                }

                if (Categories.Count > 0)
                {
                    SelectedCategory = Categories[0].Name;
                    await FilterSettingsAsync();
                }

                StatusMessage = null;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading settings: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task FilterSettingsAsync()
        {
            try
            {
                IsBusy = true;

                var settings = Categories
                    .FirstOrDefault(c => c.Name == SelectedCategory)
                    ?.Settings ?? new();

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    var search = SearchText.ToLowerInvariant();
                    settings = settings
                        .Where(s => s.Key.ToLowerInvariant().Contains(search) ||
                                   s.DisplayName.ToLowerInvariant().Contains(search) ||
                                   (s.Description?.ToLowerInvariant().Contains(search) ?? false))
                        .ToList();
                }

                VisibleSettings.Clear();
                foreach (var setting in settings)
                {
                    VisibleSettings.Add(setting);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void ClearFilters()
        {
            SearchText = string.Empty;
            if (Categories.Count > 0)
            {
                SelectedCategory = Categories[0].Name;
            }
            _ = FilterSettingsAsync();
        }

        [RelayCommand]
        private async Task EditSelectedAsync()
        {
            if (SelectedSetting == null || !CanEditSettings)
                return;

            var result = await _dialogService.ShowEditSettingAsync(SelectedSetting);
            if (result)
            {
                OnSettingUpdated();
            }
        }

        [RelayCommand]
        private async Task ResetSelectedAsync()
        {
            if (SelectedSetting == null || !CanEditSettings)
                return;

            if (MessageBox.Show(
                $"Reset '{SelectedSetting.DisplayName}' to default value?\n\nDefault: {SelectedSetting.DefaultValue}",
                "Confirm Reset",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question) != System.Windows.MessageBoxResult.Yes)
                return;

            try
            {
                IsBusy = true;
                var result = await _settingsService.ResetSettingToDefaultAsync(
                    SelectedSetting.Key,
                    _authService.CurrentUser?.Id,
                    "User reset through UI");

                if (result.Succeeded)
                {
                    StatusMessage = result.Message;
                    await LoadCategoriesAsync();
                }
                else
                {
                    StatusMessage = $"Error: {result.ErrorMessage}";
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ViewDetailsAsync()
        {
            if (SelectedSetting == null)
                return;

            await _dialogService.ShowSettingDetailsAsync(SelectedSetting);
        }

        [RelayCommand]
        private async Task ExportSnapshotAsync()
        {
            if (!CanExport)
                return;

            try
            {
                IsBusy = true;
                StatusMessage = "Exporting settings snapshot...";

                var result = await _settingsService.ExportSettingsSnapshotAsync(_authService.CurrentUser?.Id);
                if (result.Succeeded)
                {
                    StatusMessage = $"Settings exported to: {result.FilePath}";
                }
                else
                {
                    StatusMessage = $"Export failed: {result.ErrorMessage}";
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadCategoriesAsync();
        }

        // Events for window dialogs
        public event EventHandler<SettingsItemView>? EditSettingRequested;
        public event EventHandler<SettingsItemView>? DetailsViewRequested;
        public event EventHandler? SettingUpdated;

        public void OnSettingUpdated()
        {
            SettingUpdated?.Invoke(this, EventArgs.Empty);
            _ = RefreshAsync();
        }
    }
}
