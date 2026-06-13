using System.Threading.Tasks;
using KicsitLibrary.Core.Entities;

namespace KicsitLibrary.Core.Interfaces
{
    public interface IAuthenticationService
    {
        Task<User?> LoginAsync(string username, string password);
        Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword);
        
        // Forgot Password and 2FA
        Task<(bool Success, string Message)> RequestPasswordResetAsync(string usernameOrEmail);
        Task<bool> ResetPasswordAsync(string usernameOrEmail, string token, string newPassword);
        
        Task<bool> GenerateAndSendOtpAsync(int userId);
        Task<bool> VerifyOtpAsync(int userId, string otp);
        
        Task<bool> VerifyUserPermissionAsync(int userId, string permissionCode);
        User? CurrentUser { get; }
        Task LogoutAsync();
    }
}
