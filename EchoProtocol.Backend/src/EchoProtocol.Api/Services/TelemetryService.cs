using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Data.Telemetry;
using EchoProtocol.Api.DTOs.Telemetry;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.Extensions.Options;
using MongoDB.Bson;

namespace EchoProtocol.Api.Services;

public sealed class TelemetryService : ITelemetryService
{
    private readonly ITelemetryEventRepository _repository;
    private readonly IMatchAuthorityService _matchAuthority;
    private readonly MongoDbSettings _settings;

    public TelemetryService(
        ITelemetryEventRepository repository,
        IMatchAuthorityService matchAuthority,
        IOptions<MongoDbSettings> settings)
    {
        _repository = repository;
        _matchAuthority = matchAuthority;
        _settings = settings.Value;
    }

    public async Task<ServiceResult<TelemetryBatchResponse>> IngestBatchAsync(
        TelemetryBatchRequest request,
        Guid authenticatedUserId,
        CancellationToken cancellationToken)
    {
        if (request.Events is null || request.Events.Count == 0)
        {
            return ServiceResult<TelemetryBatchResponse>.Failure(
                "Telemetry batch must contain at least one event",
                ErrorCodes.ValidationError);
        }

        if (request.Events.Count > _settings.MaxBatchSize)
        {
            return ServiceResult<TelemetryBatchResponse>.Failure(
                $"Telemetry batch cannot exceed {_settings.MaxBatchSize} events",
                ErrorCodes.ValidationError);
        }

        if (request.ExtensionData is { Count: > 0 })
        {
            return ServiceResult<TelemetryBatchResponse>.Failure(
                "Telemetry batch contains unsupported fields",
                ErrorCodes.ValidationError);
        }

        var acknowledgements = new TelemetryBatchAckItem?[request.Events.Count];
        var validDocuments = new List<TelemetryEventDocument>(request.Events.Count);
        var validDocumentIndexes = new List<int>(request.Events.Count);
        var nowUtc = DateTime.UtcNow;
        var earliestAcceptedUtc = nowUtc.AddDays(-_settings.MaxEventAgeDays);
        var latestAcceptedUtc = nowUtc.AddMinutes(_settings.MaxFutureSkewMinutes);
        var batchConflictReasons = FindSameBatchConflictReasons(request.Events);
        var storedBoundaries = await _repository.LoadMatchBoundariesAsync(
            request.Events
                .Where(item => item.MatchId != Guid.Empty)
                .Select(item => item.MatchId)
                .Distinct()
                .ToArray(),
            cancellationToken);
        var sameBatchStarts = await FindSameBatchMatchStartsAsync(
            request,
            authenticatedUserId,
            earliestAcceptedUtc,
            latestAcceptedUtc,
            storedBoundaries,
            batchConflictReasons,
            nowUtc,
            cancellationToken);
        var sameBatchTerminals = await FindSameBatchTerminalSequencesAsync(
            request,
            authenticatedUserId,
            earliestAcceptedUtc,
            latestAcceptedUtc,
            storedBoundaries,
            sameBatchStarts,
            batchConflictReasons,
            nowUtc,
            cancellationToken);

        for (var index = 0; index < request.Events.Count; index++)
        {
            var telemetryEvent = request.Events[index];
            var rejectReason = ValidateEnvelope(
                telemetryEvent,
                authenticatedUserId,
                earliestAcceptedUtc,
                latestAcceptedUtc);

            if (rejectReason is null &&
                !await IsAuthorizedAsync(telemetryEvent, authenticatedUserId, cancellationToken))
            {
                rejectReason = ErrorCodes.TelemetryUserMismatch;
            }

            TelemetryValidationResult validation = new(0, false, null, false);
            if (rejectReason is null &&
                !TelemetrySchemaDispatcher.TryValidate(
                    telemetryEvent,
                    ResolveResearchCaptureAllowed(telemetryEvent, storedBoundaries, sameBatchStarts),
                    out validation,
                    out var semanticRejectReason))
            {
                rejectReason = semanticRejectReason;
            }

            if (rejectReason is null &&
                batchConflictReasons.TryGetValue(index, out var batchConflictReason))
            {
                rejectReason = batchConflictReason;
            }

            if (rejectReason is null &&
                validation.EventSequence > 0 &&
                IsBeyondTerminalBoundary(telemetryEvent, validation.EventSequence, storedBoundaries, sameBatchTerminals))
            {
                rejectReason = "TELEMETRY_MATCH_TERMINAL_SEQUENCE_EXCEEDED";
            }

            if (rejectReason is not null)
            {
                acknowledgements[index] = PermanentReject(telemetryEvent.Id, rejectReason);
                continue;
            }

            var parsedTsUtc = ParseValidatedTimestampOrThrow(telemetryEvent.Ts);
            validDocuments.Add(CreateDocument(telemetryEvent, validation.EventSequence, nowUtc, parsedTsUtc));
            validDocumentIndexes.Add(index);
        }

        if (validDocuments.Count > 0)
        {
            var atomicWriteResult = await _repository.AtomicCommitBatchAsync(validDocuments, cancellationToken);
            var writeResultsById = atomicWriteResult.Items.ToDictionary(item => item.Id);
            for (var validIndex = 0; validIndex < validDocuments.Count; validIndex++)
            {
                var document = validDocuments[validIndex];
                var responseIndex = validDocumentIndexes[validIndex];
                acknowledgements[responseIndex] = writeResultsById.TryGetValue(document.Id, out var item)
                    ? MapWriteResult(item)
                    : new TelemetryBatchAckItem
                    {
                        Id = document.Id,
                        Status = TelemetryAckStatuses.TransientFailure,
                        RejectReason = "TELEMETRY_STORAGE_ACK_MISSING"
                    };
            }
        }

        return ServiceResult<TelemetryBatchResponse>.Success(
            new TelemetryBatchResponse
            {
                Items = acknowledgements.Select(item => item!).ToArray()
            },
            "Telemetry batch processed");
    }

    private string? ValidateEnvelope(
        TelemetryEventRequest item,
        Guid authenticatedUserId,
        DateTime earliestAcceptedUtc,
        DateTime latestAcceptedUtc)
    {
        if (item.Id == Guid.Empty || item.MatchId == Guid.Empty)
        {
            return "TELEMETRY_IDENTITY_OR_TIMESTAMP_MISSING";
        }

        if (item.ExtensionData is { Count: > 0 })
        {
            return "TELEMETRY_UNKNOWN_FIELD";
        }

        if (string.IsNullOrWhiteSpace(item.SchemaVersion))
        {
            return ErrorCodes.TelemetrySchemaUnsupported;
        }

        if (!TryParseCanonicalUtcTimestamp(item.Ts, out var parsedTsUtc))
        {
            return "TELEMETRY_TIMESTAMP_NOT_UTC";
        }

        if (parsedTsUtc < earliestAcceptedUtc || parsedTsUtc > latestAcceptedUtc)
        {
            return "TELEMETRY_TIMESTAMP_OUT_OF_RANGE";
        }

        if (string.IsNullOrWhiteSpace(item.EventType))
        {
            return "TELEMETRY_EVENT_TYPE_MISSING";
        }

        if (item.ValueJson.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return "TELEMETRY_VALUE_JSON_INVALID";
        }

        if (Encoding.UTF8.GetByteCount(item.ValueJson.GetRawText()) > _settings.MaxValueJsonBytes)
        {
            return "TELEMETRY_VALUE_JSON_TOO_LARGE";
        }

        return null;
    }

    public static bool TryParseCanonicalUtcTimestamp(JsonElement value, out DateTime utcValue)
    {
        utcValue = default;
        if (value.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var raw = value.GetString();
        if (string.IsNullOrWhiteSpace(raw) || !raw.EndsWith("Z", StringComparison.Ordinal))
        {
            return false;
        }

        if (!DateTimeOffset.TryParseExact(
                raw,
                [
                    "yyyy-MM-dd'T'HH:mm:ss'Z'",
                    "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"
                ],
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return false;
        }

        utcValue = parsed.UtcDateTime;
        return true;
    }

    private static DateTime ParseValidatedTimestampOrThrow(JsonElement value)
    {
        if (!TryParseCanonicalUtcTimestamp(value, out var parsed))
        {
            throw new InvalidOperationException("Telemetry timestamp was not a canonical UTC Z value.");
        }

        return parsed;
    }

    private static Dictionary<int, string> FindSameBatchConflictReasons(IReadOnlyList<TelemetryEventRequest> events)
    {
        var result = new Dictionary<int, string>();
        foreach (var group in events
                     .Select((item, index) => new { item.Id, Index = index })
                     .Where(item => item.Id != Guid.Empty)
                     .GroupBy(item => item.Id)
                     .Where(group => group.Count() > 1))
        {
            foreach (var item in group)
            {
                result[item.Index] = "TELEMETRY_DUPLICATE_ID_IN_BATCH";
            }
        }

        foreach (var group in events
                     .Select((item, index) => new
                     {
                         item.MatchId,
                         Sequence = TryReadEventSequence(item, out var sequence) ? sequence : 0,
                         Index = index
                     })
                     .Where(item => item.MatchId != Guid.Empty && item.Sequence > 0)
                     .GroupBy(item => (item.MatchId, item.Sequence))
                     .Where(group => group.Count() > 1))
        {
            foreach (var item in group)
            {
                result.TryAdd(item.Index, "TELEMETRY_DUPLICATE_SEQUENCE_IN_BATCH");
            }
        }

        return result;
    }

    private async Task<Dictionary<Guid, bool>> FindSameBatchMatchStartsAsync(
        TelemetryBatchRequest request,
        Guid authenticatedUserId,
        DateTime earliestAcceptedUtc,
        DateTime latestAcceptedUtc,
        IReadOnlyDictionary<Guid, TelemetryMatchBoundary> storedBoundaries,
        IReadOnlyDictionary<int, string> batchConflictReasons,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var candidates = new List<BoundaryCandidate>();
        var result = new Dictionary<Guid, bool>();
        for (var index = 0; index < request.Events.Count; index++)
        {
            var telemetryEvent = request.Events[index];
            if (telemetryEvent.EventType != "MATCH_STARTED" ||
                batchConflictReasons.ContainsKey(index) ||
                ValidateEnvelope(telemetryEvent, authenticatedUserId, earliestAcceptedUtc, latestAcceptedUtc) is not null ||
                !await IsAuthorizedAsync(telemetryEvent, authenticatedUserId, cancellationToken) ||
                !TelemetrySchemaDispatcher.TryValidate(
                    telemetryEvent,
                    ResolveResearchCaptureAllowed(telemetryEvent, storedBoundaries, result),
                    out var validation,
                    out _) ||
                !validation.IsMatchStarted ||
                validation.ResearchCaptureEnabled is null)
            {
                continue;
            }

            var parsedTsUtc = ParseValidatedTimestampOrThrow(telemetryEvent.Ts);
            candidates.Add(new BoundaryCandidate(
                index,
                CreateDocument(telemetryEvent, validation.EventSequence, nowUtc, parsedTsUtc),
                validation));
        }

        var conflicts = await _repository.LoadConflictsAsync(
            candidates.Select(item => item.Document).ToArray(),
            cancellationToken);
        foreach (var candidate in candidates)
        {
            if (conflicts.TryGetValue(candidate.Document.Id, out var conflict) &&
                conflict.Status is not TelemetryWriteStatus.DuplicateAlreadyAccepted)
            {
                continue;
            }

            result.TryAdd(
                candidate.Document.MatchId,
                candidate.Validation.ResearchCaptureEnabled!.Value);
        }

        return result;
    }

    private async Task<Dictionary<Guid, long>> FindSameBatchTerminalSequencesAsync(
        TelemetryBatchRequest request,
        Guid authenticatedUserId,
        DateTime earliestAcceptedUtc,
        DateTime latestAcceptedUtc,
        IReadOnlyDictionary<Guid, TelemetryMatchBoundary> storedBoundaries,
        IReadOnlyDictionary<Guid, bool> sameBatchStarts,
        IReadOnlyDictionary<int, string> batchConflictReasons,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var candidates = new List<BoundaryCandidate>();
        var result = new Dictionary<Guid, long>();
        for (var index = 0; index < request.Events.Count; index++)
        {
            var telemetryEvent = request.Events[index];
            if (telemetryEvent.EventType != "MATCH_ENDED" ||
                batchConflictReasons.ContainsKey(index) ||
                ValidateEnvelope(telemetryEvent, authenticatedUserId, earliestAcceptedUtc, latestAcceptedUtc) is not null ||
                !await IsAuthorizedAsync(telemetryEvent, authenticatedUserId, cancellationToken) ||
                !TelemetrySchemaDispatcher.TryValidate(
                    telemetryEvent,
                    ResolveResearchCaptureAllowed(telemetryEvent, storedBoundaries, sameBatchStarts),
                    out var validation,
                    out _) ||
                !validation.IsMatchEnded)
            {
                continue;
            }

            var parsedTsUtc = ParseValidatedTimestampOrThrow(telemetryEvent.Ts);
            candidates.Add(new BoundaryCandidate(
                index,
                CreateDocument(telemetryEvent, validation.EventSequence, nowUtc, parsedTsUtc),
                validation));
        }

        var conflicts = await _repository.LoadConflictsAsync(
            candidates.Select(item => item.Document).ToArray(),
            cancellationToken);
        foreach (var candidate in candidates)
        {
            if (conflicts.TryGetValue(candidate.Document.Id, out var conflict) &&
                conflict.Status is not TelemetryWriteStatus.DuplicateAlreadyAccepted)
            {
                continue;
            }

            if (!result.TryGetValue(candidate.Document.MatchId, out var terminalSequence) ||
                candidate.Validation.EventSequence < terminalSequence)
            {
                result[candidate.Document.MatchId] = candidate.Validation.EventSequence;
            }
        }

        return result;
    }

    private async Task<bool> IsAuthorizedAsync(
        TelemetryEventRequest telemetryEvent,
        Guid authenticatedUserId,
        CancellationToken cancellationToken)
    {
        if (telemetryEvent.UserId.HasValue)
        {
            return telemetryEvent.UserId.Value == authenticatedUserId ||
                   await _matchAuthority.CanSubmitTelemetryAsync(
                       authenticatedUserId,
                       telemetryEvent.MatchId,
                       telemetryEvent.UserId.Value,
                       cancellationToken);
        }

        return await _matchAuthority.CanSubmitSystemTelemetryAsync(
            authenticatedUserId,
            telemetryEvent.MatchId,
            cancellationToken);
    }

    private static bool? ResolveResearchCaptureAllowed(
        TelemetryEventRequest telemetryEvent,
        IReadOnlyDictionary<Guid, TelemetryMatchBoundary> storedBoundaries,
        IReadOnlyDictionary<Guid, bool> sameBatchStarts)
    {
        if (storedBoundaries.TryGetValue(telemetryEvent.MatchId, out var stored) &&
            stored.ResearchCaptureEnabled.HasValue)
        {
            return stored.ResearchCaptureEnabled.Value;
        }

        if (!TryReadEventSequence(telemetryEvent, out var eventSequence))
        {
            return null;
        }

        return sameBatchStarts.TryGetValue(telemetryEvent.MatchId, out var sameBatchEnabled) &&
               eventSequence > 1
            ? sameBatchEnabled
            : null;
    }

    private static bool IsBeyondTerminalBoundary(
        TelemetryEventRequest telemetryEvent,
        long eventSequence,
        IReadOnlyDictionary<Guid, TelemetryMatchBoundary> storedBoundaries,
        IReadOnlyDictionary<Guid, long> sameBatchTerminals)
    {
        var terminalSequence = storedBoundaries.TryGetValue(telemetryEvent.MatchId, out var stored)
            ? stored.TerminalSequence
            : null;
        if (sameBatchTerminals.TryGetValue(telemetryEvent.MatchId, out var sameBatchTerminal) &&
            (!terminalSequence.HasValue || sameBatchTerminal < terminalSequence.Value))
        {
            terminalSequence = sameBatchTerminal;
        }

        return terminalSequence.HasValue &&
               eventSequence > terminalSequence.Value;
    }

    private static bool TryReadEventSequence(TelemetryEventRequest telemetryEvent, out long eventSequence)
    {
        eventSequence = 0;
        return telemetryEvent.ValueJson.ValueKind == System.Text.Json.JsonValueKind.Object &&
               telemetryEvent.ValueJson.TryGetProperty("context", out var context) &&
               context.ValueKind == System.Text.Json.JsonValueKind.Object &&
               context.TryGetProperty("eventSequence", out var sequence) &&
               sequence.ValueKind == System.Text.Json.JsonValueKind.Number &&
               sequence.TryGetInt64(out eventSequence);
    }

    private static TelemetryEventDocument CreateDocument(
        TelemetryEventRequest telemetryEvent,
        long eventSequence,
        DateTime ingestedAtUtc,
        DateTime parsedTsUtc) => new()
    {
        Id = telemetryEvent.Id,
        MatchId = telemetryEvent.MatchId,
        UserId = telemetryEvent.UserId,
        EventType = telemetryEvent.EventType,
        Ts = parsedTsUtc,
        EventSequence = eventSequence,
        ValueJson = BsonDocument.Parse(telemetryEvent.ValueJson.GetRawText()),
        ReasonCode = telemetryEvent.ReasonCode,
        SchemaVersion = telemetryEvent.SchemaVersion,
        SemanticFingerprint = ComputeSemanticFingerprint(telemetryEvent, parsedTsUtc),
        IngestedAt = ingestedAtUtc
    };

    private static TelemetryBatchAckItem PermanentReject(Guid id, string reason) => new()
    {
        Id = id,
        Status = TelemetryAckStatuses.PermanentlyRejected,
        RejectReason = reason
    };

    private static TelemetryBatchAckItem MapWriteResult(TelemetryWriteItemResult item) =>
        item.Status switch
        {
            TelemetryWriteStatus.Accepted => new TelemetryBatchAckItem
            {
                Id = item.Id,
                Status = TelemetryAckStatuses.Accepted
            },
            TelemetryWriteStatus.DuplicateAlreadyAccepted => new TelemetryBatchAckItem
            {
                Id = item.Id,
                Status = TelemetryAckStatuses.DuplicateAlreadyAccepted
            },
            TelemetryWriteStatus.IdentityConflict or TelemetryWriteStatus.SequenceConflict or TelemetryWriteStatus.PermanentRejected =>
                PermanentReject(item.Id, item.Reason ?? "TELEMETRY_IDENTITY_CONFLICT"),
            _ => new TelemetryBatchAckItem
            {
                Id = item.Id,
                Status = TelemetryAckStatuses.TransientFailure,
                RejectReason = item.Reason ?? "TELEMETRY_STORAGE_TRANSIENT_FAILURE"
            }
        };

    private static string ComputeSemanticFingerprint(TelemetryEventRequest item, DateTime parsedTsUtc)
    {
        var canonical = string.Join('\n',
            item.Id.ToString("D"),
            item.MatchId.ToString("D"),
            item.UserId?.ToString("D") ?? "null",
            item.EventType,
            parsedTsUtc.ToUniversalTime().ToString("O"),
            CanonicalizeJson(item.ValueJson),
            item.ReasonCode ?? "null",
            item.SchemaVersion);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string CanonicalizeJson(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonicalJson(writer, element);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteCanonicalJson(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalJson(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonicalJson(writer, item);
                }
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private sealed record BoundaryCandidate(
        int Index,
        TelemetryEventDocument Document,
        TelemetryValidationResult Validation);
}
