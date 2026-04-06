using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServerRentalService.Data;
using ServerRentalService.Models;
using ServerRentalService.Options;

namespace ServerRentalService.HostedServices;

public class DatabaseInitializationHostedService(
    IServiceProvider serviceProvider,
    IOptions<ServerRentalOptions> options,
    ILogger<DatabaseInitializationHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        if (await dbContext.Servers.AnyAsync(cancellationToken))
        {
            return;
        }

        var initialServers = options.Value.InitialServers;
        if (initialServers.Count == 0)
        {
            return;
        }

        var servers = initialServers.Select(x => new ComputeServer
        {
            OperatingSystem = x.OperatingSystem,
            MemoryGb = x.MemoryGb,
            DiskGb = x.DiskGb,
            CpuCores = x.CpuCores,
            PowerState = x.InitiallyPoweredOn ? ServerPowerState.On : ServerPowerState.Off,
            RentalState = RentalState.Available
        });

        await dbContext.Servers.AddRangeAsync(servers, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Database initialized with {Count} initial servers", initialServers.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
