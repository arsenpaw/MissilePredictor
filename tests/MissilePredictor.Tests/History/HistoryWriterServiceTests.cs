using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MissilePredictor.API.History;
using MissilePredictor.TestKit;
using MissilePredictor.TestKit.History;
using static MissilePredictor.TestKit.Fakes.FakeNeptunServer;

namespace MissilePredictor.Tests.History;

public class HistoryWriterServiceTests
{
    private static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Writer_PersistsRecordedItems()
    {
        using var history = new InMemoryHistoryDb();
        var recorder = new NeptunHistoryRecorder();
        var writer = new HistoryWriterService(recorder, history, NullLogger<HistoryWriterService>.Instance);
        await writer.StartAsync(CancellationToken.None);
        try
        {
            var threat = Threat("trk_1", InsideLat, InsideLon);
            recorder.RecordPosition(threat);
            recorder.RecordPosition(Threat("trk_1", InsideLat, InsideLon + 0.01));
            recorder.RecordAlert(TestAreas.Lviv, threat, true, null);

            await WaitUntilAsync(history, db => db.Alerts.Count() == 1 && db.TrackPoints.Count() == 2);
        }
        finally
        {
            await writer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Writer_WhenSaveFails_KeepsRunningAndWritesLaterBatches()
    {
        using var history = new InMemoryHistoryDb();
        var failing = new FailOnceHistoryDb(history);
        var recorder = new NeptunHistoryRecorder();
        var writer = new HistoryWriterService(recorder, failing, NullLogger<HistoryWriterService>.Instance);
        await writer.StartAsync(CancellationToken.None);
        try
        {
            recorder.RecordPosition(Threat("lost", InsideLat, InsideLon));
            await failing.FirstFailure.WaitAsync(PollTimeout);
            recorder.RecordPosition(Threat("kept", InsideLat, InsideLon));

            await WaitUntilAsync(history, db => db.TrackPoints.Any(p => p.ThreatId == "kept"));
            await using var read = history.CreateDbContext();
            Assert.DoesNotContain(read.TrackPoints, p => p.ThreatId == "lost");
        }
        finally
        {
            await writer.StopAsync(CancellationToken.None);
        }
    }

    private static async Task WaitUntilAsync(InMemoryHistoryDb history, Func<HistoryDbContext, bool> condition)
    {
        using var cts = new CancellationTokenSource(PollTimeout);
        while (true)
        {
            await using var db = history.CreateDbContext();
            if (condition(db)) return;
            await Task.Delay(50, cts.Token);
        }
    }
}
