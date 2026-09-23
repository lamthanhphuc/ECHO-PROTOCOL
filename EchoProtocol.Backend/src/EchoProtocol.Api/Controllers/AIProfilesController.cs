using System.Security.Claims;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Profiles;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoProtocol.Api.Controllers;

[ApiController]
[Authorize]
public sealed class AIProfilesController(IAIProfileReadService service) : ControllerBase
{
    [HttpGet("api/profiles/ai/me")]
    public async Task<ActionResult<ApiResponse<PlayerAIProfileResponse>>> Me(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse<object>.Fail("Invalid token", ErrorCodes.TokenInvalid));
        var result = await service.GetOwnPlayerProfileAsync(userId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("api/matches/{matchId:guid}/player-ai-profiles")]
    public async Task<ActionResult<ApiResponse<RosterAIProfileResponse>>> Roster(
        Guid matchId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse<object>.Fail("Invalid token", ErrorCodes.TokenInvalid));
        var result = await service.GetMatchRosterProfilesAsync(matchId, userId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("api/matches/{matchId:guid}/team-profile")]
    public async Task<ActionResult<ApiResponse<TeamProfileResponse>>> Team(
        Guid matchId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse<object>.Fail("Invalid token", ErrorCodes.TokenInvalid));
        var result = await service.GetTeamProfileAsync(matchId, userId, cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private ActionResult<ApiResponse<T>> ToActionResult<T>(ServiceResult<T> result)
    {
        if (result.IsSuccess) return Ok(ApiResponse<T>.Ok(result.Data!, result.Message));
        var status = result.ErrorCode switch
        {
            ErrorCodes.ProfileReadForbidden => StatusCodes.Status403Forbidden,
            ErrorCodes.AIProfileNotFound or ErrorCodes.TeamProfileNotFound or ErrorCodes.MatchNotFound => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status400BadRequest
        };
        return StatusCode(status, ApiResponse<T>.Fail(result.Message, result.ErrorCode!));
    }
}
