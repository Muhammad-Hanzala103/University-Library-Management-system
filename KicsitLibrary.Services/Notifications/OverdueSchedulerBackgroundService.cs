using System;
using System.Threading;
using System.Threading.Tasks;
using KicsitLibrary.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KicsitLibrary.Services.Notifications
{
    public sealed class OverdueSchedulerBackgroundService : BackgroundService
    {
        private static readonly TimeSpan DisabledPollingInterval =
            TimeSpan.FromSeconds(30);

        private readonly IOverdueSchedulerService _schedulerService;
        private readonly OverdueSchedulerStartupSignal _startupSignal;
        private readonly ILogger<OverdueSchedulerBackgroundService> _logger;

        public OverdueSchedulerBackgroundService(
            IOverdueSchedulerService schedulerService,
            OverdueSchedulerStartupSignal startupSignal,
            ILogger<OverdueSchedulerBackgroundService> logger)
        {
            _schedulerService = schedulerService ??
                throw new ArgumentNullException(nameof(schedulerService));
            _startupSignal = startupSignal ??
                throw new ArgumentNullException(nameof(startupSignal));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await _startupSignal.WaitAsync(stoppingToken);
                var startupRunEvaluated = false;

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var status = await _schedulerService.GetStatusAsync(stoppingToken);
                        if (!startupRunEvaluated)
                        {
                            startupRunEvaluated = true;
                            if (status.Enabled && status.RunOnStartup)
                            {
                                await Task.Delay(
                                    TimeSpan.FromSeconds(status.InitialDelaySeconds),
                                    stoppingToken);
                                await _schedulerService.RunAsync(
                                    cancellationToken: stoppingToken);
                                continue;
                            }
                        }

                        var delay = status.Enabled
                            ? TimeSpan.FromMinutes(status.IntervalMinutes)
                            : DisabledPollingInterval;
                        await Task.Delay(delay, stoppingToken);

                        status = await _schedulerService.GetStatusAsync(stoppingToken);
                        if (status.Enabled)
                        {
                            await _schedulerService.RunAsync(
                                cancellationToken: stoppingToken);
                        }
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "The overdue scheduler loop iteration failed; it will retry.");
                        await Task.Delay(DisabledPollingInterval, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal application shutdown.
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "The overdue scheduler background loop stopped unexpectedly.");
            }
        }
    }
}
