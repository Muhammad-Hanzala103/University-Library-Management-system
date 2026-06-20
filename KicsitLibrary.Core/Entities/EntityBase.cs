using System;

namespace KicsitLibrary.Core.Entities
{
    public abstract class EntityBase
    {
        public int Id { get; set; }
        
        public string TenantId { get; set; } = "default";
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public string? DeletedReason { get; set; }
        public int? DeletedByUserId { get; set; }
    }
}
