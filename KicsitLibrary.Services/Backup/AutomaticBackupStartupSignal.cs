namespace KicsitLibrary.Services.Backup;

public sealed class AutomaticBackupStartupSignal
{
    private readonly TaskCompletionSource _ready =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void MarkReady() => _ready.TrySetResult();

    public Task WaitAsync(CancellationToken cancellationToken) =>
        _ready.Task.WaitAsync(cancellationToken);
}
