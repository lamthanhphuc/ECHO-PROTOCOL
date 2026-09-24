using System.Text.Json;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.DTOs.Payments;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoProtocol.Api.Controllers;

[ApiController]
[Route("api/payments/webhooks")]
public sealed class PaymentWebhooksController(
    IPaymentProviderRegistry providerRegistry,
    IPaymentWebhookService webhookService,
    ILogger<PaymentWebhooksController> logger) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("payos")]
    public async Task<IActionResult> PayOS(
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken)
    {
        var provider = providerRegistry.Resolve(PayOSSettings.ProviderKey);
        if (provider is null || !provider.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object>.Fail("payOS provider is not configured",
                    ErrorCodes.PaymentProviderConfigurationInvalid));

        var verification = provider.VerifyAndParseWebhook(payload);
        if (!verification.IsValid)
        {
            logger.LogWarning("Rejected unverified payOS webhook with code {ErrorCode}",
                verification.ErrorCode);
            return StatusCode(
                verification.ErrorCode == ErrorCodes.PaymentWebhookSignatureInvalid
                    ? StatusCodes.Status401Unauthorized
                    : StatusCodes.Status400BadRequest,
                ApiResponse<object>.Fail(verification.Message, verification.ErrorCode!));
        }

        var result = await webhookService.ProcessVerifiedAsync(
            verification.Event!, cancellationToken);
        if (result.IsSuccess)
            return Ok(ApiResponse<PaymentWebhookResponse>.Ok(result.Data!, result.Message));

        return StatusCode(result.ErrorCode switch
        {
            ErrorCodes.PaymentWebhookConflict => StatusCodes.Status409Conflict,
            ErrorCodes.PaymentWebhookUnknownOrder => StatusCodes.Status404NotFound,
            ErrorCodes.PaymentCheckoutRecoveryPending => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status400BadRequest
        }, ApiResponse<object>.Fail(result.Message, result.ErrorCode!));
    }
}
