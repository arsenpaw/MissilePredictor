using MissilePredictor.Models;

namespace MissilePredictor.Services;

public interface ITgScraperService : IHostedService
{
    Task<IReadOnlyList<MessageDto>> GetMessagesAsync(int minId, CancellationToken ct = default);
}
