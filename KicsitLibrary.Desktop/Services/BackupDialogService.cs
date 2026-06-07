using System.Windows;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Desktop.ViewModels;
using KicsitLibrary.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace KicsitLibrary.Desktop.Services;

public interface IBackupDialogService
{
    Task ShowBackupDetailsAsync(BackupHistoryItem item);
    bool ConfirmPhysicalRetentionDeletion(
        int candidateCount,
        long totalSizeBytes);
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

    public bool ConfirmPhysicalRetentionDeletion(
        int candidateCount,
        long totalSizeBytes)
    {
        var result = MessageBox.Show(
            $"Retention will soft-delete {candidateCount} backup history record(s) " +
            $"and permanently delete eligible linked backup files " +
            $"({totalSizeBytes:N0} bytes before validation).\n\n" +
            "Restore safety files, pending restore files, failed verification files, " +
            "the live database, and files outside the configured folder remain protected.\n\n" +
            "Continue?",
            "Confirm Physical Backup Deletion",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        return result == MessageBoxResult.Yes;
    }
}
