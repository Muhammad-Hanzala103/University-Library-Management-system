using System;

namespace KicsitLibrary.Core.Entities
{
    public class NewArrival : EntityBase
    {
        public string ArrivalNumber { get; set; } = string.Empty;
        public string MaterialType { get; set; } = "Book";
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string DepartmentCategory { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int PurchaseYear { get; set; }
        public int PurchaseMonth { get; set; }
        public string? Supplier { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? InvoiceFile { get; set; }
        public DateTime ReceivedDate { get; set; } = DateTime.UtcNow;
        public string? Remarks { get; set; }
    }
}
