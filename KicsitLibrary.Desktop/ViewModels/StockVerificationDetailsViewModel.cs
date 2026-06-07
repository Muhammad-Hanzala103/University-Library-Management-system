using CommunityToolkit.Mvvm.ComponentModel;
using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Desktop.ViewModels;

public partial class StockVerificationDetailsViewModel : ObservableObject
{
    [ObservableProperty] private StockVerificationItem _item = new();
    public void Load(StockVerificationItem item) => Item = item;
}
