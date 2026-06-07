using CommunityToolkit.Mvvm.ComponentModel;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Desktop.ViewModels;

public partial class InventoryDetailsViewModel(IInventoryService service) : ObservableObject
{
    [ObservableProperty] private InventoryItemDetails _item = new();
    public async Task LoadAsync(int id) => Item = await service.GetInventoryItemDetailsAsync(id);
}
