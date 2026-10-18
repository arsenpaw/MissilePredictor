using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MissilePredictor.Config;
using MissilePredictor.Services;
using MissilePredictor.Tests.Fakes;
using Xunit.Abstractions;
using static MissilePredictor.Tests.Fakes.FakeNeptunServer;

namespace MissilePredictor.Tests;

/// <summary>
/// Boots the whole app exactly as Program.cs wires it, with only the Neptun stream faked: it sends a
/// drone over Lviv, and the app's own <see cref="HomeAssistantClient"/> calls the real Home Assistant
/// webhook from appsettings with the real token. <b>This fires the actual alarm automation.</b>
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

    [Fact]
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

    private sealed record AlertPayload(IReadOnlyList<string> Messages);

    /// <summary><see cref="Status"/> is null when the request never got a response (DNS, TLS, refused...).</summary>
    private sealed record WebhookCall(Uri? RequestUri, string Authorization, string Body, HttpStatusCode? Status, string ResponseBody);

    /// <summary>
    /// Watches the webhook call from outside the app through the diagnostic events every HttpClient
    /// raises, so the app's own client and HttpClient send it untouched.
    /// </summary>
    private sealed class WebhookObserver : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly Channel<WebhookCall> _calls = Channel.CreateUnbounded<WebhookCall>();
        private readonly List<IDisposable> _subscriptions = [];

        public static WebhookObserver Subscribe()
        {
            var observer = new WebhookObserver();
            observer._subscriptions.Add(DiagnosticListener.AllListeners.Subscribe(observer));
            return observer;
        }

        public async Task<WebhookCall> NextCallAsync(TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            return await _calls.Reader.ReadAsync(cts.Token);
        }

        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name == "HttpHandlerDiagnosticListener")
                lock (_subscriptions) _subscriptions.Add(listener.Subscribe(this));
        }

        public void OnNext(KeyValuePair<string, object?> e)
        {
            if (e.Key != "System.Net.Http.HttpRequestOut.Stop" || e.Value is null) return;

            var request = Property<HttpRequestMessage>(e.Value, "Request");
            if (request?.RequestUri?.AbsolutePath.Contains("/api/webhook/") != true) return;
            var response = Property<HttpResponseMessage>(e.Value, "Response");

            // Raised before PostAsync returns, while both contents are still buffered and readable.
            _calls.Writer.TryWrite(new WebhookCall(
                request.RequestUri,
                request.Headers.Authorization?.ToString() ?? "",
                request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "",
                response?.StatusCode,
                response?.Content.ReadAsStringAsync().GetAwaiter().GetResult() ?? ""));
        }

        private static T? Property<T>(object payload, string name) where T : class =>
            payload.GetType().GetProperty(name)?.GetValue(payload) as T;

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void Dispose()
        {
            lock (_subscriptions) _subscriptions.ForEach(s => s.Dispose());
        }
    }
}
