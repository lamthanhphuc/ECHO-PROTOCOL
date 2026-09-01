using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoProtocol.Telemetry.Harness
{
    [AddComponentMenu("Echo Protocol/Telemetry/Telemetry Acceptance Harness")]
    public sealed class TelemetryAcceptanceHarnessBehaviour : MonoBehaviour
    {
        [SerializeField] private bool runOnStart;

        public bool RunOnStart
        {
            get => runOnStart;
            set => runOnStart = value;
        }

        private void Start()
        {
            if (runOnStart)
            {
                RunAcceptanceHarness();
            }
        }

        [ContextMenu("Run Telemetry Acceptance Harness")]
        public void RunAcceptanceHarness()
        {
            try
            {
                var result = TelemetryAcceptanceHarness.Run();
                Debug.Log(
                    $"TEL-HARNESS|result=PASS|created={result.CreatedCount}|acked={result.AcknowledgedCount}|" +
                    $"lastSequence={result.LastSequence}|pending={result.PendingCount}");
            }
            catch (Exception exception)
            {
                Debug.LogError("TEL-HARNESS|result=FAIL|reason=" + exception.Message);
            }
        }
    }

    public sealed class TelemetryAcceptanceHarnessResult
    {
        public int CreatedCount { get; set; }
        public int AcknowledgedCount { get; set; }
        public long LastSequence { get; set; }
        public int PendingCount { get; set; }
    }

    public static class TelemetryAcceptanceHarness
    {
        public static TelemetryAcceptanceHarnessResult Run()
        {
            var matchId = Guid.NewGuid();
            var authority = new HarnessAuthorityContext(matchId);
            var provenance = new HarnessProvenanceProvider();
            var allocator = new TelemetrySequenceAllocator();
            var factory = new TelemetryEventFactory(allocator, authority, provenance);
            var buffer = new TelemetryBuffer(16);
            var emitter = new TelemetryEmitter(factory, buffer, provenance);
            var matchAdapter = new MatchTelemetryAdapter(emitter);
            var transport = new AcceptAllHarnessTransport();
            var sender = new TelemetryBatchSender(buffer, transport, 16);

            factory.BeginMatch();
            var createdCount = 0;
            authority.AuthorityTickValue = null;
            RequireEmission(matchAdapter.EmitMatchStarted(
                "harness-match-start", DateTime.UtcNow, "TELEMETRY_HARNESS", 1,
                Application.version, "HARNESS-1", "HARNESS-1", false,
                out _, out var failure), failure, TelemetryEventTypes.MatchStarted);
            createdCount++;

            authority.AuthorityTickValue = 101;
            RequireEmission(matchAdapter.EmitPhaseStarted(
                "harness-phase-start", DateTime.UtcNow, "CORE_COLLECTION",
                out _, out failure), failure, TelemetryEventTypes.PhaseStarted);
            createdCount++;

            authority.AuthorityTickValue = 102;
            RequireEmission(matchAdapter.EmitPhaseCompleted(
                "harness-phase-complete", DateTime.UtcNow, "CORE_COLLECTION",
                out _, out failure, 1.0, "OBJECTIVE_COMPLETED"),
                failure, TelemetryEventTypes.PhaseCompleted);
            createdCount++;

            authority.AuthorityTickValue = 103;
            RequireEmission(matchAdapter.EmitMatchEnded(
                "harness-match-end", DateTime.UtcNow, "SUCCESS", 1.0, 1, "TEAM_ESCAPED",
                out _, out failure), failure, TelemetryEventTypes.MatchEnded);
            createdCount++;

            if (!sender.TryFlush(DateTime.UtcNow))
            {
                throw new InvalidOperationException("Harness batch did not flush.");
            }

            if (buffer.PendingCount != 0 || allocator.LastAllocatedSequence != createdCount)
            {
                throw new InvalidOperationException("Harness acknowledgement or sequence invariant failed.");
            }

            return new TelemetryAcceptanceHarnessResult
            {
                CreatedCount = createdCount,
                AcknowledgedCount = transport.AcknowledgedCount,
                LastSequence = allocator.LastAllocatedSequence,
                PendingCount = buffer.PendingCount
            };
        }

        private static void RequireEmission(
            bool emitted,
            TelemetryBufferFailureReason failure,
            string eventType)
        {
            if (!emitted || failure != TelemetryBufferFailureReason.None)
            {
                throw new InvalidOperationException("Harness emission failed for " + eventType);
            }
        }

        private sealed class HarnessAuthorityContext : ITelemetryAuthorityContext
        {
            private readonly Guid _matchId;

            public HarnessAuthorityContext(Guid matchId)
            {
                _matchId = matchId;
            }

            public bool HasStateAuthority => true;
            public long? AuthorityTick => AuthorityTickValue;
            public long? AuthorityTickValue { get; set; }

            public bool TryGetMatchId(out Guid matchId)
            {
                matchId = _matchId;
                return true;
            }
        }

        private sealed class HarnessProvenanceProvider : ITelemetryProvenanceProvider
        {
            public TelemetryProvenanceSnapshot Capture()
            {
                return new TelemetryProvenanceSnapshot(
                    "HARNESS-SCENARIO-1",
                    "HARNESS-POLICY-1",
                    TelemetryConfigSource.Fixed,
                    false);
            }
        }

        private sealed class AcceptAllHarnessTransport : ITelemetryTransport
        {
            public int AcknowledgedCount { get; private set; }

            public void SendBatch(string batchJson, Action<TelemetryTransportResult> callback)
            {
                var acknowledgements = new List<TelemetryAckItem>();
                var idMarker = "\"id\":\"";
                var offset = 0;
                while ((offset = batchJson.IndexOf(idMarker, offset, StringComparison.Ordinal)) >= 0)
                {
                    offset += idMarker.Length;
                    var end = batchJson.IndexOf('"', offset);
                    if (end < 0 || !Guid.TryParse(batchJson.Substring(offset, end - offset), out var id))
                    {
                        break;
                    }

                    acknowledgements.Add(new TelemetryAckItem(id, TelemetryAckStatus.Accepted));
                    offset = end + 1;
                }

                AcknowledgedCount += acknowledgements.Count;
                callback?.Invoke(new TelemetryTransportResult(true, acknowledgements));
            }
        }
    }
}
