namespace MissilePredictor.API.Telegram.Models;

public sealed class MessageDto
{
    public int Id { get; init; }
    public DateTime Date { get; init; }
    public string Text { get; init; } = "";
}