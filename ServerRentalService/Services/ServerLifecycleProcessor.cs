using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServerRentalService.Data;
using ServerRentalService.Models;
using ServerRentalService.Options;

namespace ServerRentalService.Services;

public class ServerLifecycleProcessor(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IOptions<ServerRentalOptions> options,
    TimeProvider timeProvider,
    ILogger<ServerLifecycleProcessor> logger)
{
    private readonly TimeSpan _leaseDuration = TimeSpan.FromMinutes(options.Value.LeaseDurationMinutes);

    public async Task ProcessDueServersAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var pendingIds = await dbContext.Servers
            .AsNoTracking()
            .Where(x => x.RentalState == RentalState.PendingBoot && x.ReadyAt != null)
            .Select(x => new { x.Id, x.ReadyAt })
            .ToListAsync(cancellationToken);

        foreach (var pending in pendingIds.Where(x => x.ReadyAt <= now))
        {
            var affectedRows = await dbContext.Servers
                .Where(x => x.Id == pending.Id && x.RentalState == RentalState.PendingBoot)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.RentalState, RentalState.Rented)
                    .SetProperty(x => x.PowerState, ServerPowerState.On)
                    .SetProperty(x => x.LeasedAt, now)
                    .SetProperty(x => x.AutoReleaseAt, now + _leaseDuration), cancellationToken);

            if (affectedRows > 0)
            {
                logger.LogInformation("Server {ServerId} booted and became rented", pending.Id);
            }
        }

        var expiredIds = await dbContext.Servers
            .AsNoTracking()
            .Where(x => x.RentalState == RentalState.Rented && x.AutoReleaseAt != null)
            .Select(x => new { x.Id, x.AutoReleaseAt })
            .ToListAsync(cancellationToken);

        foreach (var expired in expiredIds.Where(x => x.AutoReleaseAt <= now))
        {
            var affectedRows = await dbContext.Servers
                .Where(x => x.Id == expired.Id && x.RentalState == RentalState.Rented)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.RentalState, RentalState.Available)
                    .SetProperty(x => x.PowerState, ServerPowerState.Off)
                    .SetProperty(x => x.BootRequestedAt, (DateTimeOffset?)null)
                    .SetProperty(x => x.ReadyAt, (DateTimeOffset?)null)
                    .SetProperty(x => x.LeasedAt, (DateTimeOffset?)null)
                    .SetProperty(x => x.AutoReleaseAt, (DateTimeOffset?)null), cancellationToken);

            if (affectedRows > 0)
            {
                logger.LogInformation("Server {ServerId} auto released and powered off", expired.Id);
            }
        }
    }
}
