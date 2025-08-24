namespace MissilePredictor.Models;

public sealed class MessageDto
{
    public long Id { get; init; }
    public DateTime Date { get; init; }
    public string Text { get; init; } = "";
}