using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissilePredictor.API.History;
using MissilePredictor.API.Jobs;
using MissilePredictor.TestKit.History;

namespace MissilePredictor.Tests.Jobs;

public class HistoryCleanupJobTests
{
    private static HistoryCleanupJob CreateJob(InMemoryHistoryDb history) =>
        new(history,
            Options.Create(new HistoryOptions { UnalertedTrackRetention = TimeSpan.FromHours(6) }),
            NullLogger<HistoryCleanupJob>.Instance);

    private static NeptunTrackPoint Point(string threatId, TimeSpan age) =>
        new() { ThreatId = threatId, ReceivedAt = DateTime.UtcNow - age, UpdatedAt = DateTime.UtcNow - age };

    [Fact]
    public async Task Purge_DeletesOldUnalertedPoints()
    {
        using var history = new InMemoryHistoryDb();
        await using (var db = history.CreateDbContext())
        {
            db.TrackPoints.Add(Point("trk_old", TimeSpan.FromHours(7)));
            await db.SaveChangesAsync();
        }

        var deleted = await CreateJob(history).PurgeAsync();

        Assert.Equal(1, deleted);
        await using var check = history.CreateDbContext();
        Assert.Empty(check.TrackPoints);
    }

    [Fact]
    public async Task Purge_KeepsRecentUnalertedPoints()
    {
        using var history = new InMemoryHistoryDb();
        await using (var db = history.CreateDbContext())
        {
            db.TrackPoints.Add(Point("trk_recent", TimeSpan.FromHours(1)));
            await db.SaveChangesAsync();
        }

        var deleted = await CreateJob(history).PurgeAsync();

        Assert.Equal(0, deleted);
        await using var check = history.CreateDbContext();
        Assert.Single(check.TrackPoints);
    }

    [Fact]
    public async Task Purge_KeepsAlertedTrackOfAnyAge()
    {
        using var history = new InMemoryHistoryDb();
        await using (var db = history.CreateDbContext())
        {
            db.TrackPoints.Add(Point("trk_alerted", TimeSpan.FromDays(30)));
            db.Alerts.Add(new NeptunAlert { ThreatId = "trk_alerted", OccurredAt = DateTime.UtcNow - TimeSpan.FromDays(30) });
            await db.SaveChangesAsync();
        }

        var deleted = await CreateJob(history).PurgeAsync();

        Assert.Equal(0, deleted);
        await using var check = history.CreateDbContext();
        Assert.Single(check.TrackPoints);
    }
}
