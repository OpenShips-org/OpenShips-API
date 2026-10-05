using Asp.Versioning;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenShipsAPI.Api.Models;
using OpenShipsAPI.Api.Models.Vessel;
using OpenShipsAPI.Infrastructure.Database;
using OpenShipsAPI.Utility;

namespace OpenShipsAPI.Api.Controllers.Destinations;

[ApiController]
[Route("api/v{version:apiVersion}/destinations")]
[ApiVersion(1.0)]
[EnableCors("Public")]
public class DestinationController(AppDbContext db, AisMapper mapper) : ControllerBase
{
    [HttpGet("{mmsi}")]
    public async Task<ActionResult<ApiResponse<VesselDestination>>> GetVesselDetails(int mmsi)
    {
        var destination = await db.ShipDestinations
            .Where(x => x.Mmsi == mmsi)
            .OrderByDescending(x => x.LastSeen)
            .FirstOrDefaultAsync();

        if (destination == null)
        {
            return NotFound();
        }
        
        return new ApiResponse<VesselDestination>
        {
            Version = 1,
            Success = true,
            Data = mapper.ToResponse(destination)
        };
    }
}