using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MissilePredictor.API.Alerts;
using MissilePredictor.API.Alerts.HomeAssistant;
using MissilePredictor.TestKit.Fakes;
using Xunit.Abstractions;
using static MissilePredictor.TestKit.Fakes.FakeNeptunServer;

namespace MissilePredictor.IntegrationTests.AlarmFlow;

/// <summary>
/// Boots the whole app exactly as Program.cs wires it, with only the Neptun stream faked: it sends a
/// drone over Lviv, and the app's own <see cref="HomeAssistantClient"/> calls the real Home Assistant
/// webhook from appsettings with the real token. <b>This fires the actual alarm automation.</b>
///
/// Run it yourself from Rider or with <c>dotnet test tests/MissilePredictor.IntegrationTests</c>. AI agents are
/// blocked from running it: a Claude Code hook refuses to run this project, and
/// <see cref="RealHomeAssistantFactAttribute"/> skips it when <c>CLAUDECODE=1</c>.
///
/// Skip it with <c>dotnet test --filter "Category!=RealHomeAssistant"</c>.
/// </summary>
[Trait("Category", "RealHomeAssistant")]
public class AlarmFlowIntegrationTests
{
    private const string TestExplanation = "Тестова тривога — MissilePredictor integration test.";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly ITestOutputHelper _output;

    public AlarmFlowIntegrationTests(ITestOutputHelper output) => _output = output;

    [RealHomeAssistantFact]
    public async Task DroneOverLviv_FromNeptunStream_TriggersHomeAssistantWebhook()
    {
        await using var neptun = await FakeNeptunServer.StartAsync();
        using var webhook = WebhookObserver.Subscribe();

        await using var factory = new WebApplicationFactory<Program>();
        await using var app = factory.WithWebHostBuilder(web =>
        {
            web.UseSetting("Neptun:StreamUrl", neptun.Url);
            web.UseSetting("Telegram:Mock", "true");
            web.UseSetting("HomeAssistant:Mock", "false");
        });

        // Resolving a service starts the host, and with it NeptunStreamService.
        Assert.IsType<HomeAssistantClient>(app.Services.GetRequiredService<IAlertClient>());
        var haConfig = app.Services.GetRequiredService<IOptions<HomeAssistantConfig>>().Value;

        var stream = await neptun.NextConnectionAsync(Timeout);
        await stream.SendAsync(Snapshot(Threat("trk_kyiv", KyivLat, KyivLon)));
        await stream.SendAsync(Upsert(Threat("trk_lviv", InsideLat, InsideLon, TestExplanation)));

        var call = await webhook.NextCallAsync(Timeout);
        _output.WriteLine($"POST {call.RequestUri} -> {(int?)call.Status} {call.Status}\n{call.Body}\n{call.ResponseBody}");

        Assert.Equal(new Uri($"{haConfig.BaseUrl.TrimEnd('/')}/api/webhook/{haConfig.WebhookId}"), call.RequestUri);
        Assert.Equal($"Bearer {haConfig.Token}", call.Authorization);

        // The Kyiv target came first but is out of range, so the only alert is the Lviv one.
        var payload = JsonSerializer.Deserialize<AlertPayload>(call.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var message = Assert.Single(payload!.Messages);
        Assert.StartsWith("🚨 БпЛА — Львів, 4 км від Львову.", message);
        Assert.EndsWith(TestExplanation, message);

        Assert.True((int?)call.Status is >= 200 and < 300,
            $"Home Assistant webhook returned {(int?)call.Status} {call.Status}: {call.ResponseBody}");
    }
}
