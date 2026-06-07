using CommunityToolkit.Mvvm.ComponentModel;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Desktop.ViewModels;

public partial class AuditRecordDetailsViewModel(
    IAuditRecordService service) : ObservableObject
{
    [ObservableProperty] private AuditRecordDetails? _details;

    public async Task LoadAsync(int auditRecordId)
    {
        Details = await service.GetAuditRecordDetailsAsync(auditRecordId);
    }
}
