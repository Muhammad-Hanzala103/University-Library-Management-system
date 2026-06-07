using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Desktop.ViewModels;

public partial class AuditRecordFormViewModel(
    IAuditRecordService service) : ObservableObject
{
    private int? _auditRecordId;
    public event Action<bool?>? CloseRequested;
    public IReadOnlyList<string> AuditTypes { get; } =
        ["Internal Audit", "External Audit", "Financial Audit", "Stock Audit",
         "HEC Audit", "PEC Audit", "QEC Audit", "NCEAC Audit", "Other"];
    public IReadOnlyList<AuditStatus> StatusValues { get; } = Enum.GetValues<AuditStatus>();

    [ObservableProperty] private string _windowTitle = "Add Audit Record";
    [ObservableProperty] private AuditRecordDetails _record = new()
    {
        AuditDate = DateTime.Today,
        AuditType = "Internal Audit",
        Status = AuditStatus.Draft
    };
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public async Task LoadAsync(int? auditRecordId)
    {
        _auditRecordId = auditRecordId;
        if (auditRecordId.HasValue)
        {
            Record = await service.GetAuditRecordDetailsAsync(auditRecordId.Value);
            WindowTitle = $"Edit Audit Record: {Record.AuditNumber}";
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            var result = _auditRecordId.HasValue
                ? await service.UpdateAuditRecordAsync(_auditRecordId.Value, Record)
                : await service.CreateAuditRecordAsync(Record);
            StatusMessage = result.Succeeded ? result.Message : result.ErrorMessage ?? result.Message;
            if (result.Succeeded)
                CloseRequested?.Invoke(true);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unable to save audit record: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task CancelAsync()
    {
        CloseRequested?.Invoke(false);
        return Task.CompletedTask;
    }
}
