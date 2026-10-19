using MissilePredictor.API.Telegram;

namespace MissilePredictor.API.Extensions;

public static class TelegramServiceCollectionExtensions
{
    public static IServiceCollection AddTelegramScraper(this IServiceCollection services, IConfiguration configuration)
    {
        // Telegram:Mock (see appsettings.Mock.json) runs without a Telegram login or connection.
        if (configuration.GetValue<bool>("Telegram:Mock"))
        {
            services.AddSingleton<ITgScraperService, MockTgScraperService>();
        }
        else
        {
            services
                .AddOptions<TelegramConfig>()
                .Bind(configuration.GetSection("Telegram"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<ITgScraperService, TgScraperService>();
        }
        services.AddHostedService(sp => sp.GetRequiredService<ITgScraperService>());

        return services;
    }
}
