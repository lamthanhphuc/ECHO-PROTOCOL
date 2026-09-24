using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Player;

namespace EchoProtocol.Api.Services.Interfaces;

public interface IPlayerProfileService
{
    Task<ServiceResult<PlayerProfileResponse>> GetCurrentAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
