using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissilePredictor.AI.Services;
using MissilePredictor.Config;
using MissilePredictor.Models;
using TL;
using WTelegram;

namespace MissilePredictor.Services;

public sealed class TgScraperService : IHostedService, IAsyncDisposable
{
    private readonly TelegramConfig _opt;
    private readonly Client _client;
    private readonly ILogger<TgScraperService> _logger;
    private readonly PredictionDangerousMessageService _predictionService;
    private readonly HomeAssistantClient _haClient;
    private long _targetChannelId;

    public TgScraperService(
        IOptions<TelegramConfig> opt,
        ILogger<TgScraperService> logger,
        PredictionDangerousMessageService predictionService,
        HomeAssistantClient haClient)
    {
        _opt = opt.Value;
        _logger = logger;
        _predictionService = predictionService;
        _haClient = haClient;
        _client = new Client(Config);
    }

    public async Task StartAsync(CancellationToken ct)
    {
        await EnsureLoginAsync(ct);
        _targetChannelId = await ResolveChannelIdAsync();
        _client.OnUpdates += HandleUpdateAsync;
        _logger.LogInformation("TgScraperService listening for real-time updates.");
    }

    public Task StopAsync(CancellationToken ct)
    {
        _client.OnUpdates -= HandleUpdateAsync;
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<MessageDto>> GetMessagesAsync(int minId, CancellationToken ct = default)
    {
        var uname = _opt.Channel.Trim().TrimStart('@');
        var peer = await _client.Contacts_ResolveUsername(uname);
        var result = new List<MessageDto>();
        int offsetId = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var hist = await _client.Messages_GetHistory(
                peer,
                offset_id: offsetId,
                min_id: minId,
                limit: 100);

            var batch = hist.Messages
                .OfType<Message>()
                .Where(m => m.message is not null)
                .Select(m => new MessageDto
                {
                    Id = m.id,
                    Date = m.Date,
                    Text = m.message!
                })
                .ToList();

            if (batch.Count == 0) break;

            result.AddRange(batch);

            if (batch.Count < 100) break;

            offsetId = batch.Min(m => m.Id);
            await Task.Delay(3000, ct);
        }

        return result.OrderBy(m => m.Id).ToList();
    }

    private async Task HandleUpdateAsync(IObject arg)
    {
        if (arg is not UpdatesBase ub) return;

        foreach (var update in ub.UpdateList)
        {
            if (update is not UpdateNewMessage { message: Message msg }) continue;
            if (string.IsNullOrWhiteSpace(msg.message)) continue;
            if (msg.peer_id is not PeerChannel ch || ch.channel_id != _targetChannelId) continue;

            var dto = new MessageDto { Id = msg.id, Date = msg.Date, Text = msg.message };

            _logger.LogInformation(
                "Telegram message received: Channel={Channel} MessageId={MessageId} Date={Date} Text={Text}",
                _opt.Channel, dto.Id, dto.Date, dto.Text);

            var prediction = _predictionService.Predict(dto.Text);
            if (prediction.Prediction)
            {
                _logger.LogWarning(
                    "Dangerous message detected, triggering Home Assistant: MessageId={MessageId} Probability={Probability:F4}",
                    dto.Id, prediction.Probability);
                await _haClient.TriggerAlertAutomationAsync([dto.Text]);
            }
        }
    }

    private async Task<long> ResolveChannelIdAsync()
    {
        var uname = _opt.Channel.Trim().TrimStart('@');
        var resolved = await _client.Contacts_ResolveUsername(uname);
        var channel = resolved.Channel
            ?? throw new InvalidOperationException($"Cannot resolve '{_opt.Channel}' as a Telegram channel.");
        return channel.id;
    }

    private async Task EnsureLoginAsync(CancellationToken ct)
    {
        var me = await _client.LoginUserIfNeeded(new CodeSettings());
        if (me is null)
            throw new InvalidOperationException("Login required. Provide PhoneNumber in TelegramOptions for first run.");
    }

    private string? Config(string what) => what switch
    {
        "api_hash"          => _opt.ApiHash,
        "api_id"            => _opt.ApiId.ToString(),
        "session_pathname"  => _opt.SessionPath,
        "phone_number"      => _opt.PhoneNumber,
        "verification_code" => Prompt("Enter Telegram code: "),
        "password"          => Prompt("Enter 2FA password: "),
        _                   => null
    };

    private static string? Prompt(string message)
    {
        Console.Write(message);
        return Console.ReadLine();
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
    }
}
