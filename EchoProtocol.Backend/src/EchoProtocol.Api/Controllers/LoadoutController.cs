using System.Security.Claims;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Inventory;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoProtocol.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/inventory")]
public sealed class LoadoutController : ControllerBase
{
    private readonly ILoadoutService _service;
    private readonly ILogger<LoadoutController> _logger;

    public LoadoutController(
        ILoadoutService service,
        ILogger<LoadoutController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("loadout")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        await Execute(
            userId => _service.GetCurrentAsync(userId, cancellationToken),
            cancellationToken);

    [HttpPost("equip")]
    public async Task<IActionResult> Equip(
        [FromBody] EquipLoadoutRequest request,
        CancellationToken cancellationToken) =>
        await Execute(
            userId => _service.EquipAsync(userId, request, cancellationToken),
            cancellationToken);

    [HttpPost("unequip")]
    public async Task<IActionResult> Unequip(
        [FromBody] UnequipLoadoutRequest request,
        CancellationToken cancellationToken) =>
        await Execute(
            userId => _service.UnequipAsync(userId, request, cancellationToken),
            cancellationToken);

    private async Task<IActionResult> Execute<T>(
        Func<Guid, Task<ServiceResult<T>>> action,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized(ApiResponse<object>.Fail("Invalid token", ErrorCodes.TokenInvalid));

        try
        {
            var result = await action(userId);
            return result.IsSuccess
                ? Ok(ApiResponse<T>.Ok(result.Data!, result.Message))
                : StatusCode(
                    StatusFor(result.ErrorCode),
                    ApiResponse<object>.Fail(result.Message, result.ErrorCode!));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected loadout API error");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail(
                    "Internal server error",
                    ErrorCodes.InternalServerError));
        }
    }

    private static int StatusFor(string? errorCode) => errorCode switch
    {
        ErrorCodes.TokenInvalid => StatusCodes.Status401Unauthorized,
        ErrorCodes.LoadoutItemNotOwned => StatusCodes.Status403Forbidden,
        ErrorCodes.NotFound => StatusCodes.Status404NotFound,
        ErrorCodes.LoadoutDuplicateTeamTool or ErrorCodes.LoadoutConflict =>
            StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest
    };
}
