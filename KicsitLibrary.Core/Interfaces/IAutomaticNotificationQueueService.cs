using System.Threading;
using System.Threading.Tasks;
using KicsitLibrary.Core.Entities;

namespace KicsitLibrary.Core.Interfaces
{
    public interface IAutomaticNotificationQueueService
    {
        Task EnqueueMessageAsync(string recipient, string message, QueueMessageChannel channel, CancellationToken cancellationToken = default);
        Task ProcessQueueAsync(CancellationToken cancellationToken = default);
    }
}
