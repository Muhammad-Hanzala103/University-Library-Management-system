using System.Collections.Generic;

namespace KicsitLibrary.Core.Entities
{
    public class Role : EntityBase
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        
        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
        public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}
