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
 
        public User? CurrentUser { get; private set; }
 
        public AuthenticationService(
            IServiceScopeFactory scopeFactory,
            IPasswordHasher passwordHasher)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        }
 
        public async Task<User?> LoginAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return null;
 
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
                var logService = scope.ServiceProvider.GetRequiredService<IActivityLogService>();
                
                var user = await context.Users
                    .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                    .FirstOrDefaultAsync(u => u.Username == username && !u.IsDeleted && u.IsActive);
 
                if (user == null)
                {
                    await logService.LogActivityAsync("Login Failed", $"Failed login attempt for username: {username}");
                    return null;
                }
 
                var isValid = _passwordHasher.VerifyPassword(password, user.PasswordHash);
                if (!isValid)
                {
                    await logService.LogActivityAsync("Login Failed", $"Failed login attempt for username: {username} (Invalid Password)", user.Id);
                    return null;
                }
 
                CurrentUser = user;
                await logService.LogActivityAsync("Login", $"User {username} logged in successfully.", user.Id);

                try
                {
                    var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                    if (!string.IsNullOrWhiteSpace(user.Email))
                    {
                        var loginNotif = new NotificationRecord
                        {
                            NotificationType = KicsitLibrary.Core.Enums.NotificationType.SystemAlert,
                            Channel = "Email",
                            RecipientEmail = user.Email,
                            RecipientName = user.FullName,
                            Subject = "New Login Alert",
                            Message = $"Hello {user.FullName},\n\nA new login was detected on your Ilm-o-Kutub library account at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC.\n\nIf this was you, no further action is required. If you do not recognize this activity, please contact the administrator immediately.",
                            Status = KicsitLibrary.Core.Enums.NotificationStatus.Pending
                        };
                        await notificationService.CreateNotificationAsync(loginNotif, cooldownHours: 0, userId: user.Id);
                    }
                }
                catch { /* Optional fire-and-forget notification handling */ }

                return user;
            }
        }
 
        public async Task LogoutAsync()
        {
            if (CurrentUser != null)
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var logService = scope.ServiceProvider.GetRequiredService<IActivityLogService>();
                    await logService.LogActivityAsync("Logout", $"User {CurrentUser.Username} logged out.", CurrentUser.Id);
                }
                CurrentUser = null;
            }
        }
 
        public async Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(oldPassword) || string.IsNullOrWhiteSpace(newPassword))
                return false;

            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
                var logService = scope.ServiceProvider.GetRequiredService<IActivityLogService>();
                
                var user = await context.Users.FindAsync(userId);
                if (user == null || user.IsDeleted || !user.IsActive)
                    return false;
 
                if (!_passwordHasher.VerifyPassword(oldPassword, user.PasswordHash))
                {
                    await logService.LogActivityAsync("Change Password Failed", "Invalid old password provided.", userId);
                    return false;
                }
 
                user.PasswordHash = _passwordHasher.HashPassword(newPassword);
                user.UpdatedAt = DateTime.UtcNow;
                await context.SaveChangesAsync();
 
                await logService.LogActivityAsync("Change Password", "User changed password successfully.", userId);
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

        public async Task<bool> RequestPasswordResetAsync(string usernameOrEmail)
        {
            if (string.IsNullOrWhiteSpace(usernameOrEmail)) return false;

            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
                
                var user = await context.Users.FirstOrDefaultAsync(u => 
                    (u.Username == usernameOrEmail || u.Email == usernameOrEmail) && 
                    !u.IsDeleted && u.IsActive);

                if (user == null || string.IsNullOrWhiteSpace(user.Email))
                    return true; // Return true safely to not expose if user exists

                var rawToken = Guid.NewGuid().ToString("N");
                user.PasswordResetTokenHash = _passwordHasher.HashPassword(rawToken);
                user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddMinutes(15);
                
                await context.SaveChangesAsync();

                // TODO: Queue or send email with rawToken to user.Email
                
                return true;
            }
        }

        public async Task<bool> ResetPasswordAsync(string usernameOrEmail, string token, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(usernameOrEmail) || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(newPassword))
                return false;

            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
                var logService = scope.ServiceProvider.GetRequiredService<IActivityLogService>();

                var user = await context.Users.FirstOrDefaultAsync(u => 
                    (u.Username == usernameOrEmail || u.Email == usernameOrEmail) && 
                    !u.IsDeleted && u.IsActive);

                if (user == null || user.PasswordResetTokenHash == null || user.PasswordResetTokenExpiresAt < DateTime.UtcNow)
                {
                    if (user != null)
                        await logService.LogActivityAsync("Password Reset Failed", "Expired or invalid token.", user.Id);
                    return false;
                }

                if (!_passwordHasher.VerifyPassword(token, user.PasswordResetTokenHash))
                {
                    await logService.LogActivityAsync("Password Reset Failed", "Invalid reset token.", user.Id);
                    return false;
                }

                user.PasswordHash = _passwordHasher.HashPassword(newPassword);
                user.PasswordResetTokenHash = null;
                user.PasswordResetTokenExpiresAt = null;
                user.UpdatedAt = DateTime.UtcNow;

                await context.SaveChangesAsync();
                await logService.LogActivityAsync("Password Reset", "User reset password successfully.", user.Id);

                return true;
            }
        }

        public async Task<bool> GenerateAndSendOtpAsync(int userId)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
                var user = await context.Users.FindAsync(userId);

                if (user == null || string.IsNullOrWhiteSpace(user.Email) || !user.IsTwoFactorEnabled)
                    return false;

                var otp = new Random().Next(100000, 999999).ToString();
                user.PendingOtpHash = _passwordHasher.HashPassword(otp);
                user.PendingOtpExpiresAt = DateTime.UtcNow.AddMinutes(5);
                user.PendingOtpAttempts = 0;

                await context.SaveChangesAsync();

                try
                {
                    var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                    var otpNotif = new NotificationRecord
                    {
                        NotificationType = KicsitLibrary.Core.Enums.NotificationType.TwoFactorOtp,
                        Channel = "Email",
                        RecipientEmail = user.Email,
                        RecipientName = user.FullName,
                        Subject = "Your Login Verification Code",
                        Message = $"Hello {user.FullName},\n\nYour 6-digit verification code is: {otp}\n\nThis code will expire in 5 minutes.",
                        Status = KicsitLibrary.Core.Enums.NotificationStatus.Pending
                    };
                    await notificationService.CreateNotificationAsync(otpNotif, cooldownHours: 0, userId: user.Id);
                }
                catch { }

                return true;
            }
        }

        public async Task<bool> VerifyOtpAsync(int userId, string otp)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
                var logService = scope.ServiceProvider.GetRequiredService<IActivityLogService>();
                var user = await context.Users.FindAsync(userId);

                if (user == null || string.IsNullOrWhiteSpace(otp) || user.PendingOtpHash == null)
                    return false;

                if (user.PendingOtpExpiresAt < DateTime.UtcNow)
                {
                    await logService.LogActivityAsync("2FA Failed", "OTP expired.", user.Id);
                    return false;
                }

                if (user.PendingOtpAttempts >= 3)
                {
                    await logService.LogActivityAsync("2FA Failed", "Too many failed attempts.", user.Id);
                    return false;
                }

                user.PendingOtpAttempts++;

                if (!_passwordHasher.VerifyPassword(otp, user.PendingOtpHash))
                {
                    await context.SaveChangesAsync();
                    await logService.LogActivityAsync("2FA Failed", "Invalid OTP entered.", user.Id);
                    return false;
                }

                user.PendingOtpHash = null;
                user.PendingOtpExpiresAt = null;
                user.PendingOtpAttempts = 0;
                await context.SaveChangesAsync();

                await logService.LogActivityAsync("2FA Success", "User verified OTP successfully.", user.Id);
                return true;
            }
        }
    }
}
