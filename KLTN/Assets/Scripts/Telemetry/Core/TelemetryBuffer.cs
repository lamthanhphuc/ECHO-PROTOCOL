using System;
using System.Collections.Generic;

namespace EchoProtocol.Telemetry
{
    public sealed class TelemetryRetryPolicy
    {
        public TelemetryRetryPolicy(
            int maxAttempts = 6,
            double initialDelaySeconds = 2d,
            double maximumDelaySeconds = 30d)
        {
            if (maxAttempts < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAttempts));
            }

            if (initialDelaySeconds <= 0d || maximumDelaySeconds < initialDelaySeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(initialDelaySeconds));
            }

            MaxAttempts = maxAttempts;
            InitialDelaySeconds = initialDelaySeconds;
            MaximumDelaySeconds = maximumDelaySeconds;
        }

        public int MaxAttempts { get; }
        public double InitialDelaySeconds { get; }
        public double MaximumDelaySeconds { get; }

        public TimeSpan GetDelay(int attemptCount)
        {
            var exponent = Math.Max(0, attemptCount - 1);
            var seconds = InitialDelaySeconds * Math.Pow(2d, exponent);
            return TimeSpan.FromSeconds(Math.Min(seconds, MaximumDelaySeconds));
        }
    }

    public sealed class TelemetryBufferedEvent
    {
        internal TelemetryBufferedEvent(
            TelemetryEvent telemetryEvent,
            string serializedJson,
            DateTime enqueuedAtUtc)
        {
            Event = telemetryEvent;
            SerializedJson = serializedJson;
            EnqueuedAtUtc = enqueuedAtUtc;
            NextAttemptAtUtc = enqueuedAtUtc;
        }

        public TelemetryEvent Event { get; }
        public string SerializedJson { get; }
        public DateTime EnqueuedAtUtc { get; }
        public DateTime NextAttemptAtUtc { get; internal set; }
        public int AttemptCount { get; internal set; }
    }

    public sealed class TelemetryQuarantinedEvent
    {
        internal TelemetryQuarantinedEvent(
            TelemetryBufferedEvent bufferedEvent,
            string reason,
            TelemetryBufferFailureReason failureReason)
        {
            BufferedEvent = bufferedEvent;
            Reason = reason ?? string.Empty;
            FailureReason = failureReason;
        }

        public TelemetryBufferedEvent BufferedEvent { get; }
        public string Reason { get; }
        public TelemetryBufferFailureReason FailureReason { get; }
    }

    public sealed class TelemetryAckItem
    {
        public TelemetryAckItem(Guid id, TelemetryAckStatus status, string rejectReason = null)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Acknowledgement event ID is required.", nameof(id));
            }

            Id = id;
            Status = status;
            RejectReason = rejectReason;
        }

        public Guid Id { get; }
        public TelemetryAckStatus Status { get; }
        public string RejectReason { get; }
    }

    public sealed class TelemetryBuffer
    {
        private readonly int _capacity;
        private readonly TelemetryRetryPolicy _retryPolicy;
        private readonly List<TelemetryBufferedEvent> _pending = new List<TelemetryBufferedEvent>();
        private readonly Dictionary<Guid, TelemetryBufferedEvent> _pendingById =
            new Dictionary<Guid, TelemetryBufferedEvent>();
        private readonly List<TelemetryQuarantinedEvent> _quarantine =
            new List<TelemetryQuarantinedEvent>();

        public TelemetryBuffer(int capacity, TelemetryRetryPolicy retryPolicy = null)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _capacity = capacity;
            _retryPolicy = retryPolicy ?? new TelemetryRetryPolicy();
        }

        public int PendingCount => _pending.Count;
        public int QuarantinedCount => _quarantine.Count;
        public int BufferOverflowCount { get; private set; }
        public int RetryExhaustedCount { get; private set; }
        public int DuplicateEnqueueCount { get; private set; }
        public IReadOnlyList<TelemetryQuarantinedEvent> Quarantined => _quarantine;

        public bool TryEnqueue(
            TelemetryEvent telemetryEvent,
            string serializedJson,
            DateTime enqueuedAtUtc,
            out TelemetryBufferFailureReason failureReason)
        {
            if (telemetryEvent == null)
            {
                throw new ArgumentNullException(nameof(telemetryEvent));
            }

            if (string.IsNullOrWhiteSpace(serializedJson))
            {
                failureReason = TelemetryBufferFailureReason.SerializationFailed;
                return false;
            }

            if (enqueuedAtUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("Enqueue timestamp must be UTC.", nameof(enqueuedAtUtc));
            }

            if (_pendingById.ContainsKey(telemetryEvent.Id))
            {
                DuplicateEnqueueCount++;
                failureReason = TelemetryBufferFailureReason.None;
                return true;
            }

            if (_pending.Count >= _capacity)
            {
                BufferOverflowCount++;
                failureReason = TelemetryBufferFailureReason.BufferCapacityExceeded;
                return false;
            }

            var bufferedEvent = new TelemetryBufferedEvent(telemetryEvent, serializedJson, enqueuedAtUtc);
            _pending.Add(bufferedEvent);
            _pendingById.Add(telemetryEvent.Id, bufferedEvent);
            failureReason = TelemetryBufferFailureReason.None;
            return true;
        }

        public IReadOnlyList<TelemetryBufferedEvent> GetReadyBatch(int maximumCount, DateTime nowUtc)
        {
            if (maximumCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumCount));
            }

            var result = new List<TelemetryBufferedEvent>(Math.Min(maximumCount, _pending.Count));
            foreach (var item in _pending)
            {
                if (item.NextAttemptAtUtc <= nowUtc)
                {
                    result.Add(item);
                    if (result.Count == maximumCount)
                    {
                        break;
                    }
                }
            }

            return result;
        }

        public void ApplyAcknowledgements(
            IReadOnlyList<TelemetryBufferedEvent> submitted,
            IReadOnlyList<TelemetryAckItem> acknowledgements,
            DateTime nowUtc,
            string missingAcknowledgementReason = "NOT_ACKNOWLEDGED")
        {
            if (submitted == null)
            {
                throw new ArgumentNullException(nameof(submitted));
            }

            var byId = new Dictionary<Guid, TelemetryAckItem>();
            if (acknowledgements != null)
            {
                foreach (var acknowledgement in acknowledgements)
                {
                    if (acknowledgement != null)
                    {
                        byId[acknowledgement.Id] = acknowledgement;
                    }
                }
            }

            foreach (var submittedEvent in submitted)
            {
                if (!_pendingById.ContainsKey(submittedEvent.Event.Id))
                {
                    continue;
                }

                if (!byId.TryGetValue(submittedEvent.Event.Id, out var acknowledgement))
                {
                    ScheduleRetryOrQuarantine(submittedEvent, nowUtc, missingAcknowledgementReason);
                    continue;
                }

                switch (acknowledgement.Status)
                {
                    case TelemetryAckStatus.Accepted:
                    case TelemetryAckStatus.DuplicateAlreadyAccepted:
                        Remove(submittedEvent);
                        break;
                    case TelemetryAckStatus.PermanentlyRejected:
                        Quarantine(submittedEvent, acknowledgement.RejectReason, TelemetryBufferFailureReason.None);
                        break;
                    case TelemetryAckStatus.TransientFailure:
                        ScheduleRetryOrQuarantine(submittedEvent, nowUtc, acknowledgement.RejectReason);
                        break;
                    default:
                        ScheduleRetryOrQuarantine(submittedEvent, nowUtc, "UNKNOWN_ACK_STATUS");
                        break;
                }
            }
        }

        public void ApplyTransportFailure(
            IReadOnlyList<TelemetryBufferedEvent> submitted,
            DateTime nowUtc,
            string reason)
        {
            if (submitted == null)
            {
                return;
            }

            foreach (var submittedEvent in submitted)
            {
                if (_pendingById.ContainsKey(submittedEvent.Event.Id))
                {
                    ScheduleRetryOrQuarantine(submittedEvent, nowUtc, reason);
                }
            }
        }

        private void ScheduleRetryOrQuarantine(
            TelemetryBufferedEvent bufferedEvent,
            DateTime nowUtc,
            string reason)
        {
            bufferedEvent.AttemptCount++;
            if (bufferedEvent.AttemptCount >= _retryPolicy.MaxAttempts)
            {
                RetryExhaustedCount++;
                Quarantine(bufferedEvent, reason, TelemetryBufferFailureReason.RetryExhausted);
                return;
            }

            bufferedEvent.NextAttemptAtUtc = nowUtc + _retryPolicy.GetDelay(bufferedEvent.AttemptCount);
        }

        private void Quarantine(
            TelemetryBufferedEvent bufferedEvent,
            string reason,
            TelemetryBufferFailureReason failureReason)
        {
            Remove(bufferedEvent);
            _quarantine.Add(new TelemetryQuarantinedEvent(bufferedEvent, reason, failureReason));
        }

        private void Remove(TelemetryBufferedEvent bufferedEvent)
        {
            _pending.Remove(bufferedEvent);
            _pendingById.Remove(bufferedEvent.Event.Id);
        }
    }
}
