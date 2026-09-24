using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Controllers;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class AIProfileReadTests
{
    private static readonly DateTime Now = new(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);

    [Fact, Trait("Category", "M4ProfileRead")]
    public async Task OwnProfile_UsesCallerIdentityAndSerializesColdStartActiveAndDeferred()
    {
        await using var fixture = await Fixture.CreateAsync();
        var response = await new AIProfileReadService(fixture.Db).GetOwnPlayerProfileAsync(fixture.HostId);
        Assert.True(response.IsSuccess);
        Assert.Equal("ACTIVE", response.Data!.Survival.Status);
        Assert.Equal("COLD_START", response.Data.Noise.Status);
        Assert.Equal(0, response.Data.Noise.SampleCount);
        Assert.Equal("DEFERRED", response.Data.Teamwork.Status);
        Assert.Null(response.Data.Teamwork.Score);
        Assert.Null(response.Data.Teamwork.SampleCount);

        var json = JsonSerializer.Serialize(response.Data, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        { Converters = { new JsonStringEnumConverter() } });
        Assert.Contains("\"status\":\"DEFERRED\"", json);
        Assert.Contains("\"score\":null", json);
    }

    [Fact, Trait("Category", "M4ProfileRead")]
    public async Task OwnProfile_DoesNotAcceptAnotherUserId()
    {
        await using var fixture = await Fixture.CreateAsync();
        var response = await new AIProfileReadService(fixture.Db).GetOwnPlayerProfileAsync(fixture.OtherId);
        Assert.Equal(fixture.OtherId, response.Data!.UserId);
        Assert.NotEqual(fixture.HostId, response.Data.UserId);
    }

    [Fact, Trait("Category", "M4ProfileRead")]
    public async Task ControllerWithoutJwtClaim_IsUnauthorized()
    {
        await using var fixture = await Fixture.CreateAsync();
        var controller = new AIProfilesController(new AIProfileReadService(fixture.Db))
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var result = await controller.Me(default);
        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact, Trait("Category", "M4ProfileRead")]
    public async Task ControllerJwtClaimSelectsOwnProfile()
    {
        await using var fixture = await Fixture.CreateAsync();
        var controller = new AIProfilesController(new AIProfileReadService(fixture.Db))
        { ControllerContext = Context(fixture.OtherId) };
        var result = await controller.Me(default);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<EchoProtocol.Api.DTOs.Profiles.PlayerAIProfileResponse>>(ok.Value);
        Assert.Equal(fixture.OtherId, body.Data!.UserId);
    }

    [Fact, Trait("Category", "M4ProfileRead")]
    public async Task OnlyVerifiedHostCanReadBoundRosterProfiles_IncludingDisconnectedPlayer()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = new AIProfileReadService(fixture.Db);
        var host = await service.GetMatchRosterProfilesAsync(fixture.MatchId, fixture.HostId);
        var nonHost = await service.GetMatchRosterProfilesAsync(fixture.MatchId, fixture.OtherId);
        Assert.True(host.IsSuccess);
        Assert.Equal(2, host.Data!.Players.Count);
        Assert.False(nonHost.IsSuccess);
        Assert.Equal(ErrorCodes.ProfileReadForbidden, nonHost.ErrorCode);
    }

    [Fact, Trait("Category", "M4ProfileRead")]
    public async Task BoundPlayerCanReadOnlyRequestedMatchTeamProfile()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = new AIProfileReadService(fixture.Db);
        var ownMatch = await service.GetTeamProfileAsync(fixture.MatchId, fixture.OtherId);
        var guessedMatch = await service.GetTeamProfileAsync(fixture.ForeignMatchId, fixture.OtherId);
        Assert.True(ownMatch.IsSuccess);
        Assert.Equal(fixture.MatchId, ownMatch.Data!.MatchId);
        Assert.False(guessedMatch.IsSuccess);
        Assert.Equal(ErrorCodes.ProfileReadForbidden, guessedMatch.ErrorCode);
    }

    [Fact, Trait("Category", "M4ProfileRead")]
    public async Task TeamPerformanceRemainsIncompleteAndNull()
    {
        await using var fixture = await Fixture.CreateAsync();
        var response = await new AIProfileReadService(fixture.Db).GetTeamProfileAsync(fixture.MatchId, fixture.HostId);
        Assert.Equal("INCOMPLETE", response.Data!.TeamPerformanceStatus);
        Assert.Null(response.Data.TeamPerformanceScore);
        Assert.Equal("DEFERRED", response.Data.Teamwork.Status);
    }

    private static ControllerContext Context(Guid userId)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test"))
        };
        return new ControllerContext { HttpContext = http };
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        public AppDbContext Db { get; }
        public Guid HostId { get; } = Guid.NewGuid();
        public Guid OtherId { get; } = Guid.NewGuid();
        public Guid ForeignHostId { get; } = Guid.NewGuid();
        public Guid MatchId { get; } = Guid.NewGuid();
        public Guid ForeignMatchId { get; } = Guid.NewGuid();
        private Fixture(SqliteConnection connection, AppDbContext db) { this.connection = connection; Db = db; }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var result = new Fixture(connection, db);
            result.Seed();
            await db.SaveChangesAsync();
            return result;
        }

        private void Seed()
        {
            Db.Users.AddRange(User(HostId), User(OtherId), User(ForeignHostId));
            Db.PlayerAIProfiles.AddRange(Profile(HostId, ProfileDimensionStatus.ColdStart, 0), Profile(OtherId, ProfileDimensionStatus.ColdStart, 0));
            AddMatch(MatchId, HostId, OtherId);
            AddMatch(ForeignMatchId, ForeignHostId);
        }

        private void AddMatch(Guid matchId, Guid hostId, Guid? second = null)
        {
            var players = new List<MatchPlayerBinding> { Binding(matchId, hostId, 1, null) };
            if (second.HasValue) players.Add(Binding(matchId, second.Value, 2, Now.AddSeconds(-1)));
            Db.MatchAuthorityBindings.Add(new MatchAuthorityBinding
            {
                MatchId = matchId, FusionSessionName = $"test-{matchId:N}", HostUserId = hostId,
                MaxPlayers = 4, Status = MatchAuthorityStatus.Ended, LeaseExpiresAtUtc = Now,
                CreatedAtUtc = Now.AddMinutes(-2), UpdatedAtUtc = Now, StartedAtUtc = Now.AddMinutes(-2), EndedAtUtc = Now,
                Players = players
            });
            Db.MatchResults.Add(new MatchResult
            {
                MatchId = matchId, SubmittedByUserId = hostId, Outcome = MatchOutcome.WIN,
                StartedAtUtc = Now.AddMinutes(-2), EndedAtUtc = Now, DurationSeconds = 120,
                ObjectiveCompletion = 1, PlayerCount = players.Count, PayloadHash = new string('A', 64),
                RewardStatus = MatchRewardStatus.Pending, SubmittedAtUtc = Now
            });
            Db.TeamProfiles.Add(new TeamProfile
            {
                MatchId = matchId, ProcessingRevision = 1, ProcessingStatus = TeamProfileProcessingStatus.Processed,
                ProcessingReason = "TEST", TelemetryCompleteness = "COMPLETE", ObjectiveTimeSeconds = 30,
                ObjectiveTimeStatus = TeamMetricStatus.Available, SplitTimeStatus = TeamMetricStatus.Deferred,
                AvgDistanceStatus = TeamMetricStatus.Deferred, ReviveSuccessStatus = TeamMetricStatus.Deferred,
                ResourceEfficiencyStatus = TeamMetricStatus.Deferred, CommunicationStatus = TeamMetricStatus.Deferred,
                WipeRecoveryStatus = TeamMetricStatus.Deferred, ObjectiveSpeedScore = 50,
                ObjectiveSpeedStatus = TeamMetricStatus.Available, SurvivalScore = 100,
                SurvivalStatus = TeamMetricStatus.Available, TeamworkStatus = TeamMetricStatus.Deferred,
                ResourceEfficiencyScoreStatus = TeamMetricStatus.Deferred,
                TeamPerformanceStatus = TeamPerformanceStatus.Incomplete, ProfileFormulaVersion = "TEST_PROFILE_V1",
                SourceTelemetrySchemaVersion = "1.1", SourceFingerprint = new string('B', 64),
                ProjectionFingerprint = new string('C', 64), CreatedAtUtc = Now, UpdatedAtUtc = Now
            });
        }

        private static User User(Guid id) => new()
        {
            Id = id, Email = $"{id:N}@test.local", Username = $"u{id:N}", PasswordHash = "hash",
            Role = UserRole.PLAYER, Status = UserStatus.ACTIVE, CreatedAt = Now, UpdatedAt = Now
        };

        private static PlayerAIProfile Profile(Guid userId, ProfileDimensionStatus noiseStatus, int noiseCount) => new()
        {
            UserId = userId, ProfileLineageId = Guid.NewGuid(), ProfileRevision = 1,
            ProfileFormulaVersion = "TEST_PROFILE_V1", MatchScoreFormulaVersion = "TEST_SCORE_V1",
            AlphaConfigVersion = "TEST_ALPHA_V1", SurvivalScore = 75, SurvivalStatus = ProfileDimensionStatus.Active,
            SurvivalSampleCount = 2, NoiseScore = noiseStatus == ProfileDimensionStatus.ColdStart ? 50 : 75,
            NoiseStatus = noiseStatus, NoiseSampleCount = noiseCount, CreatedAtUtc = Now, UpdatedAtUtc = Now
        };

        private static MatchPlayerBinding Binding(Guid matchId, Guid userId, int actor, DateTime? disconnected) => new()
        {
            Id = Guid.NewGuid(), MatchId = matchId, UserId = userId, FusionActorNumber = actor,
            JoinProofId = Guid.NewGuid(), BoundAtUtc = Now.AddMinutes(-2), LastSeenAtUtc = Now,
            DisconnectedAtUtc = disconnected
        };

        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
