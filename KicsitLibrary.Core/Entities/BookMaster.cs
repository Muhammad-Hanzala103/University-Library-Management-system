using System;
using System.Collections.Generic;
using KicsitLibrary.Core.Enums;

namespace KicsitLibrary.Core.Entities
{
    public class BookMaster : EntityBase
    {
        public string Title { get; set; } = string.Empty;
        public string? SubTitle { get; set; }
        public string UniqueTitleNumber { get; set; } = string.Empty;
        public string? Edition { get; set; }
        
        public int PublisherId { get; set; }
        public virtual Publisher Publisher { get; set; } = null!;
        
        public string? PublicationPlace { get; set; }
        public int PublicationYear { get; set; }
        public int? CopyrightYear { get; set; }
        public string? Series { get; set; }
        public string? Language { get; set; }
        public string? Format { get; set; }
        public string? BindingType { get; set; }
        public string? PhysicalDescription { get; set; }
        public string? Keywords { get; set; }
        public string? Notes { get; set; }
        public string? Contents { get; set; }
        
        public string? ISBN { get; set; }
        public string? ISSN { get; set; }
        
        public string? Source { get; set; }
        public string? StoreName { get; set; }
        public string? BillNumber { get; set; }
        public string? BookImage { get; set; }
        
        public int CategoryId { get; set; }
        public virtual Category Category { get; set; } = null!;
        
        public int DepartmentCategoryId { get; set; }
        public virtual DepartmentCategory DepartmentCategory { get; set; } = null!;
        
        public int LiteratureCategoryId { get; set; }
        public virtual LiteratureCategory LiteratureCategory { get; set; } = null!;
        
        public string? Subject { get; set; }
        public string? ClassificationNumber { get; set; }
        public string? CallNumber { get; set; }
        public string? DeweyNumber { get; set; }
        
        public string? AccessionType { get; set; }
        public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;
        public decimal PurchasePrice { get; set; }
        public string? Supplier { get; set; }
        public string? InvoiceFile { get; set; }
        
        public BookStatus Status { get; set; } = BookStatus.Available;
        public MaterialType MaterialType { get; set; } = MaterialType.Book;
        
        public int? CreatedByUserId { get; set; }
        public int? UpdatedByUserId { get; set; }

        public virtual ICollection<BookAuthor> BookAuthors { get; set; } = new List<BookAuthor>();
        public virtual ICollection<BookCopy> BookCopies { get; set; } = new List<BookCopy>();
        public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    }
}
