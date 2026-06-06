using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Tests.Infrastructure;

internal sealed class FakeEmailTransport : IEmailTransport
{
    private readonly Queue<EmailSendResult> _results = new();

    public int SendCount { get; private set; }
    public List<EmailMessage> Messages { get; } = [];

    public void EnqueueResult(EmailSendResult result)
    {
        _results.Enqueue(result);
    }

    public Task<EmailSendResult> SendAsync(
        EmailMessage message,
        EmailTransportOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SendCount++;
        Messages.Add(message);

        var result = _results.Count > 0
            ? _results.Dequeue()
            : new EmailSendResult
            {
                Succeeded = true,
                SentAt = DateTime.UtcNow,
                ProviderMessageId = $"fake-{SendCount}"
            };
        return Task.FromResult(result);
    }
}
