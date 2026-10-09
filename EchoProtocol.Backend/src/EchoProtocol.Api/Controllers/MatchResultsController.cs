using System.Security.Claims;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.MatchResults;
using EchoProtocol.Api.DTOs.Rewards;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoProtocol.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/matches")]
public sealed class MatchResultsController : ControllerBase
{
    private readonly IMatchResultService _service;
    private readonly IRewardService _rewardService;
    private readonly IMatchProfilePostProcessingService _profilePostProcessing;
    private readonly ILogger<MatchResultsController> _logger;

    public MatchResultsController(
        IMatchResultService service,
        IRewardService rewardService,
        IMatchProfilePostProcessingService profilePostProcessing,
        ILogger<MatchResultsController> logger)
    {
        _service = service;
        _rewardService = rewardService;
        _profilePostProcessing = profilePostProcessing;
        _logger = logger;
    }

    [HttpPut("{matchId:guid}/result")]
    public async Task<IActionResult> Submit(
        Guid matchId,
        [FromBody] SubmitMatchResultRequest request,
        CancellationToken cancellationToken)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(claim, out var userId))
        {
            return Unauthorized(ApiResponse<object>.Fail("Invalid token", ErrorCodes.TokenInvalid));
        }

        var result = await _service.SubmitAsync(userId, matchId, request, cancellationToken);
        if (result.IsSuccess)
        {
            var response = result.Data!;
            var message = result.Message;

            try { await _profilePostProcessing.EnqueueAsync(matchId, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not enqueue profile processing for accepted match {MatchId}", matchId);
            }

            try
            {
                var rewardResult = await _rewardService.ProcessAsync(matchId, cancellationToken);
                if (rewardResult.IsSuccess)
                {
                    response.RewardStatus = rewardResult.Data!.RewardStatus;
                }
                else
                {
                    message += "; reward processing remains pending";
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Reward processing failed after match result {MatchId} was accepted",
                    matchId);
                message += "; reward processing remains pending";
            }

            var status = response.IsReplay
                ? StatusCodes.Status200OK
                : StatusCodes.Status201Created;
            return StatusCode(status, ApiResponse<MatchResultResponse>.Ok(response, message));
        }

        return StatusCode(StatusFor(result.ErrorCode),
            ApiResponse<object>.Fail(result.Message, result.ErrorCode!));
    }

    [HttpGet("{matchId:guid}/reward/me")]
    public async Task<IActionResult> GetMyReward(
        Guid matchId,
        CancellationToken cancellationToken)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(claim, out var userId))
        {
            return Unauthorized(ApiResponse<object>.Fail("Invalid token", ErrorCodes.TokenInvalid));
        }

        var result = await _rewardService.GetForUserAsync(matchId, userId, cancellationToken);
        if (result.IsSuccess)
        {
            return Ok(ApiResponse<RewardMeResponse>.Ok(result.Data!, result.Message));
        }

        return StatusCode(StatusFor(result.ErrorCode),
            ApiResponse<object>.Fail(result.Message, result.ErrorCode!));
    }

    private static int StatusFor(string? errorCode) => errorCode switch
    {
        ErrorCodes.MatchNotFound => StatusCodes.Status404NotFound,
        ErrorCodes.MatchAuthorityForbidden => StatusCodes.Status403Forbidden,
        ErrorCodes.MatchResultConflict or ErrorCodes.MatchResultInvalidState =>
            StatusCodes.Status409Conflict,
        ErrorCodes.MatchLeaseExpired => StatusCodes.Status410Gone,
        ErrorCodes.RewardResultNotFound or ErrorCodes.RewardGrantNotFound or
            ErrorCodes.ProgressionProfileNotFound => StatusCodes.Status404NotFound,
        ErrorCodes.RewardPending or ErrorCodes.RewardConflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest
    };
}
