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

public sealed class TeamProfileTests
{
    private const string PhaseA = "TEST_OBJECTIVE_A";
    private const string PhaseB = "TEST_OBJECTIVE_B";
    private static readonly DateTime BaseTime = new(2026, 9, 21, 8, 0, 0, DateTimeKind.Utc);

    [Fact, Trait("Category", "M4TeamProfile")]
    public void ValidObjectivePair_UsesOccurrenceTimestampsAndComputesAvailableComponents()
    {
        var matchId = Guid.NewGuid();
        var projection = Project(matchId, StandardEvents(matchId), TestPolicy([PhaseA]));

        Assert.Equal(30m, projection.ObjectiveTimeSeconds);
        Assert.Equal(TeamMetricStatus.Available, projection.ObjectiveTimeStatus);
        Assert.Equal(50m, projection.ObjectiveSpeedScore);
        Assert.Equal(100m, projection.SurvivalScore);
    }

    [Fact, Trait("Category", "M4TeamProfile")]
    public void MissingRequiredPair_IsUnavailableNotZero()
    {
        var matchId = Guid.NewGuid();
        var events = StandardEvents(matchId)
            .Where(item => item.EventType != "PHASE_COMPLETED")
            .Select(item => item.EventType == "MATCH_ENDED" ? Clone(item, sequence: 4) : item)
            .ToArray();

        var projection = Project(matchId, events, TestPolicy([PhaseA]));

        Assert.Null(projection.ObjectiveTimeSeconds);
        Assert.Equal(TeamMetricStatus.Unavailable, projection.ObjectiveTimeStatus);
    }

    [Fact, Trait("Category", "M4TeamProfile")]
    public void DuplicateTelemetryIdentity_DoesNotDoubleCount()
    {
        var matchId = Guid.NewGuid();
        var events = StandardEvents(matchId).ToList();
        events.Add(events.Single(item => item.EventType == "PHASE_COMPLETED"));

        var projection = Project(matchId, events, TestPolicy([PhaseA]));

        Assert.Equal(30m, projection.ObjectiveTimeSeconds);
        Assert.Equal(TeamMetricStatus.Available, projection.ObjectiveTimeStatus);
    }

    [Fact, Trait("Category", "M4TeamProfile")]
    public void CompletionBeforeStartSequence_IsInvalid()
    {
        var matchId = Guid.NewGuid();
        var events = StandardEvents(matchId)
            .Select(item => item.EventType switch
            {
                "PHASE_STARTED" => Clone(item, sequence: 4),
                "SECURITY_HOLD_INTERRUPTED" => Clone(item, sequence: 3),
                "PHASE_COMPLETED" => Clone(item, sequence: 2),
                _ => item
            }).ToArray();

        var projection = Project(matchId, events, TestPolicy([PhaseA]));

        Assert.Null(projection.ObjectiveTimeSeconds);
        Assert.Equal(TeamMetricStatus.Invalid, projection.ObjectiveTimeStatus);
    }

    [Fact, Trait("Category", "M4TeamProfile")]
    public void NegativeOccurrenceDuration_IsInvalid()
    {
        var matchId = Guid.NewGuid();
        var events = StandardEvents(matchId)
            .Select(item => item.EventType == "PHASE_STARTED"
                ? Clone(item, timestamp: BaseTime.AddSeconds(50))
                : item).ToArray();

        var projection = Project(matchId, events, TestPolicy([PhaseA]));

        Assert.Equal(TeamMetricStatus.Invalid, projection.ObjectiveTimeStatus);
        Assert.Null(projection.ObjectiveTimeSeconds);
    }

    [Fact, Trait("Category", "M4TeamProfile")]
    public void ForbiddenObjectivePhaseOverlap_IsInvalid()
    {
        var matchId = Guid.NewGuid();
        var events = new[]
        {
            MatchStart(matchId, 1),
            Phase(matchId, "PHASE_STARTED", PhaseA, 2, 10),
            Phase(matchId, "PHASE_STARTED", PhaseB, 3, 20),
            Phase(matchId, "PHASE_COMPLETED", PhaseA, 4, 40),
            Phase(matchId, "PHASE_COMPLETED", PhaseB, 5, 50),
            MatchEnd(matchId, 6)
        };

        var projection = Project(matchId, events, TestPolicy([PhaseA, PhaseB]));

        Assert.Equal(TeamMetricStatus.Invalid, projection.ObjectiveTimeStatus);
        Assert.Equal("OBJECTIVE_PHASE_OVERLAP_FORBIDDEN", projection.ProcessingReason);
    }

    [Fact, Trait("Category", "M4TeamProfile")]
    public void InterruptedHold_RemainsInsideWallClockDuration()
    {
        var matchId = Guid.NewGuid();
        var projection = Project(matchId, StandardEvents(matchId), TestPolicy([PhaseA]));

        Assert.Equal(30m, projection.ObjectiveTimeSeconds);
    }

    [Fact, Trait("Category", "M4TeamProfile")]
    public void InvalidMatchTerminal_InvalidatesComponents()
    {
        var matchId = Guid.NewGuid();
        var facts = new TeamMatchFacts(matchId, MatchOutcome.WIN, 2, true);

        var projection = TeamProfileProjector.Project(
            facts, StandardEvents(matchId), new HashSet<Guid>(), TestPolicy([PhaseA]));

        Assert.Equal(TeamProfileProcessingStatus.Invalid, projection.ProcessingStatus);
        Assert.Null(projection.SurvivalScore);
        Assert.Null(projection.ObjectiveTimeSeconds);
    }

    [Fact, Trait("Category", "M4TeamProfile")]
    public void MissingProductionRegistry_DoesNotInferObjectivePhase()
    {
        var matchId = Guid.NewGuid();
        var policy = new ConfiguredTeamProfilePolicy(Options.Create(new TeamProfileSettings()));

        var projection = Project(matchId, StandardEvents(matchId), policy);

        Assert.Equal(TeamMetricStatus.Invalid, projection.ObjectiveTimeStatus);
        Assert.Null(projection.ObjectiveTimeSeconds);
        Assert.Equal("PHASE_REGISTRY_NOT_CONFIGURED", projection.ProcessingReason);
    }

    [Fact, Trait("Category", "M4TeamProfile")]
    public void TeamPerformance_RemainsIncompleteWithoutRenormalization()
    {
        var matchId = Guid.NewGuid();
        var projection = Project(matchId, StandardEvents(matchId), TestPolicy([PhaseA]));

        Assert.Equal(TeamPerformanceStatus.Incomplete, projection.TeamPerformanceStatus);
        Assert.Null(projection.TeamPerformanceScore);
    }

    [Fact, Trait("Category", "M4TeamProfile")]
    public async Task DuplicateProcessing_IsNoOp()
    {
        await using var fixture = await TeamFixture.CreateAsync();
        var service = fixture.Service(TestPolicy([PhaseA]));

        var first = await service.ProcessAsync(fixture.MatchId);
        var duplicate = await service.ProcessAsync(fixture.MatchId);

        Assert.True(first.IsSuccess);
        Assert.True(duplicate.IsSuccess);
        Assert.True(duplicate.Data!.IsDuplicate);
        Assert.Equal(1, (await fixture.Db.TeamProfiles.SingleAsync()).ProcessingRevision);
    }

    [Fact, Trait("Category", "M4TeamProfile")]
    public async Task ChangedInvalidEvidence_ReplacesOldAvailableProjection()
    {
        await using var fixture = await TeamFixture.CreateAsync();
        var service = fixture.Service(TestPolicy([PhaseA]));
        Assert.True((await service.ProcessAsync(fixture.MatchId)).IsSuccess);
        fixture.Repository.Set(StandardEvents(fixture.MatchId)
            .Select(item => item.EventType == "PHASE_STARTED"
                ? Clone(item, timestamp: BaseTime.AddSeconds(50)) : item));

        var changed = await service.ProcessAsync(fixture.MatchId);
        var stored = await fixture.Db.TeamProfiles.SingleAsync();

        Assert.True(changed.IsSuccess);
        Assert.Equal(2, stored.ProcessingRevision);
        Assert.Equal(TeamMetricStatus.Invalid, stored.ObjectiveTimeStatus);
        Assert.Null(stored.ObjectiveTimeSeconds);
    }

    [Fact, Trait("Category", "M4TeamProfile")]
    public async Task ProfilesAreMatchScopedAndDeferredFieldsRemainNull()
    {
        await using var fixture = await TeamFixture.CreateAsync();
        var secondMatch = await fixture.AddMatchAsync();
        fixture.Repository.Set(StandardEvents(fixture.MatchId).Concat(StandardEvents(secondMatch)));
        var service = fixture.Service(TestPolicy([PhaseA]));

        Assert.True((await service.ProcessAsync(fixture.MatchId)).IsSuccess);
        Assert.True((await service.ProcessAsync(secondMatch)).IsSuccess);
        var profiles = await fixture.Db.TeamProfiles.OrderBy(item => item.MatchId).ToArrayAsync();

        Assert.Equal(2, profiles.Length);
        Assert.All(profiles, profile =>
        {
            Assert.Null(profile.SplitTime);
            Assert.Null(profile.AvgDistance);
            Assert.Null(profile.ReviveSuccess);
            Assert.Null(profile.ResourceEfficiency);
            Assert.Null(profile.Communication);
            Assert.Null(profile.WipeRecovery);
            Assert.Equal(TeamMetricStatus.Deferred, profile.TeamworkStatus);
            Assert.Null(profile.TeamPerformanceScore);
            Assert.Equal(TeamPerformanceStatus.Incomplete, profile.TeamPerformanceStatus);
        });
    }

    private static TeamProfileProjection Project(
        Guid matchId,
        IReadOnlyCollection<TelemetryEventDocument> events,
        ITeamProfilePolicy policy) =>
        TeamProfileProjector.Project(
            new TeamMatchFacts(matchId, MatchOutcome.WIN, 1, true),
            events, new HashSet<Guid>(), policy);

    private static ITeamProfilePolicy TestPolicy(string[] phases, string[]? overlaps = null) =>
        new ConfiguredTeamProfilePolicy(Options.Create(new TeamProfileSettings
        {
            TeamPerformanceFormulaVersion = "TEST_TEAM_PERFORMANCE_FOUR_COMPONENT_V0",
            PhaseRegistryVersion = "TEST_PHASE_REGISTRY_V1",
            NormalizationConfigVersion = "TEST_OBJECTIVE_NORMALIZATION_V1",
            ObjectiveBearingPhases = phases,
            AllowedOverlapPairs = overlaps ?? [],
            ObjectiveTimeMin = 0,
            ObjectiveTimeMax = 60,
            ObjectiveWeight = 0.25m,
            SurvivalWeight = 0.25m,
            TeamworkWeight = 0.25m,
            ResourceWeight = 0.25m
        }));

    private static TelemetryEventDocument[] StandardEvents(Guid matchId) =>
    [
        MatchStart(matchId, 1),
        Phase(matchId, "PHASE_STARTED", PhaseA, 2, 10),
        Phase(matchId, "SECURITY_HOLD_INTERRUPTED", PhaseA, 3, 20),
        Phase(matchId, "PHASE_COMPLETED", PhaseA, 4, 40),
        MatchEnd(matchId, 5)
    ];

    private static TelemetryEventDocument MatchStart(Guid matchId, long sequence) =>
        Event(matchId, "MATCH_STARTED", sequence, BaseTime,
            new BsonDocument
            {
                ["context"] = new BsonDocument { ["teamSize"] = 1, ["researchCaptureEnabled"] = false },
                ["data"] = new BsonDocument { ["mapId"] = "test" }
            }, "MATCH_READY");

    private static TelemetryEventDocument MatchEnd(Guid matchId, long sequence) =>
        Event(matchId, "MATCH_ENDED", sequence, BaseTime.AddSeconds(60),
            new BsonDocument
            {
                ["context"] = new BsonDocument { ["phase"] = "MATCH_END" },
                ["data"] = new BsonDocument { ["outcome"] = "SUCCESS", ["durationSeconds"] = 60, ["survivorCount"] = 1 }
            }, "TEAM_ESCAPED");

    private static TelemetryEventDocument Phase(
        Guid matchId, string type, string phase, long sequence, int seconds) =>
        Event(matchId, type, sequence, BaseTime.AddSeconds(seconds),
            new BsonDocument
            {
                ["context"] = new BsonDocument { ["phase"] = phase },
                ["data"] = new BsonDocument()
            }, type == "PHASE_COMPLETED" ? "OBJECTIVE_COMPLETED" : null);

    private static TelemetryEventDocument Event(
        Guid matchId, string type, long sequence, DateTime timestamp,
        BsonDocument value, string? reason) => new()
    {
        Id = Guid.NewGuid(), MatchId = matchId, EventType = type, EventSequence = sequence,
        Ts = timestamp, ValueJson = value, ReasonCode = reason, SchemaVersion = "1.1",
        SemanticFingerprint = new string((char)('0' + (int)(sequence % 10)), 64), IngestedAt = BaseTime
    };

    private static TelemetryEventDocument Clone(
        TelemetryEventDocument source, long? sequence = null, DateTime? timestamp = null) => new()
    {
        Id = source.Id, MatchId = source.MatchId, UserId = source.UserId,
        EventType = source.EventType, EventSequence = sequence ?? source.EventSequence,
        Ts = timestamp ?? source.Ts, ValueJson = source.ValueJson, ReasonCode = source.ReasonCode,
        SchemaVersion = source.SchemaVersion, SemanticFingerprint = source.SemanticFingerprint,
        IngestedAt = source.IngestedAt
    };

    private sealed class MutableTelemetryRepository : ITelemetryEventRepository
    {
        private TelemetryEventDocument[] _events = [];
        public MutableTelemetryRepository(IEnumerable<TelemetryEventDocument> events) => Set(events);
        public void Set(IEnumerable<TelemetryEventDocument> events) => _events = events.ToArray();
        public Task<IReadOnlyList<TelemetryEventDocument>> LoadAcceptedMatchEventsAsync(Guid matchId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TelemetryEventDocument>>(_events.Where(item => item.MatchId == matchId).ToArray());
        public Task EnsureIndexesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<TelemetryWriteResult> AtomicCommitBatchAsync(IReadOnlyCollection<TelemetryEventDocument> events, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TelemetryWriteResult> InsertBatchAsync(IReadOnlyCollection<TelemetryEventDocument> events, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Guid, TelemetryWriteItemResult>> LoadConflictsAsync(IReadOnlyCollection<TelemetryEventDocument> events, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Guid, TelemetryMatchBoundary>> LoadMatchBoundariesAsync(IReadOnlyCollection<Guid> matchIds, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class TeamFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public AppDbContext Db { get; }
        public MutableTelemetryRepository Repository { get; }
        public Guid UserId { get; }
        public Guid MatchId { get; }

        private TeamFixture(SqliteConnection connection, AppDbContext db,
            MutableTelemetryRepository repository, Guid userId, Guid matchId)
        {
            _connection = connection; Db = db; Repository = repository;
            UserId = userId; MatchId = matchId;
        }

        public static async Task<TeamFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var userId = Guid.NewGuid();
            var matchId = Guid.NewGuid();
            db.Users.Add(new User
            {
                Id = userId, Email = $"{userId:N}@test.local", Username = $"u{userId:N}",
                PasswordHash = "hash", Role = UserRole.PLAYER, Status = UserStatus.ACTIVE,
                CreatedAt = BaseTime, UpdatedAt = BaseTime
            });
            AddMatch(db, matchId, userId);
            await db.SaveChangesAsync();
            return new TeamFixture(connection, db,
                new MutableTelemetryRepository(StandardEvents(matchId)), userId, matchId);
        }

        public async Task<Guid> AddMatchAsync()
        {
            var matchId = Guid.NewGuid();
            AddMatch(Db, matchId, UserId);
            await Db.SaveChangesAsync();
            return matchId;
        }

        public TeamProfileService Service(ITeamProfilePolicy policy) =>
            new(Db, Repository, policy, TimeProvider.System);

        private static void AddMatch(AppDbContext db, Guid matchId, Guid userId)
        {
            db.MatchAuthorityBindings.Add(new MatchAuthorityBinding
            {
                MatchId = matchId, FusionSessionName = $"team-{matchId:N}", HostUserId = userId,
                MaxPlayers = 4, Status = MatchAuthorityStatus.Ended,
                LeaseExpiresAtUtc = BaseTime, CreatedAtUtc = BaseTime.AddMinutes(-2),
                UpdatedAtUtc = BaseTime, StartedAtUtc = BaseTime.AddMinutes(-2), EndedAtUtc = BaseTime,
                Players = [new MatchPlayerBinding
                {
                    Id = Guid.NewGuid(), MatchId = matchId, UserId = userId,
                    FusionActorNumber = 1, JoinProofId = Guid.NewGuid(),
                    BoundAtUtc = BaseTime.AddMinutes(-2), LastSeenAtUtc = BaseTime
                }]
            });
            db.MatchResults.Add(new MatchResult
            {
                MatchId = matchId, SubmittedByUserId = userId, Outcome = MatchOutcome.WIN,
                StartedAtUtc = BaseTime.AddMinutes(-2), EndedAtUtc = BaseTime,
                DurationSeconds = 120, ObjectiveCompletion = 1, PlayerCount = 1,
                PayloadHash = new string('A', 64), RewardStatus = MatchRewardStatus.Pending,
                SubmittedAtUtc = BaseTime,
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
