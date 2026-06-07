using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Desktop.Services;

namespace KicsitLibrary.Desktop.ViewModels;

public partial class ReservationManagementViewModel : ObservableObject
{
    private readonly IReservationService _reservationService;
    private readonly IReservationDialogService _dialogService;

    public IReadOnlyList<string> StatusOptions { get; } =
        ["All", .. Enum.GetNames<ReservationStatus>()];
    public IReadOnlyList<string> MemberTypeOptions { get; } =
        ["All", .. Enum.GetNames<MemberType>()];

    [ObservableProperty] private ObservableCollection<ReservationQueueItem> _reservations = [];
    [ObservableProperty] private ReservationQueueItem? _selectedReservation;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedStatus = "All";
    [ObservableProperty] private string _selectedMemberType = "All";
    [ObservableProperty] private DateTime? _fromDate;
    [ObservableProperty] private DateTime? _toDate;
    [ObservableProperty] private string _actionReason = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public ReservationManagementViewModel(
        IReservationService reservationService,
        IReservationDialogService dialogService)
    {
        _reservationService = reservationService;
        _dialogService = dialogService;
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            await _reservationService.ExpireOldReservationsAsync();
            ReservationStatus? status = Enum.TryParse<ReservationStatus>(SelectedStatus, out var parsedStatus)
                ? parsedStatus : null;
            MemberType? memberType = Enum.TryParse<MemberType>(SelectedMemberType, out var parsedType)
                ? parsedType : null;
            Reservations = new ObservableCollection<ReservationQueueItem>(
                await _reservationService.GetReservationsAsync(
                    SearchText,
                    status,
                    memberType,
                    FromDate,
                    ToDate));
            StatusMessage = $"{Reservations.Count} reservation(s) loaded.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unable to load reservations: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        if (await _dialogService.ShowCreateAsync())
            await RefreshAsync();
    }

    [RelayCommand]
    private Task OpenQueueAsync() =>
        SelectedReservation == null
            ? SetMessageAsync("Select a reservation first.")
            : _dialogService.ShowQueueAsync(SelectedReservation.BookMasterId);

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (SelectedReservation == null)
        {
            StatusMessage = "Select a reservation first.";
            return;
        }
        var result = await _reservationService.CancelReservationAsync(
            SelectedReservation.ReservationId,
            ActionReason);
        StatusMessage = result.Succeeded ? result.Message : result.ErrorMessage ?? result.Message;
        if (result.Succeeded)
            await RefreshAsync();
    }

    [RelayCommand]
    private async Task ExpireAsync()
    {
        if (SelectedReservation == null)
        {
            StatusMessage = "Select a reservation first.";
            return;
        }
        var result = await _reservationService.ExpireReservationAsync(
            SelectedReservation.ReservationId,
            ActionReason);
        StatusMessage = result.Succeeded ? result.Message : result.ErrorMessage ?? result.Message;
        if (result.Succeeded)
            await RefreshAsync();
    }

    [RelayCommand]
    private async Task MarkAvailableAsync()
    {
        if (SelectedReservation == null)
        {
            StatusMessage = "Select a reservation first.";
            return;
        }
        var result = await _reservationService.MarkReservationAvailableAsync(
            SelectedReservation.BookMasterId);
        StatusMessage = result.Succeeded ? result.Message : result.ErrorMessage ?? result.Message;
        if (result.Succeeded)
            await RefreshAsync();
    }

    [RelayCommand]
    private async Task FulfillAsync()
    {
        if (SelectedReservation == null)
        {
            StatusMessage = "Select a reservation first.";
            return;
        }
        var result = await _reservationService.FulfillReservationAsync(
            SelectedReservation.ReservationId);
        StatusMessage = result.Succeeded ? result.Message : result.ErrorMessage ?? result.Message;
        if (result.Succeeded)
            await RefreshAsync();
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedStatus = "All";
        SelectedMemberType = "All";
        FromDate = null;
        ToDate = null;
    }

    private Task SetMessageAsync(string message)
    {
        StatusMessage = message;
        return Task.CompletedTask;
    }
}
