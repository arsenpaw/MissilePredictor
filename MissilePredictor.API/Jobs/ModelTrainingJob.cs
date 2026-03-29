using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissilePredictor.AI.Config;
using MissilePredictor.AI.Services;
using MissilePredictor.AI.Training;
using System.IO;
using System.Threading.Tasks;
using MissilePredictor.Jobs;
using Hangfire.Console;
using Hangfire.Server;

namespace MissilePredictor.API.Jobs;

public class TrainingJobState
{
    public int LastTrainedRowCount { get; set; } = -1;
}

public class ModelTrainingJob
{
    private readonly FileState<TrainingJobState> _state;
    private readonly SentimentModelPipeline _pipeline;
    private readonly GoogleSheetsClient _sheetsClient;
    private readonly GoogleConfig _googleConfig;
    private readonly ILogger<ModelTrainingJob> _logger;

    public ModelTrainingJob(SentimentModelPipeline pipeline, GoogleSheetsClient sheetsClient, IOptions<GoogleConfig> googleConfig, ILogger<ModelTrainingJob> logger)
    {
        _state = new FileState<TrainingJobState>(Path.Combine("data", "training_state.json"));
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
        
        context.WriteLine($"Current row count in sheet: {currentRowCount} | Last trained count: {_state.Value.LastTrainedRowCount}");

        if (currentRowCount <= _state.Value.LastTrainedRowCount)
        {
            _logger.LogInformation("Model training skipped: no new data.");
            context.WriteLine("Model training skipped: no new data.");
            return;
        }

        context.WriteLine("New data found. Initiating ML Pipeline...");

        int trainedRows = await _pipeline.RunAsync(context);
        
        if (trainedRows > 0)
        {
            _state.Save(s => s.LastTrainedRowCount = currentRowCount);
            _logger.LogInformation("Model training completed successfully due to new data.");
            context.WriteLine("Model training pipeline completed successfully.");
        }
    }
}
