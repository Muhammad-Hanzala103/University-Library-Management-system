using System;

namespace KicsitLibrary.Core.Entities
{
    public class SystemSettings : EntityBase
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Group { get; set; } = "General";
        
        public int? UpdatedByUserId { get; set; }
        public virtual User? UpdatedByUser { get; set; }
    }
}
