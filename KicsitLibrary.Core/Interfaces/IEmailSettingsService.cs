using System.Threading.Tasks;
using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Core.Interfaces
{
    public interface IEmailSettingsService
    {
        Task<EmailTransportOptions> GetOptionsAsync();
        Task<EmailSettingsValidationResult> ValidateAsync();
    }
}
