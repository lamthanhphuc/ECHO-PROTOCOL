using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.Data.Telemetry;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using EchoProtocol.Api.Services.Interfaces;
using EchoProtocol.Api.Services.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class PlayerAIProfileTests
{
    private static readonly DateTime EndedAt = new(2026, 9, 21, 5, 0, 0, DateTimeKind.Utc);

    [Fact, Trait("Category", "M4PlayerAIProfile")]
    public async Task Aggregator_ValidCompleteTelemetry_ProducesActiveMetrics()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var repository = new FakeTelemetryRepository(CreateCompleteEvents(fixture.MatchId, fixture.UserId));
        var aggregator = new MatchTelemetryAggregator(fixture.Db, repository, TestPolicy());

        var result = await aggregator.AggregateAsync(fixture.MatchId, fixture.UserId);

        Assert.Equal(MatchProfileEligibilityStatus.Eligible, result.Eligibility);
        Assert.Equal(TelemetryCompleteness.Complete, result.Completeness);
        Assert.Equal(100m, result.Metrics[PlayerAIDimension.Survival].RawValue);
        Assert.Equal(1m, result.Metrics[PlayerAIDimension.Noise].RawValue);
        Assert.True(result.ResearchCaptureEnabled);
        Assert.Null(result.ResearchEligible); // owned by the approved research/experiment protocol
    }

    [Fact, Trait("Category", "M4PlayerAIProfile")]
    public async Task Aggregator_IncompleteTelemetry_DoesNotTreatMissingNoiseAsZero()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var events = CreateCompleteEvents(fixture.MatchId, fixture.UserId)
            .Where(item => item.EventSequence != 3).ToArray();
        var aggregator = new MatchTelemetryAggregator(
            fixture.Db, new FakeTelemetryRepository(events), TestPolicy());

        var result = await aggregator.AggregateAsync(fixture.MatchId, fixture.UserId);

        Assert.Equal(MatchProfileEligibilityStatus.Eligible, result.Eligibility);
        Assert.Equal(TelemetryCompleteness.Incomplete, result.Completeness);
        Assert.Equal(MetricAvailability.Available, result.Metrics[PlayerAIDimension.Survival].Availability);
        Assert.Equal(MetricAvailability.Unavailable, result.Metrics[PlayerAIDimension.Noise].Availability);
        Assert.Null(result.Metrics[PlayerAIDimension.Noise].RawValue);
    }

    [Fact, Trait("Category", "M4PlayerAIProfile")]
    public async Task Aggregator_ContradictoryTerminalFacts_InvalidatesSurvival()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var events = CreateCompleteEvents(fixture.MatchId, fixture.UserId).ToList();
        events.Insert(2, Event(fixture.MatchId, fixture.UserId, "PLAYER_ELIMINATED", 3, "REVIVE_LIMIT_REACHED"));
        for (var index = 3; index < events.Count; index++)
        {
            events[index] = CloneAtSequence(events[index], index + 1);
        }
        var aggregator = new MatchTelemetryAggregator(
            fixture.Db, new FakeTelemetryRepository(events), TestPolicy());

        var result = await aggregator.AggregateAsync(fixture.MatchId, fixture.UserId);

        Assert.Equal(MetricAvailability.Invalid, result.Metrics[PlayerAIDimension.Survival].Availability);
    }

    [Theory, Trait("Category", "M4PlayerAIProfile")]
    [InlineData(0, 100)]
    [InlineData(5, 50)]
    [InlineData(10, 0)]
    [InlineData(99, 0)]
    public void NoiseNormalization_UsesCanonicalHigherIsWorseFormulaAndBounds(int count, int expected)
    {
        var metric = new AggregatedMetric(PlayerAIDimension.Noise, count,
            MetricAvailability.Available, "test", new string('A', 64));

        var ok = MatchScoreNormalizer.TryNormalize(metric, TestPolicy(), out var result, out _);

        Assert.True(ok);
        Assert.Equal(expected, result!.Score);
    }

    [Fact, Trait("Category", "M4PlayerAIProfile")]
    public async Task FirstObservation_ReplacesColdStartWithoutBlending()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var updater = fixture.CreateUpdater(Aggregation(fixture.MatchId, fixture.UserId, EndedAt, 100m, 0m));

        var result = await updater.ProcessAsync(fixture.MatchId, fixture.UserId);
        var profile = await fixture.Db.PlayerAIProfiles.SingleAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, profile.SurvivalScore);
        Assert.Equal(ProfileDimensionStatus.Active, profile.SurvivalStatus);
        Assert.Equal(1, profile.SurvivalSampleCount);
        Assert.Null(profile.ObjectiveScore);
    }

    [Fact, Trait("Category", "M4PlayerAIProfile")]
    public async Task LaterObservation_ReplaysCanonicalEma()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var secondMatch = await fixture.AddMatchAsync(EndedAt.AddMinutes(1));
        var aggregator = new FakeAggregator(
            Aggregation(fixture.MatchId, fixture.UserId, EndedAt, 100m, 0m),
            Aggregation(secondMatch, fixture.UserId, EndedAt.AddMinutes(1), 0m, 10m));
        var updater = fixture.CreateUpdater(aggregator);

        Assert.True((await updater.ProcessAsync(fixture.MatchId, fixture.UserId)).IsSuccess);
        Assert.True((await updater.ProcessAsync(secondMatch, fixture.UserId)).IsSuccess);
        var profile = await fixture.Db.PlayerAIProfiles.SingleAsync();

        Assert.Equal(50m, profile.SurvivalScore);
        Assert.Equal(50m, profile.NoiseScore);
        Assert.Equal(2, profile.SurvivalSampleCount);
    }

    [Fact, Trait("Category", "M4PlayerAIProfile")]
    public async Task LateOlderObservation_ReplaysByMatchEndThenMatchId()
    {
        await using var fixture = await ProfileFixture.CreateAsync(EndedAt.AddMinutes(1));
        var olderMatch = await fixture.AddMatchAsync(EndedAt);
        var aggregator = new FakeAggregator(
            Aggregation(fixture.MatchId, fixture.UserId, EndedAt.AddMinutes(1), 0m, 10m),
            Aggregation(olderMatch, fixture.UserId, EndedAt, 100m, 0m));
        var updater = fixture.CreateUpdater(aggregator);

        await updater.ProcessAsync(fixture.MatchId, fixture.UserId);
        await updater.ProcessAsync(olderMatch, fixture.UserId);
        var profile = await fixture.Db.PlayerAIProfiles.SingleAsync();

        Assert.Equal(50m, profile.SurvivalScore); // canonical order: 100 then EMA with 0
        Assert.Equal(fixture.MatchId, profile.SurvivalLastMatchId);
    }

    [Fact, Trait("Category", "M4PlayerAIProfile")]
    public async Task DuplicateMatchProcessing_IsNoOp()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var updater = fixture.CreateUpdater(Aggregation(fixture.MatchId, fixture.UserId, EndedAt, 100m, 0m));

        var first = await updater.ProcessAsync(fixture.MatchId, fixture.UserId);
        var duplicate = await updater.ProcessAsync(fixture.MatchId, fixture.UserId);

        Assert.True(first.IsSuccess);
        Assert.True(duplicate.IsSuccess);
        Assert.True(duplicate.Data!.IsDuplicate);
        Assert.Equal(2, await fixture.Db.MatchScores.CountAsync());
        Assert.Equal(1, (await fixture.Db.PlayerAIProfiles.SingleAsync()).ProfileRevision);
    }

    [Fact, Trait("Category", "M4PlayerAIProfile")]
    public async Task DimensionLateCompletion_AppliesNoiseWithoutReapplyingSurvival()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var metrics = Aggregation(fixture.MatchId, fixture.UserId, EndedAt, 100m, 2m)
            .Metrics.ToDictionary(item => item.Key, item => item.Value);
        metrics[PlayerAIDimension.Noise] = new(PlayerAIDimension.Noise, null,
            MetricAvailability.Unavailable, "SOURCE_COVERAGE_INCOMPLETE", new string('B', 64));
        var survivalOnly = Aggregation(fixture.MatchId, fixture.UserId, EndedAt, 100m, 2m) with
        {
            Completeness = TelemetryCompleteness.Incomplete,
            SourceFingerprint = new string('D', 64),
            Metrics = metrics
        };
        var survivalPolicy = new ConfiguredPlayerAIProfilePolicy(Options.Create(new PlayerAIProfileSettings
        {
            AlphaConfigVersion = "TEST_ALPHA_V1",
            SurvivalAlpha = 0.5m
        }));
        var firstUpdater = new PlayerAIProfileUpdater(fixture.Db,
            new FakeAggregator(survivalOnly), survivalPolicy, TimeProvider.System);

        var first = await firstUpdater.ProcessAsync(fixture.MatchId, fixture.UserId);
        var secondUpdater = fixture.CreateUpdater(
            Aggregation(fixture.MatchId, fixture.UserId, EndedAt, 100m, 2m));
        var second = await secondUpdater.ProcessAsync(fixture.MatchId, fixture.UserId);
        var profile = await fixture.Db.PlayerAIProfiles.SingleAsync();

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess, $"{second.ErrorCode}: {second.Message}");
        Assert.Equal([PlayerAIDimension.Noise], second.Data!.AppliedDimensions);
        Assert.Equal(1, profile.SurvivalSampleCount);
        Assert.Equal(1, profile.NoiseSampleCount);
        Assert.Equal(2, profile.ProfileRevision);
    }

    [Fact, Trait("Category", "M4PlayerAIProfile")]
    public async Task PreviouslyAppliedInvalidMetric_IsRetractedAndReplayed()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var valid = Aggregation(fixture.MatchId, fixture.UserId, EndedAt, 100m, 2m);
        var metrics = valid.Metrics.ToDictionary(item => item.Key, item => item.Value);
        metrics[PlayerAIDimension.Survival] = new(PlayerAIDimension.Survival, null,
            MetricAvailability.Invalid, "CONTRADICTORY_TERMINAL_FACT", new string('D', 64));
        var invalidSurvival = valid with
        {
            SourceFingerprint = new string('E', 64),
            Metrics = metrics
        };
        var updater = fixture.CreateUpdater(new SequencedAggregator(
            valid, valid, invalidSurvival, invalidSurvival));

        Assert.True((await updater.ProcessAsync(fixture.MatchId, fixture.UserId)).IsSuccess);
        var correction = await updater.ProcessAsync(fixture.MatchId, fixture.UserId);
        var profile = await fixture.Db.PlayerAIProfiles.SingleAsync();

        Assert.True(correction.IsSuccess);
        Assert.Contains(PlayerAIDimension.Survival, correction.Data!.RetractedDimensions);
        Assert.Equal(50m, profile.SurvivalScore);
        Assert.Equal(ProfileDimensionStatus.ColdStart, profile.SurvivalStatus);
        Assert.Equal(0, profile.SurvivalSampleCount);
        Assert.Equal(ProfileContributionStatus.Retracted,
            (await fixture.Db.MatchScores.SingleAsync(item => item.Dimension == PlayerAIDimension.Survival)).ContributionStatus);
    }

    [Fact, Trait("Category", "M4PlayerAIProfile")]
    public async Task Persistence_StoresFormulaProvenanceAndDimensionReceipts()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var updater = fixture.CreateUpdater(Aggregation(fixture.MatchId, fixture.UserId, EndedAt, 100m, 2m));

        Assert.True((await updater.ProcessAsync(fixture.MatchId, fixture.UserId)).IsSuccess);
        fixture.Db.ChangeTracker.Clear();
        var stored = await fixture.Db.MatchScores.OrderBy(item => item.Dimension).ToArrayAsync();

        Assert.Equal(2, stored.Length);
        Assert.All(stored, item => Assert.Equal("PLAYER_MATCH_SCORE_V1_1", item.MatchScoreFormulaVersion));
        Assert.All(stored, item => Assert.Equal("1.1", item.SourceTelemetrySchemaVersion));
    }

    [Fact, Trait("Category", "M4PlayerAIProfile")]
    public async Task FormulaVersionMismatch_IsRejectedWithoutMutation()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        fixture.Db.PlayerAIProfiles.Add(new PlayerAIProfile
        {
            UserId = fixture.UserId, ProfileLineageId = Guid.NewGuid(), ProfileRevision = 0,
            ProfileFormulaVersion = "OLD_FORMULA", MatchScoreFormulaVersion = "OLD_SCORE",
            NormalizationConfigVersion = "TEST_NORM_V1", ProfileNoiseFilterVersion = "TEST_FILTER_V1",
            AlphaConfigVersion = "TEST_ALPHA_V1", SurvivalScore = 50, NoiseScore = 50,
            SurvivalStatus = ProfileDimensionStatus.ColdStart, NoiseStatus = ProfileDimensionStatus.ColdStart,
            CreatedAtUtc = EndedAt, UpdatedAtUtc = EndedAt
        });
        await fixture.Db.SaveChangesAsync();
        var updater = fixture.CreateUpdater(Aggregation(fixture.MatchId, fixture.UserId, EndedAt, 100m, 0m));

        var result = await updater.ProcessAsync(fixture.MatchId, fixture.UserId);

        Assert.False(result.IsSuccess);
        Assert.Equal("AI_PROFILE_APPLY_CONFLICT", result.ErrorCode);
        Assert.Empty(await fixture.Db.MatchScores.ToArrayAsync());
    }

    [Fact, Trait("Category", "M4PlayerAIProfile")]
    public async Task UnapprovedProductionPolicy_ReturnsConfigurationError()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var unconfigured = new ConfiguredPlayerAIProfilePolicy(Options.Create(new PlayerAIProfileSettings()));
        var updater = new PlayerAIProfileUpdater(fixture.Db,
            new FakeAggregator(Aggregation(fixture.MatchId, fixture.UserId, EndedAt, 100m, 0m)),
            unconfigured, TimeProvider.System);

        var result = await updater.ProcessAsync(fixture.MatchId, fixture.UserId);

        Assert.False(result.IsSuccess);
        Assert.Equal("AI_PROFILE_POLICY_NOT_CONFIGURED", result.ErrorCode);
        Assert.Empty(await fixture.Db.PlayerAIProfiles.ToArrayAsync());
    }

    private static IPlayerAIProfilePolicy TestPolicy() =>
        new ConfiguredPlayerAIProfilePolicy(Options.Create(new PlayerAIProfileSettings
        {
            NormalizationConfigVersion = "TEST_NORM_V1",
            ProfileNoiseFilterVersion = "TEST_FILTER_V1",
            AlphaConfigVersion = "TEST_ALPHA_V1",
            SurvivalAlpha = 0.5m,
            NoiseAlpha = 0.5m,
            ProfileNoiseCountMin = 0m,
            ProfileNoiseCountMax = 10m,
            NoisePenaltyTypes = ["SPRINT"]
        }));

    private static MatchTelemetryAggregation Aggregation(
        Guid matchId, Guid userId, DateTime endedAt, decimal survival, decimal noiseCount)
    {
        var metrics = new Dictionary<PlayerAIDimension, AggregatedMetric>
        {
            [PlayerAIDimension.Survival] = new(PlayerAIDimension.Survival, survival,
                MetricAvailability.Available, "test", new string('A', 64)),
            [PlayerAIDimension.Noise] = new(PlayerAIDimension.Noise, noiseCount,
                MetricAvailability.Available, "test", new string('B', 64))
        };
        return new(matchId, userId, endedAt, MatchProfileEligibilityStatus.Eligible,
            TelemetryCompleteness.Complete, [], "1.1", new string('C', 64), true, null, metrics);
    }

    private static TelemetryEventDocument[] CreateCompleteEvents(Guid matchId, Guid userId) =>
    [
        Event(matchId, null, "MATCH_STARTED", 1, "MATCH_READY", new BsonDocument
        {
            ["context"] = new BsonDocument { ["researchCaptureEnabled"] = true },
            ["data"] = new BsonDocument { ["mapId"] = "test" }
        }),
        Event(matchId, userId, "PLAYER_ESCAPED", 2, "EXIT_REACHED"),
        Event(matchId, userId, "NOISE_EMITTED", 3, "PLAYER_SPRINT", new BsonDocument
        {
            ["context"] = new BsonDocument(),
            ["data"] = new BsonDocument { ["noiseType"] = "SPRINT" }
        }),
        Event(matchId, null, "MATCH_ENDED", 4, "TEAM_ESCAPED", new BsonDocument
        {
            ["context"] = new BsonDocument(),
            ["data"] = new BsonDocument { ["outcome"] = "SUCCESS", ["durationSeconds"] = 120, ["survivorCount"] = 1 }
        })
    ];

    private static TelemetryEventDocument Event(
        Guid matchId, Guid? userId, string type, long sequence, string reason,
        BsonDocument? value = null) => new()
    {
        Id = Guid.NewGuid(), MatchId = matchId, UserId = userId, EventType = type,
        EventSequence = sequence, ReasonCode = reason, SchemaVersion = "1.1",
        SemanticFingerprint = new string((char)('A' + (int)sequence), 64),
        ValueJson = value ?? new BsonDocument { ["context"] = new BsonDocument(), ["data"] = new BsonDocument() },
        Ts = EndedAt.AddSeconds(sequence), IngestedAt = EndedAt
    };

    private static TelemetryEventDocument CloneAtSequence(TelemetryEventDocument source, long sequence) => new()
    {
        Id = source.Id, MatchId = source.MatchId, UserId = source.UserId,
        EventType = source.EventType, EventSequence = sequence, ReasonCode = source.ReasonCode,
        SchemaVersion = source.SchemaVersion, SemanticFingerprint = source.SemanticFingerprint,
        ValueJson = source.ValueJson, Ts = source.Ts, IngestedAt = source.IngestedAt
    };

    private sealed class FakeAggregator(params MatchTelemetryAggregation[] values) : IMatchTelemetryAggregator
    {
        private readonly Dictionary<Guid, MatchTelemetryAggregation> _values = values.ToDictionary(item => item.MatchId);
        public Task<MatchTelemetryAggregation> AggregateAsync(Guid matchId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_values[matchId]);
    }

    private sealed class SequencedAggregator(params MatchTelemetryAggregation[] values) : IMatchTelemetryAggregator
    {
        private int _index;
        public Task<MatchTelemetryAggregation> AggregateAsync(Guid matchId, Guid userId, CancellationToken cancellationToken = default)
        {
            var index = Math.Min(Interlocked.Increment(ref _index) - 1, values.Length - 1);
            return Task.FromResult(values[index]);
        }
    }

    private sealed class FakeTelemetryRepository(IEnumerable<TelemetryEventDocument> values) : ITelemetryEventRepository
    {
        private readonly TelemetryEventDocument[] _values = values.ToArray();
        public Task<IReadOnlyList<TelemetryEventDocument>> LoadAcceptedMatchEventsAsync(Guid matchId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TelemetryEventDocument>>(_values.Where(item => item.MatchId == matchId).ToArray());
        public Task EnsureIndexesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<TelemetryWriteResult> AtomicCommitBatchAsync(IReadOnlyCollection<TelemetryEventDocument> events, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TelemetryWriteResult> InsertBatchAsync(IReadOnlyCollection<TelemetryEventDocument> events, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Guid, TelemetryWriteItemResult>> LoadConflictsAsync(IReadOnlyCollection<TelemetryEventDocument> events, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Guid, TelemetryMatchBoundary>> LoadMatchBoundariesAsync(IReadOnlyCollection<Guid> matchIds, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class ProfileFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;
        public AppDbContext Db { get; }
        public Guid UserId { get; }
        public Guid MatchId { get; }

        private ProfileFixture(SqliteConnection connection, DbContextOptions<AppDbContext> options, AppDbContext db, Guid userId, Guid matchId)
        {
            _connection = connection; _options = options; Db = db; UserId = userId; MatchId = matchId;
        }

        public static async Task<ProfileFixture> CreateAsync(DateTime? endedAt = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();
            var userId = Guid.NewGuid();
            var matchId = Guid.NewGuid();
            db.Users.Add(User(userId));
            AddCompletedMatch(db, matchId, userId, endedAt ?? EndedAt);
            await db.SaveChangesAsync();
            return new ProfileFixture(connection, options, db, userId, matchId);
        }

        public async Task<Guid> AddMatchAsync(DateTime endedAt)
        {
            var id = Guid.NewGuid();
            AddCompletedMatch(Db, id, UserId, endedAt);
            await Db.SaveChangesAsync();
            return id;
        }

        public PlayerAIProfileUpdater CreateUpdater(MatchTelemetryAggregation aggregation) => CreateUpdater(new FakeAggregator(aggregation));
        public PlayerAIProfileUpdater CreateUpdater(IMatchTelemetryAggregator aggregator) =>
            new(Db, aggregator, TestPolicy(), TimeProvider.System);

        private static User User(Guid id) => new()
        {
            Id = id, Email = $"{id:N}@test.local", Username = $"u{id:N}", PasswordHash = "hash",
            Role = UserRole.PLAYER, Status = UserStatus.ACTIVE, CreatedAt = EndedAt, UpdatedAt = EndedAt
        };

        private static void AddCompletedMatch(AppDbContext db, Guid matchId, Guid userId, DateTime endedAt)
        {
            var binding = new MatchPlayerBinding
            {
                Id = Guid.NewGuid(), MatchId = matchId, UserId = userId, FusionActorNumber = 1,
                JoinProofId = Guid.NewGuid(), BoundAtUtc = endedAt.AddMinutes(-2), LastSeenAtUtc = endedAt
            };
            db.MatchAuthorityBindings.Add(new MatchAuthorityBinding
            {
                MatchId = matchId, FusionSessionName = $"test-{matchId:N}", HostUserId = userId,
                MaxPlayers = 4, Status = MatchAuthorityStatus.Ended, LeaseExpiresAtUtc = endedAt,
                CreatedAtUtc = endedAt.AddMinutes(-2), UpdatedAtUtc = endedAt, StartedAtUtc = endedAt.AddMinutes(-2),
                EndedAtUtc = endedAt, Players = [binding]
            });
            db.MatchResults.Add(new MatchResult
            {
                MatchId = matchId, SubmittedByUserId = userId, Outcome = MatchOutcome.WIN,
                StartedAtUtc = endedAt.AddMinutes(-2), EndedAtUtc = endedAt, DurationSeconds = 120,
                ObjectiveCompletion = 1, PlayerCount = 1, PayloadHash = new string('A', 64),
                RewardStatus = MatchRewardStatus.Pending, SubmittedAtUtc = endedAt,
                Players = [new MatchResultPlayer { MatchId = matchId, UserId = userId, Survived = true }]
            });
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
