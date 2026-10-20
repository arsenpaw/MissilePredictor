using System.Threading.Channels;
using MissilePredictor.API.Areas;
using MissilePredictor.API.Neptun.Models;

namespace MissilePredictor.API.History;

/// <summary>
/// Turns stream observations into history entities and queues them for <see cref="HistoryWriterService"/>.
/// Not thread-safe: like <see cref="AreaThreatTracker"/>, it is driven by the single stream reader.
/// </summary>
public sealed class NeptunHistoryRecorder : INeptunHistoryRecorder
{
    private readonly Channel<object> _pending = Channel.CreateUnbounded<object>(
        new UnboundedChannelOptions { SingleReader = true });

    private readonly Dictionary<string, (double Lat, double Lon, string Status)> _lastPositions = [];

    public ChannelReader<object> Pending => _pending.Reader;

    public void RecordPosition(NeptunThreat threat)
    {
        if (threat.AreaOnly) return;

        // JSON nulls become "": the columns are NOT NULL and one bad row would drop its whole batch.
        var threatId = threat.Id ?? "";
        var position = (threat.Lat, threat.Lon, threat.Status ?? "");
        if (_lastPositions.TryGetValue(threatId, out var last) && last == position) return;
        _lastPositions[threatId] = position;

        _pending.Writer.TryWrite(new NeptunTrackPoint
        {
            ThreatId = threatId,
            ReceivedAt = DateTime.UtcNow,
            UpdatedAt = threat.UpdatedAt.UtcDateTime,
            Type = threat.Type ?? "",
            Title = threat.Title ?? "",
            Status = threat.Status ?? "",
            Lat = threat.Lat,
            Lon = threat.Lon,
            Heading = threat.Heading
        });
    }

    public void RecordAlert(Area area, NeptunThreat threat, bool alertSent, string? error) =>
        _pending.Writer.TryWrite(new NeptunAlert
        {
            OccurredAt = DateTime.UtcNow,
            AreaTitle = area.Title,
            ThreatId = threat.Id ?? "",
            Type = threat.Type ?? "",
            Title = threat.Title ?? "",
            Region = threat.Region,
            Locality = threat.Locality,
            Lat = threat.Lat,
            Lon = threat.Lon,
            DistanceKm = area.DistanceKm(threat.Lat, threat.Lon),
            ExplanationShort = threat.ExplanationShort,
            AlertSent = alertSent,
            Error = error
        });

    public void Forget(string threatId) => _lastPositions.Remove(threatId);
}
