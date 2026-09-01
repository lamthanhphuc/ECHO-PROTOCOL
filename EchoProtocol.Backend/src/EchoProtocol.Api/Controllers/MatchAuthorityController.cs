using System.Security.Claims;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.MatchAuthority;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoProtocol.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/matches")]
public sealed class MatchAuthorityController : ControllerBase
{
    private readonly IMatchAuthorityService _service;

    public MatchAuthorityController(IMatchAuthorityService service)
    {
        _service = service;
    }

    [HttpPost("authority")]
    public async Task<IActionResult> Create(
        [FromBody] CreateMatchAuthorityRequest request,
        CancellationToken cancellationToken) =>
        Respond(await _service.CreateAsync(UserId(), request, cancellationToken), StatusCodes.Status201Created);

    [HttpPost("{matchId:guid}/join-proofs")]
    public async Task<IActionResult> IssueJoinProof(
        Guid matchId,
        [FromBody] IssueJoinProofRequest request,
        CancellationToken cancellationToken) =>
        Respond(await _service.IssueJoinProofAsync(UserId(), matchId, request, cancellationToken));

    [HttpPost("{matchId:guid}/players/bind")]
    public async Task<IActionResult> BindPlayer(
        Guid matchId,
        [FromBody] BindMatchPlayerRequest request,
        CancellationToken cancellationToken) =>
        Respond(await _service.BindPlayerAsync(UserId(), matchId, request, cancellationToken));

    [HttpPost("{matchId:guid}/players/{fusionActorNumber:int}/disconnect")]
    public async Task<IActionResult> DisconnectPlayer(
        Guid matchId,
        int fusionActorNumber,
        CancellationToken cancellationToken) =>
        Respond(await _service.MarkPlayerDisconnectedAsync(
            UserId(), matchId, fusionActorNumber, cancellationToken));

    [HttpPost("{matchId:guid}/lease")]
    public async Task<IActionResult> RenewLease(Guid matchId, CancellationToken cancellationToken) =>
        Respond(await _service.RenewLeaseAsync(UserId(), matchId, cancellationToken));

    [HttpPost("{matchId:guid}/start")]
    public async Task<IActionResult> Start(Guid matchId, CancellationToken cancellationToken) =>
        Respond(await _service.StartAsync(UserId(), matchId, cancellationToken));

    [HttpPost("{matchId:guid}/end")]
    public async Task<IActionResult> End(
        Guid matchId,
        [FromBody] EndMatchAuthorityRequest request,
        CancellationToken cancellationToken) =>
        Respond(await _service.EndAsync(UserId(), matchId, request, cancellationToken));

    private Guid UserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var userId) ? userId : Guid.Empty;
    }

    private ObjectResult Respond<T>(ServiceResult<T> result, int successStatus = StatusCodes.Status200OK)
    {
        if (result.IsSuccess)
        {
            return StatusCode(successStatus, ApiResponse<T>.Ok(result.Data!, result.Message));
        }

        return StatusCode(StatusFor(result.ErrorCode),
            ApiResponse<object>.Fail(result.Message, result.ErrorCode!));
    }

    private static int StatusFor(string? errorCode) => errorCode switch
    {
        ErrorCodes.MatchNotFound or ErrorCodes.NotFound => StatusCodes.Status404NotFound,
        ErrorCodes.MatchAuthorityForbidden => StatusCodes.Status403Forbidden,
        ErrorCodes.MatchSessionConflict or ErrorCodes.MatchPlayerBindingConflict => StatusCodes.Status409Conflict,
        ErrorCodes.MatchLeaseExpired or ErrorCodes.MatchAlreadyEnded => StatusCodes.Status410Gone,
        ErrorCodes.MatchCapacityReached => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest
    };
}
