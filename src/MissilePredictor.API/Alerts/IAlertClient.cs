namespace MissilePredictor.API.Alerts;

public interface IAlertClient
{
    Task TriggerAlertAutomationAsync(IEnumerable<string> alertMessages, CancellationToken ct = default);
}
