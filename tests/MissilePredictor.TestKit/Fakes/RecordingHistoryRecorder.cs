using MissilePredictor.API.Areas;
using MissilePredictor.API.History;
using MissilePredictor.API.Neptun.Models;

namespace MissilePredictor.TestKit.Fakes;

/// <summary>Captures what the stream reports to history. Safe to read from the test thread while the stream writes.</summary>
public sealed class RecordingHistoryRecorder : INeptunHistoryRecorder
{
    private readonly object _lock = new();
    private readonly List<NeptunThreat> _positions = [];
    private readonly List<(Area Area, NeptunThreat Threat, bool Sent, string? Error)> _alerts = [];

    public List<NeptunThreat> Positions
    {
        get { lock (_lock) return [.._positions]; }
    }

    public List<(Area Area, NeptunThreat Threat, bool Sent, string? Error)> Alerts
    {
        get { lock (_lock) return [.._alerts]; }
    }

    public void RecordPosition(NeptunThreat threat)
    {
        lock (_lock) _positions.Add(threat);
    }

    public void RecordAlert(Area area, NeptunThreat threat, bool alertSent, string? error)
    {
        lock (_lock) _alerts.Add((area, threat, alertSent, error));
    }

    public void Forget(string threatId)
    {
    }
}
