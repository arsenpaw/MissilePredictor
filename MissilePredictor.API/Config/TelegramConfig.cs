namespace MissilePredictor.Config;

public sealed class TelegramConfig
{
    public int ApiId { get; init; }
    public string ApiHash { get; init; } = "";
    public string Channel { get; init; } = "";            
    public string SessionPath { get; init; } = "tg.session";
    public string LastIdPath { get; init; } = "msg.json";   

    public string? PhoneNumber { get; init; }               
    public string? VerificationCode { get; init; }          
    public string? Password { get; init; }                  
}