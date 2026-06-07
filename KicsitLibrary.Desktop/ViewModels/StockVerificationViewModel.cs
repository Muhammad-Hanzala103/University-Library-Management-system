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

public partial class StockVerificationViewModel(
    IStockVerificationService service, IInventoryDialogService dialogs,
    IReportService reports, IReportExportService exports, IAuthenticationService auth) : ObservableObject
{
    public IReadOnlyList<BookStatus> ActualStatuses { get; } =
        [BookStatus.Available, BookStatus.Issued, BookStatus.Reserved, BookStatus.Lost,
         BookStatus.Damaged, BookStatus.Missing, BookStatus.UnderRepair, BookStatus.Deleted];
    [ObservableProperty] private ObservableCollection<StockVerificationItem> _items = [];
    [ObservableProperty] private StockVerificationItem? _selectedItem;
    [ObservableProperty] private int? _sessionId;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _category = "";
    [ObservableProperty] private string _department = "";
    [ObservableProperty] private string _rack = "";
    [ObservableProperty] private string _shelf = "";
    [ObservableProperty] private bool _mismatchedOnly;
    [ObservableProperty] private bool _unverifiedOnly;
    [ObservableProperty] private BookStatus _actualStatus = BookStatus.Available;
    [ObservableProperty] private string _remarks = "";
    [ObservableProperty] private string _reconciliationReason = "";
    [ObservableProperty] private StockVerificationSummary _summary = new();
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isBusy;

    [RelayCommand] public async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            Items = new(await service.GetStockVerificationItemsAsync(new()
            { SessionId = SessionId, SearchText = SearchText, Category = Category, Department = Department,
              Rack = Rack, Shelf = Shelf, MismatchedOnly = MismatchedOnly, UnverifiedOnly = UnverifiedOnly }));
            if (Items.Count > 0) { SessionId ??= Items[0].SessionId; Summary = await service.GetStockVerificationSummaryAsync(SessionId.Value); }
            StatusMessage = $"{Items.Count} stock verification item(s) loaded.";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }
    [RelayCommand] private async Task StartNewSessionAsync()
    {
        var result = await service.StartVerificationSessionAsync(Remarks);
        StatusMessage = result.Succeeded ? result.Message : result.ErrorMessage ?? result.Message;
        if (result.Succeeded) { SessionId = result.Session!.StockVerificationSessionId; await RefreshAsync(); }
    }
    [RelayCommand] private async Task VerifySelectedAsync() => await VerifyAsync(false);
    [RelayCommand] private async Task ReconcileSelectedAsync() => await VerifyAsync(true);
    [RelayCommand] private async Task BulkMarkUnverifiedAsync()
    {
        if (!SessionId.HasValue) { StatusMessage = "Start or load a verification session first."; return; }
        var result = await service.BulkMarkUnverifiedAsync(SessionId.Value, Remarks);
        StatusMessage = result.Succeeded ? result.Message : result.ErrorMessage ?? result.Message;
        if (result.Succeeded) await RefreshAsync();
    }
    [RelayCommand] private async Task CompleteSessionAsync()
    {
        if (!SessionId.HasValue) { StatusMessage = "Start or load a verification session first."; return; }
        var result = await service.CompleteVerificationSessionAsync(SessionId.Value, Remarks);
        StatusMessage = result.Succeeded ? result.Message : result.ErrorMessage ?? result.Message;
        if (result.Succeeded) Summary = result.Summary!;
    }
    [RelayCommand] private async Task ViewDetailsAsync()
    {
        if (SelectedItem == null) { StatusMessage = "Select a book copy first."; return; }
        await dialogs.ShowStockVerificationDetailsAsync(SelectedItem);
    }
    [RelayCommand] private async Task ClearFiltersAsync() { SearchText = Category = Department = Rack = Shelf = ""; MismatchedOnly = UnverifiedOnly = false; await RefreshAsync(); }
    [RelayCommand] private async Task ExportAsync()
    {
        try
        {
            var report = await reports.GenerateAsync("stock-verification", [], auth.CurrentUser?.FullName ?? "Unknown User");
            var result = await exports.ExportAsync(report, new ReportExportRequest { Format = ReportFormat.Excel }, auth.CurrentUser?.Id);
            StatusMessage = result.Succeeded ? result.Message : result.ErrorMessage ?? result.Message;
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }
    private async Task VerifyAsync(bool reconcile)
    {
        if (SelectedItem == null) { StatusMessage = "Select a book copy first."; return; }
        var result = await service.VerifyBookCopyAsync(
            SelectedItem.SessionId, SelectedItem.BookCopyId, ActualStatus, Remarks, reconcile, ReconciliationReason);
        StatusMessage = result.Succeeded ? result.Message : result.ErrorMessage ?? result.Message;
        if (result.Succeeded) await RefreshAsync();
    }
}
