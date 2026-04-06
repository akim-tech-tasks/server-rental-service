using Microsoft.EntityFrameworkCore;
using ServerRentalService.Models;

namespace ServerRentalService.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ComputeServer> Servers => Set<ComputeServer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ComputeServer>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.RentalState);
            entity.HasIndex(x => x.PowerState);
        });
    }
}
