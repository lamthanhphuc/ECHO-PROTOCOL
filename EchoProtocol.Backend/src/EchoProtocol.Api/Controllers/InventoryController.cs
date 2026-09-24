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
public sealed class InventoryController : ControllerBase
{
    private readonly IInventoryService _service;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(
        IInventoryService service,
        ILogger<InventoryController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<InventoryResponse>>> Me(
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized(ApiResponse<object>.Fail("Invalid token", ErrorCodes.TokenInvalid));
        }

        try
        {
            var result = await _service.GetCurrentAsync(userId, cancellationToken);
            return Ok(ApiResponse<InventoryResponse>.Ok(result.Data!, result.Message));
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
