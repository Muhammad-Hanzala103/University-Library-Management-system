using System.Collections.Generic;

namespace KicsitLibrary.Core.Entities
{
    public class Rack : EntityBase
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        public virtual ICollection<Shelf> Shelves { get; set; } = new List<Shelf>();
    }
}
