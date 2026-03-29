using System.Collections.Generic;
using Microsoft.ML;
using Microsoft.ML.Data;
using MissilePredictor.AI.Models;

namespace MissilePredictor.AI.Training;

public class SentimentTrainer
{
    private readonly MLContext _ml;

    public SentimentTrainer(MLContext ml)
    {
        _ml = ml;
    }

    public (ITransformer Model, CalibratedBinaryClassificationMetrics Metrics, DataOperationsCatalog.TrainTestData SplitContext) Train(IEnumerable<SentimentData> data)
    {
        var dataView = _ml.Data.LoadFromEnumerable(data);

        var keep = _ml.Transforms.CopyColumns("Label", nameof(SentimentData.Danger))
            .Append(_ml.Transforms.SelectColumns("Text", "Label"));

        var prepared = keep.Fit(dataView).Transform(dataView);
        var split = _ml.Data.TrainTestSplit(prepared, testFraction: 0.2, seed: 42);

        var pipeline = _ml.Transforms.Text.FeaturizeText("Features", "Text")
            .AppendCacheCheckpoint(_ml)
            .Append(_ml.BinaryClassification.Trainers.SdcaLogisticRegression(
                labelColumnName: "Label", featureColumnName: "Features"));

        var model = pipeline.Fit(split.TrainSet);
        var metrics = _ml.BinaryClassification.Evaluate(model.Transform(split.TestSet), "Label");

        return (model, metrics, split);
    }
}
