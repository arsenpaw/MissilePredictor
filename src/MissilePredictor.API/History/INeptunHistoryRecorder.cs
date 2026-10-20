using MissilePredictor.API.Areas;
using MissilePredictor.API.Neptun.Models;

namespace MissilePredictor.API.History;

/// <summary>Receives what the Neptun stream sees so it can be persisted without slowing the stream down.</summary>
public interface INeptunHistoryRecorder
{
    void RecordPosition(NeptunThreat threat);

    void RecordAlert(Area area, NeptunThreat threat, bool alertSent, string? error);

    /// <summary>The threat was removed upstream; its next position is recorded even if it matches the last one.</summary>
    void Forget(string threatId);
}
