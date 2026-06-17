using System;
using System.Threading;
using System.Threading.Tasks;

namespace KicsitLibrary.Core.Interfaces
{
    public class SmsSendResult
    {
        public bool Succeeded { get; set; }
        public string? FailureReason { get; set; }
        public string? MessageSid { get; set; }
    }

    public interface ISmsTransport
    {
        Task<SmsSendResult> SendSmsAsync(string toPhone, string message, bool isWhatsApp = false, CancellationToken cancellationToken = default);
    }
}
