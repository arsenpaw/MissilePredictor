namespace MissilePredictor.Services;

public interface IAlertClient
{
    Task TriggerAlertAutomationAsync(IEnumerable<string> alertMessages, CancellationToken ct = default);
}
