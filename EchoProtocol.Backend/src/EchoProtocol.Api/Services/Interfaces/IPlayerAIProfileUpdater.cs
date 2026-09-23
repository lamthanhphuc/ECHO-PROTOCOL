using EchoProtocol.Api.Common;
using EchoProtocol.Api.Services.Models;

namespace EchoProtocol.Api.Services.Interfaces;

public interface IPlayerAIProfileUpdater
{
    Task<ServiceResult<PlayerAIProfileUpdateResult>> ProcessAsync(
        Guid matchId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
