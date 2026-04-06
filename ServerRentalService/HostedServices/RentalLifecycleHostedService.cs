using Microsoft.Extensions.Options;
using ServerRentalService.Options;
using ServerRentalService.Services;

namespace ServerRentalService.HostedServices;

public class RentalLifecycleHostedService(
    IServiceProvider serviceProvider,
    IOptions<ServerRentalOptions> options,
    ILogger<RentalLifecycleHostedService> logger) : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(options.Value.LifecycleCheckIntervalSeconds);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var lifecycleProcessor = scope.ServiceProvider.GetRequiredService<ServerLifecycleProcessor>();
                await lifecycleProcessor.ProcessDueServersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Rental lifecycle processing failed");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}
