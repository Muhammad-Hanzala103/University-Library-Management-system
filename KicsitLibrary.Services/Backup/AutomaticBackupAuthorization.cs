using KicsitLibrary.Core.Interfaces;

namespace KicsitLibrary.Services.Backup;

internal static class AutomaticBackupAuthorization
{
    public static async Task<bool> CanManageAsync(
        IAuthenticationService authenticationService)
    {
        var user = authenticationService.CurrentUser;
        if (user == null)
        {
            return false;
        }

        var roles = user.UserRoles
            .Select(item => item.Role?.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (roles.Contains("Super Admin") || roles.Contains("Admin"))
        {
            return true;
        }

        return await authenticationService.VerifyUserPermissionAsync(
                user.Id,
                "MANAGE_AUTOMATIC_BACKUPS") &&
            await authenticationService.VerifyUserPermissionAsync(
                user.Id,
                "MANAGE_BACKUPS");
    }
}
