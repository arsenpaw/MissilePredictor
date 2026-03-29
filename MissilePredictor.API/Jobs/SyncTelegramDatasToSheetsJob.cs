using Hangfire.Console;
using Hangfire.Server;
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

    public SyncTelegramDatasToSheetsJob(
        TgScraperService scraperService, 
        GoogleSheetsClient sheetsClient, 
        IOptions<GoogleConfig> googleConfig, 
        PredictionDangerousMessageService predictionService)
    {
        _scraperService = scraperService;
        _sheetsClient = sheetsClient;
        _googleConfig = googleConfig.Value;
        _predictionService = predictionService;
    }

    public async Task Execute(PerformContext context)
    {
        context.WriteLine("Starting SyncTelegramDatasToSheetsJob to check unread Telegram messages...");
        
        int lastId = 0;
        try
        {
            lastId = await _sheetsClient.GetLastIdAsync(_googleConfig.SpreadsheetId, _googleConfig.Range);
        }
        catch (Exception ex)
        {
            context.WriteLine($"Failed to read last ID from Google Sheets: {ex.Message}. Using default lastId = 0.");
        }

        var newMessages = await _scraperService.GetMessagesAsync(lastId);
        
        context.WriteLine($"Total new messages to process: {newMessages.Count}");

        if (newMessages.Count == 0)
        {
            context.WriteLine("Finished, no work to do.");
            return;
        }

        context.WriteLine($"Predicting sentiment for {newMessages.Count} new messages...");

        var predictions = _predictionService.PredictMany(newMessages.Select(x => x.Text)).ToList();
        
        var values = new List<IList<object>>();

        for (int i = 0; i < newMessages.Count; i++)
        {
            var msg = newMessages[i];
            var pred = predictions[i];
            
            var row = new List<object>
            {
                msg.Id.ToString(),
                msg.Date.ToString("o"), 
                msg.Text,
                pred.Prediction ? 1 : 0  
            };
            values.Add(row);
            
            context.WriteLine($"Prepared row: ID={row[0]}, Date={row[1]}, Text={row[2]}, Prediction={row[3]}");
        }
        
        context.WriteLine("Appending rows to Google Sheets...");
        await _sheetsClient.AppendDataAsync(_googleConfig.SpreadsheetId, _googleConfig.Range, values);
        
        context.WriteLine($"Success! Inserted {newMessages.Count} records to the spreadsheet.");
    }
}
