namespace MissilePredictor.API.History;

/// <summary>One alert decision for a threat entering an area, whether or not the alert was delivered.</summary>
public class NeptunAlert
{
    public long Id { get; set; }
    public DateTime OccurredAt { get; set; }
    public string AreaTitle { get; set; } = "";
    public string ThreatId { get; set; } = "";
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Region { get; set; }
    public string? Locality { get; set; }
    public double Lat { get; set; }
    public double Lon { get; set; }
    public double DistanceKm { get; set; }
    public string? ExplanationShort { get; set; }
    public bool AlertSent { get; set; }
    public string? Error { get; set; }
}
