using ServerRentalService.Models;

namespace ServerRentalService.DTOs.Responses;

public record ServerResponse(
    Guid Id,
    string OperatingSystem,
    int MemoryGb,
    int DiskGb,
    int CpuCores,
    ServerPowerState PowerState,
    RentalState RentalState,
    DateTimeOffset? ReadyAt,
    DateTimeOffset? LeasedAt,
    DateTimeOffset? AutoReleaseAt);
