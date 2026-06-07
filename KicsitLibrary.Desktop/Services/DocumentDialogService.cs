using System.Windows;
using KicsitLibrary.Desktop.ViewModels;
using KicsitLibrary.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace KicsitLibrary.Desktop.Services;

public interface IDocumentDialogService
{
    Task<bool> ShowUploadAsync();
    Task ShowDetailsAsync(int documentUploadId);
}

public sealed class DocumentDialogService(IServiceScopeFactory scopeFactory)
    : IDocumentDialogService
{
    public Task<bool> ShowUploadAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var viewModel = scope.ServiceProvider.GetRequiredService<DocumentUploadViewModel>();
        var window = scope.ServiceProvider.GetRequiredService<DocumentUploadWindow>();
        window.DataContext = viewModel;
        window.Owner = Application.Current.MainWindow;
        viewModel.CloseRequested += result => window.DialogResult = result;
        return Task.FromResult(window.ShowDialog() == true);
    }

    public async Task ShowDetailsAsync(int documentUploadId)
    {
        using var scope = scopeFactory.CreateScope();
        var viewModel = scope.ServiceProvider.GetRequiredService<DocumentDetailsViewModel>();
        await viewModel.LoadAsync(documentUploadId);
        var window = scope.ServiceProvider.GetRequiredService<DocumentDetailsWindow>();
        window.DataContext = viewModel;
        window.Owner = Application.Current.MainWindow;
        window.ShowDialog();
    }
}
