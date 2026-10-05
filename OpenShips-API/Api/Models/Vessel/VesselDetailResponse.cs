using OpenShipsAPI.Infrastructure.Streams.Common;

namespace OpenShipsAPI.Api.Models.Vessel;

public class VesselDetailResponse
{
    public int Mmsi { get; set; }
    public AisSource Source { get; set; }
    public AisDataLicense License  { get; set; }
    public int MessageType { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset EventTimestamp { get; set; }
    public string? ShipName { get; set; }
    public int? ShipType  { get; set; }
    public int? ImoNumber { get; set; }
    public string? CallSign { get; set; }
    public string? RawDestination { get; set; }
    public VesselDestination? Destination { get; set; }
    public Flag? Flag { get; set; }
    public float? Draught  { get; set; }
    public DateTimeOffset? Eta  { get; set; }
    public float? Dim_A { get; set; }
    public float? Dim_B { get; set; }
    public float? Dim_C { get; set; }
    public float? Dim_D { get; set; }
    public float? Length { get; set; }
    public float? Beam { get; set; }
}