using System.Collections.Generic;

namespace KicsitLibrary.Core.Entities
{
    public class Permission : EntityBase
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }
        
        public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}
