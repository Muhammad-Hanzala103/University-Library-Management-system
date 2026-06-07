using System.Windows;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Desktop.ViewModels;
using KicsitLibrary.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace KicsitLibrary.Desktop.Services;

public interface IClearanceDetailsDialogService
{
    Task ShowAsync(MemberType memberType, int memberId);
}

public sealed class ClearanceDetailsDialogService(IServiceScopeFactory scopeFactory)
    : IClearanceDetailsDialogService
{
    public async Task ShowAsync(MemberType memberType, int memberId)
    {
        using var scope = scopeFactory.CreateScope();
        var viewModel = scope.ServiceProvider.GetRequiredService<ClearanceDetailsViewModel>();
        await viewModel.LoadAsync(memberType, memberId);
        var window = scope.ServiceProvider.GetRequiredService<ClearanceDetailsWindow>();
        window.DataContext = viewModel;
        window.Owner = Application.Current.MainWindow;
        window.ShowDialog();
    }
}
