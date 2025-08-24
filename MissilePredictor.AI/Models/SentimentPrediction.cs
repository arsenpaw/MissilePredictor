using Microsoft.ML.Data;

namespace MissilePredictor.AI.Models;

public sealed class InputRow
{
    public string Text { get; set; } = "";
}

public sealed class SentimentPrediction
{
    [ColumnName("PredictedLabel")] public bool Prediction { get; set; }
    public float Probability { get; set; }
    public float Score { get; set; }
}