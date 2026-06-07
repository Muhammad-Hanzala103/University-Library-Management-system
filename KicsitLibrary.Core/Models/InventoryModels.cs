using KicsitLibrary.Core.Enums;

namespace KicsitLibrary.Core.Models;

public sealed class InventoryFilter
{
    public string? SearchText { get; set; }
    public InventoryItemType? ItemType { get; set; }
    public string? Condition { get; set; }
    public string? Location { get; set; }
    public bool DamagedOnly { get; set; }
    public bool LowQuantityOnly { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public bool IncludeDeleted { get; set; }
}

public class InventoryItemListItem
{
    public int InventoryItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public InventoryItemType ItemType { get; set; }
    public int Quantity { get; set; }
    public int AvailableQuantity { get; set; }
    public int DamagedQuantity { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public DateTime PurchaseDate { get; set; }
    public decimal PurchasePrice { get; set; }
    public string Supplier { get; set; } = string.Empty;
    public DateTime LastUpdatedDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
}

public sealed class InventoryItemDetails : InventoryItemListItem
{
    public string? ImagePath { get; set; }
    public string? DocumentPath { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string UpdatedBy { get; set; } = string.Empty;
}

public sealed class InventoryAdjustmentRequest
{
    public int Quantity { get; set; }
    public int AvailableQuantity { get; set; }
    public int DamagedQuantity { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class InventoryActionResult
{
    public bool Succeeded { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public InventoryItemDetails? InventoryItem { get; set; }
}

public sealed class InventoryStatusSummary
{
    public int TotalItems { get; set; }
    public int TotalQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public int DamagedQuantity { get; set; }
    public int LowQuantityItems { get; set; }
    public int DeletedItems { get; set; }
}

public sealed class InventoryAttachmentItem
{
    public string FileName { get; set; } = string.Empty;
    public string DocumentPath { get; set; } = string.Empty;
}

public sealed class StockVerificationFilter
{
    public int? SessionId { get; set; }
    public string? SearchText { get; set; }
    public string? Category { get; set; }
    public string? Department { get; set; }
    public string? Rack { get; set; }
    public string? Shelf { get; set; }
    public BookStatus? ExpectedStatus { get; set; }
    public BookStatus? ActualStatus { get; set; }
    public bool MismatchedOnly { get; set; }
    public bool UnverifiedOnly { get; set; }
}

public sealed class StockVerificationSession
{
    public int StockVerificationSessionId { get; set; }
    public string SessionNumber { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StartedBy { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
}

public sealed class StockVerificationItem
{
    public int StockVerificationItemId { get; set; }
    public int SessionId { get; set; }
    public int BookCopyId { get; set; }
    public string AccessionNumber { get; set; } = string.Empty;
    public string BookTitle { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Rack { get; set; } = string.Empty;
    public string Shelf { get; set; } = string.Empty;
    public BookStatus ExpectedStatus { get; set; }
    public BookStatus? ActualStatus { get; set; }
    public string VerificationRemarks { get; set; } = string.Empty;
    public DateTime? VerifiedAt { get; set; }
    public string VerifiedBy { get; set; } = string.Empty;
    public bool IsMismatch { get; set; }
    public bool IsReconciled { get; set; }
}

public sealed class StockVerificationResult
{
    public bool Succeeded { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public StockVerificationSession? Session { get; set; }
    public StockVerificationItem? Item { get; set; }
    public StockVerificationSummary? Summary { get; set; }
}

public sealed class StockVerificationSummary
{
    public int TotalCopies { get; set; }
    public int AvailableCount { get; set; }
    public int IssuedCount { get; set; }
    public int ReservedCount { get; set; }
    public int LostCount { get; set; }
    public int DamagedCount { get; set; }
    public int MissingCount { get; set; }
    public int UnderRepairCount { get; set; }
    public int DeletedCount { get; set; }
    public int MatchedCount { get; set; }
    public int MismatchedCount { get; set; }
    public int UnverifiedCount { get; set; }
}
