using System.Windows;
using KicsitLibrary.Desktop.ViewModels;
using KicsitLibrary.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace KicsitLibrary.Desktop.Services;

public interface IReservationDialogService
{
    Task<bool> ShowCreateAsync();
    Task ShowQueueAsync(int bookMasterId);
}

public sealed class ReservationDialogService(IServiceScopeFactory scopeFactory)
    : IReservationDialogService
{
    public async Task<bool> ShowCreateAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var viewModel = scope.ServiceProvider.GetRequiredService<ReservationFormViewModel>();
        await viewModel.LoadAsync();
        var window = scope.ServiceProvider.GetRequiredService<ReservationFormWindow>();
        window.DataContext = viewModel;
        window.Owner = Application.Current.MainWindow;
        viewModel.CloseRequested += result => window.DialogResult = result;
        return window.ShowDialog() == true;
    }

    public async Task ShowQueueAsync(int bookMasterId)
    {
        using var scope = scopeFactory.CreateScope();
        var viewModel = scope.ServiceProvider.GetRequiredService<ReservationQueueViewModel>();
        await viewModel.LoadAsync(bookMasterId);
        var window = scope.ServiceProvider.GetRequiredService<ReservationQueueWindow>();
        window.DataContext = viewModel;
        window.Owner = Application.Current.MainWindow;
        window.ShowDialog();
    }
}
