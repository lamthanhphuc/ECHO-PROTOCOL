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
