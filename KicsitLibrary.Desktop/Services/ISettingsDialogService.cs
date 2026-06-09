using System.Threading.Tasks;
using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Desktop.Services
{
    public interface ISettingsDialogService
    {
        Task<bool> ShowEditSettingAsync(SettingsItemView item);
        Task ShowSettingDetailsAsync(SettingsItemView item);
    }
}
