using System;
using System.Threading.Tasks;
using System.Windows;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Desktop.ViewModels;
using KicsitLibrary.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace KicsitLibrary.Desktop.Services
{
    public sealed class SettingsDialogService(IServiceScopeFactory scopeFactory) : ISettingsDialogService
    {
        public Task<bool> ShowEditSettingAsync(SettingsItemView item)
        {
            using var scope = scopeFactory.CreateScope();
            var viewModel = scope.ServiceProvider.GetRequiredService<SettingsEditViewModel>();
            viewModel.LoadSetting(item);
            
            var window = scope.ServiceProvider.GetRequiredService<SettingsEditWindow>();
            window.DataContext = viewModel;
            window.Owner = Application.Current.MainWindow;
            
            bool result = false;
            viewModel.Close += (s, success) =>
            {
                result = success;
                try
                {
                    window.DialogResult = success;
                }
                catch (InvalidOperationException)
                {
                    // DialogResult can only be set when the window is shown as a dialog
                }
                window.Close();
            };
            
            window.ShowDialog();
            return Task.FromResult(result);
        }

        public Task ShowSettingDetailsAsync(SettingsItemView item)
        {
            using var scope = scopeFactory.CreateScope();
            var viewModel = scope.ServiceProvider.GetRequiredService<SettingsDetailsViewModel>();
            viewModel.LoadSetting(item);
            
            var window = scope.ServiceProvider.GetRequiredService<SettingsDetailsWindow>();
            window.DataContext = viewModel;
            window.Owner = Application.Current.MainWindow;
            
            viewModel.CloseRequested += (s, e) => window.Close();
            
            window.ShowDialog();
            return Task.CompletedTask;
        }
    }
}
