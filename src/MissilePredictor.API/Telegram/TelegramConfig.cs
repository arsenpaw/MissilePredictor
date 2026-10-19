namespace MissilePredictor.API.Telegram;

public sealed class TelegramConfig
{
    public int ApiId { get; init; }
    public string ApiHash { get; init; } = "";
    public string Channel { get; init; } = "";            
    public string SessionPath { get; init; } = Path.Combine("data", "tg.session");

    public string? PhoneNumber { get; init; }               
    public string? VerificationCode { get; init; }          
    public string? Password { get; init; }

    /// <summary>Use <c>MockTgScraperService</c>: no login, no Telegram connection.</summary>
    public bool Mock { get; init; }
}