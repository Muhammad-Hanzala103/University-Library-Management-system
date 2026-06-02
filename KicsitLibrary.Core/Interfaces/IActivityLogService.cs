using System.Threading.Tasks;

namespace KicsitLibrary.Core.Interfaces
{
    public interface IActivityLogService
    {
        Task LogActivityAsync(string action, string detail, int? userId = null, string? ipAddress = null);
    }
}
