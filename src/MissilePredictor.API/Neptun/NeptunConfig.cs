using System.ComponentModel.DataAnnotations;
using MissilePredictor.API.Areas;

namespace MissilePredictor.API.Neptun;

public sealed class NeptunConfig
{
    [Required]
    public string StreamUrl { get; init; } = "wss://neptun.in.ua/api/v1/stream";

    // Binding appends to a pre-filled list, so the default area lives in appsettings.json only.
    public List<AreaOptions> Areas { get; init; } = [];
}
