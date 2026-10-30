using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Admin;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EchoProtocol.Api.Services;
using EchoProtocol.Api.DTOs.MatchResults;
using System.Security.Claims;
using EchoProtocol.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.ADMIN))]
[Route("api/admin")]
public sealed class AdminController(
    IAdminQueryService service,
    ILogger<AdminController> logger) : ControllerBase
{
    [HttpGet("matches/{matchId:guid}/recovery")]
    public async Task<IActionResult> InspectRecovery(Guid matchId, [FromServices] AppDbContext db, CancellationToken ct)
    {
        var match = await db.MatchAuthorityBindings.AsNoTracking().Include(item => item.Players)
            .Include(item => item.Result).SingleOrDefaultAsync(item => item.MatchId == matchId, ct);
        if (match == null) return NotFound(ApiResponse<object>.Fail("Match not found", ErrorCodes.MatchNotFound));
        return Ok(ApiResponse<object>.Ok(new
        {
            match.MatchId, match.HostUserId, match.Status, match.StartedAtUtc, match.LeaseExpiresAtUtc,
            players = match.Players.Where(player => player.Participated).Select(player => new
                { player.UserId, disconnected = player.DisconnectedAtUtc.HasValue }),
            result = match.Result == null ? null : new
                { match.Result.Outcome, match.Result.RewardStatus, match.Result.RecoveryAudit, match.Result.EndedAtUtc }
        }, "Recovery review context"));
    }
    [HttpPost("matches/{matchId:guid}/recover-result")]
    public async Task<IActionResult> RecoverResult(Guid matchId, [FromBody] RecoverMatchResultRequest request,
        [FromServices] MatchResultService results, [FromServices] IRewardService rewards,
        CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId)) return Unauthorized();
        return await Execute(async () =>
        {
            var result = await results.RecoverAsync(adminId, matchId, request.Result, request.EndedAtUtc, request.Reason, ct);
            if (result.IsSuccess)
            {
                var reward = await rewards.ProcessAsync(matchId, ct);
                if (reward.IsSuccess) result.Data!.RewardStatus = reward.Data!.RewardStatus;
                else
                {
                    logger.LogWarning("Recovered match {MatchId} saved, but reward processing failed: {ErrorCode}", matchId, reward.ErrorCode);
                    return ServiceResult<EchoProtocol.Api.DTOs.MatchResults.MatchResultResponse>.Success(
                        result.Data!, "Result saved; rewards are pending. Retry this recovery request to process rewards again.");
                }
            }
            return result;
        }, ct);
    }
    [HttpGet("users")]
    public Task<IActionResult> Users(
        [FromQuery] AdminUsersQuery query,
        CancellationToken cancellationToken) => Execute(
            () => service.GetUsersAsync(query, cancellationToken), cancellationToken);

    [HttpGet("users/{userId:guid}")]
    public Task<IActionResult> UserDetail(
        Guid userId,
        CancellationToken cancellationToken) => Execute(
            () => service.GetUserAsync(userId, cancellationToken), cancellationToken);    [HttpPost("users/{userId:guid}/wallet/credit")]
    public Task<IActionResult> GrantCredits(
        Guid userId,
        [FromBody] AdminGrantCreditsRequest request,
        CancellationToken cancellationToken) => Execute(
            () => service.GrantCreditsAsync(
                userId,
                request,
                cancellationToken),
            cancellationToken);


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

    [HttpPost("payments/{paymentOrderId:guid}/retry-fulfillment")]
    public Task<IActionResult> RetryFulfillment(Guid paymentOrderId,
        [FromServices] PaymentRecoveryService recovery, CancellationToken cancellationToken) => Execute(
            () => recovery.RecoverAsync(paymentOrderId, cancellationToken), cancellationToken);

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

public sealed class RecoverMatchResultRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    public SubmitMatchResultRequest Result { get; set; } = new();
    public DateTime EndedAtUtc { get; set; }
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(1000, MinimumLength = 10)]
    public string Reason { get; set; } = string.Empty;
}
