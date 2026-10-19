using System.Text.Json;

namespace MissilePredictor.API.Neptun.Models;

/// <summary>
/// Frame from wss://neptun.in.ua/api/v1/stream. <see cref="Type"/> is one of
/// snapshot | upsert | remove | heartbeat | alerts, and decides the shape of <see cref="Data"/>.
/// </summary>
public sealed class NeptunEnvelope
{
    public string Type { get; init; } = "";
    public DateTimeOffset Ts { get; init; }
    public JsonElement Data { get; init; }
}
