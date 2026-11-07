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

    [HttpGet("{matchId:guid}/players/{fusionActorNumber:int}/entitlements")]
    public async Task<IActionResult> GetPlayerEntitlements(
        Guid matchId, int fusionActorNumber, CancellationToken cancellationToken) =>
        Respond(await _service.GetPlayerEntitlementsAsync(
            UserId(), matchId, fusionActorNumber, cancellationToken));

    [HttpPost("{matchId:guid}/players/{fusionActorNumber:int}/disconnect")]
    public async Task<IActionResult> DisconnectPlayer(
        Guid matchId,
        int fusionActorNumber,
        [FromBody] DisconnectMatchPlayerRequest request,
        CancellationToken cancellationToken) =>
        Respond(await _service.MarkPlayerDisconnectedAsync(
            UserId(), matchId, fusionActorNumber, request.ExpectedUserId, cancellationToken));

    [HttpPost("{matchId:guid}/authority/takeover")]
    public async Task<IActionResult> Takeover(
        Guid matchId,
        [FromBody] TakeoverMatchAuthorityRequest request,
        CancellationToken cancellationToken) =>
        Respond(await _service.TakeoverAsync(
            UserId(), matchId, request, cancellationToken));

    [HttpPost("{matchId:guid}/authority/relinquish")]
    public async Task<IActionResult> Relinquish(
        Guid matchId,
        CancellationToken cancellationToken) =>
        Respond(await _service.RelinquishAsync(
            UserId(), matchId, cancellationToken));

    [HttpPost("{matchId:guid}/lease")]
    public async Task<IActionResult> RenewLease(Guid matchId, CancellationToken cancellationToken) =>
        Respond(await _service.RenewLeaseAsync(UserId(), matchId, cancellationToken));

    [HttpPost("{matchId:guid}/start")]
    public async Task<IActionResult> Start(
        Guid matchId,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)]
        StartMatchAuthorityRequest? request,
        CancellationToken cancellationToken) =>
        Respond(await _service.StartAsync(UserId(), matchId, cancellationToken, request));

    [HttpPost("{matchId:guid}/start-v2")]
    public async Task<IActionResult> StartV2(
        Guid matchId,
        [FromBody] StartMatchV2Request request,
        CancellationToken cancellationToken) =>
        Respond(await _service.StartV2Async(UserId(), matchId, request, cancellationToken));

    [HttpGet("{matchId:guid}/balance")]
    public async Task<IActionResult> GetBalance(
        Guid matchId,
        CancellationToken cancellationToken) =>
        Respond(await _service.GetLockedBalanceAsync(UserId(), matchId, cancellationToken));

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
        ErrorCodes.BalanceDisabled => StatusCodes.Status403Forbidden,
        ErrorCodes.BalanceVersionUnavailable or ErrorCodes.BalanceNotRemoteFixed => StatusCodes.Status404NotFound,
        ErrorCodes.BalanceSnapshotInvalid => StatusCodes.Status409Conflict,
        ErrorCodes.MatchSessionConflict or ErrorCodes.MatchPlayerBindingConflict => StatusCodes.Status409Conflict,
        ErrorCodes.MatchAuthorityTakeoverNotReady => StatusCodes.Status409Conflict,
        ErrorCodes.MatchLeaseExpired or ErrorCodes.MatchAlreadyEnded => StatusCodes.Status410Gone,
        ErrorCodes.MatchCapacityReached => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest
    };
}
