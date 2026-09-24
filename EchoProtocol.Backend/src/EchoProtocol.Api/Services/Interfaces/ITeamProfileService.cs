using EchoProtocol.Api.Common;
using EchoProtocol.Api.Services.Models;

namespace EchoProtocol.Api.Services.Interfaces;

public interface ITeamProfileService
{
    Task<ServiceResult<TeamProfileProcessResult>> ProcessAsync(
        Guid matchId,
        CancellationToken cancellationToken = default);
}
