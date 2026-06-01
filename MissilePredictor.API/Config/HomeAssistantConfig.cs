using System.ComponentModel.DataAnnotations;

namespace MissilePredictor.Config;

public sealed class HomeAssistantConfig
{
    [Required]
    public required string BaseUrl { get; init; }

    [Required]
    public required string Token { get; init; }
    
    [Required]
    public required string WebhookId { get; init; }
}