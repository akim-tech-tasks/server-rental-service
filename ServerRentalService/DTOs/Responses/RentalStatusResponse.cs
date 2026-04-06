using ServerRentalService.Models;

namespace ServerRentalService.DTOs.Responses;

public record RentalStatusResponse(
    Guid ServerId,
    RentalState RentalState,
    ServerPowerState PowerState,
    bool IsReady,
    DateTimeOffset? ReadyAt,
    DateTimeOffset? AutoReleaseAt);
