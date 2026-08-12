using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissilePredictor.Config;
using MissilePredictor.Services;
using Xunit.Abstractions;

namespace MissilePredictor.Tests;

public class HomeAssistantAlertTests
{
    private readonly ITestOutputHelper _output;

    public HomeAssistantAlertTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Fires a mock air-raid alarm through <see cref="HomeAssistantClient"/> and asserts the
    /// Home Assistant webhook call looks exactly like what the automation subscribes to.
    ///
    /// Settings come from the service's own appsettings.json (plus the Development/Production
    /// overrides and <c>HomeAssistant__*</c> environment variables, same as the running app).
    /// The request is intercepted by default, so the test is offline and deterministic; set
    /// </summary>
    [Fact]
    public async Task MockAlarm_IsSentToHomeAssistantWebhook()
    {
        const string mockAlarm = "🚀 Ракетна небезпека! Тестова тривога — MissilePredictor mock alarm.";

        var haConfig = LoadHomeAssistantConfig();
        var config = Options.Create(haConfig);

        _output.WriteLine($"BaseUrl={haConfig.BaseUrl}  WebhookId={haConfig.WebhookId}");
        _output.WriteLine($"Token={(string.IsNullOrEmpty(haConfig.Token) ? "<empty>" : "set")}  " +
                          $"CfAccessClientId={haConfig.AccessClientId ?? "<unset>"}  " +
                          $"CfAccessClientSecret={(string.IsNullOrEmpty(haConfig.AccessClientSecret) ? "<unset>" : "set")}");

        // No auto-redirect: Cloudflare Access answers an unauthenticated call with a 302 to its
        // login page, and following it would turn an auth failure into a misleading 200.
        using var handler = new CapturingHandler(new HttpClientHandler { AllowAutoRedirect = false });
        using var http = new HttpClient(handler);
        var client = new HomeAssistantClient(config, NullLogger<HomeAssistantClient>.Instance, http);

        var started = Stopwatch.GetTimestamp();
        await client.TriggerAlertAutomationAsync([mockAlarm]);
        var elapsed = Stopwatch.GetElapsedTime(started);

        _output.WriteLine($"status={handler.StatusCode}  elapsed={elapsed.TotalMilliseconds:F0}ms");

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(
            new Uri($"{haConfig.BaseUrl.TrimEnd('/')}/api/webhook/{haConfig.WebhookId}"),
            handler.RequestUri);
        Assert.Equal("Bearer", handler.AuthScheme);
        Assert.Equal(haConfig.Token, handler.AuthParameter);
        Assert.Equal("application/json", handler.ContentType);

        var payload = JsonSerializer.Deserialize<AlertPayload>(
            handler.Body!, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(payload);
        Assert.Equal(mockAlarm, Assert.Single(payload!.Messages));

        Assert.True(
            handler.StatusCode is >= HttpStatusCode.OK and < HttpStatusCode.Ambiguous,
            $"Home Assistant webhook returned {handler.StatusCode}.");
    }

    /// <summary>
    /// Binds the <c>HomeAssistant</c> section the same way Program.cs does, so the test uses
    /// whatever the service itself would use on this machine.
    /// </summary>
    private static HomeAssistantConfig LoadHomeAssistantConfig()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var section = configuration.GetSection("HomeAssistant");

        return new HomeAssistantConfig
        {
            BaseUrl = section["BaseUrl"]
                ?? throw new InvalidOperationException("HomeAssistant:BaseUrl is not configured."),
            Token = section["Token"] ?? string.Empty,
            WebhookId = section["WebhookId"]
                ?? throw new InvalidOperationException("HomeAssistant:WebhookId is not configured."),
            AccessClientId = section["AccessClientId"],
            AccessClientSecret = section["AccessClientSecret"]
        };
    }

    private sealed record AlertPayload(IReadOnlyList<string> Messages);

    /// <summary>
    /// Records the outgoing request. With no inner handler it short-circuits with 200 OK
    /// (mock mode); with one it forwards to the real Home Assistant and records the response.
    /// </summary>
    private sealed class CapturingHandler : DelegatingHandler
    {
        public CapturingHandler(HttpMessageHandler? inner)
        {
            if (inner is not null) InnerHandler = inner;
        }

        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? AuthScheme { get; private set; }
        public string? AuthParameter { get; private set; }
        public string? ContentType { get; private set; }
        public string? Body { get; private set; }
        public HttpStatusCode? StatusCode { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            AuthScheme = request.Headers.Authorization?.Scheme;
            AuthParameter = request.Headers.Authorization?.Parameter;
            ContentType = request.Content?.Headers.ContentType?.MediaType;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            var response = InnerHandler is null
                ? new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request }
                : await base.SendAsync(request, cancellationToken);

            StatusCode = response.StatusCode;
            return response;
        }
    }
}
