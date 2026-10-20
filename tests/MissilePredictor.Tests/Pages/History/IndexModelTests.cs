using Microsoft.Extensions.Options;
using MissilePredictor.API.History;
using MissilePredictor.API.Neptun;
using MissilePredictor.API.Pages.History;
using MissilePredictor.TestKit.History;

namespace MissilePredictor.Tests.Pages.History;

public class IndexModelTests
{
    [Fact]
    public async Task DateFilter_UsesKyivDays()
    {
        using var history = new InMemoryHistoryDb();
        await using (var db = history.CreateDbContext())
        {
            db.Alerts.AddRange(
                Alert("before", new DateTime(2026, 10, 20, 20, 59, 0, DateTimeKind.Utc)),  // 23:59 on 20 Oct in Kyiv
                Alert("night", new DateTime(2026, 10, 20, 22, 30, 0, DateTimeKind.Utc)),   // 01:30 on 21 Oct in Kyiv
                Alert("evening", new DateTime(2026, 10, 21, 20, 59, 0, DateTimeKind.Utc)), // 23:59 on 21 Oct in Kyiv
                Alert("after", new DateTime(2026, 10, 21, 21, 30, 0, DateTimeKind.Utc)));  // 00:30 on 22 Oct in Kyiv
            await db.SaveChangesAsync();
        }

        var model = new IndexModel(new HistoryQueries(history), Options.Create(new NeptunConfig()))
        {
            From = new DateTime(2026, 10, 21),
            To = new DateTime(2026, 10, 21)
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(["evening", "night"], model.Alerts.Items.Select(a => a.ThreatId));
    }

    private static NeptunAlert Alert(string threatId, DateTime occurredAt) =>
        new() { ThreatId = threatId, OccurredAt = occurredAt, AreaTitle = "Львів", Type = "uav", Title = "БпЛА" };
}
