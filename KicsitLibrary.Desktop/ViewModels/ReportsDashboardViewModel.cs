using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Reports.Contracts;
using KicsitLibrary.Reports.Models;

namespace KicsitLibrary.Desktop.ViewModels
{
    public partial class ReportsDashboardViewModel : ObservableObject
    {
        private readonly IReportService _reportService;
        private readonly IReportExportService _reportExportService;
        private readonly IAuthenticationService _authenticationService;

        public ObservableCollection<ReportDefinition> Reports { get; }
        public ReportPreviewViewModel Preview { get; } = new();
        public IReadOnlyList<string> MemberTypeOptions { get; } =
            ["", .. Enum.GetNames<MemberType>()];
        public IReadOnlyList<string> BookStatusOptions { get; } =
            ["", .. Enum.GetNames<BookStatus>()];
        public IReadOnlyList<string> FineStatusOptions { get; } =
            ["", .. Enum.GetNames<FineStatus>()];
        public IReadOnlyList<string> NotificationStatusOptions { get; } =
            ["", .. Enum.GetNames<NotificationStatus>()];
        public IReadOnlyList<string> NotificationTypeOptions { get; } =
            ["", .. Enum.GetNames<NotificationType>()];
        public IReadOnlyList<string> ChannelOptions { get; } =
            ["", "InApp", "Email", "WhatsApp"];

        [ObservableProperty] private ReportDefinition? _selectedReport;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private string _errorMessage = string.Empty;

        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _category = string.Empty;
        [ObservableProperty] private string _department = string.Empty;
        [ObservableProperty] private string _literatureCategory = string.Empty;
        [ObservableProperty] private string _author = string.Empty;
        [ObservableProperty] private string _publisher = string.Empty;
        [ObservableProperty] private string _selectedStatus = string.Empty;
        [ObservableProperty] private string _selectedMemberType = string.Empty;
        [ObservableProperty] private DateTime? _fromDate;
        [ObservableProperty] private DateTime? _toDate;
        [ObservableProperty] private bool _overdueOnly;
        [ObservableProperty] private string _minimumDays = string.Empty;
        [ObservableProperty] private string _maximumDays = string.Empty;
        [ObservableProperty] private string _minimumAmount = string.Empty;
        [ObservableProperty] private string _maximumAmount = string.Empty;
        [ObservableProperty] private string _selectedChannel = string.Empty;
        [ObservableProperty] private string _selectedNotificationType = string.Empty;

        [ObservableProperty] private bool _isCatalogReport;
        [ObservableProperty] private bool _isIssuedReport;
        [ObservableProperty] private bool _isOverdueReport;
        [ObservableProperty] private bool _isFineReport;
        [ObservableProperty] private bool _isNotificationReport;

        public ReportsDashboardViewModel(
            IReportService reportService,
            IReportExportService reportExportService,
            IAuthenticationService authenticationService)
        {
            _reportService = reportService ??
                throw new ArgumentNullException(nameof(reportService));
            _reportExportService = reportExportService ??
                throw new ArgumentNullException(nameof(reportExportService));
            _authenticationService = authenticationService ??
                throw new ArgumentNullException(nameof(authenticationService));
            Reports = new ObservableCollection<ReportDefinition>(
                _reportService.GetDefinitions());
            SelectedReport = Reports.FirstOrDefault();
            UpdateReportFlags();
            _ = RefreshPreviewAsync();
        }

        partial void OnSelectedReportChanged(ReportDefinition? value)
        {
            UpdateReportFlags();
        }

        [RelayCommand]
        private async Task SelectReportAsync(ReportDefinition report)
        {
            SelectedReport = report;
            ClearFilterValues();
            await RefreshPreviewAsync();
        }

        [RelayCommand]
        private async Task RefreshPreviewAsync()
        {
            if (SelectedReport == null)
            {
                ErrorMessage = "Select a report first.";
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                var result = await _reportService.GenerateAsync(
                    SelectedReport.Key,
                    BuildFilters(),
                    _authenticationService.CurrentUser?.FullName ?? "Unknown User");
                Preview.SetResult(result);
                StatusMessage =
                    $"{result.ReportTitle}: {result.TotalCount} record(s) loaded.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Unable to generate report preview: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ClearFiltersAsync()
        {
            ClearFilterValues();
            await RefreshPreviewAsync();
        }

        [RelayCommand]
        private Task ExportCsvAsync() => ExportAsync(ReportFormat.CSV);

        [RelayCommand]
        private Task ExportExcelAsync() => ExportAsync(ReportFormat.Excel);

        [RelayCommand]
        private Task ExportPdfAsync() => ExportAsync(ReportFormat.PDF);

        private async Task ExportAsync(ReportFormat format)
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                if (Preview.CurrentResult == null)
                {
                    await RefreshPreviewAsync();
                }

                if (Preview.CurrentResult == null)
                {
                    ErrorMessage = "Report preview could not be generated.";
                    return;
                }

                var result = await _reportExportService.ExportAsync(
                    Preview.CurrentResult,
                    new ReportExportRequest { Format = format },
                    _authenticationService.CurrentUser?.Id);
                if (result.Succeeded)
                {
                    StatusMessage = result.Message;
                }
                else
                {
                    ErrorMessage =
                        result.ErrorMessage ?? result.Message;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"{format} export failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private IReadOnlyCollection<ReportFilter> BuildFilters()
        {
            if (SelectedReport == null)
            {
                return [];
            }

            var filters = new List<ReportFilter>();
            foreach (var definition in SelectedReport.Filters)
            {
                filters.Add(new ReportFilter
                {
                    Key = definition.Key,
                    Label = definition.Label,
                    Type = definition.Type,
                    Value = GetFilterValue(definition.Key),
                    SecondaryValue = GetSecondaryFilterValue(definition.Key),
                    Options = definition.Options
                });
            }
            return filters;
        }

        private object? GetFilterValue(string key)
        {
            return key switch
            {
                "SearchText" => SearchText,
                "Category" => Category,
                "Department" => Department,
                "LiteratureCategory" => LiteratureCategory,
                "Author" => Author,
                "Publisher" => Publisher,
                "Status" => SelectedStatus,
                "MemberType" => SelectedMemberType,
                "DateRange" => FromDate,
                "OverdueOnly" => OverdueOnly,
                "DaysOverdue" => MinimumDays,
                "FineAmount" => MinimumAmount,
                "PaymentStatus" => SelectedStatus,
                "Channel" => SelectedChannel,
                "NotificationType" => SelectedNotificationType,
                _ => null
            };
        }

        private object? GetSecondaryFilterValue(string key)
        {
            return key switch
            {
                "DateRange" => ToDate,
                "DaysOverdue" => MaximumDays,
                "FineAmount" => MaximumAmount,
                _ => null
            };
        }

        private void ClearFilterValues()
        {
            SearchText = string.Empty;
            Category = string.Empty;
            Department = string.Empty;
            LiteratureCategory = string.Empty;
            Author = string.Empty;
            Publisher = string.Empty;
            SelectedStatus = string.Empty;
            SelectedMemberType = string.Empty;
            FromDate = null;
            ToDate = null;
            OverdueOnly = false;
            MinimumDays = string.Empty;
            MaximumDays = string.Empty;
            MinimumAmount = string.Empty;
            MaximumAmount = string.Empty;
            SelectedChannel = string.Empty;
            SelectedNotificationType = string.Empty;
        }

        private void UpdateReportFlags()
        {
            var key = SelectedReport?.Key;
            IsCatalogReport = key == "catalog";
            IsIssuedReport = key == "issued-books";
            IsOverdueReport = key == "overdue-books";
            IsFineReport = key == "fines";
            IsNotificationReport = key == "notifications";
        }
    }
}
