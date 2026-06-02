using System.Collections.Generic;

namespace KicsitLibrary.Core.Entities
{
    public class Publisher : EntityBase
    {
        public string Name { get; set; } = string.Empty;
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? Contact { get; set; }
        public string? Email { get; set; }
        public string? Website { get; set; }
        public bool ActiveStatus { get; set; } = true;

        public virtual ICollection<BookMaster> BookMasters { get; set; } = new List<BookMaster>();
    }
}
