using System.ComponentModel.DataAnnotations;

namespace MissilePredictor.API.Neptun;

public sealed class NeptunConfig
{
    [Required]
    public string StreamUrl { get; init; } = "wss://neptun.in.ua/api/v1/stream";
}
