using System.Threading.Tasks;
using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Core.Interfaces
{
    public interface IDashboardService
    {
        Task<DashboardStats> GetDashboardStatsAsync();
    }
}
