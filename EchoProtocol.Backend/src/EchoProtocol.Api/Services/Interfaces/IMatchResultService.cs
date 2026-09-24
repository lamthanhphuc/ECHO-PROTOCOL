using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.MatchResults;

namespace EchoProtocol.Api.Services.Interfaces;

public interface IMatchResultService
{
    Task<ServiceResult<MatchResultResponse>> SubmitAsync(
        Guid hostUserId,
        Guid matchId,
        SubmitMatchResultRequest request,
        CancellationToken cancellationToken = default);
}
