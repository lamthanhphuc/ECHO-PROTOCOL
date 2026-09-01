using System;
using System.Collections.Generic;

namespace EchoProtocol.Telemetry
{
    public sealed class TelemetryTransportResult
    {
        public TelemetryTransportResult(
            bool hasTrustworthyResponse,
            IReadOnlyList<TelemetryAckItem> acknowledgements,
            string error = null)
        {
            HasTrustworthyResponse = hasTrustworthyResponse;
            Acknowledgements = acknowledgements ?? Array.Empty<TelemetryAckItem>();
            Error = error ?? string.Empty;
        }

        public bool HasTrustworthyResponse { get; }
        public IReadOnlyList<TelemetryAckItem> Acknowledgements { get; }
        public string Error { get; }
    }

    public interface ITelemetryTransport
    {
        void SendBatch(string batchJson, Action<TelemetryTransportResult> callback);
    }

    public sealed class TelemetryBatchSender
    {
        private readonly TelemetryBuffer _buffer;
        private readonly ITelemetryTransport _transport;
        private readonly ITelemetryLocalLog _localLog;
        private readonly int _batchSize;
        private bool _inFlight;

        public TelemetryBatchSender(
            TelemetryBuffer buffer,
            ITelemetryTransport transport,
            int batchSize,
            ITelemetryLocalLog localLog = null)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            if (batchSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(batchSize));
            }

            _batchSize = batchSize;
            _localLog = localLog ?? new NullTelemetryLocalLog();
        }

        public bool IsInFlight => _inFlight;
        public int FlushAttemptCount { get; private set; }
        public int TrustworthyResponseCount { get; private set; }
        public int TransportFailureCount { get; private set; }

        public bool TryFlush(DateTime nowUtc)
        {
            if (_inFlight)
            {
                return false;
            }

            var batch = _buffer.GetReadyBatch(_batchSize, nowUtc);
            if (batch.Count == 0)
            {
                return false;
            }

            var immutableBatch = new List<TelemetryBufferedEvent>(batch);
            var json = TelemetryWireSerializer.SerializeBatch(immutableBatch);
            _inFlight = true;
            FlushAttemptCount++;
            _localLog.Append(
                "BATCH_ATTEMPT",
                null,
                new TelemetryJsonObject().AddInteger("eventCount", immutableBatch.Count).ToJson());

            _transport.SendBatch(json, result =>
            {
                _inFlight = false;
                var completedAt = DateTime.UtcNow;
                if (result != null && result.HasTrustworthyResponse)
                {
                    TrustworthyResponseCount++;
                    _buffer.ApplyAcknowledgements(
                        immutableBatch,
                        result.Acknowledgements,
                        completedAt);
                    _localLog.Append(
                        "BATCH_ACKNOWLEDGED",
                        null,
                        new TelemetryJsonObject()
                            .AddInteger("submitted", immutableBatch.Count)
                            .AddInteger("acknowledged", result.Acknowledgements.Count)
                            .ToJson());
                }
                else
                {
                    TransportFailureCount++;
                    var error = result?.Error ?? "NO_RESPONSE";
                    _buffer.ApplyTransportFailure(immutableBatch, completedAt, error);
                    _localLog.Append(
                        "BATCH_TRANSPORT_FAILURE",
                        null,
                        new TelemetryJsonObject().AddString("error", error).ToJson());
                }
            });

            return true;
        }
    }
}
