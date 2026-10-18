using System.Text.Json;

namespace MissilePredictor.Models;

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

public sealed class NeptunSnapshot
{
    public List<NeptunThreat> Threats { get; init; } = [];
}

public sealed class NeptunRemove
{
    public string Id { get; init; } = "";
}

/// <summary>
/// One tracked target. Only the fields the alert logic needs are mapped; see
/// https://neptun.in.ua/developers for the full schema.
/// </summary>
public sealed class NeptunThreat
{
    public string Id { get; init; } = "";

    /// <summary>uav, recon, missile, ballistic, kab, mig31k, fpv, unknown.</summary>
    public string Type { get; init; } = "";

    public string Title { get; init; } = "";
    public string? Region { get; init; }
    public string? Locality { get; init; }
    public double Lat { get; init; }
    public double Lon { get; init; }
    public double? Heading { get; init; }

    /// <summary>active, stale, resolved.</summary>
    public string Status { get; init; } = "";

    public string? ExplanationShort { get; init; }

    /// <summary>An observation (e.g. MiG-31K takeoff), not a shelter signal.</summary>
    public bool Advisory { get; init; }

    /// <summary>Only a region was named; <see cref="Lat"/>/<see cref="Lon"/> are its centroid, not a position.</summary>
    public bool AreaOnly { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}
