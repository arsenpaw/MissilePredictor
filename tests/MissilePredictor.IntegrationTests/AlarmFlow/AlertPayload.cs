namespace MissilePredictor.IntegrationTests.AlarmFlow;

internal sealed record AlertPayload(IReadOnlyList<string> Messages);
