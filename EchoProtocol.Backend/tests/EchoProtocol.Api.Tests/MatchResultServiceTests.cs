using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.MatchResults;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class MatchResultServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact, Trait("Category", "M4Unit")]
    public async Task ValidHostSubmitsResult()
    {
        await using var harness = await UnitHarness.CreateAsync();

        var result = await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, harness.ValidRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Data!.IsReplay);
        Assert.Equal(MatchRewardStatus.Pending, result.Data.RewardStatus);
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task NonHostIsRejected()
    {
        await using var harness = await UnitHarness.CreateAsync();

        var result = await harness.Service.SubmitAsync(
            Guid.NewGuid(), harness.MatchId, harness.ValidRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.MatchAuthorityForbidden, result.ErrorCode);
        Assert.Empty(harness.Db.MatchResults);
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task MatchNotFoundIsRejected()
    {
        await using var harness = await UnitHarness.CreateAsync();

        var result = await harness.Service.SubmitAsync(
            harness.HostId, Guid.NewGuid(), harness.ValidRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.MatchNotFound, result.ErrorCode);
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task InvalidMatchStateIsRejected()
    {
        await using var harness = await UnitHarness.CreateAsync(MatchAuthorityStatus.Lobby);

        var result = await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, harness.ValidRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.MatchResultInvalidState, result.ErrorCode);
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task InvalidPlayerRosterIsRejected()
    {
        await using var harness = await UnitHarness.CreateAsync();
        var request = harness.ValidRequest();
        request.Players.RemoveAt(1);

        var result = await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.MatchResultInvalidRoster, result.ErrorCode);
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task DuplicatePlayerIsRejected()
    {
        await using var harness = await UnitHarness.CreateAsync();
        var request = harness.ValidRequest();
        request.Players[1].UserId = request.Players[0].UserId;

        var result = await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.MatchResultDuplicatePlayer, result.ErrorCode);
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task FirstValidSubmissionPersistsOnce()
    {
        await using var harness = await UnitHarness.CreateAsync();

        var result = await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, harness.ValidRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, await harness.Db.MatchResults.CountAsync());
        Assert.Equal(2, await harness.Db.MatchResultPlayers.CountAsync());
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task IdenticalRetryReturnsStoredResult()
    {
        await using var harness = await UnitHarness.CreateAsync();
        var request = harness.ValidRequest();
        var first = await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, request, CancellationToken.None);

        var retry = await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, harness.ValidRequest(), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.True(retry.Data!.IsReplay);
        Assert.Equal(first.Data!.SubmittedAtUtc, retry.Data.SubmittedAtUtc);
        Assert.Equal(1, await harness.Db.MatchResults.CountAsync());
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task ConflictingRetryIsRejected()
    {
        await using var harness = await UnitHarness.CreateAsync();
        await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, harness.ValidRequest(), CancellationToken.None);
        var changed = harness.ValidRequest();
        changed.ObjectiveCompletion = 0.5m;

        var conflict = await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, changed, CancellationToken.None);

        Assert.False(conflict.IsSuccess);
        Assert.Equal(ErrorCodes.MatchResultConflict, conflict.ErrorCode);
        Assert.Equal(1, await harness.Db.MatchResults.CountAsync());
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task ResultPersistenceUsesServerTimestampsAndPendingReward()
    {
        await using var harness = await UnitHarness.CreateAsync();

        await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, harness.ValidRequest(), CancellationToken.None);

        harness.Db.ChangeTracker.Clear();
        var stored = await harness.Db.MatchResults.Include(item => item.Players).SingleAsync();
        Assert.Equal(harness.HostId, stored.SubmittedByUserId);
        Assert.Equal(Now.AddMinutes(-2).UtcDateTime, stored.StartedAtUtc);
        Assert.Equal(Now.UtcDateTime, stored.EndedAtUtc);
        Assert.Equal(120, stored.DurationSeconds);
        Assert.Equal(MatchRewardStatus.Pending, stored.RewardStatus);
        Assert.Equal(2, stored.Players.Count);
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task SuccessfulSubmissionTransitionsMatchToEnded()
    {
        await using var harness = await UnitHarness.CreateAsync();

        await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, harness.ValidRequest(), CancellationToken.None);

        harness.Db.ChangeTracker.Clear();
        var match = await harness.Db.MatchAuthorityBindings.SingleAsync();
        Assert.Equal(MatchAuthorityStatus.Ended, match.Status);
        Assert.Equal(Now.UtcDateTime, match.EndedAtUtc);
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task FailedSubmissionDoesNotPartiallyMutateMatch()
    {
        await using var harness = await UnitHarness.CreateAsync();
        var invalid = harness.ValidRequest();
        invalid.Players.RemoveAt(1);

        await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, invalid, CancellationToken.None);

        harness.Db.ChangeTracker.Clear();
        Assert.Empty(harness.Db.MatchResults);
        var match = await harness.Db.MatchAuthorityBindings.SingleAsync();
        Assert.Equal(MatchAuthorityStatus.InMatch, match.Status);
        Assert.Null(match.EndedAtUtc);
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task DisconnectedBoundPlayerIsIncludedInRoster()
    {
        await using var harness = await UnitHarness.CreateAsync(secondPlayerDisconnected: true);

        var result = await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId,
            harness.ValidRequest(secondPlayerDisconnected: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Data!.Players,
            player => player.UserId == harness.PlayerId && player.Disconnected);
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task NegativeGameplayStatIsRejected()
    {
        await using var harness = await UnitHarness.CreateAsync();
        var invalid = harness.ValidRequest();
        invalid.Players[0].ReviveCount = -1;

        var result = await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, invalid, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.MatchResultInvalidPayload, result.ErrorCode);
        Assert.Empty(harness.Db.MatchResults);
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task ExpiredHostLeaseCannotSubmitResult()
    {
        await using var harness = await UnitHarness.CreateAsync(leaseExpired: true);

        var result = await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, harness.ValidRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.MatchLeaseExpired, result.ErrorCode);
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task EndedMatchWithoutResultCannotSubmitLateResult()
    {
        await using var harness = await UnitHarness.CreateAsync(MatchAuthorityStatus.Ended);

        var result = await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, harness.ValidRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.MatchResultInvalidState, result.ErrorCode);
        Assert.Empty(harness.Db.MatchResults);
    }

    private sealed class UnitHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private UnitHarness(
            SqliteConnection connection,
            AppDbContext db,
            Guid hostId,
            Guid playerId,
            Guid matchId)
        {
            _connection = connection;
            Db = db;
            HostId = hostId;
            PlayerId = playerId;
            MatchId = matchId;
            Service = new MatchResultService(db, new FixedTimeProvider(Now));
        }

        public AppDbContext Db { get; }
        public MatchResultService Service { get; }
        public Guid HostId { get; }
        public Guid PlayerId { get; }
        public Guid MatchId { get; }

        public static async Task<UnitHarness> CreateAsync(
            MatchAuthorityStatus status = MatchAuthorityStatus.InMatch,
            bool secondPlayerDisconnected = false,
            bool leaseExpired = false)
        {
            var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var hostId = Guid.NewGuid();
            var playerId = Guid.NewGuid();
            var matchId = Guid.NewGuid();
            db.Users.AddRange(CreateUser(hostId, "host"), CreateUser(playerId, "player"));
            db.MatchAuthorityBindings.Add(new MatchAuthorityBinding
            {
                MatchId = matchId,
                FusionSessionName = "m4-unit",
                HostUserId = hostId,
                MaxPlayers = 4,
                Status = status,
                LeaseExpiresAtUtc = leaseExpired
                    ? Now.AddSeconds(-1).UtcDateTime
                    : Now.AddMinutes(1).UtcDateTime,
                CreatedAtUtc = Now.AddMinutes(-3).UtcDateTime,
                UpdatedAtUtc = Now.AddMinutes(-2).UtcDateTime,
                StartedAtUtc = Now.AddMinutes(-2).UtcDateTime,
                EndedAtUtc = status == MatchAuthorityStatus.Ended
                    ? Now.AddSeconds(-10).UtcDateTime
                    : null,
                Players =
                [
                    CreateBinding(matchId, hostId, 1, disconnected: false),
                    CreateBinding(matchId, playerId, 2, secondPlayerDisconnected)
                ]
            });
            await db.SaveChangesAsync();
            return new UnitHarness(connection, db, hostId, playerId, matchId);
        }

        public SubmitMatchResultRequest ValidRequest(bool secondPlayerDisconnected = false) => new()
        {
            Outcome = MatchOutcome.WIN,
            ObjectiveCompletion = 1m,
            Players =
            [
                new SubmitMatchResultPlayerRequest
                {
                    UserId = HostId,
                    Survived = true,
                    Disconnected = false,
                    DetectionCount = 2,
                    DownedCount = 0,
                    ReviveCount = 1,
                    ObjectiveContribution = 1
                },
                new SubmitMatchResultPlayerRequest
                {
                    UserId = PlayerId,
                    Survived = !secondPlayerDisconnected,
                    Disconnected = secondPlayerDisconnected,
                    DetectionCount = 3,
                    DownedCount = 1,
                    ReviveCount = 0,
                    ObjectiveContribution = 1
                }
            ]
        };

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private static User CreateUser(Guid id, string name) => new()
        {
            Id = id,
            Email = $"{name}-{id:N}@echo.invalid",
            Username = $"{name}-{id:N}",
            PasswordHash = "not-used",
            Role = UserRole.PLAYER,
            Status = UserStatus.ACTIVE,
            CreatedAt = Now.UtcDateTime,
            UpdatedAt = Now.UtcDateTime
        };

        private static MatchPlayerBinding CreateBinding(
            Guid matchId,
            Guid userId,
            int actor,
            bool disconnected) => new()
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            UserId = userId,
            FusionActorNumber = actor,
            JoinProofId = Guid.NewGuid(),
            BoundAtUtc = Now.AddMinutes(-3).UtcDateTime,
            LastSeenAtUtc = Now.AddMinutes(-1).UtcDateTime,
            DisconnectedAtUtc = disconnected ? Now.AddSeconds(-30).UtcDateTime : null
        };
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
