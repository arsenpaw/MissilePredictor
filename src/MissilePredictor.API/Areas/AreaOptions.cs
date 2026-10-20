using System.ComponentModel.DataAnnotations;

namespace MissilePredictor.API.Areas;

/// <summary>One entry of <c>Neptun:Areas</c>.</summary>
public sealed class AreaOptions
{
    [Required]
    public string Title { get; init; } = "";

    /// <summary>Genitive form used in alert text ("км від ..."); defaults to <see cref="Title"/>.</summary>
    public string? From { get; init; }

    [Range(-90, 90)]
    public double Latitude { get; init; }

    [Range(-180, 180)]
    public double Longitude { get; init; }

    [Range(0.1, 1000)]
    public double RadiusKm { get; init; }

    public Area ToArea() => new(Title, Latitude, Longitude, RadiusKm, From ?? Title);
}
