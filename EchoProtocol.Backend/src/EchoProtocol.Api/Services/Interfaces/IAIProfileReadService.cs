using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Profiles;

namespace EchoProtocol.Api.Services.Interfaces;

public interface IAIProfileReadService
{
    Task<ServiceResult<PlayerAIProfileResponse>> GetOwnPlayerProfileAsync(
        Guid callerUserId, CancellationToken cancellationToken = default);
    Task<ServiceResult<RosterAIProfileResponse>> GetMatchRosterProfilesAsync(
        Guid matchId, Guid callerUserId, CancellationToken cancellationToken = default);
    Task<ServiceResult<TeamProfileResponse>> GetTeamProfileAsync(
        Guid matchId, Guid callerUserId, CancellationToken cancellationToken = default);
}
