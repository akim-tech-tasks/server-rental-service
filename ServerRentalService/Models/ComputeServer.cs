using System.ComponentModel.DataAnnotations;

namespace ServerRentalService.Models;

public class ComputeServer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(128)]
    public string OperatingSystem { get; set; } = string.Empty;

    public int MemoryGb { get; set; }
    public int DiskGb { get; set; }
    public int CpuCores { get; set; }

    public ServerPowerState PowerState { get; set; } = ServerPowerState.Off;
    public RentalState RentalState { get; set; } = RentalState.Available;

    public DateTimeOffset? BootRequestedAt { get; set; }
    public DateTimeOffset? ReadyAt { get; set; }
    public DateTimeOffset? LeasedAt { get; set; }
    public DateTimeOffset? AutoReleaseAt { get; set; }
}
