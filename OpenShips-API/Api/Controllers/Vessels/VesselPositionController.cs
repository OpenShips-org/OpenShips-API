using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenShipsAPI.Api.Models;
using OpenShipsAPI.Api.Models.Vessel;
using OpenShipsAPI.Utility;
using OpenShipsAPI.Infrastructure.Database;
using OpenShipsAPI.Infrastructure.Database.Models;

namespace OpenShipsAPI.Api.Controllers.Vessels;

[ApiController]
[Route("api/v{version:apiVersion}/vessels/position")]
[ApiVersion(1.0)]
[EnableCors("Public")]
public class VesselPositionController(AppDbContext db, AisMapper mapper, ILogger<VesselPositionController> logger)
    : ControllerBase
{
    [HttpGet("current/{mmsi}")]
    public async Task<ActionResult<ApiResponse<VesselPositionResponse>>> GetCurrentByMmsi(int mmsi)
    {
        var currentPos = await db.CurrentAisPositions.FindAsync(mmsi);
        var shipData = await db.StaticShipData.FindAsync(mmsi);

        var shipType = shipData?.ShipType;
        var shipName = shipData?.ShipName;

        if (currentPos == null)
        {
            return NotFound();
        }

        return new ApiResponse<VesselPositionResponse>
        {
            Version = 1,
            Success = true,
            Data = mapper.ToResponse(currentPos, shipName, shipType)
        };
    }

    [HttpGet("current/box")]
    public async Task<ActionResult<ApiResponse<List<VesselPositionResponse>>>> GetCurrentByBox(
        [FromQuery] BoundingBox bbox,
        [FromQuery] List<int>? typeFilter = null,
        [FromQuery, Range(1, 5000)] int limit = 5000,
        [FromQuery] int maxAgeMinutes = 0)
    {
        if (!bbox.IsValid)
        {
            return BadRequest("Invalid bounding box.");
        }



        var query = db.CurrentAisPositions
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
            .GroupJoin(
                db.StaticShipData,
                position => position.Mmsi,
                stat => stat.Mmsi,
                (position, stats) => new
                {
                    Position = position,
                    Stats = stats
                })
            .SelectMany(
                x => x.Stats.DefaultIfEmpty(),
                (x, stat) => new
                {
                    Position = x.Position,
                    ShipName = stat != null ? stat.ShipName : null,
                    ShipType = stat != null ? stat.ShipType : null
                });

        if (typeFilter is { Count: > 0 })
        {
            query = query.Where(x =>
                x.ShipType.HasValue &&
                typeFilter.Contains(x.ShipType.Value));
        }

        if (maxAgeMinutes > 0)
        {
            var cutoff = DateTimeOffset.UtcNow.AddMinutes(-maxAgeMinutes);

            query = query.Where(x =>
                x.Position.EventTimestamp >= cutoff);
        }

        var positions = await query
            .OrderByDescending(x => x.Position.EventTimestamp)
            .Take(limit)
            .ToListAsync();

        return new ApiResponse<List<VesselPositionResponse>>
        {
            Version = 1,
            Success = true,
            Data = positions
                .Select(x => mapper.ToResponse(
                    x.Position,
                    x.ShipName,
                    x.ShipType))
                .ToList()
        };
    }

    [HttpGet("track/{mmsi}")]
    public async Task<ActionResult<ApiResponse<VesselTrackResponse>>> GetTrackByMmsi(
        [FromRoute] int mmsi,
        [FromQuery] int limit = 500,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int minIntervalSeconds = 30)
    {
        if (mmsi <= 0)
            return BadRequest("Invalid MMSI.");

        if (limit <= 0 || limit > 5000)
            return BadRequest("Limit must be between 1 and 5000.");

        if (minIntervalSeconds < 0)
            return BadRequest("Interval must be 0 or greater.");

        if (from.HasValue && to.HasValue && from > to)
            return BadRequest("'from' must be before 'to'.");

        var query = db.HistoricalAisPositions
            .AsNoTracking()
            .Where(x => x.Mmsi == mmsi)
            .GroupJoin(
                db.StaticShipData,
                position => position.Mmsi,
                stat => stat.Mmsi,
                (position, stats) => new
                {
                    Position = position,
                    Stats = stats
                })
            .SelectMany(
                x => x.Stats.DefaultIfEmpty(),
                (x, stat) => new
                {
                    Position = x.Position,
                    ShipName = stat != null ? stat.ShipName : null,
                    ShipType = stat != null ? stat.ShipType : null
                });

        if (from.HasValue)
        {
            query = query.Where(x =>
                x.Position.EventTimestamp >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x =>
                x.Position.EventTimestamp <= to.Value);
        }

        var positions = await query
            .OrderBy(x => x.Position.EventTimestamp)
            .ToListAsync();

        var filtered = new List<(
            HistoricalAisPosition Position,
            string? ShipName,
            int? ShipType
            )>();

        DateTimeOffset? lastTimestamp = null;

        foreach (var position in positions)
        {
            if (minIntervalSeconds == 0 ||
                lastTimestamp == null ||
                position.Position.EventTimestamp - lastTimestamp.Value >=
                TimeSpan.FromSeconds(minIntervalSeconds))
            {
                filtered.Add((
                    position.Position,
                    position.ShipName,
                    position.ShipType
                ));

                lastTimestamp = position.Position.EventTimestamp;

                if (filtered.Count >= limit)
                    break;
            }
        }

        return new ApiResponse<VesselTrackResponse>
        {
            Version = 1,
            Success = true,
            Data = new VesselTrackResponse
            {
                Mmsi = mmsi,
                Positions = filtered
                    .Select(x => mapper.ToResponsePosition(x.Position))
                    .ToList(),
                ShipName = filtered.FirstOrDefault().ShipName,
                ShipType = filtered.FirstOrDefault().ShipType
            }
        };
    }
}