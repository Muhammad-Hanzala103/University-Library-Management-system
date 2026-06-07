using CommunityToolkit.Mvvm.ComponentModel;
using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Desktop.ViewModels;

public partial class BackupDetailsViewModel : ObservableObject
{
    [ObservableProperty]
    private BackupHistoryItem _backup = new();

    public void Load(BackupHistoryItem backup) => Backup = backup;
}
