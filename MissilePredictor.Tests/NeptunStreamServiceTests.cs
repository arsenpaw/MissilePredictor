using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissilePredictor.Config;
using MissilePredictor.Models;
using MissilePredictor.Services;
using MissilePredictor.Tests.Fakes;
using static MissilePredictor.Tests.Fakes.FakeNeptunServer;

namespace MissilePredictor.Tests;

/// <summary>
/// End-to-end flow: a fake Neptun WebSocket server sends real frames to <see cref="NeptunStreamService"/>,
/// and the alerts it raises are captured instead of calling Home Assistant.
/// </summary>
public class NeptunStreamServiceTests
{
    private static readonly TimeSpan AlertTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ReconnectTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task TargetEnteringLviv_TriggersAlertOncePerEntry()
    {
        await using var server = await FakeNeptunServer.StartAsync();
        var alerts = new RecordingAlertClient();
        await using var _ = await StartServiceAsync(server.Url, alerts);

        var neptun = await server.NextConnectionAsync(ReconnectTimeout);
        await neptun.SendAsync(Snapshot(Threat("far", KyivLat, KyivLon)));
        await neptun.SendAsync(Upsert(Threat("trk_1", KyivLat, KyivLon)));
        await neptun.SendAsync(Upsert(Threat("trk_1", InsideLat, InsideLon)));

        var first = await alerts.NextAsync(AlertTimeout);
        var message = Assert.Single(first);
        Assert.Contains("Львів", message);
        Assert.EndsWith("trk_1", message);

        // trk_1 moving inside the area stays quiet, so the next alert is trk_2's.
        await neptun.SendAsync(Upsert(Threat("trk_1", InsideLat, InsideLon + 0.01)));
        await neptun.SendAsync(Upsert(Threat("trk_2", InsideLat, InsideLon)));

        Assert.EndsWith("trk_2", Assert.Single(await alerts.NextAsync(AlertTimeout)));
    }

    [Fact]
    public async Task Reconnect_SnapshotDoesNotRepeatAlarms()
    {
        await using var server = await FakeNeptunServer.StartAsync();
        var alerts = new RecordingAlertClient();
        await using var _ = await StartServiceAsync(server.Url, alerts);

        var first = await server.NextConnectionAsync(ReconnectTimeout);
        await first.SendAsync(Snapshot(Threat("trk_1", InsideLat, InsideLon)));
        Assert.EndsWith("trk_1", Assert.Single(await alerts.NextAsync(AlertTimeout)));

        await first.CloseAsync();

        // The service reconnects and gets a fresh snapshot: trk_1 was already reported, trk_2 is new.
        var second = await server.NextConnectionAsync(ReconnectTimeout);
        await second.SendAsync(Snapshot(
            Threat("trk_1", InsideLat, InsideLon),
            Threat("trk_2", InsideLat, InsideLon)));

        Assert.EndsWith("trk_2", Assert.Single(await alerts.NextAsync(AlertTimeout)));
    }

    private static async Task<IAsyncDisposable> StartServiceAsync(string url, IAlertClient alerts)
    {
        var service = new NeptunStreamService(
            Options.Create(new NeptunConfig { StreamUrl = url }),
            new AreaThreatTracker(Area.Lviv),
            alerts,
            NullLogger<NeptunStreamService>.Instance);

        await service.StartAsync(CancellationToken.None);
        return new AsyncDisposable(() => service.StopAsync(CancellationToken.None));
    }

    private sealed class RecordingAlertClient : IAlertClient
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

    private sealed class AsyncDisposable(Func<Task> dispose) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() => await dispose();
    }
}
