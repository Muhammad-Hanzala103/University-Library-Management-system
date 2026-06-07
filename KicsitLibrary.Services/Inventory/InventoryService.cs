using System.Text.Json;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Services.Inventory;

public sealed class InventoryService(
    KicsitLibraryDbContext context,
    IAuthenticationService authenticationService) : IInventoryService
{
    public async Task<IReadOnlyList<InventoryItemListItem>> GetInventoryItemsAsync(
        InventoryFilter filter, CancellationToken cancellationToken = default)
    {
        await RequireViewAsync();
        filter ??= new InventoryFilter();
        var query = filter.IncludeDeleted
            ? context.InventoryItems.IgnoreQueryFilters().AsNoTracking()
            : context.InventoryItems.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.SearchText))
            query = query.Where(item => item.ItemName.Contains(filter.SearchText) ||
                (item.Supplier != null && item.Supplier.Contains(filter.SearchText)) ||
                (item.Remarks != null && item.Remarks.Contains(filter.SearchText)));
        if (filter.ItemType.HasValue) query = query.Where(item => item.ItemType == filter.ItemType);
        if (!string.IsNullOrWhiteSpace(filter.Condition)) query = query.Where(item => item.Condition.Contains(filter.Condition));
        if (!string.IsNullOrWhiteSpace(filter.Location)) query = query.Where(item => item.Location.Contains(filter.Location));
        if (filter.DamagedOnly) query = query.Where(item => item.DamagedQuantity > 0);
        if (filter.LowQuantityOnly) query = query.Where(item => item.AvailableQuantity <= 2);
        if (filter.FromDate.HasValue) query = query.Where(item => item.PurchaseDate >= filter.FromDate.Value.Date);
        if (filter.ToDate.HasValue) query = query.Where(item => item.PurchaseDate < filter.ToDate.Value.Date.AddDays(1));
        return await query.OrderBy(item => item.ItemName).Select(item => MapList(item)).ToListAsync(cancellationToken);
    }

    public async Task<InventoryItemDetails> GetInventoryItemDetailsAsync(int inventoryItemId, CancellationToken cancellationToken = default)
    {
        await RequireViewAsync();
        var item = await context.InventoryItems.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(value => value.Id == inventoryItemId, cancellationToken) ??
            throw new InvalidOperationException("Inventory item was not found.");
        return MapDetails(item);
    }

    public async Task<InventoryActionResult> CreateInventoryItemAsync(InventoryItemDetails request, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync()) return Failure("The current user cannot create inventory items.");
        var validation = Validate(request);
        if (validation != null) return Failure(validation);
        var item = new InventoryItem();
        Apply(item, request);
        return await SaveAsync(item, "Inventory Item Created", "Inventory item created.", cancellationToken);
    }

    public async Task<InventoryActionResult> UpdateInventoryItemAsync(int inventoryItemId, InventoryItemDetails request, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync()) return Failure("The current user cannot update inventory items.");
        var validation = Validate(request);
        if (validation != null) return Failure(validation);
        var item = await context.InventoryItems.FindAsync([inventoryItemId], cancellationToken);
        if (item == null) return Failure("Inventory item was not found.");
        Apply(item, request);
        return await SaveAsync(item, "Inventory Item Updated", "Inventory item updated.", cancellationToken);
    }

    public async Task<InventoryActionResult> AdjustInventoryQuantityAsync(int inventoryItemId, InventoryAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync()) return Failure("The current user cannot adjust inventory.");
        if (string.IsNullOrWhiteSpace(request.Reason)) return Failure("Quantity adjustment reason is required.");
        var validation = ValidateQuantities(request.Quantity, request.AvailableQuantity, request.DamagedQuantity);
        if (validation != null) return Failure(validation);
        var item = await context.InventoryItems.FindAsync([inventoryItemId], cancellationToken);
        if (item == null) return Failure("Inventory item was not found.");
        item.Quantity = request.Quantity;
        item.AvailableQuantity = request.AvailableQuantity;
        item.DamagedQuantity = request.DamagedQuantity;
        item.LastUpdatedDate = DateTime.UtcNow;
        return await SaveAsync(item, "Inventory Quantity Adjusted", $"Inventory quantity adjusted. Reason={Sanitize(request.Reason)}", cancellationToken);
    }

    public Task<InventoryActionResult> MarkInventoryDamagedAsync(int inventoryItemId, int quantity, string reason, CancellationToken cancellationToken = default) =>
        MoveQuantityAsync(inventoryItemId, quantity, reason, true, cancellationToken);

    public Task<InventoryActionResult> MarkInventoryRepairedAsync(int inventoryItemId, int quantity, string reason, CancellationToken cancellationToken = default) =>
        MoveQuantityAsync(inventoryItemId, quantity, reason, false, cancellationToken);

    public async Task<InventoryActionResult> SoftDeleteInventoryItemAsync(int inventoryItemId, string reason, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync()) return Failure("The current user cannot delete inventory items.");
        if (string.IsNullOrWhiteSpace(reason)) return Failure("A delete reason is required.");
        var item = await context.InventoryItems.FindAsync([inventoryItemId], cancellationToken);
        if (item == null) return Failure("Inventory item was not found.");
        var userId = authenticationService.CurrentUser!.Id;
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        item.IsDeleted = true; item.DeletedAt = DateTime.UtcNow; item.DeletedReason = reason.Trim(); item.DeletedByUserId = userId;
        context.DeletedRecordArchives.Add(new DeletedRecordArchive
        {
            TableName = "InventoryItems", RecordId = item.Id,
            SerializedData = JsonSerializer.Serialize(new { item.ItemName, item.ItemType, item.Quantity, item.AvailableQuantity, item.DamagedQuantity }),
            DeletedByUserId = userId, DeletedAt = DateTime.UtcNow, DeletedReason = reason.Trim()
        });
        AddLog("Inventory Item Deleted", item, $"Reason={Sanitize(reason)}");
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new InventoryActionResult { Succeeded = true, Message = "Inventory item was soft-deleted." };
    }

    public async Task<InventoryActionResult> RestoreInventoryItemAsync(int inventoryItemId, string reason, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync()) return Failure("The current user cannot restore inventory items.");
        if (string.IsNullOrWhiteSpace(reason)) return Failure("A restore reason is required.");
        var item = await context.InventoryItems.IgnoreQueryFilters().FirstOrDefaultAsync(value => value.Id == inventoryItemId, cancellationToken);
        if (item == null) return Failure("Inventory item was not found.");
        item.IsDeleted = false; item.DeletedAt = null; item.DeletedReason = null; item.DeletedByUserId = null;
        return await SaveAsync(item, "Inventory Item Restored", $"Inventory item restored. Reason={Sanitize(reason)}", cancellationToken);
    }

    public async Task<InventoryStatusSummary> GetInventorySummaryAsync(CancellationToken cancellationToken = default)
    {
        await RequireViewAsync();
        var active = await context.InventoryItems.AsNoTracking().ToListAsync(cancellationToken);
        return new InventoryStatusSummary
        {
            TotalItems = active.Count, TotalQuantity = active.Sum(x => x.Quantity),
            AvailableQuantity = active.Sum(x => x.AvailableQuantity), DamagedQuantity = active.Sum(x => x.DamagedQuantity),
            LowQuantityItems = active.Count(x => x.AvailableQuantity <= 2),
            DeletedItems = await context.InventoryItems.IgnoreQueryFilters().CountAsync(x => x.IsDeleted, cancellationToken)
        };
    }

    private async Task<InventoryActionResult> MoveQuantityAsync(int id, int quantity, string reason, bool damaged, CancellationToken token)
    {
        if (!await CanManageAsync()) return Failure("The current user cannot change inventory condition.");
        if (string.IsNullOrWhiteSpace(reason)) return Failure($"{(damaged ? "Damage" : "Repair")} reason is required.");
        if (quantity <= 0) return Failure("Quantity must be greater than zero.");
        var item = await context.InventoryItems.FindAsync([id], token);
        if (item == null) return Failure("Inventory item was not found.");
        if (damaged && item.AvailableQuantity < quantity) return Failure("Damaged quantity exceeds available quantity.");
        if (!damaged && item.DamagedQuantity < quantity) return Failure("Repaired quantity exceeds damaged quantity.");
        item.AvailableQuantity += damaged ? -quantity : quantity;
        item.DamagedQuantity += damaged ? quantity : -quantity;
        item.Condition = item.DamagedQuantity == 0 ? "Good" : "Damaged";
        item.LastUpdatedDate = DateTime.UtcNow;
        var action = damaged ? "Inventory Marked Damaged" : "Inventory Marked Repaired";
        return await SaveAsync(item, action, $"{action}. Quantity={quantity};Reason={Sanitize(reason)}", token);
    }

    private async Task<InventoryActionResult> SaveAsync(InventoryItem item, string action, string message, CancellationToken token)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(token);
        if (item.Id == 0) context.InventoryItems.Add(item);
        item.LastUpdatedDate = DateTime.UtcNow;
        await context.SaveChangesAsync(token);
        AddLog(action, item, message);
        await context.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return new InventoryActionResult { Succeeded = true, Message = message, InventoryItem = MapDetails(item) };
    }

    private void AddLog(string action, InventoryItem item, string detail) => context.ActivityLogs.Add(new ActivityLog
    {
        Action = action, UserId = authenticationService.CurrentUser!.Id, IpAddress = "127.0.0.1",
        Detail = $"EntityName=InventoryItem;EntityId={item.Id};ItemName={Sanitize(item.ItemName)};{detail}"
    });
    private async Task RequireViewAsync() { if (!await InventoryAuthorization.CanViewAsync(authenticationService)) throw new UnauthorizedAccessException("The current user cannot view inventory."); }
    private Task<bool> CanManageAsync() => InventoryAuthorization.CanManageAsync(authenticationService);
    private static string? Validate(InventoryItemDetails request) =>
        request == null ? "Inventory data is required." :
        string.IsNullOrWhiteSpace(request.ItemName) ? "Item name is required." :
        !Enum.IsDefined(request.ItemType) ? "Item type is required." :
        ValidateQuantities(request.Quantity, request.AvailableQuantity, request.DamagedQuantity);
    private static string? ValidateQuantities(int total, int available, int damaged) =>
        total < 0 ? "Quantity cannot be negative." :
        available < 0 ? "Available quantity cannot be negative." :
        damaged < 0 ? "Damaged quantity cannot be negative." :
        available + damaged > total ? "Available plus damaged quantity cannot exceed total quantity." : null;
    private static void Apply(InventoryItem item, InventoryItemDetails value)
    {
        item.ItemName = value.ItemName.Trim(); item.ItemType = value.ItemType; item.Quantity = value.Quantity;
        item.AvailableQuantity = value.AvailableQuantity; item.DamagedQuantity = value.DamagedQuantity;
        item.Location = value.Location?.Trim() ?? ""; item.Condition = value.Condition?.Trim() ?? "Good";
        item.PurchaseDate = value.PurchaseDate; item.PurchasePrice = value.PurchasePrice;
        item.Supplier = value.Supplier?.Trim(); item.Remarks = value.Remarks?.Trim();
    }
    private static InventoryItemListItem MapList(InventoryItem x) => new()
    {
        InventoryItemId = x.Id, ItemName = x.ItemName, ItemType = x.ItemType, Quantity = x.Quantity,
        AvailableQuantity = x.AvailableQuantity, DamagedQuantity = x.DamagedQuantity, Location = x.Location,
        Condition = x.Condition, PurchaseDate = x.PurchaseDate, PurchasePrice = x.PurchasePrice,
        Supplier = x.Supplier ?? "", LastUpdatedDate = x.LastUpdatedDate, Status = x.IsDeleted ? "Deleted" : "Active", Remarks = x.Remarks ?? ""
    };
    private static InventoryItemDetails MapDetails(InventoryItem x)
    {
        var row = MapList(x);
        return new InventoryItemDetails
        {
            InventoryItemId = row.InventoryItemId, ItemName = row.ItemName, ItemType = row.ItemType,
            Quantity = row.Quantity, AvailableQuantity = row.AvailableQuantity, DamagedQuantity = row.DamagedQuantity,
            Location = row.Location, Condition = row.Condition, PurchaseDate = row.PurchaseDate, PurchasePrice = row.PurchasePrice,
            Supplier = row.Supplier, LastUpdatedDate = row.LastUpdatedDate, Status = row.Status, Remarks = row.Remarks,
            ImagePath = x.ImagePath, DocumentPath = x.DocumentPath, CreatedAt = x.CreatedAt, UpdatedAt = x.UpdatedAt
        };
    }
    private static InventoryActionResult Failure(string error) => new() { Message = "Inventory action failed.", ErrorMessage = error };
    private static string Sanitize(string value) => value.Replace(";", ",").Replace("=", "-");
}
