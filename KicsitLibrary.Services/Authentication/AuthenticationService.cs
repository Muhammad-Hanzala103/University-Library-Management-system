using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Data;

namespace KicsitLibrary.Services.Authentication
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IActivityLogService _activityLogService;

        public User? CurrentUser { get; private set; }

        public AuthenticationService(
            IServiceScopeFactory scopeFactory,
            IPasswordHasher passwordHasher,
            IActivityLogService activityLogService)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _activityLogService = activityLogService ?? throw new ArgumentNullException(nameof(activityLogService));
        }

        public async Task<User?> LoginAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return null;

            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
                
                var user = await context.Users
                    .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                    .FirstOrDefaultAsync(u => u.Username == username && !u.IsDeleted && u.IsActive);

                if (user == null)
                {
                    await _activityLogService.LogActivityAsync("Login Failed", $"Failed login attempt for username: {username}");
                    return null;
                }

                var isValid = _passwordHasher.VerifyPassword(password, user.PasswordHash);
                if (!isValid)
                {
                    await _activityLogService.LogActivityAsync("Login Failed", $"Failed login attempt for username: {username} (Invalid Password)", user.Id);
                    return null;
                }

                CurrentUser = user;
                await _activityLogService.LogActivityAsync("Login", $"User {username} logged in successfully.", user.Id);
                return user;
            }
        }

        public void Logout()
        {
            if (CurrentUser != null)
            {
                _activityLogService.LogActivityAsync("Logout", $"User {CurrentUser.Username} logged out.", CurrentUser.Id).Wait();
                CurrentUser = null;
            }
        }

        public async Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
                
                var user = await context.Users.FindAsync(userId);
                if (user == null || user.IsDeleted || !user.IsActive)
                    return false;

                if (!_passwordHasher.VerifyPassword(oldPassword, user.PasswordHash))
                {
                    await _activityLogService.LogActivityAsync("Change Password Failed", "Invalid old password provided.", userId);
                    return false;
                }

                user.PasswordHash = _passwordHasher.HashPassword(newPassword);
                user.UpdatedAt = DateTime.UtcNow;
                await context.SaveChangesAsync();

                await _activityLogService.LogActivityAsync("Change Password", "User changed password successfully.", userId);
                return true;
            }
        }

        public async Task<bool> VerifyUserPermissionAsync(int userId, string permissionCode)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
                
                var user = await context.Users
                    .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                    .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted && u.IsActive);

                if (user == null)
                    return false;

                if (user.UserRoles.Any(ur => ur.Role.Name == "Super Admin"))
                    return true;

                var roleIds = user.UserRoles.Select(ur => ur.RoleId).ToList();

                var hasPermission = await context.RolePermissions
                    .Include(rp => rp.Permission)
                    .AnyAsync(rp => roleIds.Contains(rp.RoleId) && rp.Permission.Code == permissionCode);

                return hasPermission;
            }
        }
    }
}
