using MissilePredictor.API.Alerts;

namespace MissilePredictor.TestKit.Fakes;

/// <summary>An alert client whose Home Assistant call always fails.</summary>
public sealed class ThrowingAlertClient : IAlertClient
{
    public Task TriggerAlertAutomationAsync(IEnumerable<string> alertMessages, CancellationToken ct = default) =>
        throw new InvalidOperationException("HA down");
}
