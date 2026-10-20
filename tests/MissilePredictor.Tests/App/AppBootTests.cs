using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.DependencyInjection;
using MissilePredictor.AI.GoogleSheets;
using MissilePredictor.AI.Sentiment;
using MissilePredictor.API.Alerts;
using MissilePredictor.API.Telegram;

namespace MissilePredictor.Tests.App;

/// <summary>
/// The app boots once per class: Hangfire.Console registers itself process-wide and throws if a second host
/// in the same process tries to configure it again.
/// </summary>
public class AppBootTests : IClassFixture<OfflineAppFixture>
{
    private readonly OfflineAppFactory _factory;

    public AppBootTests(OfflineAppFixture app) => _factory = app.Factory;

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
    }
}
