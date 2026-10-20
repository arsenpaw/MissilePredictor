using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MissilePredictor.API.Alerts;
using MissilePredictor.API.Areas;
using MissilePredictor.API.Neptun.Models;

namespace MissilePredictor.API.Neptun;

/// <summary>
/// Listens to the NEPTUN threat stream and fires the Home Assistant webhook when a target
/// enters a tracked area. Reconnects with exponential backoff whenever the socket drops.
/// </summary>
public sealed class NeptunStreamService : BackgroundService
{
    private static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(1);

    // The server sends a heartbeat roughly every 15s; silence longer than this means a dead link.
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(60);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly NeptunConfig _config;
    private readonly IReadOnlyList<AreaThreatTracker> _trackers;
    private readonly IAlertClient _alertClient;
    private readonly ILogger<NeptunStreamService> _logger;
    private TimeSpan _backoff = InitialBackoff;

    public NeptunStreamService(
        IOptions<NeptunConfig> config,
        IReadOnlyList<AreaThreatTracker> trackers,
        IAlertClient alertClient,
        ILogger<NeptunStreamService> logger)
    {
        _config = config.Value;
        _trackers = trackers;
        _alertClient = alertClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        foreach (var tracker in _trackers)
        {
            _logger.LogInformation("NeptunStreamService watching area '{Area}' ({Lat}, {Lon}, r={Radius} km)",
                tracker.Area.Title, tracker.Area.Latitude, tracker.Area.Longitude, tracker.Area.RadiusKm);
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ListenAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Neptun stream failed");
            }

            _logger.LogInformation("Reconnecting to Neptun stream in {Delay}", _backoff);
            await Task.Delay(_backoff, ct);
            _backoff = TimeSpan.FromTicks(Math.Min(_backoff.Ticks * 2, MaxBackoff.Ticks));
        }
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri(_config.StreamUrl), ct);
        _logger.LogInformation("Connected to Neptun stream {Url}", _config.StreamUrl);

        var buffer = new byte[16 * 1024];
        using var frame = new MemoryStream();

        while (ws.State == WebSocketState.Open)
        {
            using var idle = CancellationTokenSource.CreateLinkedTokenSource(ct);
            idle.CancelAfter(IdleTimeout);

            frame.SetLength(0);
            ValueWebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(buffer.AsMemory(), idle.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogWarning("Neptun stream closed by server: {Status} {Description}",
                        ws.CloseStatus, ws.CloseStatusDescription);
                    return;
                }

                frame.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            // The link is healthy again, so the next drop starts from a short delay.
            _backoff = InitialBackoff;

            await HandleFrameAsync(frame.GetBuffer().AsMemory(0, (int)frame.Length), ct);
        }
    }

    private async Task HandleFrameAsync(ReadOnlyMemory<byte> json, CancellationToken ct)
    {
        var entered = new List<(Area Area, NeptunThreat Threat)>();
        try
        {
            var envelope = JsonSerializer.Deserialize<NeptunEnvelope>(json.Span, JsonOptions);
            switch (envelope?.Type)
            {
                case "snapshot":
                    var threats = envelope.Data.Deserialize<NeptunSnapshot>(JsonOptions)?.Threats ?? [];
                    foreach (var tracker in _trackers)
                        entered.AddRange(tracker.ApplySnapshot(threats).Select(t => (tracker.Area, t)));
                    break;
                case "upsert" when envelope.Data.Deserialize<NeptunThreat>(JsonOptions) is { } threat:
                    foreach (var tracker in _trackers.Where(t => t.TryEnter(threat)))
                        entered.Add((tracker.Area, threat));
                    break;
                case "remove" when envelope.Data.Deserialize<NeptunRemove>(JsonOptions) is { } removed:
                    foreach (var tracker in _trackers)
                        tracker.Remove(removed.Id);
                    break;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Skipping malformed Neptun frame");
            return;
        }

        if (entered.Count == 0) return;

        foreach (var (area, threat) in entered)
        {
            _logger.LogWarning(
                "Target entered '{Area}', sending alert: Id={Id} Type={Type} Title={Title} Lat={Lat} Lon={Lon} DistanceKm={Distance:F1}",
                area.Title, threat.Id, threat.Type, threat.Title, threat.Lat, threat.Lon,
                area.DistanceKm(threat.Lat, threat.Lon));
        }

        try
        {
            await _alertClient.TriggerAlertAutomationAsync(entered.Select(e => FormatAlert(e.Area, e.Threat)), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Home Assistant alert failed for {Count} Neptun target(s)", entered.Count);
        }
    }

    private static string FormatAlert(Area area, NeptunThreat threat)
    {
        var distance = area.DistanceKm(threat.Lat, threat.Lon);
        var alert = $"🚨 {threat.Title} — {area.Title}, {distance:F0} км від {area.From}.";
        return string.IsNullOrWhiteSpace(threat.ExplanationShort) ? alert : $"{alert} {threat.ExplanationShort}";
    }
}
