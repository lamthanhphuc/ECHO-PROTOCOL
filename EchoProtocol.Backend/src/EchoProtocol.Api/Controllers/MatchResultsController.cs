using System.Security.Claims;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.MatchResults;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoProtocol.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/matches")]
public sealed class MatchResultsController : ControllerBase
{
    private readonly IMatchResultService _service;

    public MatchResultsController(IMatchResultService service)
    {
        _service = service;
    }

    [HttpPut("{matchId:guid}/result")]
    public async Task<IActionResult> Submit(
        Guid matchId,
        [FromBody] SubmitMatchResultRequest request,
        CancellationToken cancellationToken)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(claim, out var userId))
        {
            return Unauthorized(ApiResponse<object>.Fail("Invalid token", ErrorCodes.TokenInvalid));
        }

        var result = await _service.SubmitAsync(userId, matchId, request, cancellationToken);
        if (result.IsSuccess)
        {
            var status = result.Data!.IsReplay
                ? StatusCodes.Status200OK
                : StatusCodes.Status201Created;
            return StatusCode(status, ApiResponse<MatchResultResponse>.Ok(result.Data, result.Message));
        }

        return StatusCode(StatusFor(result.ErrorCode),
            ApiResponse<object>.Fail(result.Message, result.ErrorCode!));
    }

    private static int StatusFor(string? errorCode) => errorCode switch
    {
        ErrorCodes.MatchNotFound => StatusCodes.Status404NotFound,
        ErrorCodes.MatchAuthorityForbidden => StatusCodes.Status403Forbidden,
        ErrorCodes.MatchResultConflict or ErrorCodes.MatchResultInvalidState =>
            StatusCodes.Status409Conflict,
        ErrorCodes.MatchLeaseExpired => StatusCodes.Status410Gone,
        _ => StatusCodes.Status400BadRequest
    };
}
