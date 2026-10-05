using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenShipsAPI.Api.Models;
using OpenShipsAPI.Api.Models.Ports;
using OpenShipsAPI.Utility;
using OpenShipsAPI.Infrastructure.Database;

namespace OpenShipsAPI.Api.Controllers.Ports;

[ApiController]
[Route("api/v{version:apiVersion}/ports")]
[ApiVersion(1.0)]
[EnableCors("Public")]
public class PortController(
    AppDbContext db,
    AisMapper mapper) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<PortResponse>>> Get(int id)
    {
        var port = await db.Ports
            .Include(x => x.Aliases)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (port == null)
        {
            return NotFound("Port not found.");
        }

        return new ApiResponse<PortResponse>
        {
            Version = 1,
            Success = true,
            Data = mapper.ToResponse(
                port,
                port.Aliases
                    .Select(x => x.Alias)
                    .ToList())
        };
    }

    [HttpGet("locode/{id:int}")]
    public async Task<ActionResult<ApiResponse<PortLocodeResponse>>> GetLocode(int id)
    {
        var port = await db.Ports.FindAsync(id);

        if (port == null)
        {
            return NotFound("Port not found.");
        }

        return new ApiResponse<PortLocodeResponse>
        {
            Version = 1,
            Success = true,
            Data = new PortLocodeResponse
            {
                Id = port.Id,
                Country = port.Country,
                Location = port.Location,
                Subregion = port.Subregion,
                TimeZone = port.TimeZone,
            }
        };
    }

    [HttpGet("box")]
    public async Task<ActionResult<ApiResponse<List<PortResponse>>>> GetByBox(
        [FromQuery] BoundingBox bbox,
        [FromQuery, Range(1, 5000)] int limit = 5000)
    {
        if (!bbox.IsValid)
        {
            return BadRequest("Invalid bounding box.");
        }

        var ports = await db.Ports
            .Include(x => x.Aliases)
            .AsNoTracking()
            .Where(x =>
                x.Latitude >= bbox.MinLat &&
                x.Latitude <= bbox.MaxLat &&
                (
                    bbox.MinLon <= bbox.MaxLon
                        ? x.Longitude >= bbox.MinLon &&
                          x.Longitude <= bbox.MaxLon
                        : x.Longitude >= bbox.MinLon ||
                          x.Longitude <= bbox.MaxLon
                ))
            .OrderBy(x => x.Latitude)
            .Take(limit)
            .ToListAsync();

        return new ApiResponse<List<PortResponse>>
        {
            Version = 1,
            Success = true,
            Data = ports
                .Select(x => mapper.ToResponse(
                    x,
                    x.Aliases
                        .Select(a => a.Alias)
                        .ToList()))
                .ToList()
        };
    }

    [HttpGet("{country}/{location}")]
    public async Task<ActionResult<ApiResponse<PortResponse>>> GetByLocation(string country, string location)
    {
        var port = await db.Ports
            .Include(x => x.Aliases)
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Country == country &&
                x.Location == location);

        if (port == null)
        {
            return NotFound("Port not found.");
        }

        return new ApiResponse<PortResponse>
        {
            Version = 1,
            Success = true,
            Data = mapper.ToResponse(
                port,
                port.Aliases
                    .Select(x => x.Alias)
                    .ToList())
        };
    }

    [HttpGet("{country}")]
    public async Task<ActionResult<ApiResponse<List<PortResponse>>>> GetByCountry(string country)
    {
        var ports = await db.Ports
            .Include(x => x.Aliases)
            .AsNoTracking()
            .Where(x => x.Country == country)
            .OrderBy(x => x.Location)
            .ToListAsync();

        return new ApiResponse<List<PortResponse>>
        {
            Version = 1,
            Success = true,
            Data = ports
                .Select(x => mapper.ToResponse(
                    x,
                    x.Aliases
                        .Select(a => a.Alias)
                        .ToList()))
                .ToList()
        };
    }

    [HttpGet("timezone/{country}/{location}")]
    public async Task<ActionResult<ApiResponse<PortTimeZoneResponse>>> GetTimeZone(string country, string location)
    {
        var port = await db.Ports.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Country == country &&
            x.Location == location);

        if (port == null)
        {
            return NotFound("Port not found.");
        }

        return new ApiResponse<PortTimeZoneResponse>
        {
            Version = 1,
            Success = true,
            Data = new PortTimeZoneResponse
            {
                Id = port.Id,
                TimeZone = port.TimeZone
            }
        };
    }
}