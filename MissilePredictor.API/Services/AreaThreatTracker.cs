using MissilePredictor.Models;

namespace MissilePredictor.Services;

/// <summary>
/// Keeps track of which Neptun targets are inside <see cref="Area"/> and reports each one
/// once, at the moment it enters. A target that leaves and comes back is reported again.
/// Not thread-safe: feed it from a single stream reader.
/// </summary>
public sealed class AreaThreatTracker
{
    private const string ActiveStatus = "active";

    private readonly HashSet<string> _inside = [];

    public AreaThreatTracker(Area area) => Area = area;

    public Area Area { get; }

    /// <summary>
    /// Replaces the known state with a full snapshot (sent on every connect) and returns the
    /// targets that are newly inside. Targets already reported before a reconnect stay quiet.
    /// </summary>
    public IReadOnlyList<NeptunThreat> ApplySnapshot(IEnumerable<NeptunThreat> threats)
    {
        var list = threats.ToList();
        _inside.IntersectWith(list.Select(t => t.Id));
        return list.Where(Upsert).ToList();
    }

    /// <summary>Returns true when this update moves the target into the area.</summary>
    public bool Upsert(NeptunThreat threat)
    {
        // No real position, so there is nothing to measure.
        if (threat.AreaOnly) return false;

        if (!Area.Contains(threat.Lat, threat.Lon))
        {
            _inside.Remove(threat.Id);
            return false;
        }

        return threat.Status == ActiveStatus && !threat.Advisory && _inside.Add(threat.Id);
    }

    public void Remove(string id) => _inside.Remove(id);
}
