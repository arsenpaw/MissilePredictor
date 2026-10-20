using Hangfire;
using Hangfire.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MissilePredictor.AI.GoogleSheets;
using MissilePredictor.AI.Sentiment;
using MissilePredictor.API.Alerts;
using MissilePredictor.API.History;
using MissilePredictor.API.Telegram;
using static MissilePredictor.TestKit.Fakes.FakeNeptunServer;

namespace MissilePredictor.Tests.App;

/// <summary>
/// The app boots once per class: Hangfire.Console registers itself process-wide and throws if a second host
/// in the same process tries to configure it again.
/// </summary>
public class AppBootTests : IClassFixture<OfflineAppFixture>
{
    private readonly OfflineAppFixture _app;
    private readonly OfflineAppFactory _factory;

    public AppBootTests(OfflineAppFixture app)
    {
        _app = app;
        _factory = app.Factory;
    }

    [Fact]
    public void Disabled_StartsWithoutTelegramOrAiServices()
    {
        var sp = _factory.Services;

        // Safety first: never let a boot test reach the real home alarm.
        Assert.IsType<ConsoleAlertClient>(sp.GetRequiredService<IAlertClient>());

        Assert.Null(sp.GetService<ITgScraperService>());
        Assert.Null(sp.GetService<PredictionDangerousMessageService>());
        Assert.Null(sp.GetService<GoogleSheetsClient>());
    }

    [Fact]
    public void Disabled_DoesNotScheduleTelegramJobs()
    {
        Assert.IsType<ConsoleAlertClient>(_factory.Services.GetRequiredService<IAlertClient>());

        var ids = JobStorage.Current.GetConnection().GetRecurringJobs().Select(j => j.Id).ToList();

        Assert.DoesNotContain("sync-telegram", ids);
        Assert.DoesNotContain("model-training", ids);
        Assert.DoesNotContain("restart-app", ids);
        Assert.Contains("history-cleanup", ids);
    }

    [Fact]
    public void Boot_CreatesHistoryDatabase()
    {
        Assert.IsType<ConsoleAlertClient>(_factory.Services.GetRequiredService<IAlertClient>());

        Assert.True(File.Exists(_factory.HistoryDbPath));
    }

    [Fact]
    public async Task DroneOverLviv_IsStoredInHistory()
    {
        var sp = _factory.Services;
        Assert.IsType<ConsoleAlertClient>(sp.GetRequiredService<IAlertClient>());

        await _app.Neptun.SendAsync(Upsert(Threat("trk_boot", InsideLat, InsideLon)));

        var options = new DbContextOptionsBuilder<HistoryDbContext>()
            .UseSqlite($"Data Source={_factory.HistoryDbPath}").Options;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            await using var db = new HistoryDbContext(options);
            if (await db.Alerts.AnyAsync(a => a.ThreatId == "trk_boot", cts.Token)
                && await db.TrackPoints.AnyAsync(p => p.ThreatId == "trk_boot", cts.Token))
            {
                Assert.Equal("trk_boot", (await db.Alerts.SingleAsync(a => a.ThreatId == "trk_boot", cts.Token)).ThreatId);
                return;
            }

            await Task.Delay(50, cts.Token);
        }
    }
}
