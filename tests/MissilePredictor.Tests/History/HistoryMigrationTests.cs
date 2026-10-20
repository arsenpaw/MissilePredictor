using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissilePredictor.API.History;
using MissilePredictor.TestKit.History;

namespace MissilePredictor.Tests.History;

public class HistoryMigrationTests
{
    [Fact]
    public async Task Migrate_CreatesTables()
    {
        using var history = new InMemoryHistoryDb();
        var occurredAt = new DateTime(2026, 10, 21, 12, 30, 15, DateTimeKind.Utc);
        var receivedAt = new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

        await using (var db = history.CreateDbContext())
        {
            db.Alerts.Add(new NeptunAlert
            {
                OccurredAt = occurredAt,
                AreaTitle = "Home",
                ThreatId = "t-1",
                Type = "shahed",
                Title = "Shahed",
                Region = "Kyiv",
                Locality = "Bucha",
                Lat = 50.5,
                Lon = 30.2,
                DistanceKm = 12.5,
                ExplanationShort = "short",
                AlertSent = true,
                Error = null,
            });
            db.TrackPoints.Add(new NeptunTrackPoint
            {
                ThreatId = "t-1",
                ReceivedAt = receivedAt,
                UpdatedAt = occurredAt,
                Type = "shahed",
                Title = "Shahed",
                Status = "active",
                Lat = 50.4,
                Lon = 30.1,
                Heading = 270.5,
            });
            await db.SaveChangesAsync();
        }

        await using var read = history.CreateDbContext();
        var alert = await read.Alerts.SingleAsync();
        var point = await read.TrackPoints.SingleAsync();

        Assert.True(alert.Id > 0);
        Assert.Equal(occurredAt, alert.OccurredAt);
        Assert.Equal(DateTimeKind.Utc, alert.OccurredAt.Kind);
        Assert.Equal("Home", alert.AreaTitle);
        Assert.Equal("t-1", alert.ThreatId);
        Assert.Equal("Bucha", alert.Locality);
        Assert.Equal(12.5, alert.DistanceKm);
        Assert.Equal("short", alert.ExplanationShort);
        Assert.True(alert.AlertSent);
        Assert.Null(alert.Error);

        Assert.True(point.Id > 0);
        Assert.Equal(receivedAt, point.ReceivedAt);
        Assert.Equal(DateTimeKind.Utc, point.ReceivedAt.Kind);
        Assert.Equal(occurredAt, point.UpdatedAt);
        Assert.Equal(DateTimeKind.Utc, point.UpdatedAt.Kind);
        Assert.Equal(270.5, point.Heading);
    }

    [Fact]
    public async Task Migrate_CreatesMissingDataDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"history-{Guid.NewGuid():N}");
        var file = Path.Combine(root, "sub", "history.db");
        var options = new HistoryOptions { ConnectionString = $"Data Source={file}" };
        var factory = new DbContextFactory(options);

        try
        {
            var service = new HistoryMigrationService(factory, Options.Create(options), NullLogger<HistoryMigrationService>.Instance);
            await service.StartAsync(CancellationToken.None);

            Assert.True(File.Exists(file));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private sealed class DbContextFactory(HistoryOptions options) : IDbContextFactory<HistoryDbContext>
    {
        public HistoryDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<HistoryDbContext>().UseSqlite(options.ConnectionString).Options);
    }
}
