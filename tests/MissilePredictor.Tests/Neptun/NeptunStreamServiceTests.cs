using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissilePredictor.API.Alerts;
using MissilePredictor.API.Areas;
using MissilePredictor.API.History;
using MissilePredictor.API.Neptun;
using MissilePredictor.TestKit;
using MissilePredictor.TestKit.Fakes;
using static MissilePredictor.TestKit.Fakes.FakeNeptunServer;

namespace MissilePredictor.Tests.Neptun;

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

    [Fact]
    public async Task TargetEnteringLviv_AlertTextMatchesFormat()
    {
        await using var server = await FakeNeptunServer.StartAsync();
        var alerts = new RecordingAlertClient();
        await using var _ = await StartServiceAsync(server.Url, alerts);

        var neptun = await server.NextConnectionAsync(ReconnectTimeout);
        await neptun.SendAsync(Upsert(Threat("trk_1", InsideLat, InsideLon)));

        Assert.Equal("🚨 БпЛА — Львів, 4 км від Львову. trk_1", Assert.Single(await alerts.NextAsync(AlertTimeout)));
    }

    [Fact]
    public async Task TargetInsideTwoAreas_AlertsOncePerArea()
    {
        await using var server = await FakeNeptunServer.StartAsync();
        var alerts = new RecordingAlertClient();
        var sykhiv = new Area("Сихів", InsideLat, InsideLon, 3, "Сихова");
        await using var _ = await StartServiceAsync(server.Url, alerts, TestAreas.Lviv, sykhiv);

        var neptun = await server.NextConnectionAsync(ReconnectTimeout);
        await neptun.SendAsync(Upsert(Threat("trk_1", InsideLat, InsideLon)));

        var messages = await alerts.NextAsync(AlertTimeout);
        Assert.Equal(2, messages.Count());
        Assert.Single(messages.Where(m => m.Contains("Львів")));
        Assert.Single(messages.Where(m => m.Contains("Сихів")));
    }

    [Fact]
    public async Task Stream_RecordsPositionsForAllThreats()
    {
        await using var server = await FakeNeptunServer.StartAsync();
        var history = new RecordingHistoryRecorder();
        await using var _ = await StartServiceAsync(server.Url, new RecordingAlertClient(), history, TestAreas.Lviv);

        var neptun = await server.NextConnectionAsync(ReconnectTimeout);
        await neptun.SendAsync(Upsert(Threat("kyiv", KyivLat, KyivLon)));
        await neptun.SendAsync(Upsert(Threat("lviv", InsideLat, InsideLon)));

        await WaitUntilAsync(() => history.Positions.Count == 2);
        Assert.Equal(["kyiv", "lviv"], history.Positions.Select(p => p.Id));
    }

    [Fact]
    public async Task Stream_RecordsAlertAsSent()
    {
        await using var server = await FakeNeptunServer.StartAsync();
        var alerts = new RecordingAlertClient();
        var history = new RecordingHistoryRecorder();
        await using var _ = await StartServiceAsync(server.Url, alerts, history, TestAreas.Lviv);

        var neptun = await server.NextConnectionAsync(ReconnectTimeout);
        await neptun.SendAsync(Upsert(Threat("trk_1", InsideLat, InsideLon)));
        await alerts.NextAsync(AlertTimeout);

        await WaitUntilAsync(() => history.Alerts.Count == 1);
        var alert = Assert.Single(history.Alerts);
        Assert.Equal("Львів", alert.Area.Title);
        Assert.Equal("trk_1", alert.Threat.Id);
        Assert.True(alert.Sent);
        Assert.Null(alert.Error);
    }

    [Fact]
    public async Task Stream_RecordsAlertAsFailed_WhenHomeAssistantThrows()
    {
        await using var server = await FakeNeptunServer.StartAsync();
        var history = new RecordingHistoryRecorder();
        await using var _ = await StartServiceAsync(server.Url, new ThrowingAlertClient(), history, TestAreas.Lviv);

        var neptun = await server.NextConnectionAsync(ReconnectTimeout);
        await neptun.SendAsync(Upsert(Threat("trk_1", InsideLat, InsideLon)));
        await WaitUntilAsync(() => history.Alerts.Count == 1);

        var alert = Assert.Single(history.Alerts);
        Assert.False(alert.Sent);
        Assert.Equal("HA down", alert.Error);

        // The stream survives the failure and keeps processing frames.
        await neptun.SendAsync(Upsert(Threat("trk_2", InsideLat, InsideLon)));
        await WaitUntilAsync(() => history.Alerts.Count == 2);
        Assert.Equal("trk_2", history.Alerts[1].Threat.Id);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var cts = new CancellationTokenSource(AlertTimeout);
        while (!condition()) await Task.Delay(20, cts.Token);
    }

    private static Task<IAsyncDisposable> StartServiceAsync(
        string url, IAlertClient alerts, params Area[] areas) =>
        StartServiceAsync(url, alerts, new RecordingHistoryRecorder(), areas);

    private static async Task<IAsyncDisposable> StartServiceAsync(
        string url, IAlertClient alerts, INeptunHistoryRecorder history, params Area[] areas)
    {
        var watched = areas.Length > 0 ? areas : [TestAreas.Lviv];
        var service = new NeptunStreamService(
            Options.Create(new NeptunConfig { StreamUrl = url }),
            watched.Select(a => new AreaThreatTracker(a)).ToList(),
            alerts,
            history,
            NullLogger<NeptunStreamService>.Instance);

        await service.StartAsync(CancellationToken.None);
        return new AsyncDisposable(() => service.StopAsync(CancellationToken.None));
    }
}
