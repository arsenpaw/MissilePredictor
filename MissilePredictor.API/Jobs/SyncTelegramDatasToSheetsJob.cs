using Hangfire.Console;
using Hangfire.Server;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissilePredictor.AI.Config;
using MissilePredictor.AI.Services;
using MissilePredictor.Services;

namespace MissilePredictor.Jobs;

public class SyncTelegramDatasToSheetsJob
{
    private readonly TgScraperService _scraperService;
    private readonly GoogleSheetsClient _sheetsClient;
    private readonly GoogleConfig _googleConfig;
    private readonly PredictionDangerousMessageService _predictionService;
    private readonly ILogger<SyncTelegramDatasToSheetsJob> _logger;

    public SyncTelegramDatasToSheetsJob(
        TgScraperService scraperService,
        GoogleSheetsClient sheetsClient,
        IOptions<GoogleConfig> googleConfig,
        PredictionDangerousMessageService predictionService,
        ILogger<SyncTelegramDatasToSheetsJob> logger)
    {
        _scraperService = scraperService;
        _sheetsClient = sheetsClient;
        _googleConfig = googleConfig.Value;
        _predictionService = predictionService;
        _logger = logger;
    }

    public async Task Execute(PerformContext context)
    {
        _logger.LogInformation("SyncTelegramDatasToSheetsJob started");
        context.WriteLine("Starting SyncTelegramDatasToSheetsJob to check unread Telegram messages...");

        int lastId = 0;
        try
        {
            lastId = await _sheetsClient.GetLastIdAsync(_googleConfig.SpreadsheetId, _googleConfig.Range);
            _logger.LogInformation("Last synced message ID from sheet: {LastId}", lastId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read last ID from Google Sheets, defaulting to 0");
            context.WriteLine($"Failed to read last ID from Google Sheets: {ex.Message}. Using default lastId = 0.");
        }

        var newMessages = await _scraperService.GetMessagesAsync(lastId);

        _logger.LogInformation("Fetched {MessageCount} new messages since ID {LastId}", newMessages.Count, lastId);
        context.WriteLine($"Total new messages to process: {newMessages.Count}");

        if (newMessages.Count == 0)
        {
            _logger.LogInformation("No new messages to sync");
            context.WriteLine("Finished, no work to do.");
            return;
        }

        var predictions = _predictionService.PredictMany(newMessages.Select(x => x.Text)).ToList();

        var values = new List<IList<object>>();

        for (int i = 0; i < newMessages.Count; i++)
        {
            var msg = newMessages[i];
            var pred = predictions[i];

            _logger.LogInformation(
                "Syncing message: MessageId={MessageId} Date={Date} IsDangerous={IsDangerous} Probability={Probability:F4} Text={Text}",
                msg.Id, msg.Date, pred.Prediction, pred.Probability, msg.Text);

            values.Add([msg.Id.ToString(), msg.Date.ToString("o"), msg.Text, pred.Prediction ? 1 : 0]);

            context.WriteLine($"Prepared row: ID={msg.Id}, Date={msg.Date:o}, Prediction={pred.Prediction}, Probability={pred.Probability:F4}");
        }

        await _sheetsClient.AppendDataAsync(_googleConfig.SpreadsheetId, _googleConfig.Range, values);

        _logger.LogInformation("SyncTelegramDatasToSheetsJob completed: {RowCount} rows written to sheet", values.Count);
        context.WriteLine($"Success! Inserted {newMessages.Count} records to the spreadsheet.");
    }
}
