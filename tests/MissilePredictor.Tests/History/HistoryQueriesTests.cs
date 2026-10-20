using MissilePredictor.API.History;
using MissilePredictor.TestKit.History;

namespace MissilePredictor.Tests.History;

public class HistoryQueriesTests
{
    private static readonly DateTime Noon = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static NeptunAlert Alert(string area, string type, DateTime at, string threatId = "trk") =>
        new() { AreaTitle = area, Type = type, OccurredAt = at, ThreatId = threatId, Title = "t" };

    private static async Task Seed(InMemoryHistoryDb history, params NeptunAlert[] alerts)
    {
        await using var db = history.CreateDbContext();
        db.Alerts.AddRange(alerts);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetAlerts_FiltersByAreaTypeAndDate()
    {
        using var history = new InMemoryHistoryDb();
        await Seed(history,
            Alert("Львів", "drone", Noon),
            Alert("Львів", "missile", Noon),
            Alert("Київ", "drone", Noon),
            Alert("Львів", "drone", Noon.AddHours(-5)),
            Alert("Львів", "drone", Noon.AddHours(5)));
        var queries = new HistoryQueries(history);

        var page = await queries.GetAlertsAsync(
            new AlertFilter(From: Noon.AddHours(-1), To: Noon.AddHours(5), Area: "Львів", Type: "drone"),
            CancellationToken.None);

        var alert = Assert.Single(page.Items);
        Assert.Equal(1, page.Total);
        Assert.Equal(Noon, alert.OccurredAt);
        Assert.Equal(["drone", "missile"], await queries.GetThreatTypesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetAlerts_PagesNewestFirst()
    {
        using var history = new InMemoryHistoryDb();
        await Seed(history, Enumerable.Range(0, 60).Select(i => Alert("Львів", "drone", Noon.AddMinutes(i))).ToArray());
        var queries = new HistoryQueries(history);

        var page = await queries.GetAlertsAsync(new AlertFilter(Page: 2, PageSize: 50), CancellationToken.None);

        Assert.Equal(60, page.Total);
        Assert.Equal(10, page.Items.Count);
        Assert.Equal(Noon, page.Items[^1].OccurredAt);
        Assert.Equal(Noon.AddMinutes(9), page.Items[0].OccurredAt);
    }

    [Fact]
    public async Task GetTrack_ReturnsPointsInOrder()
    {
        using var history = new InMemoryHistoryDb();
        await using (var db = history.CreateDbContext())
        {
            db.TrackPoints.AddRange(
                new NeptunTrackPoint { ThreatId = "a", UpdatedAt = Noon.AddMinutes(2), ReceivedAt = Noon },
                new NeptunTrackPoint { ThreatId = "a", UpdatedAt = Noon, ReceivedAt = Noon },
                new NeptunTrackPoint { ThreatId = "b", UpdatedAt = Noon.AddMinutes(1), ReceivedAt = Noon },
                new NeptunTrackPoint { ThreatId = "a", UpdatedAt = Noon.AddMinutes(1), ReceivedAt = Noon });
            await db.SaveChangesAsync();
        }

        var track = await new HistoryQueries(history).GetTrackAsync("a", CancellationToken.None);

        Assert.Equal([Noon, Noon.AddMinutes(1), Noon.AddMinutes(2)], track.Select(p => p.UpdatedAt));
    }
}
