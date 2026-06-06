using System.Threading;
using System.Threading.Tasks;
using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Core.Interfaces
{
    public interface IOverdueSchedulerService
    {
        Task<OverdueSchedulerStatus> GetStatusAsync(
            CancellationToken cancellationToken = default);

        Task<OverdueSchedulerRunResult> RunAsync(
            int? userId = null,
            CancellationToken cancellationToken = default);

        Task<OverdueSchedulerRunResult> RunManualOverdueCheckAsync(
            int? userId = null,
            CancellationToken cancellationToken = default);
    }
}
