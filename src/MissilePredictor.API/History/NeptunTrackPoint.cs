namespace MissilePredictor.API.History;

/// <summary>One observed position of a threat.</summary>
public class NeptunTrackPoint
{
    public long Id { get; set; }
    public string ThreatId { get; set; } = "";
    public DateTime ReceivedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Status { get; set; } = "";
    public double Lat { get; set; }
    public double Lon { get; set; }
    public double? Heading { get; set; }
}
