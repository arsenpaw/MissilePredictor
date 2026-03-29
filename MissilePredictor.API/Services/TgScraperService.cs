using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissilePredictor.Jobs;
using MissilePredictor.Config;
using MissilePredictor.Models;
using TL;
using WTelegram;

namespace MissilePredictor.Services;

public class ScraperState
{
    public int LastId { get; set; }
}

public sealed class TgScraperService : IAsyncDisposable
{
    private readonly TelegramConfig _opt;
    private readonly Client _client;
    private readonly ILogger<TgScraperService> _logger;
    private readonly FileState<ScraperState> _state;

    public TgScraperService(IOptions<TelegramConfig> opt,  ILogger<TgScraperService> logger)
    {
        _opt = opt.Value;
        var cwd = AppContext.BaseDirectory;
       _logger = logger;
        _client = new Client(Config);
        
        var dir = Path.GetDirectoryName(_opt.LastIdPath);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
        _state = new FileState<ScraperState>(_opt.LastIdPath);
    }



    public async Task<IReadOnlyList<MessageDto>> GetAndSaveUnreadMessagesAsync(CancellationToken ct = default)
    {
        var lastId = _state.Value.LastId;
        var result =  await GetMessagesAsync(lastId, ct);
    

        if (result.Count > 0)
            _state.Save(s => s.LastId = result.Max(m => m.Id));

        return result;
    }

    public async Task<IReadOnlyList<MessageDto>> GetMessagesAsync(int minId , CancellationToken ct = default)
    {
        await EnsureLoginAsync(ct);

        var uname = _opt.Channel.Trim();
        if (uname.StartsWith("@")) uname = uname[1..];

        var peer     = await _client.Contacts_ResolveUsername(uname);
        var result   = new List<MessageDto>();
        int offsetId = 0;                        // 0 = start from latest message

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var hist = await _client.Messages_GetHistory(
                peer,
                offset_id: offsetId,
                min_id:    minId,               // server filters out already-seen messages
                limit:     100);

            var batch = hist.Messages
                .OfType<Message>()
                .Where(m => m.message is not null)
                .Select(m => new MessageDto
                {
                    Id   = m.id,
                    Date = m.Date,
                    Text = m.message!
                })
                .ToList();

            if (batch.Count == 0)
                break;

            result.AddRange(batch);

            if (batch.Count < 100)
                break;                           // last page

            offsetId = batch.Min(m => m.Id);    // go further back
            await Task.Delay(400, ct);
        }

        return result
            .OrderBy(m => m.Id)
            .ToList();
    }

    private async Task EnsureLoginAsync(CancellationToken ct)
    {
        var me = await _client.LoginUserIfNeeded(new CodeSettings()
        {
            
        });
        if (me is null)
            throw new InvalidOperationException("Login required. Provide PhoneNumber/VerificationCode/Password in TelegramOptions for first run.");
    }

    private string? Config(string what) => what switch
    {
        "api_hash"          => _opt.ApiHash,
        "api_id"            => _opt.ApiId.ToString(),
        "session_pathname"  => _opt.SessionPath,
        "phone_number"      => _opt.PhoneNumber,
        "verification_code" => Prompt("Enter Telegram code: "),
        "password"          => Prompt("Enter 2FA password: "),   // only called if 2FA enabled
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
        GC.SuppressFinalize(this);
    }
}
