using System;
using System.Threading;
using System.Threading.Tasks;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace KicsitLibrary.Services.Notifications
{
    public class MailKitEmailTransport : IEmailTransport
    {
        public async Task<EmailSendResult> SendAsync(
            EmailMessage message,
            EmailTransportOptions options,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(options);

            try
            {
                var mimeMessage = new MimeMessage();
                mimeMessage.From.Add(new MailboxAddress(message.FromName, message.FromEmail));
                mimeMessage.To.Add(new MailboxAddress(message.ToName, message.ToEmail));
                mimeMessage.Subject = message.Subject;

                var bodyBuilder = new BodyBuilder
                {
                    TextBody = message.PlainTextBody
                };
                if (!string.IsNullOrWhiteSpace(message.HtmlBody))
                {
                    bodyBuilder.HtmlBody = message.HtmlBody;
                }
                mimeMessage.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                var socketOptions = options.UseSsl
                    ? options.Port == 465
                        ? SecureSocketOptions.SslOnConnect
                        : SecureSocketOptions.StartTls
                    : SecureSocketOptions.None;

                await client.ConnectAsync(
                    options.Host,
                    options.Port,
                    socketOptions,
                    cancellationToken);
                await client.AuthenticateAsync(
                    options.User,
                    options.Password,
                    cancellationToken);
                var providerMessageId = await client.SendAsync(mimeMessage, cancellationToken);
                await client.DisconnectAsync(true, cancellationToken);

                return new EmailSendResult
                {
                    Succeeded = true,
                    ProviderMessageId = providerMessageId,
                    SentAt = DateTime.UtcNow
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new EmailSendResult
                {
                    Succeeded = false,
                    FailureReason = ex.Message
                };
            }
        }
    }
}
