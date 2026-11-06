using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Leaderboards;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class LeaderboardServiceTests
{
    [Fact]
    public async Task SoloRanksEachPlayersFastestSuccessfulRun()
    {
        await using var harness = await Harness.CreateAsync();
        var alice = await harness.AddUserAsync("Alice");
        var bob = await harness.AddUserAsync("Bob");
        await harness.AddResultAsync(150, MatchOutcome.WIN, "SciFi", "NORMAL", 1m, alice);
        await harness.AddResultAsync(90, MatchOutcome.WIN, "SciFi", "NORMAL", 1m, alice);
        await harness.AddResultAsync(110, MatchOutcome.WIN, "SciFi", "NORMAL", 1m, bob);
        await harness.AddResultAsync(60, MatchOutcome.LOSE, "SciFi", "NORMAL", 1m, bob);
        await harness.AddResultAsync(65, MatchOutcome.WIN, "SciFi", "NORMAL", 0.5m, bob);

        var page = await harness.Service.GetAsync(
            LeaderboardMode.SOLO, "SciFi", "NORMAL", 1, 10, CancellationToken.None);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(new[] { 9000, 11000 }, page.Items.Select(item => item.DurationCentiseconds));
        Assert.Equal(new[] { "Alice", "Bob" }, page.Items.Select(item => item.DisplayName));
        Assert.Equal(new[] { 1, 2 }, page.Items.Select(item => item.Rank));
        Assert.All(page.Items, item => Assert.Single(item.Players));
    }

    [Fact]
    public async Task TeamRanksBestExactRosterAndKeepsFiltersAndPagesSeparate()
    {
        await using var harness = await Harness.CreateAsync();
        var alice = await harness.AddUserAsync("Alice");
        var bob = await harness.AddUserAsync("Bob");
        var cy = await harness.AddUserAsync("Cy");
        await harness.AddResultAsync(180, MatchOutcome.WIN, "SciFi", "NORMAL", 1m, alice, bob);
        await harness.AddResultAsync(120, MatchOutcome.WIN, "SciFi", "NORMAL", 1m, bob, alice);
        await harness.AddResultAsync(130, MatchOutcome.WIN, "SciFi", "NORMAL", 1m, alice, cy);
        await harness.AddResultAsync(70, MatchOutcome.WIN, "Scene_A", "NORMAL", 1m, alice, bob);
        await harness.AddResultAsync(80, MatchOutcome.WIN, "SciFi", "HARD", 1m, alice, bob);

        var first = await harness.Service.GetAsync(
            LeaderboardMode.TEAM, "SciFi", "NORMAL", 1, 1, CancellationToken.None);
        var second = await harness.Service.GetAsync(
            LeaderboardMode.TEAM, "SciFi", "NORMAL", 2, 1, CancellationToken.None);
        var otherMap = await harness.Service.GetAsync(
            LeaderboardMode.TEAM, "Scene_A", "NORMAL", 1, 10, CancellationToken.None);

        Assert.Equal(2, first.TotalCount);
        Assert.Equal(12000, Assert.Single(first.Items).DurationCentiseconds);
        Assert.Equal(1, first.Items[0].Rank);
        Assert.Equal(new[] { alice, bob }, first.Items[0].Players.Select(player => player.UserId));
        Assert.StartsWith("SQUAD ", first.Items[0].SquadLabel);
        Assert.Equal(13000, Assert.Single(second.Items).DurationCentiseconds);
        Assert.Equal(2, second.Items[0].Rank);
        Assert.Equal(7000, Assert.Single(otherMap.Items).DurationCentiseconds);
    }

    [Fact]
    public async Task LegacyResultsWithoutStartContextAreNotRanked()
    {
        await using var harness = await Harness.CreateAsync();
        var alice = await harness.AddUserAsync("Alice");
        await harness.AddResultAsync(90, MatchOutcome.WIN, null, null, 1m, alice);

        var page = await harness.Service.GetAsync(
            LeaderboardMode.SOLO, "SciFi", "NORMAL", 1, 10, CancellationToken.None);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Contains("SciFi", page.Maps);
        Assert.Contains("NORMAL", page.Difficulties);
    }

    [Fact]
    public async Task TestOnlyFastWinIsExcludedFromSoloLeaderboard()
    {
        await using var harness = await Harness.CreateAsync();
        var alice = await harness.AddUserAsync("Alice");
        await harness.AddResultAsync(90, MatchOutcome.WIN, "SciFi", "NORMAL", 1m, alice);
        await harness.AddTestOnlyResultAsync(60, alice);

        var page = await harness.Service.GetAsync(LeaderboardMode.SOLO, "SciFi", "NORMAL", 1, 10, default);

        Assert.Equal(9000, Assert.Single(page.Items).DurationCentiseconds);
    }

    [Fact]
    public async Task TestOnlyFastWinDoesNotReplaceStandardTeamBest()
    {
        await using var harness = await Harness.CreateAsync();
        var alice = await harness.AddUserAsync("Alice");
        var bob = await harness.AddUserAsync("Bob");
        await harness.AddResultAsync(120, MatchOutcome.WIN, "SciFi", "NORMAL", 1m, alice, bob);
        await harness.AddTestOnlyResultAsync(60, alice, bob);

        var page = await harness.Service.GetAsync(LeaderboardMode.TEAM, "SciFi", "NORMAL", 1, 10, default);

        Assert.Equal(12000, Assert.Single(page.Items).DurationCentiseconds);
    }

    [Fact]
    public async Task OnlyTestOnlyResultsProducesEmptyRanking()
    {
        await using var harness = await Harness.CreateAsync();
        var alice = await harness.AddUserAsync("Alice");
        await harness.AddTestOnlyResultAsync(60, alice);

        var page = await harness.Service.GetAsync(LeaderboardMode.SOLO, "SciFi", "NORMAL", 1, 10, default);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task LegacyStandardResultsRemainEligible()
    {
        await using var harness = await Harness.CreateAsync();
        var alice = await harness.AddUserAsync("Alice");
        await harness.AddResultAsync(90, MatchOutcome.WIN, "SciFi", "NORMAL", 1m, alice);

        var page = await harness.Service.GetAsync(LeaderboardMode.SOLO, "SciFi", "NORMAL", 1, 10, default);

        Assert.Single(page.Items);
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private int _sequence;

        public AppDbContext Db { get; }
        public LeaderboardService Service { get; }

        private Harness(SqliteConnection connection, AppDbContext db)
        {
            _connection = connection;
            Db = db;
            Service = new LeaderboardService(db);
        }

        public static async Task<Harness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection).Options;
            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return new Harness(connection, db);
        }

        public async Task<Guid> AddUserAsync(string name)
        {
            var id = Guid.NewGuid();
            Db.Users.Add(new User
            {
                Id = id,
                Email = $"{id:N}@example.test",
                Username = name,
                PasswordHash = "hashed",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await Db.SaveChangesAsync();
            return id;
        }

        public async Task AddResultAsync(
            int duration,
            MatchOutcome outcome,
            string? mapId,
            string? difficulty,
            decimal objectiveCompletion,
            params Guid[] players) =>
            await AddResultAsync(duration, outcome, mapId, difficulty, objectiveCompletion, false, players);

        public async Task AddTestOnlyResultAsync(int duration, params Guid[] players) =>
            await AddResultAsync(duration, MatchOutcome.WIN, "SciFi", "NORMAL", 1m, true, players);

        private async Task AddResultAsync(
            int duration,
            MatchOutcome outcome,
            string? mapId,
            string? difficulty,
            decimal objectiveCompletion,
            bool testOnly,
            params Guid[] players)
        {
            var matchId = Guid.NewGuid();
            var endedAt = new DateTime(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc)
                .AddMinutes(_sequence++);
            if (testOnly && !await Db.RemoteFixedBalanceVersions.AnyAsync(item => item.Version == "TEST-RFB-1"))
            {
                Db.RemoteFixedBalanceVersions.Add(new RemoteFixedBalanceVersion
                {
                    Version = "TEST-RFB-1", MapId = "SciFi", Difficulty = "NORMAL",
                    SnapshotJson = "{}", ContentHash = new string('A', 64), IsActive = true,
                    CreatedAtUtc = endedAt
                });
            }
            Db.MatchAuthorityBindings.Add(new MatchAuthorityBinding
            {
                MatchId = matchId,
                FusionSessionName = matchId.ToString("N"),
                HostUserId = players[0],
                MaxPlayers = Math.Max(2, players.Length),
                Status = MatchAuthorityStatus.Ended,
                MapId = mapId,
                Difficulty = difficulty,
                ResolutionMode = testOnly ? "FIXED" : null,
                BalanceSource = testOnly ? "REMOTE_FIXED" : null,
                BalanceProtocolVersion = testOnly ? "RFB-1" : null,
                BalanceVersion = testOnly ? "TEST-RFB-1" : null,
                BalanceHash = testOnly ? new string('A', 64) : null,
                MatchDataPolicy = testOnly ? RemoteFixedBalanceContract.TestOnly : RemoteFixedBalanceContract.Standard,
                CreatedAtUtc = endedAt.AddSeconds(-duration),
                UpdatedAtUtc = endedAt,
                StartedAtUtc = endedAt.AddSeconds(-duration),
                EndedAtUtc = endedAt,
                LeaseExpiresAtUtc = endedAt.AddMinutes(1)
            });
            foreach (var (userId, actor) in players.Select((userId, actor) => (userId, actor)))
            {
                Db.MatchPlayerBindings.Add(new MatchPlayerBinding
                {
                    Id = Guid.NewGuid(),
                    MatchId = matchId,
                    UserId = userId,
                    FusionActorNumber = actor + 1,
                    JoinProofId = Guid.NewGuid(),
                    BoundAtUtc = endedAt.AddSeconds(-duration),
                    LastSeenAtUtc = endedAt
                });
            }
            Db.MatchResults.Add(new MatchResult
            {
                MatchId = matchId,
                SubmittedByUserId = players[0],
                Outcome = outcome,
                StartedAtUtc = endedAt.AddSeconds(-duration),
                EndedAtUtc = endedAt,
                DurationSeconds = duration,
                ObjectiveCompletion = objectiveCompletion,
                PlayerCount = players.Length,
                RosterKey = string.Join(",", players.Select(id => id.ToString("N"))
                    .OrderBy(id => id, StringComparer.Ordinal)),
                PayloadHash = new string('A', 64),
                RewardStatus = MatchRewardStatus.Pending,
                SubmittedAtUtc = endedAt,
                Players = players.Select(userId => new MatchResultPlayer
                {
                    MatchId = matchId,
                    UserId = userId,
                    Survived = true
                }).ToArray()
            });
            await Db.SaveChangesAsync();
            Db.ChangeTracker.Clear();
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
