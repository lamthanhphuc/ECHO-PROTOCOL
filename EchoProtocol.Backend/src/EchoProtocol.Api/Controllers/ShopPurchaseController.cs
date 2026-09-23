using System.Security.Claims;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Shop;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoProtocol.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/shop")]
public sealed class ShopPurchaseController : ControllerBase
{
    private readonly IPurchaseService _service;
    private readonly ILogger<ShopPurchaseController> _logger;

    public ShopPurchaseController(
        IPurchaseService service,
        ILogger<ShopPurchaseController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpPost("purchase")]
    public async Task<IActionResult> Purchase(
        [FromBody] PurchaseRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized(ApiResponse<object>.Fail("Invalid token", ErrorCodes.TokenInvalid));
        }

        try
        {
            var result = await _service.PurchaseAsync(userId, request, cancellationToken);
            if (result.IsSuccess)
            {
                var status = result.Data!.IsReplay
                    ? StatusCodes.Status200OK
                    : StatusCodes.Status201Created;
                return StatusCode(
                    status,
                    ApiResponse<PurchaseResponse>.Ok(result.Data, result.Message));
            }

            return StatusCode(
                StatusFor(result.ErrorCode),
                ApiResponse<object>.Fail(result.Message, result.ErrorCode!));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected error in {Action}", nameof(Purchase));
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail("Internal server error", ErrorCodes.InternalServerError));
        }
    }

    private static int StatusFor(string? errorCode) => errorCode switch
    {
        ErrorCodes.ShopItemNotFound or ErrorCodes.PurchaseWalletNotFound =>
            StatusCodes.Status404NotFound,
        ErrorCodes.ShopItemInactive or
        ErrorCodes.ShopItemAlreadyOwned or
        ErrorCodes.InsufficientWalletBalance or
        ErrorCodes.PurchaseIdempotencyConflict or
        ErrorCodes.PurchaseConflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest
    };
}
