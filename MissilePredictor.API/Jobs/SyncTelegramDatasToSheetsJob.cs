using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissilePredictor.AI.Config;
using MissilePredictor.AI.Services;
using MissilePredictor.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MissilePredictor.API.Jobs;

public class SyncTelegramDatasToSheetsJob
{
    private readonly TgScraperService _scraperService;
    private readonly GoogleSheetsClient _sheetsClient;
    private readonly GoogleConfig _googleConfig;
    private readonly ILogger<SyncTelegramDatasToSheetsJob> _logger;
    private readonly PredictionDangerousMessageService _predictionService;

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

    public async Task Execute()
    {
        _logger.LogInformation("Starting SyncTelegramDatasToSheetsJob...");
        var newMessages = await _scraperService.GetUnreadMessagesAsync();
        
        if (newMessages.Count == 0)
        {
            _logger.LogInformation("No new messages found from Telegram to push.");
            return;
        }

        _logger.LogInformation($"Found {newMessages.Count} new messages, syncing to Google Sheets...");

        var predictions = _predictionService.PredictMany(newMessages.Select(x => x.Text)).ToList();
        
        var values = new List<IList<object>>();

        for (int i = 0; i < newMessages.Count; i++)
        {
            var msg = newMessages[i];
            var pred = predictions[i];
            
            values.Add(new List<object>
            {
                msg.Id.ToString(),
                msg.Date.ToString("o"), 
                msg.Text,
                pred.Prediction ? 1 : 0  
            });
        }
        
        await _sheetsClient.AppendDataAsync(_googleConfig.SpreadsheetId, _googleConfig.Range, values);
        
        _logger.LogInformation($"Successfully appended {newMessages.Count} new rows to sheets.");
    }
}
