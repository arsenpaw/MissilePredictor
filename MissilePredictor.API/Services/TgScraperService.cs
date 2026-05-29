using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    private readonly System.Threading.Channels.Channel<MessageDto> _incoming =
        System.Threading.Channels.Channel.CreateUnbounded<MessageDto>();
    private long _targetChannelId;

    public TgScraperService(IOptions<TelegramConfig> opt, ILogger<TgScraperService> logger)
    {
        _opt = opt.Value;
        _logger = logger;
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
        _incoming.Writer.TryComplete();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Drains all buffered messages (startup replay + real-time pushes) since last call.
    /// </summary>
    public IReadOnlyList<MessageDto> DrainNewMessages()
    {
        var result = new List<MessageDto>();
        while (_incoming.Reader.TryRead(out var msg))
            result.Add(msg);
        return result;
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
            await _incoming.Writer.WriteAsync(dto);
            _logger.LogInformation("Real-time message received #{Id}", dto.Id);
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
