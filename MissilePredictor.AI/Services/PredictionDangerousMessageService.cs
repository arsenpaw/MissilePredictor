using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ML;
using MissilePredictor.AI.Models;

namespace MissilePredictor.AI.Services;

public sealed class PredictionDangerousMessageService
{
    private readonly PredictionEnginePool<InputRow, SentimentPrediction> _pool;
    private readonly ILogger<PredictionDangerousMessageService> _logger;
    private readonly string _modelName;

    public PredictionDangerousMessageService(
        PredictionEnginePool<InputRow, SentimentPrediction> pool,
        ILogger<PredictionDangerousMessageService> logger,
        string modelName = "sentiment")
    {
        _pool = pool;
        _logger = logger;
        _modelName = modelName;
    }

    public SentimentPrediction Predict(string? text)
    {
        var result = _pool.Predict(_modelName, new InputRow { Text = text ?? string.Empty });

        _logger.LogInformation(
            "Model scored message: Prediction={Prediction} Probability={Probability:F4} Score={Score:F4} Text={Text}",
            result.Prediction, result.Probability, result.Score, text);

        return result;
    }

    public IEnumerable<SentimentPrediction> PredictMany(IEnumerable<string> texts)
    {
        foreach (var t in texts ?? [])
            yield return Predict(t);
    }

    public bool IsDangerous(string text, float threshold = 0.5f) =>
        Predict(text).Probability >= threshold;
}
