using System.Threading;
using System.Threading.Tasks;
using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Core.Interfaces
{
    public interface IEmailTransport
    {
        Task<EmailSendResult> SendAsync(
            EmailMessage message,
            EmailTransportOptions options,
            CancellationToken cancellationToken = default);
    }
}
