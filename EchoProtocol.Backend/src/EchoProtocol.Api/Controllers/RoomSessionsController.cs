using System.Security.Claims;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.MatchAuthority;
using EchoProtocol.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoProtocol.Api.Controllers;

[ApiController, Authorize, Route("api/rooms")]
public sealed class RoomSessionsController(RoomSessionService rooms) : ControllerBase
{
    private Guid UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    [HttpPost]
    public async Task<IActionResult> Create(CreateRoomRequest request, CancellationToken ct) =>
        Respond(await rooms.CreateAsync(UserId, request, ct));

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] ActivateRoomRequest? request, CancellationToken ct) =>
        Respond(await rooms.ActivateAsync(UserId, id, request, ct));

    [HttpPost("{id:guid}/lease")]
    public async Task<IActionResult> RenewLease(Guid id, CancellationToken ct) =>
        Respond(await rooms.RenewLeaseAsync(UserId, id, ct));

    [HttpPost("{id:guid}/release")]
    public async Task<IActionResult> Release(Guid id, CancellationToken ct) =>
        Respond(await rooms.ReleaseAsync(UserId, id, ct));

    [HttpPost("{id:guid}/sync-host")]
    public async Task<IActionResult> SyncHost(Guid id, CancellationToken ct) =>
        Respond(await rooms.SyncHostAfterTakeoverAsync(UserId, id, ct));

    [HttpPost("{id:guid}/matches")]
    public async Task<IActionResult> PrepareMatch(Guid id, PrepareRoomMatchRequest request, CancellationToken ct) =>
        Respond(await rooms.PrepareMatchAsync(UserId, id, request, ct));

    [HttpPost("{id:guid}/matches/{matchId:guid}/publication-ack")]
    public async Task<IActionResult> ConfirmPublication(Guid id, Guid matchId, CancellationToken ct) =>
        Respond(await rooms.ConfirmPublicationAsync(UserId, id, matchId, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Respond(await rooms.GetAsync(UserId, id, ct));

    [HttpGet("{id:guid}/close-status/me")]
    public async Task<IActionResult> GetMyCloseStatus(Guid id, CancellationToken ct) =>
        Respond(await rooms.GetCloseStatusAsync(UserId, id, ct));

    [HttpPost("{id:guid}/close-host-ack")]
    public async Task<IActionResult> AckClose(Guid id, CancellationToken ct) =>
        Respond(await rooms.AckCloseAsync(UserId, id, ct));

    private ObjectResult Respond<T>(ServiceResult<T> result) =>
        StatusCode(result.IsSuccess ? 200 : result.ErrorCode == ErrorCodes.NotFound ? 404 : 409,
            result.IsSuccess ? ApiResponse<T>.Ok(result.Data!, result.Message) : ApiResponse<T>.Fail(result.Message, result.ErrorCode ?? ErrorCodes.Conflict));
}
