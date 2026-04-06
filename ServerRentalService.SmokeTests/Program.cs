using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ServerRentalService.Data;
using ServerRentalService.DTOs.Requests;
using ServerRentalService.Models;
using ServerRentalService.Options;
using ServerRentalService.Services;
using ServerRentalService.SmokeTests;

var results = new List<(string Name, bool Success, string? Error)>();

await Run("Acquire_OnPoweredServer_ReturnsRentedImmediately", Acquire_OnPoweredServer_ReturnsRentedImmediately);
await Run("Acquire_OffServer_BecomesRentedAfterBootWindow", Acquire_OffServer_BecomesRentedAfterBootWindow);
await Run("AutoRelease_PowersOffAfterLeaseDuration", AutoRelease_PowersOffAfterLeaseDuration);
await Run("ConcurrentAcquire_OnlyOneRequestSucceeds", ConcurrentAcquire_OnlyOneRequestSucceeds);
await Run("SearchAvailable_FiltersByResources", SearchAvailable_FiltersByResources);

foreach (var result in results)
{
    Console.WriteLine($"[{(result.Success ? "PASS" : "FAIL")}] {result.Name}");
    if (!result.Success)
    {
        Console.WriteLine($"  {result.Error}");
    }
}

if (results.Any(x => !x.Success))
{
    Environment.ExitCode = 1;
}

return;

async Task Run(string name, Func<Task> test)
{
    try
    {
        await test();
        results.Add((name, true, null));
    }
    catch (Exception ex)
    {
        results.Add((name, false, ex.Message));
    }
}

async Task Acquire_OnPoweredServer_ReturnsRentedImmediately()
{
    using var fixture = new TestFixture();
    var serverId = await fixture.CreateServerAsync(ServerPowerState.On);

    var result = await fixture.Service.AcquireAsync(serverId, CancellationToken.None);

    Assert(result.Success, "Acquire should succeed");
    Assert(result.Value is not null, "Result should contain value");
    Assert(result.Value!.RentalState == RentalState.Rented, "Server should be rented immediately");
    Assert(result.Value.IsReady, "Server should be ready");
    Assert(result.Value.AutoReleaseAt == fixture.TimeProvider.GetUtcNow().AddMinutes(20), "Auto release should be set");
}

async Task Acquire_OffServer_BecomesRentedAfterBootWindow()
{
    using var fixture = new TestFixture();
    var serverId = await fixture.CreateServerAsync(ServerPowerState.Off);

    var acquireResult = await fixture.Service.AcquireAsync(serverId, CancellationToken.None);
    Assert(acquireResult.Success, "Acquire should succeed");
    Assert(acquireResult.Value!.RentalState == RentalState.PendingBoot, "Server should be pending boot");

    fixture.TimeProvider.Advance(TimeSpan.FromMinutes(5));
    await fixture.Processor.ProcessDueServersAsync(CancellationToken.None);

    var status = await fixture.Service.GetStatusAsync(serverId, CancellationToken.None);
    Assert(status.Success, "Status should be available");
    Assert(status.Value!.RentalState == RentalState.Rented, "Server should become rented");
    Assert(status.Value.IsReady, "Server should become ready");
}

async Task AutoRelease_PowersOffAfterLeaseDuration()
{
    using var fixture = new TestFixture();
    var serverId = await fixture.CreateServerAsync(ServerPowerState.On);

    var acquireResult = await fixture.Service.AcquireAsync(serverId, CancellationToken.None);
    Assert(acquireResult.Success, "Acquire should succeed");

    fixture.TimeProvider.Advance(TimeSpan.FromMinutes(20));
    await fixture.Processor.ProcessDueServersAsync(CancellationToken.None);

    var status = await fixture.Service.GetStatusAsync(serverId, CancellationToken.None);
    Assert(status.Success, "Status should be available");
    Assert(status.Value!.RentalState == RentalState.Available, "Server should be released automatically");
    Assert(status.Value.PowerState == ServerPowerState.Off, "Server should be powered off automatically");
}

async Task ConcurrentAcquire_OnlyOneRequestSucceeds()
{
    using var fixture = new TestFixture();
    var serverId = await fixture.CreateServerAsync(ServerPowerState.On);

    var task1 = fixture.Service.AcquireAsync(serverId, CancellationToken.None);
    var task2 = fixture.Service.AcquireAsync(serverId, CancellationToken.None);

    var results = await Task.WhenAll(task1, task2);
    Assert(results.Count(x => x.Success) == 1, "Exactly one acquire should succeed");
    Assert(results.Count(x => x.Error == ServiceError.Conflict) == 1, "One acquire should return conflict");
}

async Task SearchAvailable_FiltersByResources()
{
    using var fixture = new TestFixture();
    await fixture.CreateServerAsync(ServerPowerState.Off, "Ubuntu 22.04", 16, 200, 8);
    await fixture.CreateServerAsync(ServerPowerState.Off, "Ubuntu 20.04", 8, 100, 4);

    var available = await fixture.Service.SearchAvailableAsync(new AvailableServersQuery
    {
        OperatingSystem = "Ubuntu",
        MinMemoryGb = 12,
        MinDiskGb = 150,
        MinCpuCores = 8
    }, CancellationToken.None);

    Assert(available.Count == 1, "One server should match filters");
    Assert(available[0].MemoryGb == 16, "Expected 16 GB server");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed class TestFixture : IDisposable
{
    private readonly SqliteConnection _connection;

    private readonly TestDbContextFactory _dbContextFactory;
    private readonly ServerRentalOptions _options;

    public FakeTimeProvider TimeProvider { get; }
    public ServerRentalService.Services.ServerRentalService Service { get; }
    public ServerLifecycleProcessor Processor { get; }

    public TestFixture()
    {
        _connection = new SqliteConnection("Data Source=:memory:;Cache=Shared");
        _connection.Open();

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContextFactory = new TestDbContextFactory(dbOptions);
        TimeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 4, 6, 12, 0, 0, TimeSpan.Zero));
        _options = new ServerRentalOptions
        {
            BootDurationMinutes = 5,
            LeaseDurationMinutes = 20
        };

        using var dbContext = _dbContextFactory.CreateDbContext();
        dbContext.Database.EnsureCreated();

        Service = new ServerRentalService.Services.ServerRentalService(
            _dbContextFactory,
            Options.Create(_options),
            TimeProvider,
            NullLogger<ServerRentalService.Services.ServerRentalService>.Instance);

        Processor = new ServerLifecycleProcessor(
            _dbContextFactory,
            Options.Create(_options),
            TimeProvider,
            NullLogger<ServerLifecycleProcessor>.Instance);
    }

    public async Task<Guid> CreateServerAsync(
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

    public void Dispose()
    {
        _connection.Dispose();
    }
}
