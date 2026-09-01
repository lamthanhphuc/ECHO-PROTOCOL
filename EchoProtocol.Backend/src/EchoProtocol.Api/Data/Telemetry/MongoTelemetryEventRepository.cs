using EchoProtocol.Api.Configurations;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace EchoProtocol.Api.Data.Telemetry;

public sealed class MongoTelemetryEventRepository : ITelemetryEventRepository
{
    private readonly IMongoCollection<TelemetryEventDocument> _collection;

    public MongoTelemetryEventRepository(
        IMongoDatabase database,
        IOptions<MongoDbSettings> settings)
    {
        _collection = database.GetCollection<TelemetryEventDocument>(
            settings.Value.TelemetryCollectionName);
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

    public async Task<TelemetryWriteResult> InsertBatchAsync(
        IReadOnlyCollection<TelemetryEventDocument> events,
        CancellationToken cancellationToken)
    {
        try
        {
            await _collection.InsertManyAsync(
                events,
                new InsertManyOptions { IsOrdered = false },
                cancellationToken);

            return new TelemetryWriteResult(events
                .Select(item => new TelemetryWriteItemResult(
                    item.Id,
                    TelemetryWriteStatus.Accepted))
                .ToArray());
        }
        catch (MongoBulkWriteException<TelemetryEventDocument> ex)
        {
            var submitted = events.ToArray();
            var errorsByIndex = ex.WriteErrors.ToDictionary(error => error.Index);
            var duplicateDocuments = await LoadConflictingDocumentsAsync(
                submitted,
                errorsByIndex,
                cancellationToken);

            var results = new List<TelemetryWriteItemResult>(submitted.Length);
            for (var index = 0; index < submitted.Length; index++)
            {
                var item = submitted[index];
                if (!errorsByIndex.TryGetValue(index, out var writeError))
                {
                    results.Add(new TelemetryWriteItemResult(
                        item.Id,
                        TelemetryWriteStatus.Accepted));
                    continue;
                }

                if (writeError.Category != ServerErrorCategory.DuplicateKey)
                {
                    results.Add(new TelemetryWriteItemResult(
                        item.Id,
                        TelemetryWriteStatus.TransientFailure,
                        "MONGO_WRITE_FAILED"));
                    continue;
                }

                var existingById = duplicateDocuments.FirstOrDefault(existing => existing.Id == item.Id);
                if (existingById is not null)
                {
                    results.Add(existingById.SemanticFingerprint == item.SemanticFingerprint
                        ? new TelemetryWriteItemResult(
                            item.Id,
                            TelemetryWriteStatus.DuplicateAlreadyAccepted)
                        : new TelemetryWriteItemResult(
                            item.Id,
                            TelemetryWriteStatus.IdentityConflict,
                            "TELEMETRY_IDENTITY_CONFLICT"));
                    continue;
                }

                var existingSequence = duplicateDocuments.FirstOrDefault(existing =>
                    existing.MatchId == item.MatchId &&
                    existing.EventSequence == item.EventSequence);
                results.Add(existingSequence is not null
                    ? new TelemetryWriteItemResult(
                        item.Id,
                        TelemetryWriteStatus.SequenceConflict,
                        "TELEMETRY_SEQUENCE_CONFLICT")
                    : new TelemetryWriteItemResult(
                        item.Id,
                        TelemetryWriteStatus.TransientFailure,
                        "MONGO_DUPLICATE_CAUSE_UNRESOLVED"));
            }

            return new TelemetryWriteResult(results);
        }
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

        var filterBuilder = Builders<TelemetryEventDocument>.Filter;
        var idFilter = filterBuilder.In(
            item => item.Id,
            conflictCandidates.Select(item => item.Id));
        var sequenceFilters = conflictCandidates.Select(item =>
            filterBuilder.And(
                filterBuilder.Eq(existing => existing.MatchId, item.MatchId),
                filterBuilder.Eq(existing => existing.EventSequence, item.EventSequence)));
        var filter = filterBuilder.Or(new[] { idFilter }.Concat(sequenceFilters));

        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }
}
