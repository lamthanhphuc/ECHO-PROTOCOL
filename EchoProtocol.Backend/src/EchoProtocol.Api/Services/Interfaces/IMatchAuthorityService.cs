using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.MatchAuthority;

namespace EchoProtocol.Api.Services.Interfaces;

public interface IMatchAuthorityService
{
    Task<ServiceResult<MatchAuthorityResponse>> CreateAsync(
        Guid hostUserId, CreateMatchAuthorityRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<JoinProofResponse>> IssueJoinProofAsync(
        Guid userId, Guid matchId, IssueJoinProofRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<MatchPlayerBindingResponse>> BindPlayerAsync(
        Guid hostUserId, Guid matchId, BindMatchPlayerRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<MatchAuthorityResponse>> RenewLeaseAsync(
        Guid hostUserId, Guid matchId, CancellationToken cancellationToken);
    Task<ServiceResult<MatchAuthorityResponse>> StartAsync(
        Guid hostUserId, Guid matchId, CancellationToken cancellationToken);
    Task<ServiceResult<MatchPlayerBindingResponse>> MarkPlayerDisconnectedAsync(
        Guid hostUserId, Guid matchId, int fusionActorNumber, CancellationToken cancellationToken);
    Task<ServiceResult<MatchAuthorityResponse>> EndAsync(
        Guid hostUserId, Guid matchId, EndMatchAuthorityRequest request, CancellationToken cancellationToken);
    Task<bool> CanSubmitTelemetryAsync(
        Guid submittingUserId, Guid matchId, Guid eventUserId, CancellationToken cancellationToken);
    Task<bool> CanSubmitSystemTelemetryAsync(
        Guid submittingUserId, Guid matchId, CancellationToken cancellationToken);
}
