using System.Collections.Generic;

namespace KicsitLibrary.Core.Entities
{
    public class Author : EntityBase
    {
        public string Name { get; set; } = string.Empty;
        public string? AlternateName { get; set; }
        public string? Biography { get; set; }
        public string? Language { get; set; }
        public bool ActiveStatus { get; set; } = true;

        public virtual ICollection<BookAuthor> BookAuthors { get; set; } = new List<BookAuthor>();
    }
}
