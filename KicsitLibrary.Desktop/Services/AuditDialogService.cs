using System.Windows;
using KicsitLibrary.Desktop.ViewModels;
using KicsitLibrary.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace KicsitLibrary.Desktop.Services;

public interface IAuditDialogService
{
    Task ShowActivityLogDetailsAsync(int activityLogId);
    Task<bool> ShowAuditFormAsync(int? auditRecordId = null);
    Task ShowAuditDetailsAsync(int auditRecordId);
}

public sealed class AuditDialogService(IServiceScopeFactory scopeFactory)
    : IAuditDialogService
{
    public async Task ShowActivityLogDetailsAsync(int activityLogId)
    {
        using var scope = scopeFactory.CreateScope();
        var viewModel = scope.ServiceProvider.GetRequiredService<ActivityLogDetailsViewModel>();
        await viewModel.LoadAsync(activityLogId);
        var window = scope.ServiceProvider.GetRequiredService<ActivityLogDetailsWindow>();
        window.DataContext = viewModel;
        window.Owner = Application.Current.MainWindow;
        window.ShowDialog();
    }

    public async Task<bool> ShowAuditFormAsync(int? auditRecordId = null)
    {
        using var scope = scopeFactory.CreateScope();
        var viewModel = scope.ServiceProvider.GetRequiredService<AuditRecordFormViewModel>();
        await viewModel.LoadAsync(auditRecordId);
        var window = scope.ServiceProvider.GetRequiredService<AuditRecordFormWindow>();
        window.DataContext = viewModel;
        window.Owner = Application.Current.MainWindow;
        viewModel.CloseRequested += result => window.DialogResult = result;
        return window.ShowDialog() == true;
    }

    public async Task ShowAuditDetailsAsync(int auditRecordId)
    {
        using var scope = scopeFactory.CreateScope();
        var viewModel = scope.ServiceProvider.GetRequiredService<AuditRecordDetailsViewModel>();
        await viewModel.LoadAsync(auditRecordId);
        var window = scope.ServiceProvider.GetRequiredService<AuditRecordDetailsWindow>();
        window.DataContext = viewModel;
        window.Owner = Application.Current.MainWindow;
        window.ShowDialog();
    }
}
