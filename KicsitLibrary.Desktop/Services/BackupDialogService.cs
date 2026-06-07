using System.Windows;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Desktop.ViewModels;
using KicsitLibrary.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace KicsitLibrary.Desktop.Services;

public interface IBackupDialogService
{
    Task ShowBackupDetailsAsync(BackupHistoryItem item);
}

public sealed class BackupDialogService(IServiceScopeFactory scopeFactory)
    : IBackupDialogService
{
    public Task ShowBackupDetailsAsync(BackupHistoryItem item)
    {
        using var scope = scopeFactory.CreateScope();
        var viewModel = scope.ServiceProvider.GetRequiredService<BackupDetailsViewModel>();
        viewModel.Load(item);
        var window = scope.ServiceProvider.GetRequiredService<BackupDetailsWindow>();
        window.DataContext = viewModel;
        window.Owner = Application.Current.MainWindow;
        window.ShowDialog();
        return Task.CompletedTask;
    }
}
