using Microsoft.ML.Data;

namespace MissilePredictor.AI.Sentiment.Models;

public sealed class SentimentPrediction
{
    [ColumnName("PredictedLabel")] public bool Prediction { get; set; }
    public float Probability { get; set; }
    public float Score { get; set; }
}