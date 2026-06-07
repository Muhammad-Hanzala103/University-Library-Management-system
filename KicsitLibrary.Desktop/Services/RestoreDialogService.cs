using System.Windows;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Desktop.ViewModels;
using KicsitLibrary.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace KicsitLibrary.Desktop.Services;

public interface IRestoreDialogService
{
    Task ShowRestorePreviewAsync(string backupFilePath);
    Task ShowRestoreHistoryDetailsAsync(RestoreHistoryItem item);
}

public sealed class RestoreDialogService(IServiceScopeFactory scopeFactory)
    : IRestoreDialogService
{
    public async Task ShowRestorePreviewAsync(string backupFilePath)
    {
        using var scope = scopeFactory.CreateScope();
        var viewModel = scope.ServiceProvider.GetRequiredService<RestorePreviewViewModel>();
        await viewModel.LoadAsync(backupFilePath);
        Show(viewModel, scope.ServiceProvider);
    }

    public Task ShowRestoreHistoryDetailsAsync(RestoreHistoryItem item)
    {
        using var scope = scopeFactory.CreateScope();
        var viewModel = scope.ServiceProvider.GetRequiredService<RestorePreviewViewModel>();
        viewModel.LoadHistory(item);
        Show(viewModel, scope.ServiceProvider);
        return Task.CompletedTask;
    }

    private static void Show(
        RestorePreviewViewModel viewModel,
        IServiceProvider serviceProvider)
    {
        var window = serviceProvider.GetRequiredService<RestorePreviewWindow>();
        window.DataContext = viewModel;
        window.Owner = Application.Current.MainWindow;
        window.ShowDialog();
    }
}
