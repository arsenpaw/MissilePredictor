using System.Text.Json;
using Microsoft.Extensions.Options;
using MissilePredictor.Config;
using MissilePredictor.Models;
using TL;
using WTelegram;

namespace MissilePredictor.Services;

public sealed class TgScraperService : IAsyncDisposable
{
    private readonly TelegramConfig _opt;
    private readonly Client _client;

    public TgScraperService(IOptions<TelegramConfig> opt)
    {
        _opt = opt.Value;
        var cwd = Directory.GetCurrentDirectory();
        var path = Path.Combine(cwd, opt.Value.SessionPath);
        var isExists = File.Exists(path);
        _client = !isExists ? new Client() : new Client(Config);

    }

    public async Task<IReadOnlyList<MessageDto>> GetUnreadMessagesAsync(int limit = 10, CancellationToken ct = default)
    {
        await EnsureLoginAsync(ct);

        var uname = _opt.Channel.Trim();
        if (uname.StartsWith("@")) uname = uname[1..];

        var peer = await _client.Contacts_ResolveUsername(uname);
        var hist = await _client.Messages_GetHistory(peer, limit: limit);

        var messages = hist.Messages
            .OfType<Message>()
            .Where(m => m.message is not null)
            .Select(m => new MessageDto
            {
                Id = m.id,
                Date = m.Date,
                Text = m.message!
            })
            .OrderBy(m => m.Id)
            .ToList();

        var lastId = ReadLastId();
        var newOnes = messages.Where(m => m.Id > lastId).ToList();

        if (newOnes.Count > 0)
            SaveLastId(newOnes.Max(m => m.Id));

        return newOnes;
    }

    private async Task EnsureLoginAsync(CancellationToken ct)
    {
        var me = await _client.LoginUserIfNeeded();
        if (me is null)
            throw new InvalidOperationException("Login required. Provide PhoneNumber/VerificationCode/Password in TelegramOptions for first run.");
    }

    private string? Config(string what) => what switch
    {
        "api_hash" => _opt.ApiHash,
        "phone_number" => _opt.PhoneNumber,
        _ => null
    };

    private long ReadLastId()
    {
        var path = _opt.LastIdPath;
        if (!File.Exists(path)) return 0;
        try
        {
            using var fs = File.OpenRead(path);
            var doc = JsonSerializer.Deserialize<Dictionary<string, long>>(fs);
            return doc is not null && doc.TryGetValue("msg", out var v) ? v : 0;
        }
        catch { return 0; }
    }

    private void SaveLastId(long id)
    {
        var path = _opt.LastIdPath;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
        var payload = new Dictionary<string, long> { ["msg"] = id };
        File.WriteAllText(path, JsonSerializer.Serialize(payload));
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
