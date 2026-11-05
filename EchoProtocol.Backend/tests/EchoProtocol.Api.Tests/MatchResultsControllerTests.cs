using System.Security.Claims;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Controllers;
using EchoProtocol.Api.DTOs.MatchResults;
using EchoProtocol.Api.DTOs.Rewards;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class MatchResultsControllerTests
{
    [Fact, Trait("Category", "M4RewardUnit")]
    public async Task SuccessfulSubmissionProcessesRewardAndReturnsCompletedStatus()
    {
        var hostId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var matchService = new StubMatchResultService(success: true, isReplay: false);
        var rewardService = new StubRewardService(success: true);
        var controller = CreateController(hostId, matchService, rewardService);

        var action = await controller.Submit(
            matchId,
            new SubmitMatchResultRequest(),
            CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        var response = Assert.IsType<ApiResponse<MatchResultResponse>>(result.Value);
        Assert.Equal(MatchRewardStatus.Completed, response.Data!.RewardStatus);
        Assert.Equal(1, rewardService.CallCount);
        Assert.Equal(matchId, rewardService.LastMatchId);
    }

    [Fact, Trait("Category", "M4RewardUnit")]
    public async Task IdenticalReplayRetriesPendingRewardProcessing()
    {
        var hostId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var matchService = new StubMatchResultService(success: true, isReplay: true);
        var rewardService = new StubRewardService(success: true);
        var controller = CreateController(hostId, matchService, rewardService);

        var action = await controller.Submit(
            matchId,
            new SubmitMatchResultRequest(),
            CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(1, rewardService.CallCount);
    }

    [Fact, Trait("Category", "M4RewardUnit")]
    public async Task RewardFailureKeepsAcceptedResultPending()
    {
        var hostId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var matchService = new StubMatchResultService(success: true, isReplay: false);
        var rewardService = new StubRewardService(success: false);
        var controller = CreateController(hostId, matchService, rewardService);

        var action = await controller.Submit(
            matchId,
            new SubmitMatchResultRequest(),
            CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        var response = Assert.IsType<ApiResponse<MatchResultResponse>>(result.Value);
        Assert.Equal(MatchRewardStatus.Pending, response.Data!.RewardStatus);
        Assert.Contains("reward processing remains pending", response.Message);
    }


    [Fact, Trait("Category", "M4RewardUnit")]
    public async Task UnexpectedRewardExceptionKeepsAcceptedResultPending()
    {
        var hostId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var matchService = new StubMatchResultService(success: true, isReplay: false);
        var controller = CreateController(hostId, matchService, new ThrowingRewardService());

        var action = await controller.Submit(
            matchId,
            new SubmitMatchResultRequest(),
            CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        var response = Assert.IsType<ApiResponse<MatchResultResponse>>(result.Value);
        Assert.Equal(MatchRewardStatus.Pending, response.Data!.RewardStatus);
        Assert.Contains("reward processing remains pending", response.Message);
    }

    [Fact, Trait("Category", "M4RewardUnit")]
    public async Task RejectedSubmissionDoesNotAttemptRewardProcessing()
    {
        var hostId = Guid.NewGuid();
        var matchService = new StubMatchResultService(success: false, isReplay: false);
        var rewardService = new StubRewardService(success: true);
        var controller = CreateController(hostId, matchService, rewardService);

        var action = await controller.Submit(
            Guid.NewGuid(),
            new SubmitMatchResultRequest(),
            CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal(0, rewardService.CallCount);
    }

    [Fact, Trait("Category", "M4RewardUnit")]
    public async Task GetMyRewardUsesAuthenticatedUserIdentity()
    {
        var userId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var rewardService = new StubRewardService(success: true);
        var controller = CreateController(
            userId,
            new StubMatchResultService(success: true, isReplay: false),
            rewardService);

        var action = await controller.GetMyReward(matchId, CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(action);
        var response = Assert.IsType<ApiResponse<RewardMeResponse>>(result.Value);
        Assert.Equal(matchId, response.Data!.MatchId);
        Assert.Equal(userId, rewardService.LastRewardUserId);
    }

    private static MatchResultsController CreateController(
        Guid hostId,
        IMatchResultService matchService,
        IRewardService rewardService)
    {
        var controller = new MatchResultsController(
            matchService,
            rewardService,
            NullLogger<MatchResultsController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, hostId.ToString("D"))],
                    "test"))
            }
        };
        return controller;
    }

    private sealed class StubMatchResultService(bool success, bool isReplay) : IMatchResultService
    {
        public Task<ServiceResult<MatchResultResponse>> SubmitAsync(
            Guid hostUserId,
            Guid matchId,
            SubmitMatchResultRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!success)
            {
                return Task.FromResult(ServiceResult<MatchResultResponse>.Failure(
                    "conflict",
                    ErrorCodes.MatchResultConflict));
            }

            return Task.FromResult(ServiceResult<MatchResultResponse>.Success(
                new MatchResultResponse
                {
                    MatchId = matchId,
                    RewardStatus = MatchRewardStatus.Pending,
                    IsReplay = isReplay
                },
                isReplay ? "Match result already accepted" : "Match result submitted"));
        }
    }

    private sealed class ThrowingRewardService : IRewardService
    {
        public Task<ServiceResult<RewardProcessingResponse>> ProcessAsync(
            Guid matchId,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("simulated reward failure");
        }

        public Task<ServiceResult<RewardMeResponse>> GetForUserAsync(
            Guid matchId,
            Guid userId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubRewardService(bool success) : IRewardService
    {
        public int CallCount { get; private set; }
        public Guid LastMatchId { get; private set; }
        public Guid LastRewardUserId { get; private set; }

        public Task<ServiceResult<RewardProcessingResponse>> ProcessAsync(
            Guid matchId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastMatchId = matchId;

            return Task.FromResult(success
                ? ServiceResult<RewardProcessingResponse>.Success(new RewardProcessingResponse
                {
                    MatchId = matchId,
                    RewardStatus = MatchRewardStatus.Completed,
                    PolicyVersion = RewardPolicyV1.PolicyVersion,
                    ProcessedAtUtc = DateTime.UtcNow
                })
                : ServiceResult<RewardProcessingResponse>.Failure(
                    "wallet unavailable",
                    ErrorCodes.RewardWalletNotFound));
        }

        public Task<ServiceResult<RewardMeResponse>> GetForUserAsync(
            Guid matchId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            LastRewardUserId = userId;
            return Task.FromResult(ServiceResult<RewardMeResponse>.Success(new RewardMeResponse
            {
                MatchId = matchId,
                RewardStatus = MatchRewardStatus.Completed,
                CurrencyAmount = 130,
                ExperiencePointsAwarded = 225,
                CurrentLevel = 1
            }, "Match reward retrieved"));
        }
    }
}
