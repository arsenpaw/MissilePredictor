namespace MissilePredictor.IntegrationTests;

/// <summary>
/// A <see cref="FactAttribute"/> for tests that fire the real Home Assistant alarm. It skips itself when
/// Claude Code runs it (Claude Code sets <c>CLAUDECODE=1</c>), so an AI agent never sets off the alarm,
/// even if the hook that blocks running this project is bypassed.
/// </summary>
public sealed class RealHomeAssistantFactAttribute : FactAttribute
{
    public RealHomeAssistantFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("CLAUDECODE") == "1")
            Skip = "Fires the real Home Assistant alarm; AI agents (CLAUDECODE=1) must not run it.";
    }
}
