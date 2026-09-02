using EchoProtocol.Api.Configurations;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EchoProtocol.Api.Data.Telemetry;

public sealed class MongoTelemetryEventRepository : ITelemetryEventRepository
{
    private static readonly HashSet<string> ResearchEventTypes =
    [
        "MONSTER_INVESTIGATE_STARTED",
        "MONSTER_INVESTIGATE_RESOLVED",
        "MONSTER_ATTACK_RESOLVED",
        "MONSTER_SEARCH_ENDED",
        "WARDEN_TELEGRAPH_STARTED",
        "WARDEN_ROUTE_ACTION_APPLIED",
        "WARDEN_ROUTE_SAFETY_CHECKED",
        "WARDEN_ROUTE_ACTION_RELEASED"
    ];

    private readonly IMongoCollection<TelemetryEventDocument> _collection;
    private readonly IMongoCollection<TelemetryMatchStateDocument> _matchStateCollection;

    public MongoTelemetryEventRepository(
        IMongoDatabase database,
        IOptions<MongoDbSettings> settings)
    {
        _collection = database.GetCollection<TelemetryEventDocument>(
            settings.Value.TelemetryCollectionName);
        _matchStateCollection = database.GetCollection<TelemetryMatchStateDocument>(
            settings.Value.TelemetryCollectionName + "_match_state");
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        await DropLegacyV1IndexesAsync(cancellationToken);

        var matchTime = new CreateIndexModel<TelemetryEventDocument>(
            Builders<TelemetryEventDocument>.IndexKeys
                .Ascending(e => e.MatchId)
                .Ascending(e => e.Ts),
            new CreateIndexOptions { Name = "IX_Telemetry_MatchId_Ts" });

        var userTime = new CreateIndexModel<TelemetryEventDocument>(
            Builders<TelemetryEventDocument>.IndexKeys
                .Ascending(e => e.UserId)
                .Ascending(e => e.Ts),
            new CreateIndexOptions { Name = "IX_Telemetry_UserId_Ts" });

        var eventType = new CreateIndexModel<TelemetryEventDocument>(
            Builders<TelemetryEventDocument>.IndexKeys.Ascending(e => e.EventType),
            new CreateIndexOptions { Name = "IX_Telemetry_EventType" });

        var matchSequence = new CreateIndexModel<TelemetryEventDocument>(
            Builders<TelemetryEventDocument>.IndexKeys
                .Ascending(e => e.MatchId)
                .Ascending(e => e.EventSequence),
            new CreateIndexOptions<TelemetryEventDocument>
            {
                Name = "UX_Telemetry_MatchId_EventSequence",
                Unique = true,
                PartialFilterExpression = Builders<TelemetryEventDocument>.Filter.Exists(
                    e => e.EventSequence)
            });

        await _collection.Indexes.CreateManyAsync(
            [matchTime, userTime, eventType, matchSequence],
            cancellationToken);
    }

    private async Task DropLegacyV1IndexesAsync(CancellationToken cancellationToken)
    {
        using var cursor = await _collection.Indexes.ListAsync(cancellationToken);
        var existingNames = (await cursor.ToListAsync(cancellationToken))
            .Select(index => index.GetValue("name", string.Empty).AsString)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var legacyName in new[]
                 {
                     "IX_Telemetry_MatchId_OccurredAt",
                     "IX_Telemetry_UserId_OccurredAt"
                 })
        {
            if (existingNames.Contains(legacyName))
            {
                await _collection.Indexes.DropOneAsync(legacyName, cancellationToken);
            }
        }
    }

    public async Task<TelemetryWriteResult> AtomicCommitBatchAsync(
        IReadOnlyCollection<TelemetryEventDocument> events,
        CancellationToken cancellationToken = default)
    {
        var submitted = events.ToArray();
        if (submitted.Length == 0)
        {
            return new TelemetryWriteResult(Array.Empty<TelemetryWriteItemResult>());
        }

        var retryCount = 0;
        while (true)
        {
            try
            {
                using var session = await _collection.Database.Client.StartSessionAsync(cancellationToken: cancellationToken);
                var options = new TransactionOptions(
                    ReadConcern.Snapshot,
                    ReadPreference.Primary,
                    WriteConcern.WMajority);

                var result = await session.WithTransactionAsync(
                    async (_, txCt) =>
                    {
                        var indexed = submitted
                            .Select((document, index) => new { Document = document, Index = index })
                            .ToArray();
                        var decisions = new TelemetryWriteItemResult?[submitted.Length];
                        foreach (var matchGroup in indexed.GroupBy(item => item.Document.MatchId))
                        {
                            foreach (var entry in matchGroup.OrderBy(item => item.Document.EventSequence))
                            {
                                decisions[entry.Index] = await CommitSingleEventAsync(
                                    session,
                                    entry.Document,
                                    txCt);
                            }
                        }

                        return new TelemetryWriteResult(decisions.Select(decision => decision!).ToArray());
                    },
                    options,
                    cancellationToken);

                return result;
            }
            catch (MongoException ex) when (IsTransientTransactionError(ex) && retryCount < 3)
            {
                retryCount++;
                continue;
            }
        }
    }

    public async Task<TelemetryWriteResult> InsertBatchAsync(
        IReadOnlyCollection<TelemetryEventDocument> events,
        CancellationToken cancellationToken)
    {
        return await AtomicCommitBatchAsync(events, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, TelemetryWriteItemResult>> LoadConflictsAsync(
        IReadOnlyCollection<TelemetryEventDocument> events,
        CancellationToken cancellationToken)
    {
        var submitted = events.ToArray();
        if (submitted.Length == 0)
        {
            return new Dictionary<Guid, TelemetryWriteItemResult>();
        }

        var duplicateDocuments = await LoadPotentialConflictDocumentsAsync(submitted, cancellationToken);
        var results = new Dictionary<Guid, TelemetryWriteItemResult>();
        foreach (var item in submitted)
        {
            var existingById = duplicateDocuments.FirstOrDefault(existing => existing.Id == item.Id);
            if (existingById is not null)
            {
                results[item.Id] = existingById.SemanticFingerprint == item.SemanticFingerprint
                    ? new TelemetryWriteItemResult(item.Id, TelemetryWriteStatus.DuplicateAlreadyAccepted)
                    : new TelemetryWriteItemResult(
                        item.Id,
                        TelemetryWriteStatus.IdentityConflict,
                        "TELEMETRY_IDENTITY_CONFLICT");
                continue;
            }

            var existingSequence = duplicateDocuments.FirstOrDefault(existing =>
                existing.MatchId == item.MatchId &&
                existing.EventSequence == item.EventSequence);
            if (existingSequence is not null)
            {
                results[item.Id] = new TelemetryWriteItemResult(
                    item.Id,
                    TelemetryWriteStatus.SequenceConflict,
                    "TELEMETRY_SEQUENCE_CONFLICT");
            }
        }

        return results;
    }

    private async Task<TelemetryWriteItemResult> CommitSingleEventAsync(
        IClientSessionHandle session,
        TelemetryEventDocument document,
        CancellationToken cancellationToken)
    {
        var existingById = await _collection.Find(session, Builders<TelemetryEventDocument>.Filter.Eq(item => item.Id, document.Id)).FirstOrDefaultAsync(cancellationToken);
        if (existingById is not null)
        {
            return existingById.SemanticFingerprint == document.SemanticFingerprint
                ? new TelemetryWriteItemResult(document.Id, TelemetryWriteStatus.DuplicateAlreadyAccepted)
                : new TelemetryWriteItemResult(document.Id, TelemetryWriteStatus.IdentityConflict, "TELEMETRY_IDENTITY_CONFLICT");
        }

        var existingSequence = await _collection.Find(session,
            Builders<TelemetryEventDocument>.Filter.And(
                Builders<TelemetryEventDocument>.Filter.Eq(item => item.MatchId, document.MatchId),
                Builders<TelemetryEventDocument>.Filter.Eq(item => item.EventSequence, document.EventSequence))).FirstOrDefaultAsync(cancellationToken);
        if (existingSequence is not null)
        {
            return new TelemetryWriteItemResult(document.Id, TelemetryWriteStatus.SequenceConflict, "TELEMETRY_SEQUENCE_CONFLICT");
        }

        var matchState = await _matchStateCollection.Find(session,
            Builders<TelemetryMatchStateDocument>.Filter.Eq(item => item.MatchId, document.MatchId)).FirstOrDefaultAsync(cancellationToken);
        if (matchState is null)
        {
            matchState = await BootstrapMatchStateAsync(session, document.MatchId, cancellationToken);
        }

        if (matchState.TerminalSequence.HasValue && document.EventSequence > matchState.TerminalSequence.Value)
        {
            return new TelemetryWriteItemResult(document.Id, TelemetryWriteStatus.PermanentRejected, "TELEMETRY_MATCH_TERMINAL_SEQUENCE_EXCEEDED");
        }

        if (document.EventType == "MATCH_ENDED" &&
            matchState.HighestAcceptedSequence > document.EventSequence)
        {
            return new TelemetryWriteItemResult(document.Id, TelemetryWriteStatus.PermanentRejected, "TELEMETRY_MATCH_TERMINAL_SEQUENCE_CONFLICT");
        }

        if (ResearchEventTypes.Contains(document.EventType) &&
            (!matchState.HasAcceptedMatchStarted || !matchState.ResearchCaptureEnabled))
        {
            return new TelemetryWriteItemResult(document.Id, TelemetryWriteStatus.PermanentRejected, "TELEMETRY_RESEARCH_CAPTURE_NOT_ENABLED");
        }

        await _collection.InsertOneAsync(session, document, cancellationToken: cancellationToken);
        matchState.HighestAcceptedSequence = Math.Max(matchState.HighestAcceptedSequence, document.EventSequence);
        if (document.EventType == "MATCH_STARTED")
        {
            matchState.HasAcceptedMatchStarted = true;
            matchState.ResearchCaptureEnabled = document.ValueJson.TryGetValue("context", out var context) &&
                context.AsBsonDocument.TryGetValue("researchCaptureEnabled", out var capture) &&
                capture.IsBoolean && capture.AsBoolean;
        }
        else if (document.EventType == "MATCH_ENDED")
        {
            matchState.TerminalSequence ??= document.EventSequence;
        }

        matchState.UpdatedAtUtc = DateTime.UtcNow;
        await _matchStateCollection.ReplaceOneAsync(
            session,
            Builders<TelemetryMatchStateDocument>.Filter.Eq(item => item.MatchId, matchState.MatchId),
            matchState,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);

        return new TelemetryWriteItemResult(document.Id, TelemetryWriteStatus.Accepted);
    }

    private async Task<TelemetryMatchStateDocument> BootstrapMatchStateAsync(
        IClientSessionHandle session,
        Guid matchId,
        CancellationToken cancellationToken)
    {
        var rawDocs = await _collection.Find(session,
            Builders<TelemetryEventDocument>.Filter.Eq(item => item.MatchId, matchId)).ToListAsync(cancellationToken);

        var state = new TelemetryMatchStateDocument
        {
            MatchId = matchId,
            HasAcceptedMatchStarted = false,
            ResearchCaptureEnabled = false,
            HighestAcceptedSequence = 0,
            TerminalSequence = null,
            UpdatedAtUtc = DateTime.UtcNow
        };

        state.HighestAcceptedSequence = rawDocs
            .Select(doc => doc.EventSequence)
            .DefaultIfEmpty(0)
            .Max();

        var earliestStart = rawDocs
            .Where(doc => doc.EventType == "MATCH_STARTED")
            .OrderBy(doc => doc.EventSequence)
            .FirstOrDefault();
        if (earliestStart is not null)
        {
            state.HasAcceptedMatchStarted = true;
            if (earliestStart.ValueJson.TryGetValue("context", out var context) &&
                context.AsBsonDocument.TryGetValue("researchCaptureEnabled", out var research) &&
                research.IsBoolean)
            {
                state.ResearchCaptureEnabled = research.AsBoolean;
            }
        }

        var terminals = rawDocs
            .Where(doc => doc.EventType == "MATCH_ENDED")
            .Select(doc => (long?)doc.EventSequence)
            .ToArray();
        state.TerminalSequence = terminals.Length == 0 ? null : terminals.Min();

        await _matchStateCollection.ReplaceOneAsync(
            session,
            Builders<TelemetryMatchStateDocument>.Filter.Eq(item => item.MatchId, matchId),
            state,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);

        return state;
    }

    private static bool IsTransientTransactionError(MongoException ex)
    {
        return ex.HasErrorLabel("TransientTransactionError")
            || ex.HasErrorLabel("UnknownTransactionCommitResult");
    }

    public async Task<IReadOnlyDictionary<Guid, TelemetryMatchBoundary>> LoadMatchBoundariesAsync(
        IReadOnlyCollection<Guid> matchIds,
        CancellationToken cancellationToken)
    {
        if (matchIds.Count == 0)
        {
            return new Dictionary<Guid, TelemetryMatchBoundary>();
        }

        var filterBuilder = Builders<TelemetryEventDocument>.Filter;
        var filter = filterBuilder.And(
            filterBuilder.In(item => item.MatchId, matchIds),
            filterBuilder.In(item => item.EventType, ["MATCH_STARTED", "MATCH_ENDED"]));

        var documents = await _collection.Find(filter).ToListAsync(cancellationToken);
        return documents
            .GroupBy(item => item.MatchId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var matchStarted = group
                        .Where(item => item.EventType == "MATCH_STARTED")
                        .OrderBy(item => item.EventSequence)
                        .FirstOrDefault();
                    var matchEnded = group
                        .Where(item => item.EventType == "MATCH_ENDED")
                        .OrderBy(item => item.EventSequence)
                        .FirstOrDefault();

                    return new TelemetryMatchBoundary(
                        TryReadResearchCaptureEnabled(matchStarted),
                        matchEnded?.EventSequence);
                });
    }

    private static bool? TryReadResearchCaptureEnabled(TelemetryEventDocument? document)
    {
        if (document?.ValueJson is null ||
            !document.ValueJson.TryGetValue("context", out var contextValue) ||
            contextValue is not BsonDocument context ||
            !context.TryGetValue("researchCaptureEnabled", out var enabled) ||
            !enabled.IsBoolean)
        {
            return null;
        }

        return enabled.AsBoolean;
    }

    private async Task<IReadOnlyList<TelemetryEventDocument>> LoadConflictingDocumentsAsync(
        IReadOnlyList<TelemetryEventDocument> submitted,
        IReadOnlyDictionary<int, BulkWriteError> errorsByIndex,
        CancellationToken cancellationToken)
    {
        var conflictCandidates = errorsByIndex
            .Where(pair => pair.Value.Category == ServerErrorCategory.DuplicateKey)
            .Select(pair => submitted[pair.Key])
            .ToArray();
        if (conflictCandidates.Length == 0)
        {
            return [];
        }

        return await LoadPotentialConflictDocumentsAsync(conflictCandidates, cancellationToken);
    }

    private async Task<IReadOnlyList<TelemetryEventDocument>> LoadPotentialConflictDocumentsAsync(
        IReadOnlyCollection<TelemetryEventDocument> candidates,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var filterBuilder = Builders<TelemetryEventDocument>.Filter;
        var idFilter = filterBuilder.In(
            item => item.Id,
            candidates.Select(item => item.Id));
        var sequenceFilters = candidates.Select(item =>
            filterBuilder.And(
                filterBuilder.Eq(existing => existing.MatchId, item.MatchId),
                filterBuilder.Eq(existing => existing.EventSequence, item.EventSequence)));
        var filter = filterBuilder.Or(new[] { idFilter }.Concat(sequenceFilters));

        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }
}
