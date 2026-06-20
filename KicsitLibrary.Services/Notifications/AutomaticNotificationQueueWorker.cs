using System;
using System.Threading;
using System.Threading.Tasks;
using KicsitLibrary.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KicsitLibrary.Services.Notifications
{
    public class AutomaticNotificationQueueWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public AutomaticNotificationQueueWorker(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Delay startup slightly to allow application initialization
            try
            {
                await Task.Delay(5000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var queueService = scope.ServiceProvider.GetRequiredService<IAutomaticNotificationQueueService>();
                    await queueService.ProcessQueueAsync(stoppingToken);
                }
                catch
                {
                    // Catch and suppress transient exceptions to keep background worker alive
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
