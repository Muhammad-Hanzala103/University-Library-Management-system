using System.Threading.Tasks;
using KicsitLibrary.Core.Entities;

namespace KicsitLibrary.Core.Interfaces
{
    public interface IAuthenticationService
    {
        Task<User?> LoginAsync(string username, string password);
        Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword);
        Task<bool> VerifyUserPermissionAsync(int userId, string permissionCode);
        User? CurrentUser { get; }
        Task LogoutAsync();
    }
}
