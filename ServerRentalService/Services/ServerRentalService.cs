using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServerRentalService.Data;
using ServerRentalService.DTOs.Requests;
using ServerRentalService.DTOs.Responses;
using ServerRentalService.Models;
using ServerRentalService.Options;

namespace ServerRentalService.Services;

public class ServerRentalService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IOptions<ServerRentalOptions> options,
    TimeProvider timeProvider,
    ILogger<ServerRentalService> logger) : IServerRentalService
{
    private readonly TimeSpan _bootDuration = TimeSpan.FromMinutes(options.Value.BootDurationMinutes);
    private readonly TimeSpan _leaseDuration = TimeSpan.FromMinutes(options.Value.LeaseDurationMinutes);

    public async Task<ServerResponse> AddServerAsync(AddServerRequest request, CancellationToken cancellationToken)
    {
        var server = new ComputeServer
        {
            OperatingSystem = request.OperatingSystem,
            MemoryGb = request.MemoryGb,
            DiskGb = request.DiskGb,
            CpuCores = request.CpuCores,
            PowerState = request.InitiallyPoweredOn ? ServerPowerState.On : ServerPowerState.Off,
            RentalState = RentalState.Available
        };

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.Servers.Add(server);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Server {ServerId} added to pool", server.Id);

        return Map(server);
    }

    public async Task<IReadOnlyList<ServerResponse>> SearchAvailableAsync(AvailableServersQuery query, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var serversQuery = dbContext.Servers
            .AsNoTracking()
            .Where(x => x.RentalState == RentalState.Available);

        if (!string.IsNullOrWhiteSpace(query.OperatingSystem))
        {
            serversQuery = serversQuery.Where(x => x.OperatingSystem.Contains(query.OperatingSystem));
        }

        if (query.MinMemoryGb.HasValue)
        {
            serversQuery = serversQuery.Where(x => x.MemoryGb >= query.MinMemoryGb);
        }

        if (query.MinDiskGb.HasValue)
        {
            serversQuery = serversQuery.Where(x => x.DiskGb >= query.MinDiskGb);
        }

        if (query.MinCpuCores.HasValue)
        {
            serversQuery = serversQuery.Where(x => x.CpuCores >= query.MinCpuCores);
        }

        var servers = await serversQuery.OrderBy(x => x.OperatingSystem).ToListAsync(cancellationToken);
        return servers.Select(Map).ToList();
    }

    public async Task<ServiceResult<RentalStatusResponse>> AcquireAsync(Guid serverId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var snapshot = await dbContext.Servers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == serverId, cancellationToken);
        if (snapshot is null)
        {
            return ServiceResult<RentalStatusResponse>.Fail(ServiceError.NotFound);
        }

        if (snapshot.RentalState != RentalState.Available)
        {
            return ServiceResult<RentalStatusResponse>.Fail(ServiceError.Conflict);
        }

        var affectedRows = snapshot.PowerState == ServerPowerState.On
            ? await dbContext.Servers
                .Where(x => x.Id == serverId && x.RentalState == RentalState.Available && x.PowerState == ServerPowerState.On)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.RentalState, RentalState.Rented)
                    .SetProperty(x => x.LeasedAt, now)
                    .SetProperty(x => x.AutoReleaseAt, now + _leaseDuration)
                    .SetProperty(x => x.BootRequestedAt, (DateTimeOffset?)null)
                    .SetProperty(x => x.ReadyAt, now), cancellationToken)
            : await dbContext.Servers
                .Where(x => x.Id == serverId && x.RentalState == RentalState.Available && x.PowerState != ServerPowerState.On)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.RentalState, RentalState.PendingBoot)
                    .SetProperty(x => x.PowerState, ServerPowerState.Booting)
                    .SetProperty(x => x.BootRequestedAt, now)
                    .SetProperty(x => x.ReadyAt, now + _bootDuration)
                    .SetProperty(x => x.LeasedAt, (DateTimeOffset?)null)
                    .SetProperty(x => x.AutoReleaseAt, (DateTimeOffset?)null), cancellationToken);

        if (affectedRows == 0)
        {
            return ServiceResult<RentalStatusResponse>.Fail(ServiceError.Conflict);
        }

        var updated = await dbContext.Servers.AsNoTracking().FirstAsync(x => x.Id == serverId, cancellationToken);

        logger.LogInformation(
            "Server {ServerId} acquire requested. State={RentalState}, ReadyAt={ReadyAt}",
            updated.Id,
            updated.RentalState,
            updated.ReadyAt);

        return ServiceResult<RentalStatusResponse>.Ok(MapStatus(updated));
    }

    public async Task<ServiceResult> ReleaseAsync(Guid serverId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var exists = await dbContext.Servers.AnyAsync(x => x.Id == serverId, cancellationToken);
        if (!exists)
        {
            return ServiceResult.Fail(ServiceError.NotFound);
        }

        var affectedRows = await dbContext.Servers
            .Where(x => x.Id == serverId && x.RentalState != RentalState.Available)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.RentalState, RentalState.Available)
                .SetProperty(x => x.PowerState, ServerPowerState.Off)
                .SetProperty(x => x.BootRequestedAt, (DateTimeOffset?)null)
                .SetProperty(x => x.ReadyAt, (DateTimeOffset?)null)
                .SetProperty(x => x.LeasedAt, (DateTimeOffset?)null)
                .SetProperty(x => x.AutoReleaseAt, (DateTimeOffset?)null), cancellationToken);

        if (affectedRows == 0)
        {
            return ServiceResult.Fail(ServiceError.Conflict);
        }

        logger.LogInformation("Server {ServerId} released by user and powered off", serverId);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<RentalStatusResponse>> GetStatusAsync(Guid serverId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var server = await dbContext.Servers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == serverId, cancellationToken);

        if (server is null)
        {
            return ServiceResult<RentalStatusResponse>.Fail(ServiceError.NotFound);
        }

        return ServiceResult<RentalStatusResponse>.Ok(MapStatus(server));
    }

    private static ServerResponse Map(ComputeServer server) =>
        new(
            server.Id,
            server.OperatingSystem,
            server.MemoryGb,
            server.DiskGb,
            server.CpuCores,
            server.PowerState,
            server.RentalState,
            server.ReadyAt,
            server.LeasedAt,
            server.AutoReleaseAt);

    private static RentalStatusResponse MapStatus(ComputeServer server) =>
        new(
            server.Id,
            server.RentalState,
            server.PowerState,
            server.RentalState == RentalState.Rented,
            server.ReadyAt,
            server.AutoReleaseAt);
}
