namespace MissilePredictor.API.Neptun.Models;

public sealed class NeptunSnapshot
{
    public List<NeptunThreat> Threats { get; init; } = [];
}
