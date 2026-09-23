using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Rewards;

namespace EchoProtocol.Api.Services.Interfaces;

public interface IRewardService
{
    Task<ServiceResult<RewardProcessingResponse>> ProcessAsync(
        Guid matchId,
        CancellationToken cancellationToken = default);
}
