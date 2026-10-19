using System.Threading.Channels;
using MissilePredictor.API.Alerts;

namespace MissilePredictor.TestKit.Fakes;

/// <summary>Captures the alerts instead of calling Home Assistant.</summary>
public sealed class RecordingAlertClient : IAlertClient
{
    private readonly Channel<string[]> _alerts = Channel.CreateUnbounded<string[]>();

    public Task TriggerAlertAutomationAsync(IEnumerable<string> alertMessages, CancellationToken ct = default)
    {
        _alerts.Writer.TryWrite(alertMessages.ToArray());
        return Task.CompletedTask;
    }

    public async Task<string[]> NextAsync(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        return await _alerts.Reader.ReadAsync(cts.Token);
    }
}
