using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Scenarios;

namespace EchoProtocol.Api.Services.Interfaces;

public interface IAEDDirectorApprovalV1Service
{
    Task<ServiceResult<AEDDirectorResearchResponseV1>> SubmitResearchAsync(Guid callerId, Guid matchId, AEDDirectorResearchRequestV1 request, CancellationToken ct = default);
    Task<ServiceResult<AEDDirectorResearchResponseV1>> GetDecisionAsync(Guid callerId, Guid matchId, Guid decisionId, CancellationToken ct = default);
    Task<ServiceResult<AEDDirectorResearchResponseV1>> AbortResearchDecisionAsync(Guid callerId, Guid matchId, Guid decisionId, CancellationToken ct = default);
}
