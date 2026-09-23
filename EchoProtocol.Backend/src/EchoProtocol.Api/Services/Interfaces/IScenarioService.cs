using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Scenarios;

namespace EchoProtocol.Api.Services.Interfaces;

public interface IScenarioService
{
    Task<ServiceResult<ScenarioDecisionResponse>> ResolvePreMatchAsync(
        Guid callerUserId, Guid matchId, ResolveScenarioRequest request,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<ScenarioDecisionResponse>> ConfirmAppliedAsync(
        Guid callerUserId, Guid matchId, Guid decisionId, ConfirmScenarioAppliedRequest request,
        CancellationToken cancellationToken = default);
}
