using Hangfire.Console;
using Hangfire.Server;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissilePredictor.AI.Config;
using MissilePredictor.AI.Services;
using MissilePredictor.AI.Training;
using MissilePredictor.Config;

namespace MissilePredictor.API.Jobs;

public class ModelTrainingJob
{
    private readonly string _metaPath;
    private readonly SentimentModelPipeline _pipeline;
    private readonly GoogleSheetsClient _sheetsClient;
    private readonly GoogleConfig _googleConfig;
    private readonly ILogger<ModelTrainingJob> _logger;

    public ModelTrainingJob(SentimentModelPipeline pipeline, GoogleSheetsClient sheetsClient, IOptions<GoogleConfig> googleConfig, IOptions<MlConfig> mlConfig, ILogger<ModelTrainingJob> logger)
    {
        _metaPath = mlConfig.Value.ModelPath + ".meta";
        _pipeline = pipeline;
        _sheetsClient = sheetsClient;
        _googleConfig = googleConfig.Value;
        _logger = logger;
    }

    public async Task Execute(PerformContext context)
    {
        _logger.LogInformation("Starting recurring model training check...");
        context.WriteLine("Starting recurring model training check...");

        var rawData = await _sheetsClient.ReadDataAsync(_googleConfig.SpreadsheetId, _googleConfig.Range);
        int currentRowCount = rawData?.Count ?? 0;
        int lastTrainedRowCount = File.Exists(_metaPath) && int.TryParse(File.ReadAllText(_metaPath), out var n) ? n : -1;

        context.WriteLine($"Current row count in sheet: {currentRowCount} | Last trained count: {lastTrainedRowCount}");

        if (currentRowCount <= lastTrainedRowCount)
        {
            _logger.LogInformation("Model training skipped: no new data.");
            context.WriteLine("Model training skipped: no new data.");
            return;
        }

        context.WriteLine("New data found. Initiating ML Pipeline...");

        int trainedRows = await _pipeline.RunAsync(context);

        if (trainedRows > 0)
        {
            File.WriteAllText(_metaPath, currentRowCount.ToString());
            _logger.LogInformation("Model training completed successfully due to new data.");
            context.WriteLine("Model training pipeline completed successfully.");
        }
    }
}
