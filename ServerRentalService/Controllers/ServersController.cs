using Microsoft.AspNetCore.Mvc;
using ServerRentalService.DTOs.Requests;
using ServerRentalService.Services;

namespace ServerRentalService.Controllers;

[ApiController]
[Route("api/servers")]
public class ServersController(IServerRentalService serverRentalService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> AddServer([FromBody] AddServerRequest request, CancellationToken cancellationToken)
    {
        var added = await serverRentalService.AddServerAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetServerStatus), new { serverId = added.Id }, added);
    }

    [HttpGet("available")]
    public async Task<IActionResult> SearchAvailable([FromQuery] AvailableServersQuery query, CancellationToken cancellationToken)
    {
        var available = await serverRentalService.SearchAvailableAsync(query, cancellationToken);
        return Ok(available);
    }

    [HttpPost("{serverId:guid}/rent")]
    public async Task<IActionResult> AcquireServer(Guid serverId, CancellationToken cancellationToken)
    {
        var result = await serverRentalService.AcquireAsync(serverId, cancellationToken);

        return result.Error switch
        {
            ServiceError.None => Ok(result.Value),
            ServiceError.NotFound => NotFound(),
            ServiceError.Conflict => Conflict("Server is not available for rent."),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPost("{serverId:guid}/release")]
    public async Task<IActionResult> ReleaseServer(Guid serverId, CancellationToken cancellationToken)
    {
        var result = await serverRentalService.ReleaseAsync(serverId, cancellationToken);

        return result.Error switch
        {
            ServiceError.None => NoContent(),
            ServiceError.NotFound => NotFound(),
            ServiceError.Conflict => Conflict("Server is already free."),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpGet("{serverId:guid}/status")]
    public async Task<IActionResult> GetServerStatus(Guid serverId, CancellationToken cancellationToken)
    {
        var result = await serverRentalService.GetStatusAsync(serverId, cancellationToken);

        return result.Error switch
        {
            ServiceError.None => Ok(result.Value),
            ServiceError.NotFound => NotFound(),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
