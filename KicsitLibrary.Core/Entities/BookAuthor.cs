namespace KicsitLibrary.Core.Entities
{
    public class BookAuthor
    {
        public int BookMasterId { get; set; }
        public virtual BookMaster BookMaster { get; set; } = null!;
        
        public int AuthorId { get; set; }
        public virtual Author Author { get; set; } = null!;
    }
}
