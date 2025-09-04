using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.ML;
using Microsoft.ML.Data;

public sealed class SentimentData
{
    public long Id { get; set; }
    public string Date { get; set; } = "";
    public string Text { get; set; } = "";
    public bool Danger { get; set; }
}

public static class Program
{
    public static void Main(string[] args)
    {
        var dataPath = args.Length > 0 ? args[0] : @"D:\MissilePredictor\MissilePredictor\data\train.csv";
        var modelPath = args.Length > 1 ? args[1] : @"D:\MissilePredictor\MissilePredictor\models\sentiment.zip";

        if (!File.Exists(dataPath))
            throw new FileNotFoundException("Dataset not found", dataPath);

        var modelDir = Path.GetDirectoryName(modelPath);
        if (string.IsNullOrWhiteSpace(modelDir))
            throw new ArgumentException("Invalid model path", nameof(modelPath));
        Directory.CreateDirectory(modelDir);

        var ml = new MLContext(seed: 42);

        var dataEnumerable = ReadCsv(dataPath);
        var dataView = ml.Data.LoadFromEnumerable(dataEnumerable);

        var keep = ml.Transforms.CopyColumns("Label", nameof(SentimentData.Danger))
            .Append(ml.Transforms.SelectColumns("Text", "Label"));

        var prepared = keep.Fit(dataView).Transform(dataView);
        var split = ml.Data.TrainTestSplit(prepared, testFraction: 0.2, seed: 42);

        var pipeline = ml.Transforms.Text.FeaturizeText("Features", "Text")
            .AppendCacheCheckpoint(ml)
            .Append(ml.BinaryClassification.Trainers.SdcaLogisticRegression(
                labelColumnName: "Label", featureColumnName: "Features"));

        var model = pipeline.Fit(split.TrainSet);
        var metrics = ml.BinaryClassification.Evaluate(model.Transform(split.TestSet), "Label");

        Console.WriteLine($"AUC={metrics.AreaUnderRocCurve:F3} Acc={metrics.Accuracy:F3} F1={metrics.F1Score:F3}");

        ml.Model.Save(model, split.TrainSet.Schema, modelPath);
        Console.WriteLine($"Saved model → {modelPath}");
        // var onnxPath = Path.ChangeExtension(modelPath, ".onnx");
        // using var fs = File.Create(onnxPath);
        // ml.Model.ConvertToOnnx(model, split.TrainSet, fs);
    }

    private static IEnumerable<SentimentData> ReadCsv(string path)
    {
        var cfg = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ",",
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            DetectColumnCountChanges = false
        };

        using var sr = new StreamReader(path, System.Text.Encoding.UTF8, true);
        using var csv = new CsvReader(sr, cfg);

        csv.Read();
        csv.ReadHeader();

        while (csv.Read())
        {
            var idStr = csv.TryGetField(0, out string? _id) ? _id : "";
            var dateStr = csv.TryGetField(1, out string? _date) ? _date : "";
            var textStr = csv.TryGetField(2, out string? _text) ? _text : "";
            var dangerStr = csv.TryGetField(3, out string? _danger) ? _danger : "";

            if (!long.TryParse(idStr, out var id))
                continue;

            var t = (dangerStr ?? "").Trim().ToLowerInvariant();
            var danger = t is "1" or "true";

            yield return new SentimentData
            {
                Id = id,
                Date = dateStr ?? "",
                Text = textStr ?? "",
                Danger = danger
            };
        }
    }
}
