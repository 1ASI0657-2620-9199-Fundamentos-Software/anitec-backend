using Anitec.Platform.Notifications.Application;
using Anitec.Platform.Notifications.Application.Internal.CommandServices;
using Microsoft.Extensions.Options;

namespace Anitec.Platform.Notifications.Infrastructure.Scheduling;

/// <summary>
///     The first background process of the system (iteration 5). It wakes up every
///     <see cref="NotificationsOptions.ScanIntervalMinutes" /> minutes inside the same ASP.NET Core process, looks for
///     due dates that nobody confirmed and creates their alerts. A failure in one pass is logged and the next pass
///     tries again: a bad pass never stops the scanner or the API.
/// </summary>
public class DueDateScanningService(
    IServiceScopeFactory scopes,
    IOptions<NotificationsOptions> options,
    ILogger<DueDateScanningService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation("The scanner of due dates is turned off (Notifications:Enabled is false)");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, settings.ScanIntervalMinutes));
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, settings.StartDelaySeconds)), stoppingToken);
            using var timer = new PeriodicTimer(interval);
            do
            {
                await RunOnceAsync(stoppingToken);
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // The application is stopping.
        }
    }

    /// <summary>One pass of the scanner. It never throws, except when the application is stopping.</summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        // Each pass has its own id, so everything that it logs can be found together (QAS-10).
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = $"scan-{Guid.NewGuid():N}" });
        try
        {
            await using var serviceScope = scopes.CreateAsyncScope();
            var result = await serviceScope.ServiceProvider.GetRequiredService<IDueDateScanService>()
                .ScanAsync(cancellationToken);
            logger.LogInformation("Due date scan finished: {Created} alerts created, {Reactivated} snoozed alerts back",
                result.Created.Count, result.Reactivated.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Due date scan failed; it will run again in the next pass");
        }
    }
}
