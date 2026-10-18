using MissilePredictor.Models;

namespace MissilePredictor.Services;

/// <summary>
/// Stand-in for <see cref="TgScraperService"/> when <c>Telegram:Mock</c> is on: no login,
/// no Telegram connection, no channel messages and an empty history.
/// </summary>
public sealed class MockTgScraperService : ITgScraperService
{
    private readonly ILogger<MockTgScraperService> _logger;

    public MockTgScraperService(ILogger<MockTgScraperService> logger) => _logger = logger;

    public Task StartAsync(CancellationToken ct)
    {
        _logger.LogWarning("Telegram mock enabled: skipping login, no channel messages will arrive.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    public Task<IReadOnlyList<MessageDto>> GetMessagesAsync(int minId, CancellationToken ct = default)
    {
        _logger.LogInformation("Telegram mock: no history to return since message {MinId}", minId);
        return Task.FromResult<IReadOnlyList<MessageDto>>([]);
    }
}
