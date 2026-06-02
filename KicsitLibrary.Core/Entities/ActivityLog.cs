using System;

namespace KicsitLibrary.Core.Entities
{
    public class ActivityLog
    {
        public int Id { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
        
        public int? UserId { get; set; }
        public virtual User? User { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
