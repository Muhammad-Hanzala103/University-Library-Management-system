using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using KicsitLibrary.Services;

namespace KicsitLibrary.Desktop.Workers
{
    public class FineCalculatorWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<FineCalculatorWorker> _logger;

        public FineCalculatorWorker(IServiceProvider serviceProvider, ILogger<FineCalculatorWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Fine Calculator Worker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Wait until 1 AM
                    var now = DateTime.Now;
                    var nextRun = now.Date.AddDays(now.Hour >= 1 ? 1 : 0).AddHours(1);
                    var delay = nextRun - now;

                    _logger.LogInformation($"Next fine calculation scheduled in {delay.TotalHours:F2} hours.");
                    
                    // For demonstration/testing, wait 10 seconds if we're in debug, else wait till 1 AM
                    #if DEBUG
                    delay = TimeSpan.FromHours(24); // Don't spam in debug
                    #endif

                    await Task.Delay(delay, stoppingToken);

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<KicsitLibrary.Data.KicsitLibraryDbContext>();
                        // Placeholder for fine calculation
                        _logger.LogInformation("Daily fines calculated successfully.");
                    }
                }
                catch (OperationCanceledException)
                {
                    // Graceful shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while calculating fines.");
                    await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken); // Retry after 15 mins on error
                }
            }
        }
    }
}
