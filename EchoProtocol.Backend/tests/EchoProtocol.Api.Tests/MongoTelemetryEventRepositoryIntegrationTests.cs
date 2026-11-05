using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Data.Telemetry;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class MongoTelemetryEventRepositoryIntegrationTests
{
    private const string MongoUriEnvironmentVariable = "ECHO_PHASE6R_MONGO_URI";
    private const string TelemetryCollectionName = "telemetry_events";
    private const string MatchStateCollectionName = "telemetry_events_match_state";

    [Fact]
    public async Task AtomicCommitBatchAsync_ReverseSubmittedResearchAfterStart_UsesSequenceOrderAndPreservesResultOrder()
    {
        await WithRepositoryAsync(async (database, repository) =>
        {
            var matchId = Guid.NewGuid();
            var research = CreateDocument(matchId, "MONSTER_ATTACK_RESOLVED", 2);
            var start = CreateMatchStarted(matchId, 1, researchCaptureEnabled: true);

            var result = await repository.AtomicCommitBatchAsync([research, start]);

            Assert.Equal(2, result.Items.Count);
            Assert.Equal(research.Id, result.Items[0].Id);
            Assert.Equal(TelemetryWriteStatus.Accepted, result.Items[0].Status);
            Assert.Equal(start.Id, result.Items[1].Id);
            Assert.Equal(TelemetryWriteStatus.Accepted, result.Items[1].Status);

            var stored = await LoadTelemetryEventsAsync(database);
            Assert.Equal(2, stored.Count);
            Assert.Equal([1L, 2L], stored.Select(item => item.EventSequence).Order().ToArray());
        });
    }

    [Fact]
    public async Task AtomicCommitBatchAsync_ReverseSubmittedPostTerminalEvent_RejectsPostTerminal()
    {
        await WithRepositoryAsync(async (database, repository) =>
        {
            var matchId = Guid.NewGuid();
            var start = CreateMatchStarted(matchId, 1, researchCaptureEnabled: false);
            var ended = CreateDocument(matchId, "MATCH_ENDED", 5);
            var phase = CreateDocument(matchId, "PHASE_STARTED", 6);

            var result = await repository.AtomicCommitBatchAsync([phase, ended, start]);

            Assert.Equal(3, result.Items.Count);
            Assert.Equal(phase.Id, result.Items[0].Id);
            Assert.Equal(TelemetryWriteStatus.PermanentRejected, result.Items[0].Status);
            Assert.Equal("TELEMETRY_MATCH_TERMINAL_SEQUENCE_EXCEEDED", result.Items[0].Reason);
            Assert.Equal(ended.Id, result.Items[1].Id);
            Assert.Equal(TelemetryWriteStatus.Accepted, result.Items[1].Status);
            Assert.Equal(start.Id, result.Items[2].Id);
            Assert.Equal(TelemetryWriteStatus.Accepted, result.Items[2].Status);

            var stored = await LoadTelemetryEventsAsync(database);
            Assert.Equal([1L, 5L], stored.Select(item => item.EventSequence).Order().ToArray());
        });
    }

    [Fact]
    public async Task AtomicCommitBatchAsync_MatchEndedBelowHighestAcceptedSequence_IsPermanentConflict()
    {
        await WithRepositoryAsync(async (database, repository) =>
        {
            var matchId = Guid.NewGuid();
            var seed = await repository.AtomicCommitBatchAsync(
                [
                    CreateMatchStarted(matchId, 1, researchCaptureEnabled: false),
                    CreateDocument(matchId, "PHASE_STARTED", 6)
                ]);
            Assert.All(seed.Items, item => Assert.Equal(TelemetryWriteStatus.Accepted, item.Status));

            var ended = CreateDocument(matchId, "MATCH_ENDED", 5);
            var result = await repository.AtomicCommitBatchAsync([ended]);

            var ack = Assert.Single(result.Items);
            Assert.Equal(ended.Id, ack.Id);
            Assert.Equal(TelemetryWriteStatus.PermanentRejected, ack.Status);
            Assert.Equal("TELEMETRY_MATCH_TERMINAL_SEQUENCE_CONFLICT", ack.Reason);

            var stored = await LoadTelemetryEventsAsync(database);
            Assert.Equal(2, stored.Count);
            Assert.DoesNotContain(stored, item => item.EventType == "MATCH_ENDED");
        });
    }

    [Fact]
    public async Task AtomicCommitBatchAsync_ConcurrentTerminalAndHigherSequence_NeverAcceptsBoth()
    {
        var mongoUri = Environment.GetEnvironmentVariable(MongoUriEnvironmentVariable);
        // Opt-in integration test. Phase 6R runtime validation supplies ECHO_PHASE6R_MONGO_URI explicitly.
        if (string.IsNullOrWhiteSpace(mongoUri))
        {
            return;
        }

        var databaseName = CreateDatabaseName();
        var clientA = new MongoClient(mongoUri);
        var clientB = new MongoClient(mongoUri);
        try
        {
            var databaseA = clientA.GetDatabase(databaseName);
            var databaseB = clientB.GetDatabase(databaseName);
            var repoA = CreateRepository(databaseA, databaseName);
            var repoB = CreateRepository(databaseB, databaseName);
            await repoA.EnsureIndexesAsync();
            await repoB.EnsureIndexesAsync();

            var matchId = Guid.NewGuid();
            var start = await repoA.AtomicCommitBatchAsync([CreateMatchStarted(matchId, 1, researchCaptureEnabled: false)]);
            Assert.Equal(TelemetryWriteStatus.Accepted, Assert.Single(start.Items).Status);

            var ended = CreateDocument(matchId, "MATCH_ENDED", 5);
            var phase = CreateDocument(matchId, "PHASE_STARTED", 6);
            var results = await Task.WhenAll(
                repoA.AtomicCommitBatchAsync([ended]),
                repoB.AtomicCommitBatchAsync([phase]));
            var endAck = Assert.Single(results[0].Items);
            var phaseAck = Assert.Single(results[1].Items);

            Assert.Equal(1, new[] { endAck, phaseAck }.Count(item => item.Status == TelemetryWriteStatus.Accepted));
            Assert.Equal(1, new[] { endAck, phaseAck }.Count(item => item.Status == TelemetryWriteStatus.PermanentRejected));
            if (endAck.Status == TelemetryWriteStatus.Accepted)
            {
                Assert.Equal("TELEMETRY_MATCH_TERMINAL_SEQUENCE_EXCEEDED", phaseAck.Reason);
            }
            else
            {
                Assert.Equal("TELEMETRY_MATCH_TERMINAL_SEQUENCE_CONFLICT", endAck.Reason);
            }

            var stored = await LoadTelemetryEventsAsync(databaseA);
            Assert.Equal(2, stored.Count);
            Assert.Contains(stored, item => item.EventType == "MATCH_STARTED");
            Assert.False(stored.Any(item => item.EventSequence == 5) && stored.Any(item => item.EventSequence == 6));
        }
        finally
        {
            await clientA.DropDatabaseAsync(databaseName);
        }
    }

    [Fact]
    public async Task AtomicCommitBatchAsync_BootstrapUsesEarliestStartMinimumTerminalAndHighestSequence()
    {
        await WithRepositoryAsync(async (database, repository) =>
        {
            var matchId = Guid.NewGuid();
            var collection = database.GetCollection<TelemetryEventDocument>(TelemetryCollectionName);
            await collection.InsertManyAsync(
                [
                    CreateMatchStarted(matchId, 1, researchCaptureEnabled: true),
                    CreateMatchStarted(matchId, 3, researchCaptureEnabled: false),
                    CreateDocument(matchId, "MATCH_ENDED", 5),
                    CreateDocument(matchId, "MATCH_ENDED", 8)
                ]);

            Assert.Equal(0, await database.GetCollection<TelemetryMatchStateDocument>(MatchStateCollectionName)
                .CountDocumentsAsync(Builders<TelemetryMatchStateDocument>.Filter.Empty));

            var research = await repository.AtomicCommitBatchAsync(
                [CreateDocument(matchId, "MONSTER_ATTACK_RESOLVED", 4)]);
            Assert.Equal(TelemetryWriteStatus.Accepted, Assert.Single(research.Items).Status);

            var phase = await repository.AtomicCommitBatchAsync(
                [CreateDocument(matchId, "PHASE_STARTED", 6)]);
            var phaseAck = Assert.Single(phase.Items);
            Assert.Equal(TelemetryWriteStatus.PermanentRejected, phaseAck.Status);
            Assert.Equal("TELEMETRY_MATCH_TERMINAL_SEQUENCE_EXCEEDED", phaseAck.Reason);

            var state = await database.GetCollection<TelemetryMatchStateDocument>(MatchStateCollectionName)
                .Find(Builders<TelemetryMatchStateDocument>.Filter.Eq(item => item.MatchId, matchId))
                .SingleAsync();
            Assert.True(state.HasAcceptedMatchStarted);
            Assert.True(state.ResearchCaptureEnabled);
            Assert.Equal(5, state.TerminalSequence);
            Assert.Equal(8, state.HighestAcceptedSequence);
        });
    }

    private static async Task WithRepositoryAsync(Func<IMongoDatabase, MongoTelemetryEventRepository, Task> body)
    {
        var mongoUri = Environment.GetEnvironmentVariable(MongoUriEnvironmentVariable);
        // Opt-in integration tests. Phase 6R runtime validation supplies ECHO_PHASE6R_MONGO_URI explicitly.
        if (string.IsNullOrWhiteSpace(mongoUri))
        {
            return;
        }

        var databaseName = CreateDatabaseName();
        var client = new MongoClient(mongoUri);
        try
        {
            var database = client.GetDatabase(databaseName);
            var repository = CreateRepository(database, databaseName);
            await repository.EnsureIndexesAsync();
            await body(database, repository);
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName);
        }
    }

    private static MongoTelemetryEventRepository CreateRepository(IMongoDatabase database, string databaseName) =>
        new(
            database,
            Options.Create(new MongoDbSettings
            {
                DatabaseName = databaseName,
                TelemetryCollectionName = TelemetryCollectionName
            }));

    private static string CreateDatabaseName() =>
        $"echo_protocol_phase6r_it_{Guid.NewGuid():N}";

    private static async Task<IReadOnlyList<TelemetryEventDocument>> LoadTelemetryEventsAsync(IMongoDatabase database) =>
        await database
            .GetCollection<TelemetryEventDocument>(TelemetryCollectionName)
            .Find(Builders<TelemetryEventDocument>.Filter.Empty)
            .ToListAsync();

    private static TelemetryEventDocument CreateMatchStarted(
        Guid matchId,
        long sequence,
        bool researchCaptureEnabled) =>
        CreateDocument(
            matchId,
            "MATCH_STARTED",
            sequence,
            new BsonDocument
            {
                ["context"] = new BsonDocument
                {
                    ["researchCaptureEnabled"] = researchCaptureEnabled
                }
            });

    private static TelemetryEventDocument CreateDocument(
        Guid matchId,
        string eventType,
        long sequence,
        BsonDocument? valueJson = null)
    {
        var id = Guid.NewGuid();
        return new TelemetryEventDocument
        {
            Id = id,
            MatchId = matchId,
            UserId = null,
            EventType = eventType,
            Ts = DateTime.UtcNow,
            EventSequence = sequence,
            ValueJson = valueJson ?? new BsonDocument
            {
                ["context"] = new BsonDocument(),
                ["data"] = new BsonDocument()
            },
            ReasonCode = null,
            SchemaVersion = "1.1",
            SemanticFingerprint = $"{eventType}:{matchId:D}:{sequence}:{id:D}",
            IngestedAt = DateTime.UtcNow
        };
    }
}
