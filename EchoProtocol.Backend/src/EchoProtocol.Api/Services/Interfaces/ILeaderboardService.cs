using EchoProtocol.Api.DTOs.Leaderboards;

namespace EchoProtocol.Api.Services.Interfaces;

public interface ILeaderboardService
{
    Task<LeaderboardPageResponse> GetAsync(
        LeaderboardMode mode,
        string mapId,
        string difficulty,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
