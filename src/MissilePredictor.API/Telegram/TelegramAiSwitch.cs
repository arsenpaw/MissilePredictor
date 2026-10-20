namespace MissilePredictor.API.Telegram;

/// <summary>Kill switch for the Telegram scraper, ML model, Google Sheets and nightly jobs, so the app can run Neptun-only.</summary>
public static class TelegramAiSwitch
{
    public const string Key = "TelegramAi:Enabled";

    public static bool IsEnabled(IConfiguration configuration) => configuration.GetValue(Key, true);
}
