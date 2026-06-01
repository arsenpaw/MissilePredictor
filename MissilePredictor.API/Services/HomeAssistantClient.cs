using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MissilePredictor.Config;

namespace MissilePredictor.Services;

public sealed class HomeAssistantClient
{
    private readonly HttpClient _http;
    private readonly HomeAssistantConfig _config;
    private readonly ILogger<HomeAssistantClient> _logger;

    public HomeAssistantClient(IOptions<HomeAssistantConfig> config, ILogger<HomeAssistantClient> logger)
    {
        _config = config.Value;
        _logger = logger;

        _http = new HttpClient { BaseAddress = new Uri(_config.BaseUrl.TrimEnd('/') + "/") };
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _config.Token);
    }

    public async Task TriggerAlertAutomationAsync(IEnumerable<string> alertMessages, CancellationToken ct = default)
    {
        var messages = alertMessages.ToList();
        var payload = new { messages };
        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync($"api/webhook/{_config.WebhookId}", content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("HomeAssistant webhook failed: {StatusCode} — {Body}", response.StatusCode, body);
        }
        else
        {
            _logger.LogInformation("HomeAssistant webhook '{WebhookId}' triggered with {Count} alert(s)",
                _config.WebhookId, messages.Count);
        }
    }
}
