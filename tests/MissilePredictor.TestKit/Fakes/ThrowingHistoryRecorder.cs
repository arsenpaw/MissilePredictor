using MissilePredictor.API.Areas;
using MissilePredictor.API.History;
using MissilePredictor.API.Neptun.Models;

namespace MissilePredictor.TestKit.Fakes;

/// <summary>A broken history recorder: every call throws.</summary>
public sealed class ThrowingHistoryRecorder : INeptunHistoryRecorder
{
    public void RecordPosition(NeptunThreat threat) => throw new InvalidOperationException("history broken");

    public void RecordAlert(Area area, NeptunThreat threat, bool alertSent, string? error) =>
        throw new InvalidOperationException("history broken");

    public void Forget(string threatId) => throw new InvalidOperationException("history broken");
}
