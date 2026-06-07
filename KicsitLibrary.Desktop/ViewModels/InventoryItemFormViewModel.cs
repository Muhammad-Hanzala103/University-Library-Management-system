using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Desktop.ViewModels;

public partial class InventoryItemFormViewModel(IInventoryService service) : ObservableObject
{
    private int? _itemId;
    public event Action<bool>? CloseRequested;
    public IReadOnlyList<InventoryItemType> ItemTypes { get; } = Enum.GetValues<InventoryItemType>();
    [ObservableProperty] private string _title = "Add Inventory Item";
    [ObservableProperty] private string _itemName = "";
    [ObservableProperty] private InventoryItemType _itemType;
    [ObservableProperty] private int _quantity;
    [ObservableProperty] private int _availableQuantity;
    [ObservableProperty] private int _damagedQuantity;
    [ObservableProperty] private string _location = "";
    [ObservableProperty] private string _condition = "Good";
    [ObservableProperty] private DateTime _purchaseDate = DateTime.Today;
    [ObservableProperty] private decimal _purchasePrice;
    [ObservableProperty] private string _supplier = "";
    [ObservableProperty] private string _remarks = "";
    [ObservableProperty] private string _statusMessage = "";

    public async Task LoadAsync(int? itemId)
    {
        _itemId = itemId;
        if (!itemId.HasValue) return;
        Title = "Edit Inventory Item";
        var x = await service.GetInventoryItemDetailsAsync(itemId.Value);
        ItemName = x.ItemName; ItemType = x.ItemType; Quantity = x.Quantity; AvailableQuantity = x.AvailableQuantity;
        DamagedQuantity = x.DamagedQuantity; Location = x.Location; Condition = x.Condition; PurchaseDate = x.PurchaseDate;
        PurchasePrice = x.PurchasePrice; Supplier = x.Supplier; Remarks = x.Remarks;
    }

    [RelayCommand] private async Task SaveAsync()
    {
        var request = new InventoryItemDetails
        {
            ItemName = ItemName, ItemType = ItemType, Quantity = Quantity, AvailableQuantity = AvailableQuantity,
            DamagedQuantity = DamagedQuantity, Location = Location, Condition = Condition, PurchaseDate = PurchaseDate,
            PurchasePrice = PurchasePrice, Supplier = Supplier, Remarks = Remarks
        };
        var result = _itemId.HasValue
            ? await service.UpdateInventoryItemAsync(_itemId.Value, request)
            : await service.CreateInventoryItemAsync(request);
        StatusMessage = result.Succeeded ? result.Message : result.ErrorMessage ?? result.Message;
        if (result.Succeeded) CloseRequested?.Invoke(true);
    }
    [RelayCommand] private void Cancel() => CloseRequested?.Invoke(false);
}
