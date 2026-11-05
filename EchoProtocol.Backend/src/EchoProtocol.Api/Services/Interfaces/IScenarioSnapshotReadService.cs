using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Scenarios;

namespace EchoProtocol.Api.Services.Interfaces;

public interface IScenarioSnapshotReadService
{
    Task<ServiceResult<AdaptiveInputSnapshotReadResponse>> GetAsync(
        Guid callerUserId, Guid matchId, Guid decisionId,
        CancellationToken cancellationToken = default);
}
