using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.ML;
using MissilePredictor.AI.Config;
using MissilePredictor.AI.Models;
using MissilePredictor.AI.Services;
using MissilePredictor.Config;

namespace MissilePredictor.AI.Training;

public class SentimentModelPipeline
{
    private readonly GoogleSheetsClient _sheetsClient;
    private readonly GoogleConfig _googleConfig;
    private readonly MlConfig _mlConfig;
    private readonly MLContext _ml;
    private readonly SentimentTrainer _trainer;

    public SentimentModelPipeline(GoogleSheetsClient sheetsClient, IOptions<GoogleConfig> googleConfig, IOptions<MlConfig> mlConfig)
    {
        _sheetsClient = sheetsClient;
        _googleConfig = googleConfig.Value;
        _mlConfig = mlConfig.Value;
        _ml = new MLContext(seed: 42);
        _trainer = new SentimentTrainer(_ml);
    }

    public async Task<int> RunAsync()
    {
        var modelDir = Path.GetDirectoryName(_mlConfig.ModelPath);
        if (!string.IsNullOrWhiteSpace(modelDir))
        {
            Directory.CreateDirectory(modelDir);
        }

        var rawData = await _sheetsClient.ReadDataAsync(_googleConfig.SpreadsheetId, _googleConfig.Range);
        
        if (rawData == null || rawData.Count == 0)
        {
            Console.WriteLine("No data found.");
            return 0;
        }

        var dataEnumerable = ParseSheetData(rawData);

        var (model, metrics, split) = _trainer.Train(dataEnumerable);

        Console.WriteLine($"AUC={metrics.AreaUnderRocCurve:F3} Acc={metrics.Accuracy:F3} F1={metrics.F1Score:F3}");

        _ml.Model.Save(model, split.TrainSet.Schema, _mlConfig.ModelPath);
        Console.WriteLine($"Saved model → {_mlConfig.ModelPath}");
        
        return rawData.Count;
    }

    private IEnumerable<SentimentData> ParseSheetData(IList<IList<object>> rawData)
    {
        if (rawData == null || rawData.Count == 0)
            yield break;

        foreach (var row in rawData)
        {
            if (row.Count < 4) continue;

            var idStr = row[0]?.ToString() ?? "";
            var dateStr = row[1]?.ToString() ?? "";
            var textStr = row[2]?.ToString() ?? "";
            var dangerStr = row[3]?.ToString() ?? "";

            if (!long.TryParse(idStr, out var id))
                continue;

            var t = dangerStr.Trim().ToLowerInvariant();
            var danger = t is "1" or "true";

            yield return new SentimentData
            {
                Id = id,
                Date = dateStr,
                Text = textStr,
                Danger = danger
            };
        }
    }
}
