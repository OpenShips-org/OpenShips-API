namespace OpenShipsAPI.Api.Models.Vessel;

public class VesselTrackTimeframeResponse
{
    public int Mmsi { get; set; }
    public DateTimeOffset FirstPositionTimestamp { get; set; }
    public DateTimeOffset LastestPositionTimestamp { get; set; }
}