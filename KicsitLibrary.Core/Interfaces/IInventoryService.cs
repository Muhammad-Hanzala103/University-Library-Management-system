using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Core.Interfaces;

public interface IInventoryService
{
    Task<IReadOnlyList<InventoryItemListItem>> GetInventoryItemsAsync(InventoryFilter filter, CancellationToken cancellationToken = default);
    Task<InventoryItemDetails> GetInventoryItemDetailsAsync(int inventoryItemId, CancellationToken cancellationToken = default);
    Task<InventoryActionResult> CreateInventoryItemAsync(InventoryItemDetails request, CancellationToken cancellationToken = default);
    Task<InventoryActionResult> UpdateInventoryItemAsync(int inventoryItemId, InventoryItemDetails request, CancellationToken cancellationToken = default);
    Task<InventoryActionResult> AdjustInventoryQuantityAsync(int inventoryItemId, InventoryAdjustmentRequest request, CancellationToken cancellationToken = default);
    Task<InventoryActionResult> MarkInventoryDamagedAsync(int inventoryItemId, int quantity, string reason, CancellationToken cancellationToken = default);
    Task<InventoryActionResult> MarkInventoryRepairedAsync(int inventoryItemId, int quantity, string reason, CancellationToken cancellationToken = default);
    Task<InventoryActionResult> SoftDeleteInventoryItemAsync(int inventoryItemId, string reason, CancellationToken cancellationToken = default);
    Task<InventoryActionResult> RestoreInventoryItemAsync(int inventoryItemId, string reason, CancellationToken cancellationToken = default);
    Task<InventoryStatusSummary> GetInventorySummaryAsync(CancellationToken cancellationToken = default);
}
