using System;
using System.Collections.Generic;

namespace EchoProtocol.Telemetry
{
    public sealed class TelemetryEmissionRequest
    {
        public string SourceOccurrenceKey { get; set; }
        public string EventType { get; set; }
        public Guid? UserId { get; set; }
        public string ReasonCode { get; set; }
        public DateTime OccurredAtUtc { get; set; }
        public TelemetryJsonObject Context { get; set; }
        public TelemetryJsonObject Data { get; set; }
    }

    public sealed class TelemetryEventFactory
    {
        private readonly TelemetrySequenceAllocator _sequenceAllocator;
        private readonly ITelemetryAuthorityContext _authorityContext;
        private readonly ITelemetryProvenanceProvider _provenanceProvider;
        private readonly int _occurrenceCapacity;
        private readonly Dictionary<string, TelemetryEvent> _eventsByOccurrence =
            new Dictionary<string, TelemetryEvent>(StringComparer.Ordinal);

        public TelemetryEventFactory(
            TelemetrySequenceAllocator sequenceAllocator,
            ITelemetryAuthorityContext authorityContext,
            ITelemetryProvenanceProvider provenanceProvider,
            int occurrenceCapacity = 4096)
        {
            _sequenceAllocator = sequenceAllocator ?? throw new ArgumentNullException(nameof(sequenceAllocator));
            _authorityContext = authorityContext ?? throw new ArgumentNullException(nameof(authorityContext));
            _provenanceProvider = provenanceProvider ?? throw new ArgumentNullException(nameof(provenanceProvider));
            if (occurrenceCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(occurrenceCapacity));
            }

            _occurrenceCapacity = occurrenceCapacity;
        }

        public void BeginMatch()
        {
            EnsureAuthority();
            if (!_authorityContext.TryGetMatchId(out var matchId) || matchId == Guid.Empty)
            {
                throw new InvalidOperationException("Authoritative match ID is unavailable.");
            }

            _sequenceAllocator.BeginMatch(matchId);
            _eventsByOccurrence.Clear();
        }

        public TelemetryEvent CreateOrGet(TelemetryEmissionRequest request, out bool created)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            EnsureAuthority();
            ValidateRequest(request);

            if (!_authorityContext.TryGetMatchId(out var currentMatchId)
                || currentMatchId == Guid.Empty
                || currentMatchId != _sequenceAllocator.MatchId)
            {
                throw new InvalidOperationException("Authority match ID does not match the active telemetry match.");
            }

            var occurrenceKey = request.EventType + "|" + request.SourceOccurrenceKey;
            if (_eventsByOccurrence.TryGetValue(occurrenceKey, out var existing))
            {
                created = false;
                return existing;
            }

            if (_eventsByOccurrence.Count >= _occurrenceCapacity)
            {
                throw new InvalidOperationException("Telemetry occurrence identity capacity is exhausted.");
            }

            if (_sequenceAllocator.LastAllocatedSequence == 0
                && request.EventType != TelemetryEventTypes.MatchStarted)
            {
                throw new InvalidOperationException("MATCH_STARTED must be the first telemetry event.");
            }

            if (_sequenceAllocator.LastAllocatedSequence > 0
                && request.EventType == TelemetryEventTypes.MatchStarted)
            {
                throw new InvalidOperationException("MATCH_STARTED may only occur once per match.");
            }

            var provenance = _provenanceProvider.Capture()
                ?? throw new InvalidOperationException("Telemetry provenance is unavailable.");
            if (_authorityContext.AuthorityTick.HasValue && _authorityContext.AuthorityTick.Value < 0)
            {
                throw new InvalidOperationException("Authority tick cannot be negative.");
            }

            var sequence = _sequenceAllocator.Allocate();
            var context = new TelemetryJsonObject()
                .AddInteger("eventSequence", sequence);

            if (_authorityContext.AuthorityTick.HasValue)
            {
                context.AddInteger("authorityTick", _authorityContext.AuthorityTick.Value);
            }
            else
            {
                context.AddNull("authorityTick");
            }

            context
                .AddString("scenarioConfigVersion", provenance.ScenarioConfigVersion)
                .AddString("policyVersion", provenance.PolicyVersion)
                .AddString("configSource", provenance.ConfigSource == TelemetryConfigSource.Fixed ? "FIXED" : "ADAPTIVE");

            TelemetryEventCatalog.TryGetStatus(request.EventType, out var eventStatus);
            if (eventStatus == TelemetryEventStatus.ResearchCapture)
            {
                context.AddBoolean("researchCaptureEnabled", true);
            }

            context.Merge(request.Context);

            var timestamp = request.OccurredAtUtc == default
                ? DateTime.UtcNow
                : request.OccurredAtUtc;
            if (timestamp.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("Telemetry occurrence timestamp must be UTC.", nameof(request));
            }

            var telemetryEvent = new TelemetryEvent(
                Guid.NewGuid(),
                currentMatchId,
                request.UserId,
                request.EventType,
                timestamp,
                context.ToJson(),
                (request.Data ?? new TelemetryJsonObject()).ToJson(),
                request.ReasonCode,
                TelemetrySchemaVersions.CurrentV11);

            _eventsByOccurrence.Add(occurrenceKey, telemetryEvent);
            if (request.EventType == TelemetryEventTypes.MatchEnded)
            {
                _sequenceAllocator.MarkTerminal();
            }

            created = true;
            return telemetryEvent;
        }

        private void EnsureAuthority()
        {
            if (!_authorityContext.HasStateAuthority)
            {
                throw new InvalidOperationException("Only State Authority may create authoritative telemetry.");
            }
        }

        private static void ValidateRequest(TelemetryEmissionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SourceOccurrenceKey))
            {
                throw new ArgumentException("Source occurrence key is required.", nameof(request));
            }

            if (!TelemetryEventCatalog.TryGetStatus(request.EventType, out _))
            {
                throw new ArgumentException("Event type is not emittable in telemetry v1.1.", nameof(request));
            }
        }
    }

    public sealed class TelemetryEmitter
    {
        private readonly TelemetryEventFactory _factory;
        private readonly TelemetryBuffer _buffer;
        private readonly ITelemetryProvenanceProvider _provenanceProvider;
        private readonly ITelemetryLocalLog _localLog;
        private readonly HashSet<Guid> _failedEnqueueIds = new HashSet<Guid>();

        public TelemetryEmitter(
            TelemetryEventFactory factory,
            TelemetryBuffer buffer,
            ITelemetryProvenanceProvider provenanceProvider,
            ITelemetryLocalLog localLog = null)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _provenanceProvider = provenanceProvider ?? throw new ArgumentNullException(nameof(provenanceProvider));
            _localLog = localLog ?? new NullTelemetryLocalLog();
        }

        public bool TryEmit(
            TelemetryEmissionRequest request,
            out TelemetryEvent telemetryEvent,
            out TelemetryBufferFailureReason failureReason)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!TelemetryEventCatalog.TryGetStatus(request.EventType, out var status))
            {
                throw new ArgumentException("Event type is not emittable in telemetry v1.1.", nameof(request));
            }

            var provenance = _provenanceProvider.Capture();
            if (status == TelemetryEventStatus.ResearchCapture
                && (provenance == null || !provenance.ResearchCaptureEnabled))
            {
                throw new InvalidOperationException("Research telemetry is disabled for the current match.");
            }

            telemetryEvent = _factory.CreateOrGet(request, out var created);
            if (!created)
            {
                if (_failedEnqueueIds.Contains(telemetryEvent.Id))
                {
                    failureReason = TelemetryBufferFailureReason.BufferCapacityExceeded;
                    return false;
                }

                failureReason = TelemetryBufferFailureReason.None;
                return true;
            }

            var serialized = TelemetryWireSerializer.SerializeEvent(telemetryEvent);
            if (!_buffer.TryEnqueue(telemetryEvent, serialized, DateTime.UtcNow, out failureReason))
            {
                _failedEnqueueIds.Add(telemetryEvent.Id);
                _localLog.Append(
                    "BUFFER_REJECTED",
                    telemetryEvent.Id,
                    new TelemetryJsonObject().AddString("reason", failureReason.ToString()).ToJson());
                return false;
            }

            _localLog.Append("EVENT_CREATED", telemetryEvent.Id, serialized);
            return true;
        }
    }
}
