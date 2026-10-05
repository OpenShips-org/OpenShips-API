using OpenShipsAPI.Api.Models;
using OpenShipsAPI.Api.Models.Ports;
using OpenShipsAPI.Api.Models.Vessel;
using OpenShipsAPI.Domain.Destination;
using OpenShipsAPI.Infrastructure.Database.Models;
using Riok.Mapperly.Abstractions;

namespace OpenShipsAPI.Utility;

[Mapper]
public partial class AisMapper
{
    public partial VesselPositionResponse ToResponse(
        CurrentAisPosition source,
        string? ShipName,
        int? ShipType);

    public partial VesselDetailResponse ToResponse(
        StaticShipDataAis source,
        ShipDestination? destination,
        Flag? flag);

    [MapProperty(nameof(ShipDestination.RouteType), nameof(VesselDestination.DestinationType))]
    [MapProperty(nameof(ShipDestination.To), nameof(VesselDestination.ToName))]
    [MapProperty(nameof(ShipDestination.From), nameof(VesselDestination.FromName))]
    public partial VesselDestination? ToResponse(
        ShipDestination? source);

    public partial PortResponse ToResponse(
        PortDb source,
        List<string> aliases);
    
    public partial VesselTrackPosition ToResponsePosition(HistoricalAisPosition source);
}