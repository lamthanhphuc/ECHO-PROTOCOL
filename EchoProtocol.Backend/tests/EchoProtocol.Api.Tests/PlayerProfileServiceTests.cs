using System.Security.Claims;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Controllers;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Player;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class PlayerProfileServiceTests
{
    private static readonly DateTime Now =
        new DateTime(2026, 9, 20, 14, 0, 0, DateTimeKind.Utc);

    [Fact, Trait("Category", "M4ProfileUnit")]
    public async Task ProfileReadReturnsProfileAndWalletSummary()
    {
        await using var harness = await ProfileHarness.CreateAsync();

        var result = await harness.Service.GetCurrentAsync(harness.FirstUserId);

        Assert.True(result.IsSuccess);
        Assert.Equal(harness.FirstUserId, result.Data!.UserId);
        Assert.Equal("First Player", result.Data.DisplayName);
        Assert.Equal(3, result.Data.TotalMatches);
        Assert.Equal(2, result.Data.TotalWins);
        Assert.Equal(450, result.Data.ExperiencePoints);
        Assert.Equal(4, result.Data.Level);
        Assert.Equal(725, result.Data.WalletBalance);
    }

    [Fact, Trait("Category", "M4ProfileUnit")]
    public async Task ControllerWithoutUserIdClaimReturnsUnauthorized()
    {
        await using var harness = await ProfileHarness.CreateAsync();
        var controller = CreateController(harness.Service, new ClaimsPrincipal());

        var action = await controller.Me(CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(action.Result);
        var response = Assert.IsType<ApiResponse<object>>(unauthorized.Value);
        Assert.Equal(ErrorCodes.TokenInvalid, response.ErrorCode);
    }

    [Fact, Trait("Category", "M4ProfileUnit")]
    public async Task ControllerUsesJwtOwnerAndCannotReadAnotherProfile()
    {
        await using var harness = await ProfileHarness.CreateAsync();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, harness.SecondUserId.ToString())],
            "Test"));
        var controller = CreateController(harness.Service, principal);

        var action = await controller.Me(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<ApiResponse<PlayerProfileResponse>>(ok.Value);
        Assert.Equal(harness.SecondUserId, response.Data!.UserId);
        Assert.Equal("Second Player", response.Data.DisplayName);
        Assert.NotEqual(harness.FirstUserId, response.Data.UserId);
    }

    [Fact, Trait("Category", "M4ProfileUnit")]
    public async Task ProfileUpdatesPersistAndAreReturnedBySubsequentRead()
    {
        await using var harness = await ProfileHarness.CreateAsync();
        var profile = await harness.Db.PlayerProfiles.SingleAsync(
            item => item.UserId == harness.FirstUserId);
        profile.TotalMatches = 4;
        profile.TotalWins = 3;
        profile.ExperiencePoints = 600;
        profile.Level = 5;
        await harness.Db.SaveChangesAsync();
        harness.Db.ChangeTracker.Clear();

        var result = await harness.Service.GetCurrentAsync(harness.FirstUserId);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Data!.TotalMatches);
        Assert.Equal(3, result.Data.TotalWins);
        Assert.Equal(600, result.Data.ExperiencePoints);
        Assert.Equal(5, result.Data.Level);
    }

    [Fact, Trait("Category", "M4ProfileUnit")]
    public async Task ActivityShowsLatestMatchWinAndNonSurvivalForOnlyTheSignedInPlayer()
    {
        await using var harness = await ProfileHarness.CreateAsync();
        await harness.AddResultAsync(Now.AddMinutes(-20), MatchOutcome.WIN, survived: true);
        await harness.AddResultAsync(Now.AddMinutes(-5), MatchOutcome.LOSE, survived: false);

        var profile = await harness.Service.GetCurrentAsync(harness.FirstUserId);
        var other = await harness.Service.GetCurrentAsync(harness.SecondUserId);

        Assert.Equal("LOSE", profile.Data!.RecentMatch!.Outcome);
        Assert.Equal("SciFi", profile.Data.RecentMatch.MapId);
        Assert.Equal("WIN", profile.Data.LatestWin!.Outcome);
        Assert.Equal(profile.Data.RecentMatch.MatchId, profile.Data.LatestNonSurvival!.MatchId);
        Assert.Null(other.Data!.RecentMatch);
        Assert.Null(other.Data.LatestWin);
        Assert.Null(other.Data.LatestNonSurvival);
    }

    private static PlayerController CreateController(
        PlayerProfileService service,
        ClaimsPrincipal user) => new(service, NullLogger<PlayerController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };

    private sealed class ProfileHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private ProfileHarness(
            SqliteConnection connection,
            AppDbContext db,
            Guid firstUserId,
            Guid secondUserId)
        {
            _connection = connection;
            Db = db;
            Service = new PlayerProfileService(db);
            FirstUserId = firstUserId;
            SecondUserId = secondUserId;
        }

        public AppDbContext Db { get; }
        public PlayerProfileService Service { get; }
        public Guid FirstUserId { get; }
        public Guid SecondUserId { get; }

        public async Task AddResultAsync(DateTime endedAt, MatchOutcome outcome, bool survived)
        {
            var matchId = Guid.NewGuid();
            Db.MatchAuthorityBindings.Add(new MatchAuthorityBinding
            {
                MatchId = matchId,
                FusionSessionName = "profile-activity-" + matchId.ToString("N"),
                HostUserId = FirstUserId,
                MaxPlayers = 2,
                MapId = "SciFi",
                Difficulty = "NORMAL",
                Status = MatchAuthorityStatus.Ended,
                LeaseExpiresAtUtc = endedAt.AddMinutes(1),
                CreatedAtUtc = endedAt.AddMinutes(-2),
                UpdatedAtUtc = endedAt,
                StartedAtUtc = endedAt.AddMinutes(-2),
                EndedAtUtc = endedAt,
                Players = [new MatchPlayerBinding
                {
                    Id = Guid.NewGuid(), MatchId = matchId, UserId = FirstUserId,
                    FusionActorNumber = 1, JoinProofId = Guid.NewGuid(),
                    BoundAtUtc = endedAt.AddMinutes(-2), LastSeenAtUtc = endedAt
                }],
                Result = new MatchResult
                {
                    MatchId = matchId, SubmittedByUserId = FirstUserId,
                    Outcome = outcome, StartedAtUtc = endedAt.AddMinutes(-2),
                    EndedAtUtc = endedAt, DurationSeconds = 120,
                    ObjectiveCompletion = outcome == MatchOutcome.WIN ? 1m : 0m,
                    PlayerCount = 1, PayloadHash = Guid.NewGuid().ToString("N"),
                    RewardStatus = MatchRewardStatus.Completed, SubmittedAtUtc = endedAt,
                    Players = [new MatchResultPlayer
                    {
                        MatchId = matchId, UserId = FirstUserId,
                        Survived = survived, Disconnected = false
                    }]
                }
            });
            await Db.SaveChangesAsync();
            Db.ChangeTracker.Clear();
        }

        public static async Task<ProfileHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options);
            await db.Database.EnsureCreatedAsync();

            var firstId = Guid.NewGuid();
            var secondId = Guid.NewGuid();
            db.Users.AddRange(
                CreateUser(firstId, "first", "First Player", 3, 2, 450, 4, 725),
                CreateUser(secondId, "second", "Second Player", 1, 0, 50, 1, 100));
            await db.SaveChangesAsync();
            return new ProfileHarness(connection, db, firstId, secondId);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private static User CreateUser(
            Guid userId,
            string username,
            string displayName,
            int matches,
            int wins,
            long experiencePoints,
            int level,
            int balance) => new()
        {
            Id = userId,
            Email = $"{username}@echo.invalid",
            Username = username,
            PasswordHash = "not-used",
            Role = UserRole.PLAYER,
            Status = UserStatus.ACTIVE,
            CreatedAt = Now,
            UpdatedAt = Now,
            PlayerProfile = new PlayerProfile
            {
                Id = Guid.NewGuid(),
                DisplayName = displayName,
                TotalMatches = matches,
                TotalWins = wins,
                ExperiencePoints = experiencePoints,
                Level = level,
                CreatedAt = Now,
                UpdatedAt = Now
            },
            Wallet = new Wallet
            {
                Id = Guid.NewGuid(),
                Balance = balance,
                UpdatedAt = Now
            }
        };
    }
}
