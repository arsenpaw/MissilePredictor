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

    /// <summary>
    /// Cloudflare Access service token credentials. Set both when Home Assistant sits behind
    /// a Zero Trust application, so the webhook call authenticates without the SSO redirect.
    /// </summary>
    public string? AccessClientId { get; init; }

    public string? AccessClientSecret { get; init; }

    /// <summary>
    /// Use <c>ConsoleAlertClient</c>: alerts go to the console and the webhook is never called.
    /// The other settings are then not required.
    /// </summary>
    public bool Mock { get; init; }
}