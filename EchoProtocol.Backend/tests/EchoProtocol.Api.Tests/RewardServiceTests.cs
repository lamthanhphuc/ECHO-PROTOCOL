using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class RewardServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 20, 13, 0, 0, TimeSpan.Zero);

    [Fact, Trait("Category", "M4RewardUnit")]
    public async Task ApprovedPolicyProcessesRewardSuccessfully()
    {
        await using var harness = await RewardHarness.CreateAsync();

        var result = await harness.Service.ProcessAsync(harness.MatchId);

        Assert.True(result.IsSuccess);
        Assert.False(result.Data!.IsReplay);
        Assert.Equal("TEST-POLICY-v1", result.Data.PolicyVersion);
        Assert.Equal(2, result.Data.Grants.Count);
        Assert.Equal(2, await harness.Db.MatchRewardGrants.CountAsync());
        Assert.Equal(2, await harness.Db.WalletTransactions.CountAsync());
        Assert.All(await harness.Db.PlayerProfiles.ToListAsync(), profile =>
        {
            Assert.Equal(1, profile.TotalMatches);
            Assert.Equal(1, profile.TotalWins);
            Assert.Equal(10, profile.ExperiencePoints);
            Assert.Equal(2, profile.Level);
        });
    }

    [Fact, Trait("Category", "M4RewardUnit")]
    public async Task DuplicateRewardReturnsReplayWithoutCreditingAgain()
    {
        await using var harness = await RewardHarness.CreateAsync();
        var first = await harness.Service.ProcessAsync(harness.MatchId);

        var duplicate = await harness.Service.ProcessAsync(harness.MatchId);

        Assert.True(first.IsSuccess);
        Assert.True(duplicate.IsSuccess);
        Assert.True(duplicate.Data!.IsReplay);
        Assert.Equal(2, await harness.Db.MatchRewardGrants.CountAsync());
        Assert.Equal(2, await harness.Db.WalletTransactions.CountAsync());
        Assert.All(await harness.Db.Wallets.ToListAsync(), wallet => Assert.Equal(125, wallet.Balance));
        Assert.All(await harness.Db.PlayerProfiles.ToListAsync(), profile =>
        {
            Assert.Equal(1, profile.TotalMatches);
            Assert.Equal(1, profile.TotalWins);
            Assert.Equal(10, profile.ExperiencePoints);
        });
    }

    [Fact, Trait("Category", "M4RewardUnit")]
    public async Task MissingMatchResultIsRejected()
    {
        await using var harness = await RewardHarness.CreateAsync();

        var result = await harness.Service.ProcessAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.RewardResultNotFound, result.ErrorCode);
    }

    [Fact, Trait("Category", "M4RewardUnit")]
    public async Task ResultWhoseMatchIsNotEndedIsRejected()
    {
        await using var harness = await RewardHarness.CreateAsync();
        var match = await harness.Db.MatchAuthorityBindings.SingleAsync();
        match.Status = MatchAuthorityStatus.InMatch;
        match.EndedAtUtc = null;
        await harness.Db.SaveChangesAsync();

        var result = await harness.Service.ProcessAsync(harness.MatchId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.RewardInvalidResult, result.ErrorCode);
        Assert.Empty(await harness.Db.MatchRewardGrants.ToListAsync());
        Assert.All(await harness.Db.Wallets.ToListAsync(), wallet => Assert.Equal(100, wallet.Balance));
    }

    [Fact, Trait("Category", "M4RewardUnit")]
    public async Task WalletBalancePersistsWithAuditableBeforeAndAfterValues()
    {
        await using var harness = await RewardHarness.CreateAsync();

        var result = await harness.Service.ProcessAsync(harness.MatchId);

        Assert.True(result.IsSuccess);
        harness.Db.ChangeTracker.Clear();
        Assert.All(await harness.Db.Wallets.AsNoTracking().ToListAsync(), wallet =>
            Assert.Equal(125, wallet.Balance));
        Assert.All(await harness.Db.WalletTransactions.AsNoTracking().ToListAsync(), entry =>
        {
            Assert.Equal(25, entry.Amount);
            Assert.Equal(100, entry.BalanceBefore);
            Assert.Equal(125, entry.BalanceAfter);
        });
    }

    [Fact, Trait("Category", "M4RewardUnit")]
    public async Task SuccessfulRewardTransitionsStatusToCompleted()
    {
        await using var harness = await RewardHarness.CreateAsync();

        var result = await harness.Service.ProcessAsync(harness.MatchId);

        Assert.True(result.IsSuccess);
        harness.Db.ChangeTracker.Clear();
        Assert.Equal(
            MatchRewardStatus.Completed,
            (await harness.Db.MatchResults.AsNoTracking().SingleAsync()).RewardStatus);
    }

    [Fact, Trait("Category", "M4RewardUnit")]
    public async Task MissingWalletRollsBackAllRewardMutation()
    {
        await using var harness = await RewardHarness.CreateAsync(removePlayerWallet: true);

        var result = await harness.Service.ProcessAsync(harness.MatchId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.RewardWalletNotFound, result.ErrorCode);
        harness.Db.ChangeTracker.Clear();
        Assert.Empty(await harness.Db.MatchRewardGrants.AsNoTracking().ToListAsync());
        Assert.Empty(await harness.Db.WalletTransactions.AsNoTracking().ToListAsync());
        Assert.Equal(
            MatchRewardStatus.Pending,
            (await harness.Db.MatchResults.AsNoTracking().SingleAsync()).RewardStatus);
        Assert.All(await harness.Db.Wallets.AsNoTracking().ToListAsync(), wallet =>
            Assert.Equal(100, wallet.Balance));
    }

    [Fact, Trait("Category", "M4RewardUnit")]
    public async Task InvalidPolicyAllocationDoesNotMutateWalletOrResult()
    {
        await using var harness = await RewardHarness.CreateAsync(
            policy: new InvalidTestRewardPolicy());

        var result = await harness.Service.ProcessAsync(harness.MatchId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.RewardPolicyInvalid, result.ErrorCode);
        harness.Db.ChangeTracker.Clear();
        Assert.Empty(await harness.Db.MatchRewardGrants.AsNoTracking().ToListAsync());
        Assert.Equal(
            MatchRewardStatus.Pending,
            (await harness.Db.MatchResults.AsNoTracking().SingleAsync()).RewardStatus);
    }

    [Fact, Trait("Category", "M4RewardUnit")]
    public async Task UnconfiguredProductionPolicyRefusesToIssueRewards()
    {
        await using var harness = await RewardHarness.CreateAsync(
            policy: new UnconfiguredRewardPolicy());

        var result = await harness.Service.ProcessAsync(harness.MatchId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.RewardPolicyNotConfigured, result.ErrorCode);
        Assert.Empty(await harness.Db.MatchRewardGrants.ToListAsync());
    }

    [Fact, Trait("Category", "M4ProfileUnit")]
    public async Task UnconfiguredProgressionPolicyRefusesRewardWithoutMutation()
    {
        await using var harness = await RewardHarness.CreateAsync(
            progressionPolicy: new UnconfiguredProgressionPolicy());

        var result = await harness.Service.ProcessAsync(harness.MatchId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ProgressionPolicyNotConfigured, result.ErrorCode);
        Assert.Empty(await harness.Db.MatchRewardGrants.ToListAsync());
        Assert.All(await harness.Db.Wallets.ToListAsync(), wallet => Assert.Equal(100, wallet.Balance));
        Assert.All(await harness.Db.PlayerProfiles.ToListAsync(), profile =>
            Assert.Equal(0, profile.TotalMatches));
    }

    private sealed class RewardHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private RewardHarness(
            SqliteConnection connection,
            AppDbContext db,
            RewardService service,
            Guid matchId)
        {
            _connection = connection;
            Db = db;
            Service = service;
            MatchId = matchId;
        }

        public AppDbContext Db { get; }
        public RewardService Service { get; }
        public Guid MatchId { get; }

        public static async Task<RewardHarness> CreateAsync(
            bool removePlayerWallet = false,
            IRewardPolicy? policy = null,
            IProgressionPolicy? progressionPolicy = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
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
            db.PlayerProfiles.AddRange(
                CreateProfile(hostId, "Host"),
                CreateProfile(playerId, "Player"));
            db.Wallets.Add(new Wallet
            {
                Id = Guid.NewGuid(), UserId = hostId, Balance = 100, UpdatedAt = Now.UtcDateTime
            });
            if (!removePlayerWallet)
            {
                db.Wallets.Add(new Wallet
                {
                    Id = Guid.NewGuid(), UserId = playerId, Balance = 100, UpdatedAt = Now.UtcDateTime
                });
            }

            db.MatchAuthorityBindings.Add(new MatchAuthorityBinding
            {
                MatchId = matchId,
                FusionSessionName = $"reward-{matchId:N}",
                HostUserId = hostId,
                MaxPlayers = 4,
                Status = MatchAuthorityStatus.Ended,
                LeaseExpiresAtUtc = Now.UtcDateTime,
                CreatedAtUtc = Now.AddMinutes(-3).UtcDateTime,
                UpdatedAtUtc = Now.UtcDateTime,
                StartedAtUtc = Now.AddMinutes(-2).UtcDateTime,
                EndedAtUtc = Now.UtcDateTime,
                Players =
                [
                    CreateBinding(matchId, hostId, 1),
                    CreateBinding(matchId, playerId, 2)
                ],
                Result = CreateResult(matchId, hostId, playerId)
            });
            await db.SaveChangesAsync();

            var rewardPolicy = policy ?? new FixedTestRewardPolicy();
            var progression = new ProgressionService(
                progressionPolicy ?? new FixedTestProgressionPolicy());
            return new RewardHarness(
                connection,
                db,
                new RewardService(db, rewardPolicy, progression, new FixedTimeProvider(Now)),
                matchId);
        }

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

        private static PlayerProfile CreateProfile(Guid userId, string displayName) => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DisplayName = displayName,
            Level = 1,
            CreatedAt = Now.UtcDateTime,
            UpdatedAt = Now.UtcDateTime
        };

        private static MatchPlayerBinding CreateBinding(Guid matchId, Guid userId, int actor) => new()
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            UserId = userId,
            FusionActorNumber = actor,
            JoinProofId = Guid.NewGuid(),
            BoundAtUtc = Now.AddMinutes(-3).UtcDateTime,
            LastSeenAtUtc = Now.UtcDateTime
        };

        private static MatchResult CreateResult(Guid matchId, Guid hostId, Guid playerId) => new()
        {
            MatchId = matchId,
            SubmittedByUserId = hostId,
            Outcome = MatchOutcome.WIN,
            StartedAtUtc = Now.AddMinutes(-2).UtcDateTime,
            EndedAtUtc = Now.UtcDateTime,
            DurationSeconds = 120,
            ObjectiveCompletion = 1m,
            PlayerCount = 2,
            PayloadHash = new string('B', 64),
            RewardStatus = MatchRewardStatus.Pending,
            SubmittedAtUtc = Now.UtcDateTime,
            Players =
            [
                new MatchResultPlayer { MatchId = matchId, UserId = hostId, Survived = true },
                new MatchResultPlayer { MatchId = matchId, UserId = playerId, Survived = true }
            ]
        };
    }

    private sealed class FixedTestRewardPolicy : IRewardPolicy
    {
        public string Version => "TEST-POLICY-v1";
        public bool IsConfigured => true;

        public IReadOnlyList<RewardAllocation> Calculate(RewardMatchSnapshot match) =>
            match.Players.Select(item => new RewardAllocation(item.UserId, 25)).ToArray();
    }

    private sealed class InvalidTestRewardPolicy : IRewardPolicy
    {
        public string Version => "TEST-INVALID-v1";
        public bool IsConfigured => true;

        public IReadOnlyList<RewardAllocation> Calculate(RewardMatchSnapshot match) =>
            [new(Guid.NewGuid(), -1)];
    }

    private sealed class FixedTestProgressionPolicy : IProgressionPolicy
    {
        public string Version => "TEST-PROGRESSION-v1";
        public bool IsConfigured => true;

        public IReadOnlyList<ProgressionAllocation> Calculate(RewardMatchSnapshot match) =>
            match.Players.Select(item => new ProgressionAllocation(item.UserId, 10, true)).ToArray();

        public int GetLevel(long totalExperiencePoints) => totalExperiencePoints >= 10 ? 2 : 1;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
