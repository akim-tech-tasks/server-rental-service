using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerRentalService.Data;
using ServerRentalService.DTOs.Requests;
using ServerRentalService.Models;
using ServerRentalService.Options;
using ServerRentalService.Services;

namespace ServerRentalService.Tests;

[TestClass]
public class ServerRentalServiceTests
{
    private SqliteConnection _connection = default!;
    private TestDbContextFactory _dbContextFactory = default!;
    private FakeTimeProvider _timeProvider = default!;
    private ServerRentalOptions _options = default!;

    [TestInitialize]
    public void Initialize()
    {
        _connection = new SqliteConnection("Data Source=:memory:;Cache=Shared");
        _connection.Open();

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContextFactory = new TestDbContextFactory(dbOptions);
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 4, 6, 12, 0, 0, TimeSpan.Zero));
        _options = new ServerRentalOptions
        {
            BootDurationMinutes = 5,
            LeaseDurationMinutes = 20
        };

        using var dbContext = _dbContextFactory.CreateDbContext();
        dbContext.Database.EnsureCreated();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _connection.Dispose();
    }

    [TestMethod]
    public async Task Acquire_OnPoweredServer_ReturnsRentedImmediately()
    {
        var service = CreateService();
        var serverId = await CreateServerAsync(ServerPowerState.On);

        var result = await service.AcquireAsync(serverId, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Value);
        Assert.AreEqual(RentalState.Rented, result.Value!.RentalState);
        Assert.IsTrue(result.Value.IsReady);
        Assert.AreEqual(_timeProvider.GetUtcNow().AddMinutes(20), result.Value.AutoReleaseAt);
    }

    [TestMethod]
    public async Task Acquire_OffServer_BecomesRentedAfterBootWindow()
    {
        var service = CreateService();
        var processor = CreateProcessor();
        var serverId = await CreateServerAsync(ServerPowerState.Off);

        var acquireResult = await service.AcquireAsync(serverId, CancellationToken.None);
        Assert.IsTrue(acquireResult.Success);
        Assert.AreEqual(RentalState.PendingBoot, acquireResult.Value!.RentalState);

        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        await processor.ProcessDueServersAsync(CancellationToken.None);

        var status = await service.GetStatusAsync(serverId, CancellationToken.None);
        Assert.IsTrue(status.Success);
        Assert.AreEqual(RentalState.Rented, status.Value!.RentalState);
        Assert.IsTrue(status.Value.IsReady);
    }

    [TestMethod]
    public async Task AutoRelease_PowersOffAfterLeaseDuration()
    {
        var service = CreateService();
        var processor = CreateProcessor();
        var serverId = await CreateServerAsync(ServerPowerState.On);

        var acquireResult = await service.AcquireAsync(serverId, CancellationToken.None);
        Assert.IsTrue(acquireResult.Success);

        _timeProvider.Advance(TimeSpan.FromMinutes(20));
        await processor.ProcessDueServersAsync(CancellationToken.None);

        var status = await service.GetStatusAsync(serverId, CancellationToken.None);
        Assert.IsTrue(status.Success);
        Assert.AreEqual(RentalState.Available, status.Value!.RentalState);
        Assert.AreEqual(ServerPowerState.Off, status.Value.PowerState);
    }

    [TestMethod]
    public async Task ConcurrentAcquire_OnlyOneRequestSucceeds()
    {
        var service = CreateService();
        var serverId = await CreateServerAsync(ServerPowerState.On);

        var task1 = service.AcquireAsync(serverId, CancellationToken.None);
        var task2 = service.AcquireAsync(serverId, CancellationToken.None);

        var results = await Task.WhenAll(task1, task2);

        Assert.AreEqual(1, results.Count(x => x.Success));
        Assert.AreEqual(1, results.Count(x => x.Error == ServiceError.Conflict));
    }

    [TestMethod]
    public async Task SearchAvailable_FiltersByResources()
    {
        var service = CreateService();
        await CreateServerAsync(ServerPowerState.Off, "Ubuntu 22.04", 16, 200, 8);
        await CreateServerAsync(ServerPowerState.Off, "Ubuntu 20.04", 8, 100, 4);

        var available = await service.SearchAvailableAsync(new AvailableServersQuery
        {
            OperatingSystem = "Ubuntu",
            MinMemoryGb = 12,
            MinDiskGb = 150,
            MinCpuCores = 8
        }, CancellationToken.None);

        Assert.AreEqual(1, available.Count);
        Assert.AreEqual(16, available[0].MemoryGb);
    }

    private ServerRentalService.Services.ServerRentalService CreateService() =>
        new(
            _dbContextFactory,
            Options.Create(_options),
            _timeProvider,
            NullLogger<ServerRentalService.Services.ServerRentalService>.Instance);

    private ServerLifecycleProcessor CreateProcessor() =>
        new(
            _dbContextFactory,
            Options.Create(_options),
            _timeProvider,
            NullLogger<ServerLifecycleProcessor>.Instance);

    private async Task<Guid> CreateServerAsync(
        ServerPowerState powerState,
        string os = "Ubuntu 22.04",
        int memoryGb = 16,
        int diskGb = 200,
        int cpuCores = 8)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var server = new ComputeServer
        {
            OperatingSystem = os,
            MemoryGb = memoryGb,
            DiskGb = diskGb,
            CpuCores = cpuCores,
            PowerState = powerState,
            RentalState = RentalState.Available
        };

        dbContext.Servers.Add(server);
        await dbContext.SaveChangesAsync();

        return server.Id;
    }
}
