using CommunityToolkit.Mvvm.ComponentModel;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Desktop.ViewModels;

public partial class ActivityLogDetailsViewModel(
    IActivityLogBrowserService service) : ObservableObject
{
    [ObservableProperty] private ActivityLogDetails? _details;

    public async Task LoadAsync(int activityLogId)
    {
        Details = await service.GetActivityLogDetailsAsync(activityLogId);
    }
}
