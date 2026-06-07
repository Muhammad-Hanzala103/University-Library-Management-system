using KicsitLibrary.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KicsitLibrary.Services.Backup;

public sealed class AutomaticBackupBackgroundService(
    IAutomaticBackupSchedulerService schedulerService,
    AutomaticBackupStartupSignal startupSignal,
    ILogger<AutomaticBackupBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan DisabledPollingInterval =
        TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await startupSignal.WaitAsync(stoppingToken);
            var startupRunEvaluated = false;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var status = await schedulerService.GetSchedulerStatusAsync(
                        stoppingToken);
                    if (!startupRunEvaluated)
                    {
                        startupRunEvaluated = true;
                        if (status.Enabled && status.RunOnStartup)
                        {
                            await Task.Delay(
                                TimeSpan.FromSeconds(
                                    status.InitialDelaySeconds),
                                stoppingToken);
                            await schedulerService.RunScheduledBackupAsync(
                                stoppingToken);
                            continue;
                        }
                    }

                    var delay = status.Enabled
                        ? TimeSpan.FromHours(status.IntervalHours)
                        : DisabledPollingInterval;
                    await Task.Delay(delay, stoppingToken);
                    status = await schedulerService.GetSchedulerStatusAsync(
                        stoppingToken);
                    if (status.Enabled)
                    {
                        await schedulerService.RunScheduledBackupAsync(
                            stoppingToken);
                    }
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "The automatic backup scheduler iteration failed; it will retry.");
                    await Task.Delay(
                        DisabledPollingInterval,
                        stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "The automatic backup scheduler stopped unexpectedly.");
        }
    }
}
