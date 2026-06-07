using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Interfaces;

namespace KicsitLibrary.Desktop.ViewModels;

public partial class InventoryAdjustmentViewModel(IInventoryService service) : ObservableObject
{
    private int _itemId;
    public event Action<bool>? CloseRequested;
    [ObservableProperty] private string _itemName = "";
    [ObservableProperty] private int _quantity;
    [ObservableProperty] private int _availableQuantity;
    [ObservableProperty] private int _damagedQuantity;
    [ObservableProperty] private string _reason = "";
    [ObservableProperty] private string _statusMessage = "";
    public async Task LoadAsync(int id)
    {
        _itemId = id; var item = await service.GetInventoryItemDetailsAsync(id);
        ItemName = item.ItemName; Quantity = item.Quantity; AvailableQuantity = item.AvailableQuantity; DamagedQuantity = item.DamagedQuantity;
    }
    [RelayCommand] private async Task SaveAsync()
    {
        var result = await service.AdjustInventoryQuantityAsync(_itemId, new()
        { Quantity = Quantity, AvailableQuantity = AvailableQuantity, DamagedQuantity = DamagedQuantity, Reason = Reason });
        StatusMessage = result.Succeeded ? result.Message : result.ErrorMessage ?? result.Message;
        if (result.Succeeded) CloseRequested?.Invoke(true);
    }
    [RelayCommand] private void Cancel() => CloseRequested?.Invoke(false);
}
