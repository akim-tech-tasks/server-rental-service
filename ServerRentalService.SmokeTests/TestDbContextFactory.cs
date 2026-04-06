using Microsoft.EntityFrameworkCore;
using ServerRentalService.Data;

namespace ServerRentalService.SmokeTests;

public class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext() => new(options);

    public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new AppDbContext(options));
}
