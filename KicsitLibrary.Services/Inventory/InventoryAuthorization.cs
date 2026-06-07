using KicsitLibrary.Core.Interfaces;

namespace KicsitLibrary.Services.Inventory;

internal static class InventoryAuthorization
{
    public static async Task<bool> CanViewAsync(IAuthenticationService authenticationService)
    {
        var user = authenticationService.CurrentUser;
        if (user == null) return false;
        var roles = user.UserRoles.Select(item => item.Role?.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return roles.Contains("Super Admin") || roles.Contains("Admin") ||
            roles.Contains("Librarian") || roles.Contains("Auditor") ||
            await authenticationService.VerifyUserPermissionAsync(user.Id, "VIEW_INVENTORY");
    }

    public static async Task<bool> CanManageAsync(IAuthenticationService authenticationService)
    {
        var user = authenticationService.CurrentUser;
        if (user == null) return false;
        var roles = user.UserRoles.Select(item => item.Role?.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return roles.Contains("Super Admin") || roles.Contains("Admin") ||
            roles.Contains("Librarian") ||
            await authenticationService.VerifyUserPermissionAsync(user.Id, "MANAGE_INVENTORY");
    }
}
