using KicsitLibrary.Core.Interfaces;

namespace KicsitLibrary.Services.Backup;

internal static class BackupAuthorization
{
    public static async Task<bool> CanViewAsync(IAuthenticationService authenticationService)
    {
        var user = authenticationService.CurrentUser;
        if (user == null)
        {
            return false;
        }

        var roles = user.UserRoles
            .Select(item => item.Role?.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return roles.Contains("Super Admin") ||
            roles.Contains("Admin") ||
            roles.Contains("Librarian") ||
            roles.Contains("Auditor") ||
            await authenticationService.VerifyUserPermissionAsync(user.Id, "VIEW_BACKUPS");
    }

    public static async Task<bool> CanManageAsync(IAuthenticationService authenticationService)
    {
        var user = authenticationService.CurrentUser;
        if (user == null)
        {
            return false;
        }

        var roles = user.UserRoles
            .Select(item => item.Role?.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return roles.Contains("Super Admin") ||
            roles.Contains("Admin") ||
            await authenticationService.VerifyUserPermissionAsync(user.Id, "MANAGE_BACKUPS");
    }
}
