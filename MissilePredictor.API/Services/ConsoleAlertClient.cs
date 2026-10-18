namespace MissilePredictor.Services;

/// <summary>
/// Stand-in for <see cref="HomeAssistantClient"/> when <c>HomeAssistant:Mock</c> is on:
/// alerts are only written to the log, no webhook is called.
/// </summary>
public sealed class ConsoleAlertClient : IAlertClient
{
    private readonly ILogger<ConsoleAlertClient> _logger;

    public ConsoleAlertClient(ILogger<ConsoleAlertClient> logger) => _logger = logger;

    public Task TriggerAlertAutomationAsync(IEnumerable<string> alertMessages, CancellationToken ct = default)
    {
        foreach (var message in alertMessages)
            _logger.LogWarning("MOCK alert (Home Assistant not called): {Message}", message);

        return Task.CompletedTask;
    }
}
