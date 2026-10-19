using MissilePredictor.API.Alerts;
using MissilePredictor.API.Alerts.HomeAssistant;

namespace MissilePredictor.API.Extensions;

public static class AlertsServiceCollectionExtensions
{
    public static IServiceCollection AddAlerts(this IServiceCollection services, IConfiguration configuration)
    {
        // HomeAssistant:Mock writes alerts to the console instead of calling the webhook.
        if (configuration.GetValue<bool>("HomeAssistant:Mock"))
        {
            services.AddSingleton<IAlertClient, ConsoleAlertClient>();
        }
        else
        {
            services
                .AddOptions<HomeAssistantConfig>()
                .Bind(configuration.GetSection("HomeAssistant"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IAlertClient, HomeAssistantClient>();
        }

        return services;
    }
}
