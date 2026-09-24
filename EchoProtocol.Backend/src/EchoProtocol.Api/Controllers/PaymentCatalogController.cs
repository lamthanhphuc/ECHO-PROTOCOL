using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Payments;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoProtocol.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/payments/catalog")]
public sealed class PaymentCatalogController(
    IPaymentCatalogService service,
    ILogger<PaymentCatalogController> logger) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        try
        {
            var result = service.GetActiveWalletProducts();
            return result.IsSuccess
                ? Ok(ApiResponse<PaymentCatalogResponse>.Ok(result.Data!, result.Message))
                : StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    ApiResponse<object>.Fail(result.Message, result.ErrorCode!));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unexpected payment catalog failure");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail(
                    "Internal server error", ErrorCodes.InternalServerError));
        }
    }
}
