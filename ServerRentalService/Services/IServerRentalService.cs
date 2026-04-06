using ServerRentalService.DTOs.Requests;
using ServerRentalService.DTOs.Responses;

namespace ServerRentalService.Services;

public interface IServerRentalService
{
    Task<ServerResponse> AddServerAsync(AddServerRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ServerResponse>> SearchAvailableAsync(AvailableServersQuery query, CancellationToken cancellationToken);
    Task<ServiceResult<RentalStatusResponse>> AcquireAsync(Guid serverId, CancellationToken cancellationToken);
    Task<ServiceResult> ReleaseAsync(Guid serverId, CancellationToken cancellationToken);
    Task<ServiceResult<RentalStatusResponse>> GetStatusAsync(Guid serverId, CancellationToken cancellationToken);
}
