using MissilePredictor.API.Telegram.Models;


namespace MissilePredictor.API.Telegram;

public interface ITgScraperService : IHostedService
{
    Task<IReadOnlyList<MessageDto>> GetMessagesAsync(int minId, CancellationToken ct = default);
}
