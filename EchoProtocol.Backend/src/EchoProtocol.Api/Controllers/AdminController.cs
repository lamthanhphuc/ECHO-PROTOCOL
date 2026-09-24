using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Admin;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoProtocol.Api.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.ADMIN))]
[Route("api/admin")]
public sealed class AdminController(
    IAdminQueryService service,
    ILogger<AdminController> logger) : ControllerBase
{
    [HttpGet("users")]
    public Task<IActionResult> Users(
        [FromQuery] AdminUsersQuery query,
        CancellationToken cancellationToken) => Execute(
            () => service.GetUsersAsync(query, cancellationToken), cancellationToken);

    [HttpGet("users/{userId:guid}")]
    public Task<IActionResult> UserDetail(
        Guid userId,
        CancellationToken cancellationToken) => Execute(
            () => service.GetUserAsync(userId, cancellationToken), cancellationToken);

    [HttpGet("payments")]
    public Task<IActionResult> Payments(
        [FromQuery] AdminPaymentsQuery query,
        CancellationToken cancellationToken) => Execute(
            () => service.GetPaymentsAsync(query, cancellationToken), cancellationToken);

    [HttpGet("payments/{paymentOrderId:guid}")]
    public Task<IActionResult> PaymentDetail(
        Guid paymentOrderId,
        CancellationToken cancellationToken) => Execute(
            () => service.GetPaymentAsync(paymentOrderId, cancellationToken), cancellationToken);

    [HttpGet("wallet-transactions")]
    public Task<IActionResult> WalletTransactions(
        [FromQuery] AdminWalletTransactionsQuery query,
        CancellationToken cancellationToken) => Execute(
            () => service.GetWalletTransactionsAsync(query, cancellationToken), cancellationToken);

    [HttpGet("purchases")]
    public Task<IActionResult> Purchases(
        [FromQuery] AdminPurchasesQuery query,
        CancellationToken cancellationToken) => Execute(
            () => service.GetPurchasesAsync(query, cancellationToken), cancellationToken);

    private async Task<IActionResult> Execute<T>(
        Func<Task<ServiceResult<T>>> action,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await action();
            return result.IsSuccess
                ? Ok(ApiResponse<T>.Ok(result.Data!, result.Message))
                : StatusCode(
                    result.ErrorCode is ErrorCodes.NotFound or ErrorCodes.PaymentOrderNotFound
                        ? StatusCodes.Status404NotFound
                        : StatusCodes.Status400BadRequest,
                    ApiResponse<object>.Fail(result.Message, result.ErrorCode!));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unexpected admin query failure");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail(
                    "Internal server error", ErrorCodes.InternalServerError));
        }
    }
}
