using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Desktop.ViewModels;

public partial class ReservationQueueViewModel : ObservableObject
{
    private readonly IReservationService _reservationService;
    private int _bookMasterId;

    [ObservableProperty] private string _bookTitle = string.Empty;
    [ObservableProperty] private ObservableCollection<ReservationQueueItem> _queue = [];
    [ObservableProperty] private ReservationQueueItem? _selectedReservation;
    [ObservableProperty] private string _reason = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public ReservationQueueViewModel(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    public async Task LoadAsync(int bookMasterId)
    {
        _bookMasterId = bookMasterId;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        Queue = new ObservableCollection<ReservationQueueItem>(
            await _reservationService.GetReservationQueueAsync(_bookMasterId));
        BookTitle = Queue.FirstOrDefault()?.BookTitle ?? "Reservation Queue";
        StatusMessage = $"{Queue.Count} active queue item(s).";
    }

    [RelayCommand]
    private async Task MarkAvailableAsync()
    {
        var result = await _reservationService.MarkReservationAvailableAsync(_bookMasterId);
        StatusMessage = result.Succeeded ? result.Message : result.ErrorMessage ?? result.Message;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task FulfillAsync()
    {
        if (SelectedReservation == null)
        {
            StatusMessage = "Select a queue item first.";
            return;
        }
        var result = await _reservationService.FulfillReservationAsync(
            SelectedReservation.ReservationId);
        StatusMessage = result.Succeeded ? result.Message : result.ErrorMessage ?? result.Message;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (SelectedReservation == null)
        {
            StatusMessage = "Select a queue item first.";
            return;
        }
        var result = await _reservationService.CancelReservationAsync(
            SelectedReservation.ReservationId,
            Reason);
        StatusMessage = result.Succeeded ? result.Message : result.ErrorMessage ?? result.Message;
        await RefreshAsync();
    }
}
