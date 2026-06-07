using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Desktop.Services;
using KicsitLibrary.Reports.Contracts;
using KicsitLibrary.Reports.Models;

namespace KicsitLibrary.Desktop.ViewModels;

public partial class InventoryManagementViewModel(
    IInventoryService service, IInventoryDialogService dialogs, IReportService reports,
    IReportExportService exports, IAuthenticationService auth) : ObservableObject
{
    public IReadOnlyList<string> ItemTypes { get; } = ["", .. Enum.GetNames<InventoryItemType>()];
    [ObservableProperty] private ObservableCollection<InventoryItemListItem> _items = [];
    [ObservableProperty] private InventoryItemListItem? _selectedItem;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _selectedItemType = "";
    [ObservableProperty] private string _condition = "";
    [ObservableProperty] private string _location = "";
    [ObservableProperty] private bool _damagedOnly;
    [ObservableProperty] private bool _lowQuantityOnly;
    [ObservableProperty] private bool _includeDeleted;
    [ObservableProperty] private DateTime? _fromDate;
    [ObservableProperty] private DateTime? _toDate;
    [ObservableProperty] private int _actionQuantity = 1;
    [ObservableProperty] private string _actionReason = "";
    [ObservableProperty] private InventoryStatusSummary _summary = new();
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isBusy;

    [RelayCommand] public async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            InventoryItemType? type = Enum.TryParse<InventoryItemType>(SelectedItemType, out var parsed) ? parsed : null;
            Items = new(await service.GetInventoryItemsAsync(new()
            { SearchText = SearchText, ItemType = type, Condition = Condition, Location = Location, DamagedOnly = DamagedOnly,
              LowQuantityOnly = LowQuantityOnly, IncludeDeleted = IncludeDeleted, FromDate = FromDate, ToDate = ToDate }));
            Summary = await service.GetInventorySummaryAsync(); StatusMessage = $"{Items.Count} inventory item(s) loaded.";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }
    [RelayCommand] private async Task AddAsync() { if (await dialogs.ShowInventoryFormAsync()) await RefreshAsync(); }
    [RelayCommand] private async Task EditAsync() { if (NeedSelection()) return; if (await dialogs.ShowInventoryFormAsync(SelectedItem!.InventoryItemId)) await RefreshAsync(); }
    [RelayCommand] private async Task ViewDetailsAsync() { if (NeedSelection()) return; await dialogs.ShowInventoryDetailsAsync(SelectedItem!.InventoryItemId); }
    [RelayCommand] private async Task AdjustAsync() { if (NeedSelection()) return; if (await dialogs.ShowInventoryAdjustmentAsync(SelectedItem!.InventoryItemId)) await RefreshAsync(); }
    [RelayCommand] private async Task MarkDamagedAsync() => await ActAsync(() => service.MarkInventoryDamagedAsync(SelectedItem!.InventoryItemId, ActionQuantity, ActionReason));
    [RelayCommand] private async Task MarkRepairedAsync() => await ActAsync(() => service.MarkInventoryRepairedAsync(SelectedItem!.InventoryItemId, ActionQuantity, ActionReason));
    [RelayCommand] private async Task DeleteAsync() => await ActAsync(() => service.SoftDeleteInventoryItemAsync(SelectedItem!.InventoryItemId, ActionReason));
    [RelayCommand] private async Task RestoreAsync() => await ActAsync(() => service.RestoreInventoryItemAsync(SelectedItem!.InventoryItemId, ActionReason));
    [RelayCommand] private async Task ClearFiltersAsync() { SearchText = SelectedItemType = Condition = Location = ""; DamagedOnly = LowQuantityOnly = IncludeDeleted = false; FromDate = ToDate = null; await RefreshAsync(); }
    [RelayCommand] private async Task ExportAsync()
    {
        try
        {
            var report = await reports.GenerateAsync("inventory", [], auth.CurrentUser?.FullName ?? "Unknown User");
            var result = await exports.ExportAsync(report, new ReportExportRequest { Format = ReportFormat.Excel }, auth.CurrentUser?.Id);
            StatusMessage = result.Succeeded ? result.Message : result.ErrorMessage ?? result.Message;
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }
    private bool NeedSelection() { if (SelectedItem != null) return false; StatusMessage = "Select an inventory item first."; return true; }
    private async Task ActAsync(Func<Task<InventoryActionResult>> action)
    {
        if (NeedSelection()) return; var result = await action();
        StatusMessage = result.Succeeded ? result.Message : result.ErrorMessage ?? result.Message;
        if (result.Succeeded) await RefreshAsync();
    }
}
