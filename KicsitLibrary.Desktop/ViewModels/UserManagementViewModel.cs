using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KicsitLibrary.Desktop.ViewModels
{
    public partial class UserManagementViewModel : ObservableObject
    {
        private readonly IServiceScopeFactory _scopeFactory;

        [ObservableProperty]
        private ObservableCollection<User> _pendingRequests = new();

        [ObservableProperty]
        private User? _selectedRequest;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        public UserManagementViewModel(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
            _ = RefreshAsync();
        }

        [RelayCommand]
        public async Task RefreshAsync()
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            StatusMessage = string.Empty;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
                
                var pendingUsers = await context.Users
                    .Where(u => !u.IsActive && !u.IsDeleted && u.AccountStatus == "PendingApproval")
                    .OrderByDescending(u => u.CreatedAt)
                    .ToListAsync();

                PendingRequests = new ObservableCollection<User>(pendingUsers);
                StatusMessage = $"Found {PendingRequests.Count} pending account requests.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading requests: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ApproveAsync()
        {
            if (SelectedRequest == null) return;

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
                
                var user = await context.Users.FindAsync(SelectedRequest.Id);
                if (user != null)
                {
                    user.IsActive = true;
                    user.AccountStatus = "Approved";
                    user.UpdatedAt = DateTime.UtcNow;
                    await context.SaveChangesAsync();
                    
                    StatusMessage = $"User {user.Username} approved successfully.";
                    await RefreshAsync();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error approving user: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task RejectAsync()
        {
            if (SelectedRequest == null) return;

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
                
                var user = await context.Users.FindAsync(SelectedRequest.Id);
                if (user != null)
                {
                    user.AccountStatus = "Rejected";
                    user.IsDeleted = true;
                    user.UpdatedAt = DateTime.UtcNow;
                    await context.SaveChangesAsync();
                    
                    StatusMessage = $"User {user.Username} rejected.";
                    await RefreshAsync();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error rejecting user: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
