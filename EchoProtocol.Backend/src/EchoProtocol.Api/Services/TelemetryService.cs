using System.Security.Cryptography;
using System.Text;
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

        var acknowledgements = new TelemetryBatchAckItem?[request.Events.Count];
        var validDocuments = new List<TelemetryEventDocument>(request.Events.Count);
        var validDocumentIndexes = new List<int>(request.Events.Count);
        var idsInBatch = new HashSet<Guid>();
        var sequencesInBatch = new HashSet<(Guid MatchId, long Sequence)>();
        var nowUtc = DateTime.UtcNow;
        var earliestAcceptedUtc = nowUtc.AddDays(-_settings.MaxEventAgeDays);
        var latestAcceptedUtc = nowUtc.AddMinutes(_settings.MaxFutureSkewMinutes);

        for (var index = 0; index < request.Events.Count; index++)
        {
            var telemetryEvent = request.Events[index];
            var rejectReason = ValidateEnvelope(
                telemetryEvent,
                authenticatedUserId,
                earliestAcceptedUtc,
                latestAcceptedUtc);

            if (rejectReason is null
                && telemetryEvent.UserId.HasValue
                && telemetryEvent.UserId.Value != authenticatedUserId
                && !await _matchAuthority.CanSubmitTelemetryAsync(
                    authenticatedUserId,
                    telemetryEvent.MatchId,
                    telemetryEvent.UserId.Value,
                    cancellationToken))
            {
                rejectReason = ErrorCodes.TelemetryUserMismatch;
            }
            else if (rejectReason is null
                && !telemetryEvent.UserId.HasValue
                && !await _matchAuthority.CanSubmitSystemTelemetryAsync(
                    authenticatedUserId,
                    telemetryEvent.MatchId,
                    cancellationToken))
            {
                rejectReason = ErrorCodes.TelemetryUserMismatch;
            }

            long eventSequence = 0;
            if (rejectReason is null &&
                !TelemetryV11Validator.TryValidate(
                    telemetryEvent,
                    out eventSequence,
                    out var semanticRejectReason))
            {
                rejectReason = semanticRejectReason;
            }

            if (rejectReason is null && !idsInBatch.Add(telemetryEvent.Id))
            {
                rejectReason = "TELEMETRY_DUPLICATE_ID_IN_BATCH";
            }

            if (rejectReason is null &&
                !sequencesInBatch.Add((telemetryEvent.MatchId, eventSequence)))
            {
                rejectReason = "TELEMETRY_DUPLICATE_SEQUENCE_IN_BATCH";
            }

            if (rejectReason is not null)
            {
                acknowledgements[index] = PermanentReject(telemetryEvent.Id, rejectReason);
                continue;
            }

            validDocuments.Add(new TelemetryEventDocument
            {
                Id = telemetryEvent.Id,
                MatchId = telemetryEvent.MatchId,
                UserId = telemetryEvent.UserId,
                EventType = telemetryEvent.EventType,
                Ts = telemetryEvent.Ts.ToUniversalTime(),
                EventSequence = eventSequence,
                ValueJson = BsonDocument.Parse(telemetryEvent.ValueJson.GetRawText()),
                ReasonCode = telemetryEvent.ReasonCode,
                SchemaVersion = telemetryEvent.SchemaVersion,
                SemanticFingerprint = ComputeSemanticFingerprint(telemetryEvent),
                IngestedAt = nowUtc
            });
            validDocumentIndexes.Add(index);
        }

        if (validDocuments.Count > 0)
        {
            var writeResult = await _repository.InsertBatchAsync(validDocuments, cancellationToken);
            var writeResultsById = writeResult.Items.ToDictionary(item => item.Id);
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
        if (item.Id == Guid.Empty || item.MatchId == Guid.Empty || item.Ts == default)
        {
            return "TELEMETRY_IDENTITY_OR_TIMESTAMP_MISSING";
        }

        if (!string.Equals(
                item.SchemaVersion,
                _settings.SupportedSchemaVersion,
                StringComparison.Ordinal))
        {
            return ErrorCodes.TelemetrySchemaUnsupported;
        }

        if (item.Ts.Kind != DateTimeKind.Utc)
        {
            return "TELEMETRY_TIMESTAMP_NOT_UTC";
        }

        if (item.Ts < earliestAcceptedUtc || item.Ts > latestAcceptedUtc)
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
            TelemetryWriteStatus.IdentityConflict or TelemetryWriteStatus.SequenceConflict =>
                PermanentReject(item.Id, item.Reason ?? "TELEMETRY_IDENTITY_CONFLICT"),
            _ => new TelemetryBatchAckItem
            {
                Id = item.Id,
                Status = TelemetryAckStatuses.TransientFailure,
                RejectReason = item.Reason ?? "TELEMETRY_STORAGE_TRANSIENT_FAILURE"
            }
        };

    private static string ComputeSemanticFingerprint(TelemetryEventRequest item)
    {
        var canonical = string.Join('\n',
            item.Id.ToString("D"),
            item.MatchId.ToString("D"),
            item.UserId?.ToString("D") ?? "null",
            item.EventType,
            item.Ts.ToUniversalTime().ToString("O"),
            item.ValueJson.GetRawText(),
            item.ReasonCode ?? "null",
            item.SchemaVersion);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
