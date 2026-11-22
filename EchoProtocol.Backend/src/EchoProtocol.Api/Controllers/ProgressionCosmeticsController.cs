using System.Security.Claims;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Player;
using EchoProtocol.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoProtocol.Api.Controllers;

[ApiController, Authorize, Route("api/player/cosmetics")]
public sealed class ProgressionCosmeticsController(AppDbContext db, TimeProvider clock) : ControllerBase
{
    [HttpPost("reconcile")]
    public Task<IActionResult> Reconcile(CancellationToken ct) => Apply(null, ct);

    [HttpPost("equip")]
    public Task<IActionResult> Equip(EquipProgressionCosmeticRequest request, CancellationToken ct) => Apply(request, ct);

    private async Task<IActionResult> Apply(EquipProgressionCosmeticRequest? request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized(ApiResponse<object>.Fail("Invalid token", ErrorCodes.TokenInvalid));
        var result = await ProgressionCosmeticService.ReconcileAsync(db, userId, clock.GetUtcNow().UtcDateTime, ct, request);
        return result.IsSuccess ? Ok(ApiResponse<ProgressionCosmeticResponse[]>.Ok(result.Data!, result.Message))
            : BadRequest(ApiResponse<object>.Fail(result.Message, result.ErrorCode!));
    }
}
