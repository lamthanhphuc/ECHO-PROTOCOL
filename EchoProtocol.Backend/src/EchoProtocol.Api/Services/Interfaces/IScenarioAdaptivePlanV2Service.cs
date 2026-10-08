using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Scenarios;

namespace EchoProtocol.Api.Services.Interfaces;

public interface IScenarioAdaptivePlanV2Service
{
    Task<ServiceResult<ScenarioAdaptivePlanV2Dto>> SubmitAsync(
        Guid callerUserId, Guid matchId, SubmitScenarioAdaptivePlanV2Request request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ScenarioAdaptivePlanV2Dto>> ConfirmAppliedAsync(
        Guid callerUserId, Guid matchId, Guid decisionId, string planFingerprint,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ScenarioAdaptivePlanV2Dto>> AbortPendingAsync(
        Guid callerUserId, Guid matchId, Guid decisionId,
        CancellationToken cancellationToken = default);
}
