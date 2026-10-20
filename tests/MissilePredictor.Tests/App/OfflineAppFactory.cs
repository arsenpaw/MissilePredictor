using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace MissilePredictor.Tests.App;

/// <summary>
/// Boots the real app with every outbound dependency faked or off: mock Home Assistant (so no real alarm can
/// fire), mock Telegram, the Telegram/AI pipeline disabled and the Neptun stream pointed at a local fake.
/// </summary>
public sealed class OfflineAppFactory : WebApplicationFactory<Program>
{
    private readonly string _neptunUrl;
    private readonly string _historyDbPath = Path.Combine(Path.GetTempPath(), $"history-{Guid.NewGuid():N}.db");

    public string HistoryDbPath => _historyDbPath;

    public OfflineAppFactory(string neptunUrl) => _neptunUrl = neptunUrl;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("HomeAssistant:Mock", "true");
        builder.UseSetting("Telegram:Mock", "true");
        builder.UseSetting("TelegramAi:Enabled", "false");
        builder.UseSetting("Neptun:StreamUrl", _neptunUrl);
        builder.UseSetting("History:ConnectionString", $"Data Source={_historyDbPath}");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            try { File.Delete(_historyDbPath + suffix); }
            catch (IOException) { }
        }
    }
}
