using System.Security.Claims;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Scenarios;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoProtocol.Api.Controllers;

[ApiController, Authorize, Route("api/matches/{matchId:guid}/scenario")]
public sealed class ScenariosController(IScenarioService service,
    IScenarioSnapshotReadService snapshotReadService,
    IScenarioAdaptivePlanV2Service adaptivePlanV2Service) : ControllerBase
{
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
