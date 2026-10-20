using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MissilePredictor.Tests.App;

/// <summary>
/// Boots the real app with every outbound dependency faked or off: mock Home Assistant (so no real alarm can
/// fire), mock Telegram, the Telegram/AI pipeline disabled and the Neptun stream pointed at a local fake.
/// </summary>
public sealed class OfflineAppFactory : WebApplicationFactory<Program>
{
    private readonly string _neptunUrl;

    public OfflineAppFactory(string neptunUrl) => _neptunUrl = neptunUrl;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("HomeAssistant:Mock", "true");
        builder.UseSetting("Telegram:Mock", "true");
        builder.UseSetting("TelegramAi:Enabled", "false");
        builder.UseSetting("Neptun:StreamUrl", _neptunUrl);
    }
}
