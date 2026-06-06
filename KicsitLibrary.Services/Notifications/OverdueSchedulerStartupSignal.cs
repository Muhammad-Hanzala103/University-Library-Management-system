using System.Threading;
using System.Threading.Tasks;

namespace KicsitLibrary.Services.Notifications
{
    public sealed class OverdueSchedulerStartupSignal
    {
        private readonly TaskCompletionSource _ready =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void MarkReady()
        {
            _ready.TrySetResult();
        }

        public Task WaitAsync(CancellationToken cancellationToken)
        {
            return _ready.Task.WaitAsync(cancellationToken);
        }
    }
}
