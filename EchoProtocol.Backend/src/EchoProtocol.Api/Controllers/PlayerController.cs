using System.Security.Claims;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Player;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoProtocol.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/player")]
public sealed class PlayerController : ControllerBase
{
    private readonly IPlayerProfileService _service;
    private readonly ILogger<PlayerController> _logger;

    public PlayerController(
        IPlayerProfileService service,
        ILogger<PlayerController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<PlayerProfileResponse>>> Me(
        CancellationToken cancellationToken)
    {
        try
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                return Unauthorized(ApiResponse<object>.Fail("Invalid token", ErrorCodes.TokenInvalid));
            }

            var result = await _service.GetCurrentAsync(userId, cancellationToken);
            if (!result.IsSuccess)
            {
                var status = result.ErrorCode == ErrorCodes.PlayerProfileNotFound
                    ? StatusCodes.Status404NotFound
                    : StatusCodes.Status400BadRequest;
                return StatusCode(status, ApiResponse<object>.Fail(result.Message, result.ErrorCode!));
            }

            return Ok(ApiResponse<PlayerProfileResponse>.Ok(result.Data!, result.Message));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected error in {Action}", nameof(Me));
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail("Internal server error", ErrorCodes.InternalServerError));
        }
    }
}
