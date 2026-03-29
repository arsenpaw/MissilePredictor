namespace MissilePredictor.AI.Models;

public sealed class SentimentData
{
    public long Id { get; set; }
    public string Date { get; set; } = "";
    public string Text { get; set; } = "";
    public bool Danger { get; set; }
}
