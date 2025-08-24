using System;
using System.Collections.Generic;
using Microsoft.Extensions.ML;
using Microsoft.ML.Data;
using MissilePredictor.AI.Models;

namespace MissilePredictor.AI.Services
{
    public sealed class PredictionDangerousMessageService
    {
        private readonly PredictionEnginePool<InputRow, SentimentPrediction> _pool;
        private readonly string _modelName;

        public PredictionDangerousMessageService(
            PredictionEnginePool<InputRow, SentimentPrediction> pool,
            string modelName = "sentiment")
        {
            _pool = pool;
            _modelName = modelName;
        }

        public SentimentPrediction Predict(string? text) =>
            _pool.Predict(_modelName, new InputRow { Text = text ?? string.Empty });

        public IEnumerable<SentimentPrediction> PredictMany(IEnumerable<string> texts)
        {
            foreach (var t in texts ?? [])
                yield return _pool.Predict(_modelName, new InputRow { Text = t });
        }

        public bool IsDangerous(string text, float threshold = 0.5f) =>
            Predict(text).Probability >= threshold;
    }

  
}