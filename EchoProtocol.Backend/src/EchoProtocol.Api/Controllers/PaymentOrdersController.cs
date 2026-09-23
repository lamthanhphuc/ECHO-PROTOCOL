using System.Security.Claims;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Payments;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoProtocol.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/payments/orders")]
public sealed class PaymentOrdersController(
    IPaymentOrderService service,
    IPaymentCheckoutService checkoutService,
    ILogger<PaymentOrdersController> logger) : ControllerBase
{
    [HttpGet("{paymentOrderId:guid}")]
    public async Task<IActionResult> GetOwned(
        Guid paymentOrderId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized(ApiResponse<object>.Fail("Invalid token", ErrorCodes.TokenInvalid));

        var result = await service.GetOwnedAsync(userId, paymentOrderId, cancellationToken);
        return result.IsSuccess
            ? Ok(ApiResponse<PaymentOrderResponse>.Ok(result.Data!, result.Message))
            : StatusCode(
                StatusFor(result.ErrorCode),
                ApiResponse<object>.Fail(result.Message, result.ErrorCode!));
    }

    // PROPOSED M4-054 route. Freeze with Networking before production integration.
    [HttpPost]
    public async Task<IActionResult> Create(
        CreatePaymentOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized(ApiResponse<object>.Fail("Invalid token", ErrorCodes.TokenInvalid));

        try
        {
            var result = await service.CreateAsync(userId, request, cancellationToken);
            if (result.IsSuccess)
            {
                return StatusCode(
                    result.Data!.IsReplay ? StatusCodes.Status200OK : StatusCodes.Status201Created,
                    ApiResponse<PaymentOrderResponse>.Ok(result.Data, result.Message));
            }

            return StatusCode(StatusFor(result.ErrorCode),
                ApiResponse<object>.Fail(result.Message, result.ErrorCode!));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unexpected payment order creation failure");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail("Internal server error", ErrorCodes.InternalServerError));
        }
    }

    // PROPOSED M4-055 route. Owner-only; all payment facts remain server-authoritative.
    [HttpPost("{paymentOrderId:guid}/checkout")]
    public async Task<IActionResult> Checkout(
        Guid paymentOrderId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized(ApiResponse<object>.Fail("Invalid token", ErrorCodes.TokenInvalid));

        var result = await checkoutService.CreateCheckoutAsync(
            userId, paymentOrderId, cancellationToken);
        if (result.IsSuccess)
            return Ok(ApiResponse<PaymentCheckoutResponse>.Ok(result.Data!, result.Message));
        return StatusCode(StatusFor(result.ErrorCode),
            ApiResponse<object>.Fail(result.Message, result.ErrorCode!));
    }

    private static int StatusFor(string? errorCode) => errorCode switch
    {
        ErrorCodes.PaymentProductInvalid => StatusCodes.Status404NotFound,
        ErrorCodes.PaymentIdempotencyConflict or ErrorCodes.PaymentOrderConflict => StatusCodes.Status409Conflict,
        ErrorCodes.PaymentOrderForbidden => StatusCodes.Status403Forbidden,
        ErrorCodes.PaymentOrderNotFound => StatusCodes.Status404NotFound,
        ErrorCodes.PaymentProviderUnavailable or ErrorCodes.PaymentCheckoutRecoveryPending =>
            StatusCodes.Status503ServiceUnavailable,
        ErrorCodes.PaymentProviderEvidenceMismatch or ErrorCodes.PaymentInvalidStateTransition =>
            StatusCodes.Status409Conflict,
        ErrorCodes.PaymentConfigurationMissing or ErrorCodes.PaymentConfigurationInvalid or
        ErrorCodes.PaymentProviderConfigurationInvalid =>
            StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status400BadRequest
    };
}
