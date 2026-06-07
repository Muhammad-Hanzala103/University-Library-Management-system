using System.Windows;
using KicsitLibrary.Desktop.ViewModels;
using KicsitLibrary.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Desktop.Services;

public interface IInventoryDialogService
{
    Task<bool> ShowInventoryFormAsync(int? itemId = null);
    Task ShowInventoryDetailsAsync(int itemId);
    Task<bool> ShowInventoryAdjustmentAsync(int itemId);
    Task ShowStockVerificationDetailsAsync(StockVerificationItem item);
}

public sealed class InventoryDialogService(IServiceScopeFactory scopeFactory) : IInventoryDialogService
{
    public async Task<bool> ShowInventoryFormAsync(int? itemId = null)
    {
        using var scope = scopeFactory.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<InventoryItemFormViewModel>();
        await vm.LoadAsync(itemId);
        var window = scope.ServiceProvider.GetRequiredService<InventoryItemFormWindow>();
        window.DataContext = vm; window.Owner = Application.Current.MainWindow;
        vm.CloseRequested += value => window.DialogResult = value;
        return window.ShowDialog() == true;
    }

    public async Task ShowInventoryDetailsAsync(int itemId)
    {
        using var scope = scopeFactory.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<InventoryDetailsViewModel>();
        await vm.LoadAsync(itemId);
        var window = scope.ServiceProvider.GetRequiredService<InventoryDetailsWindow>();
        window.DataContext = vm; window.Owner = Application.Current.MainWindow; window.ShowDialog();
    }

    public async Task<bool> ShowInventoryAdjustmentAsync(int itemId)
    {
        using var scope = scopeFactory.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<InventoryAdjustmentViewModel>();
        await vm.LoadAsync(itemId);
        var window = scope.ServiceProvider.GetRequiredService<InventoryAdjustmentWindow>();
        window.DataContext = vm; window.Owner = Application.Current.MainWindow;
        vm.CloseRequested += value => window.DialogResult = value;
        return window.ShowDialog() == true;
    }

    public Task ShowStockVerificationDetailsAsync(StockVerificationItem item)
    {
        using var scope = scopeFactory.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<StockVerificationDetailsViewModel>();
        vm.Load(item);
        var window = scope.ServiceProvider.GetRequiredService<StockVerificationDetailsWindow>();
        window.DataContext = vm; window.Owner = Application.Current.MainWindow; window.ShowDialog();
        return Task.CompletedTask;
    }
}
