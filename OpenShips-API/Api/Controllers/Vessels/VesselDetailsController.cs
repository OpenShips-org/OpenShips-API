using Asp.Versioning;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenShipsAPI.Api.Models;
using OpenShipsAPI.Api.Models.Vessel;
using OpenShipsAPI.Utility;
using OpenShipsAPI.Infrastructure.Database;

namespace OpenShipsAPI.Api.Controllers.Vessels;

[ApiController]
[Route("api/v{version:apiVersion}/vessels/details")]
[ApiVersion(1.0)]
[EnableCors("Public")]
public class VesselDetailsController(AppDbContext db, AisMapper mapper) : ControllerBase
{
    [HttpGet("{mmsi}")]
    public async Task<ActionResult<ApiResponse<VesselDetailResponse>>> GetVesselDetails(int mmsi)
    {
        var data = await db.StaticShipData.FindAsync(mmsi);
        
        if (data == null)
        {
            return NotFound();
        }
        
        var destination = await db.ShipDestinations
            .Where(x => x.Mmsi == mmsi)
            .OrderByDescending(x => x.LastSeen)
            .FirstOrDefaultAsync();
        
        var flag = FlagResolver.GetFlag(data.Mmsi);
        
        return new ApiResponse<VesselDetailResponse>
        {
            Version = 1,
            Success = true,
            Data = mapper.ToResponse(data, destination, flag)
        };
    }
}