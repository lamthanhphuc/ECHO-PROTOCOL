using System.Security.Claims;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Scenarios;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace EchoProtocol.Api.Controllers;

[ApiController, Authorize, Route("api/matches/{matchId:guid}/scenario")]
public sealed class ScenariosController(IScenarioService service,
    IScenarioSnapshotReadService snapshotReadService,
    IScenarioAdaptivePlanV2Service adaptivePlanV2Service,
    IAEDv2PhaseEvidenceVerifier phaseEvidenceVerifier,
    AppDbContext db,
    TimeProvider clock) : ControllerBase
{
    [HttpPost("director-evidence-v1")]
    public async Task<IActionResult> VerifyDirectorEvidenceV1(Guid matchId, AEDPhase8EvidenceProbeRequestV1 request, CancellationToken ct)
    {
        if (!TryUser(out var userId)) return Unauthorized(ApiResponse<object>.Fail("Invalid token", ErrorCodes.TokenInvalid));
        if (matchId == Guid.Empty || request == null || request.CompletedEvidencePhaseOrdinal < 1 || request.EvidenceFingerprint?.Length != 64 || !request.EvidenceFingerprint.All(Uri.IsHexDigit)) return BadRequest();
        var binding = await db.MatchAuthorityBindings.AsNoTracking().SingleOrDefaultAsync(x => x.MatchId == matchId, ct);
        if (binding == null) return NotFound();
        if (binding.HostUserId != userId) return StatusCode(403);
        if (binding.LeaseExpiresAtUtc <= clock.GetUtcNow().UtcDateTime) return StatusCode(410);
        if (binding.Status != MatchAuthorityStatus.InMatch) return StatusCode(409);
        var scenario = await db.ScenarioDecisions.AsNoTracking().SingleOrDefaultAsync(x => x.MatchId == matchId && x.IsCurrent, ct);
        if (scenario == null || scenario.ResolutionMode != ScenarioResolutionMode.Adaptive || scenario.UsedFixedFallback) return StatusCode(409);
        if (!await phaseEvidenceVerifier.VerifyAsync(matchId, request.CompletedEvidencePhaseOrdinal, request.EvidenceFingerprint, ct)) return StatusCode(409);
        var receipt = new AEDPhase8EvidenceProbeResultV1(matchId, request.CompletedEvidencePhaseOrdinal, request.EvidenceFingerprint, true, false, false, false, "P8_CANONICAL_VERIFIED_EXTENDED_UNSUPPORTED");
        return Ok(ApiResponse<AEDPhase8EvidenceProbeResultV1>.Ok(receipt, "AED Phase 8 canonical evidence verified"));
    }

    [HttpPost("plans-v2")]
    public async Task<IActionResult> SubmitPlanV2(Guid matchId,
        SubmitScenarioAdaptivePlanV2Request request, CancellationToken ct)
    {
        if (!TryUser(out var userId)) return Unauthorized(ApiResponse<object>.Fail("Invalid token", ErrorCodes.TokenInvalid));
        var result = await adaptivePlanV2Service.SubmitAsync(userId, matchId, request, ct);
        return Respond(result, result.Data?.IsReplay == true ? 200 : 201);
    }

    [HttpPut("plans-v2/{decisionId:guid}/applied")]
    public async Task<IActionResult> ApplyPlanV2(Guid matchId, Guid decisionId,
        ConfirmScenarioAdaptivePlanV2AppliedRequest request, CancellationToken ct)
    {
        if (!TryUser(out var userId)) return Unauthorized(ApiResponse<object>.Fail("Invalid token", ErrorCodes.TokenInvalid));
        return Respond(await adaptivePlanV2Service.ConfirmAppliedAsync(userId, matchId,
            decisionId, request.PlanFingerprint, ct), 200);
    }

    [HttpPut("plans-v2/{decisionId:guid}/aborted")]
    public async Task<IActionResult> AbortPlanV2(Guid matchId, Guid decisionId, CancellationToken ct)
    {
        if (!TryUser(out var userId)) return Unauthorized(ApiResponse<object>.Fail("Invalid token", ErrorCodes.TokenInvalid));
        return Respond(await adaptivePlanV2Service.AbortPendingAsync(
            userId, matchId, decisionId, ct), 200);
    }

    [HttpGet("decisions/{decisionId:guid}/input-snapshot")]
    public async Task<IActionResult> InputSnapshot(Guid matchId, Guid decisionId, CancellationToken ct)
    {
        if (!TryUser(out var userId)) return Unauthorized(ApiResponse<object>.Fail("Invalid token", ErrorCodes.TokenInvalid));
        return Respond(await snapshotReadService.GetAsync(userId, matchId, decisionId, ct), 200);
    }
    [HttpPost("resolve")]
    public async Task<IActionResult> Resolve(Guid matchId, ResolveScenarioRequest request, CancellationToken ct)
    {
        if (!TryUser(out var userId)) return Unauthorized(ApiResponse<object>.Fail("Invalid token", ErrorCodes.TokenInvalid));
        var result = await service.ResolvePreMatchAsync(userId, matchId, request, ct);
        return Respond(result, result.Data?.IsReplay == true ? 200 : 201);
    }

    [HttpPut("decisions/{decisionId:guid}/applied")]
    public async Task<IActionResult> Applied(Guid matchId, Guid decisionId, ConfirmScenarioAppliedRequest request, CancellationToken ct)
    {
        if (!TryUser(out var userId)) return Unauthorized(ApiResponse<object>.Fail("Invalid token", ErrorCodes.TokenInvalid));
        return Respond(await service.ConfirmAppliedAsync(userId, matchId, decisionId, request, ct), 200);
    }

    private bool TryUser(out Guid id) => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out id);
    private ObjectResult Respond<T>(ServiceResult<T> result, int success) => result.IsSuccess
        ? StatusCode(success, ApiResponse<T>.Ok(result.Data!, result.Message))
        : StatusCode(result.ErrorCode switch
        {
            ErrorCodes.MatchAuthorityForbidden => 403,
            ErrorCodes.MatchNotFound or ErrorCodes.ScenarioDecisionNotFound => 404,
            ErrorCodes.MatchLeaseExpired => 410,
            ErrorCodes.ScenarioDecisionIdentityConflict or ErrorCodes.ScenarioDecisionConflict
                or ErrorCodes.ScenarioDecisionStaleRoster or ErrorCodes.ScenarioApplyConflict => 409,
            _ => 400
        }, ApiResponse<object>.Fail(result.Message, result.ErrorCode!));
}
