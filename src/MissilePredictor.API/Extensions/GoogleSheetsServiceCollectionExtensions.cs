using MissilePredictor.AI.GoogleSheets;

namespace MissilePredictor.API.Extensions;

public static class GoogleSheetsServiceCollectionExtensions
{
    public static IServiceCollection AddGoogleSheets(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<GoogleConfig>()
            .Bind(configuration.GetSection("Google"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<GoogleSheetsClient>();

        return services;
    }
}
