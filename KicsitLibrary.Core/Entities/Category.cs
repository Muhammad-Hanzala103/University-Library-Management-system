using System.Collections.Generic;

namespace KicsitLibrary.Core.Entities
{
    public class Category : EntityBase
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        public int? ParentCategoryId { get; set; }
        public virtual Category? ParentCategory { get; set; }

        public virtual ICollection<BookMaster> BookMasters { get; set; } = new List<BookMaster>();
    }
}
