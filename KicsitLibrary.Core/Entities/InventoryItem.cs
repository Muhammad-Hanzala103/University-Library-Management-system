using System;
using KicsitLibrary.Core.Enums;

namespace KicsitLibrary.Core.Entities
{
    public class InventoryItem : EntityBase
    {
        public string ItemName { get; set; } = string.Empty;
        public InventoryItemType ItemType { get; set; } = InventoryItemType.Chair;
        public int Quantity { get; set; }
        public int AvailableQuantity { get; set; }
        public int DamagedQuantity { get; set; }
        public string Location { get; set; } = string.Empty;
        public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;
        public decimal PurchasePrice { get; set; }
        public string? Supplier { get; set; }
        public string Condition { get; set; } = "Good";
        public DateTime LastUpdatedDate { get; set; } = DateTime.UtcNow;
        public string? Remarks { get; set; }
        public string? ImagePath { get; set; }
        public string? DocumentPath { get; set; }
    }
}
