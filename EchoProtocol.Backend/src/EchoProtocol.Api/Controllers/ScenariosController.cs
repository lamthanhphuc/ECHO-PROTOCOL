using System.Security.Claims;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Scenarios;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoProtocol.Api.Controllers;

[ApiController, Authorize, Route("api/matches/{matchId:guid}/scenario")]
public sealed class ScenariosController(IScenarioService service) : ControllerBase
{
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
