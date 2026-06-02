using System;

namespace KicsitLibrary.Core.Entities
{
    public class ActivityLog : EntityBase
    {
        public string Action { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
        
        public int? UserId { get; set; }
        public virtual User? User { get; set; }
    }
}
