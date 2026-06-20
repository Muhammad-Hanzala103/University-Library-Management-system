using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KicsitLibrary.Services.Notifications
{
    public class AutomaticNotificationQueueService : IAutomaticNotificationQueueService
    {
        private readonly IServiceProvider _serviceProvider;

        public AutomaticNotificationQueueService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public async Task EnqueueMessageAsync(string recipient, string message, QueueMessageChannel channel, CancellationToken cancellationToken = default)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();

            var queueItem = new NotificationQueue
            {
                Recipient = recipient.Trim(),
                Message = message.Trim(),
                Channel = channel,
                Status = QueueMessageStatus.Pending,
                AttemptCount = 0
            };

            context.NotificationQueues.Add(queueItem);
            await context.SaveChangesAsync(cancellationToken);
        }

        public async Task ProcessQueueAsync(CancellationToken cancellationToken = default)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
            
            var pendingItems = await context.NotificationQueues
                .Where(q => q.Status == QueueMessageStatus.Pending && q.AttemptCount < 3)
                .OrderBy(q => q.Id)
                .ToListAsync(cancellationToken);

            if (pendingItems.Count == 0) return;

            var twilioTransport = scope.ServiceProvider.GetRequiredService<ISmsTransport>();
            var infobipTransport = scope.ServiceProvider.GetServices<ISmsTransport>().FirstOrDefault(t => t.GetType() == typeof(InfobipSmsTransport));
            var emailTransport = scope.ServiceProvider.GetRequiredService<IEmailTransport>();

            // Email Options resolution
            var emailSettings = scope.ServiceProvider.GetRequiredService<IEmailSettingsService>();
            var emailVal = await emailSettings.ValidateAsync();
            var emailOptions = emailVal.Options;

            foreach (var item in pendingItems)
            {
                item.AttemptCount++;
                try
                {
                    switch (item.Channel)
                    {
                        case QueueMessageChannel.SmsTwilio:
                            var twilioRes = await twilioTransport.SendSmsAsync(item.Recipient, item.Message, false, cancellationToken);
                            if (twilioRes.Succeeded)
                            {
                                item.Status = QueueMessageStatus.Sent;
                                item.SentAt = DateTime.UtcNow;
                                item.ErrorMessage = null;
                            }
                            else
                            {
                                item.Status = item.AttemptCount >= 3 ? QueueMessageStatus.Failed : QueueMessageStatus.Pending;
                                item.ErrorMessage = twilioRes.FailureReason;
                            }
                            break;

                        case QueueMessageChannel.WhatsAppTwilio:
                            var waRes = await twilioTransport.SendSmsAsync(item.Recipient, item.Message, true, cancellationToken);
                            if (waRes.Succeeded)
                            {
                                item.Status = QueueMessageStatus.Sent;
                                item.SentAt = DateTime.UtcNow;
                                item.ErrorMessage = null;
                            }
                            else
                            {
                                item.Status = item.AttemptCount >= 3 ? QueueMessageStatus.Failed : QueueMessageStatus.Pending;
                                item.ErrorMessage = waRes.FailureReason;
                            }
                            break;

                        case QueueMessageChannel.SmsInfobip:
                            if (infobipTransport != null)
                            {
                                var infobipRes = await infobipTransport.SendSmsAsync(item.Recipient, item.Message, false, cancellationToken);
                                if (infobipRes.Succeeded)
                                {
                                    item.Status = QueueMessageStatus.Sent;
                                    item.SentAt = DateTime.UtcNow;
                                    item.ErrorMessage = null;
                                }
                                else
                                {
                                    item.Status = item.AttemptCount >= 3 ? QueueMessageStatus.Failed : QueueMessageStatus.Pending;
                                    item.ErrorMessage = infobipRes.FailureReason;
                                }
                            }
                            else
                            {
                                item.Status = QueueMessageStatus.Failed;
                                item.ErrorMessage = "Infobip transport not registered.";
                            }
                            break;

                        case QueueMessageChannel.Email:
                            if (emailOptions != null)
                            {
                                var parts = item.Recipient.Split('|');
                                var toEmail = parts[0];
                                var subject = parts.Length > 1 ? parts[1] : "Library Notification";

                                var emailRes = await emailTransport.SendAsync(
                                    new EmailMessage
                                    {
                                        ToEmail = toEmail,
                                        ToName = "Recipient",
                                        Subject = subject,
                                        PlainTextBody = item.Message,
                                        FromEmail = emailOptions.FromEmail,
                                        FromName = emailOptions.FromName
                                    },
                                    emailOptions,
                                    cancellationToken);

                                if (emailRes.Succeeded)
                                {
                                    item.Status = QueueMessageStatus.Sent;
                                    item.SentAt = DateTime.UtcNow;
                                    item.ErrorMessage = null;
                                }
                                else
                                {
                                    item.Status = item.AttemptCount >= 3 ? QueueMessageStatus.Failed : QueueMessageStatus.Pending;
                                    item.ErrorMessage = emailRes.FailureReason;
                                }
                            }
                            else
                            {
                                item.Status = QueueMessageStatus.Failed;
                                item.ErrorMessage = "Email options validation failed.";
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    item.Status = item.AttemptCount >= 3 ? QueueMessageStatus.Failed : QueueMessageStatus.Pending;
                    item.ErrorMessage = ex.Message;
                }

                context.Entry(item).State = EntityState.Modified;
                await context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
