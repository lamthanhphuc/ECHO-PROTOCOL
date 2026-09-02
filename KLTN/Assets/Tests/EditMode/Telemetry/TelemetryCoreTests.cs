using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace EchoProtocol.Telemetry.Tests
{
    public sealed class TelemetryCoreTests
    {
        [Test]
        public void SequenceAllocator_StartsAtOneAndNeverAllocatesAfterTerminal()
        {
            var allocator = new TelemetrySequenceAllocator();
            allocator.BeginMatch(Guid.NewGuid());

            Assert.That(allocator.Allocate(), Is.EqualTo(1));
            Assert.That(allocator.Allocate(), Is.EqualTo(2));
            allocator.MarkTerminal();

            Assert.That(allocator.LastAllocatedSequence, Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => allocator.Allocate());
        }

        [Test]
        public void EventFactory_RequiresMatchStartedAsFirstEvent()
        {
            var fixture = new Fixture();
            fixture.Factory.BeginMatch();

            Assert.Throws<InvalidOperationException>(() => fixture.Factory.CreateOrGet(
                fixture.Request(TelemetryEventTypes.PhaseStarted, "phase-before-match"),
                out _));
        }

        [Test]
        public void EventFactory_DuplicateOccurrenceReturnsImmutableSameEvent()
        {
            var fixture = new Fixture();
            fixture.Factory.BeginMatch();
            var request = fixture.Request(TelemetryEventTypes.MatchStarted, "match-start");

            var first = fixture.Factory.CreateOrGet(request, out var firstCreated);
            var retry = fixture.Factory.CreateOrGet(request, out var retryCreated);

            Assert.That(firstCreated, Is.True);
            Assert.That(retryCreated, Is.False);
            Assert.That(retry, Is.SameAs(first));
            Assert.That(retry.Id, Is.EqualTo(first.Id));
            Assert.That(retry.TimestampUtc, Is.EqualTo(first.TimestampUtc));
            StringAssert.Contains("\"eventSequence\":1", first.ContextJson);
        }

        [Test]
        public void EventFactory_ProxyCannotCreateAuthoritativeTelemetry()
        {
            var fixture = new Fixture(hasAuthority: false);
            Assert.Throws<InvalidOperationException>(() => fixture.Factory.BeginMatch());
        }

        [Test]
        public void Emitter_RejectsResearchEventBeforeSequenceAllocationWhenCaptureDisabled()
        {
            var fixture = new Fixture(researchCaptureEnabled: false);
            fixture.Factory.BeginMatch();
            fixture.Emit(TelemetryEventTypes.MatchStarted, "start");
            var before = fixture.Allocator.LastAllocatedSequence;

            Assert.Throws<InvalidOperationException>(() => fixture.Emitter.TryEmit(
                fixture.Request(TelemetryEventTypes.MonsterSearchEnded, "research-disabled"),
                out _,
                out _));
            Assert.That(fixture.Allocator.LastAllocatedSequence, Is.EqualTo(before));
        }

        [Test]
        public void WireSerializer_UsesCanonicalV11FieldNamesAndStringVersion()
        {
            var fixture = new Fixture();
            fixture.Factory.BeginMatch();
            var telemetryEvent = fixture.Factory.CreateOrGet(
                fixture.Request(TelemetryEventTypes.MatchStarted, "start"),
                out _);

            var json = TelemetryWireSerializer.SerializeEvent(telemetryEvent);

            StringAssert.Contains("\"id\":", json);
            StringAssert.Contains("\"ts\":", json);
            StringAssert.Contains("\"valueJson\":{\"context\":", json);
            StringAssert.Contains("\"schemaVersion\":\"1.1\"", json);
            StringAssert.DoesNotContain("\"eventId\":", json);
            StringAssert.DoesNotContain("\"occurredAt\":", json);
        }

        [Test]
        public void JsonObject_RejectsDuplicateFieldsAndNonFiniteNumbers()
        {
            var json = new TelemetryJsonObject().AddString("phase", "ONE");

            Assert.Throws<InvalidOperationException>(() => json.AddString("phase", "TWO"));
            Assert.Throws<ArgumentOutOfRangeException>(() => json.AddNumber("bad", double.NaN));
        }

        [Test]
        public void Buffer_OverflowDoesNotEvictExistingEvent()
        {
            var fixture = new Fixture(bufferCapacity: 1);
            fixture.Factory.BeginMatch();
            Assert.That(fixture.Emit(TelemetryEventTypes.MatchStarted, "start"), Is.True);

            var second = fixture.Emitter.TryEmit(
                fixture.Request(TelemetryEventTypes.PhaseStarted, "phase"),
                out var failedEvent,
                out var failure);

            Assert.That(second, Is.False);
            Assert.That(failure, Is.EqualTo(TelemetryBufferFailureReason.BufferCapacityExceeded));
            Assert.That(fixture.Buffer.PendingCount, Is.EqualTo(1));
            Assert.That(fixture.Buffer.BufferOverflowCount, Is.EqualTo(1));
            Assert.That(fixture.Allocator.LastAllocatedSequence, Is.EqualTo(2));

            var repeated = fixture.Emitter.TryEmit(
                fixture.Request(TelemetryEventTypes.PhaseStarted, "phase"),
                out var repeatedEvent,
                out var repeatedFailure);
            Assert.That(repeated, Is.False);
            Assert.That(repeatedFailure, Is.EqualTo(TelemetryBufferFailureReason.BufferCapacityExceeded));
            Assert.That(repeatedEvent.Id, Is.EqualTo(failedEvent.Id));
            Assert.That(repeatedEvent, Is.SameAs(failedEvent));
            Assert.That(fixture.Allocator.LastAllocatedSequence, Is.EqualTo(2));
        }

        [Test]
        public void Emitter_RetriesFailedEnqueueWithSameEventIdentityAfterCapacityFrees()
        {
            var fixture = new Fixture(bufferCapacity: 1);
            fixture.Factory.BeginMatch();
            Assert.That(fixture.Emitter.TryEmit(
                fixture.Request(TelemetryEventTypes.MatchStarted, "start"),
                out _,
                out var startFailure), Is.True);
            Assert.That(startFailure, Is.EqualTo(TelemetryBufferFailureReason.None));

            Assert.That(fixture.Emitter.TryEmit(
                fixture.Request(TelemetryEventTypes.PhaseStarted, "phase"),
                out var failedEvent,
                out var failedReason), Is.False);
            Assert.That(failedReason, Is.EqualTo(TelemetryBufferFailureReason.BufferCapacityExceeded));
            Assert.That(fixture.Allocator.LastAllocatedSequence, Is.EqualTo(2));
            StringAssert.Contains("\"eventSequence\":2", failedEvent.ContextJson);

            var submitted = fixture.Buffer.GetReadyBatch(10, DateTime.UtcNow);
            fixture.Buffer.ApplyAcknowledgements(
                submitted,
                new[] { new TelemetryAckItem(submitted[0].Event.Id, TelemetryAckStatus.Accepted) },
                DateTime.UtcNow);
            Assert.That(fixture.Buffer.PendingCount, Is.EqualTo(0));

            Assert.That(fixture.Emitter.TryEmit(
                fixture.Request(TelemetryEventTypes.PhaseStarted, "phase"),
                out var retriedEvent,
                out var retryReason), Is.True);
            Assert.That(retryReason, Is.EqualTo(TelemetryBufferFailureReason.None));
            Assert.That(retriedEvent, Is.SameAs(failedEvent));
            Assert.That(retriedEvent.Id, Is.EqualTo(failedEvent.Id));
            Assert.That(retriedEvent.ContextJson, Is.EqualTo(failedEvent.ContextJson));
            StringAssert.Contains("\"eventSequence\":2", retriedEvent.ContextJson);
            Assert.That(fixture.Allocator.LastAllocatedSequence, Is.EqualTo(2));
            Assert.That(fixture.Buffer.PendingCount, Is.EqualTo(1));
            Assert.That(fixture.Buffer.GetReadyBatch(10, DateTime.UtcNow)[0].Event.Id, Is.EqualTo(failedEvent.Id));
        }

        [Test]
        public void Emitter_DuplicatePendingOccurrenceRemainsIdempotentSuccess()
        {
            var fixture = new Fixture();
            fixture.Factory.BeginMatch();
            var request = fixture.Request(TelemetryEventTypes.MatchStarted, "start");

            Assert.That(fixture.Emitter.TryEmit(request, out var first, out var firstFailure), Is.True);
            Assert.That(firstFailure, Is.EqualTo(TelemetryBufferFailureReason.None));
            Assert.That(fixture.Emitter.TryEmit(request, out var duplicate, out var duplicateFailure), Is.True);

            Assert.That(duplicateFailure, Is.EqualTo(TelemetryBufferFailureReason.None));
            Assert.That(duplicate, Is.SameAs(first));
            Assert.That(duplicate.Id, Is.EqualTo(first.Id));
            Assert.That(duplicate.ContextJson, Is.EqualTo(first.ContextJson));
            Assert.That(fixture.Allocator.LastAllocatedSequence, Is.EqualTo(1));
            Assert.That(fixture.Buffer.PendingCount, Is.EqualTo(1));
        }

        [Test]
        public void Buffer_PartialAcknowledgementRemovesAcceptedAndRetriesMissing()
        {
            var fixture = new Fixture();
            fixture.Factory.BeginMatch();
            fixture.Emit(TelemetryEventTypes.MatchStarted, "start");
            fixture.Emit(TelemetryEventTypes.PhaseStarted, "phase");
            var submitted = fixture.Buffer.GetReadyBatch(10, DateTime.UtcNow);
            var acceptedId = submitted[0].Event.Id;

            fixture.Buffer.ApplyAcknowledgements(
                submitted,
                new[] { new TelemetryAckItem(acceptedId, TelemetryAckStatus.Accepted) },
                DateTime.UtcNow);

            Assert.That(fixture.Buffer.PendingCount, Is.EqualTo(1));
            Assert.That(submitted[1].AttemptCount, Is.EqualTo(1));
            Assert.That(submitted[1].NextAttemptAtUtc, Is.GreaterThan(DateTime.UtcNow));
        }

        [Test]
        public void Buffer_PermanentRejectMovesEventToQuarantine()
        {
            var fixture = new Fixture();
            fixture.Factory.BeginMatch();
            fixture.Emit(TelemetryEventTypes.MatchStarted, "start");
            var submitted = fixture.Buffer.GetReadyBatch(10, DateTime.UtcNow);

            fixture.Buffer.ApplyAcknowledgements(
                submitted,
                new[]
                {
                    new TelemetryAckItem(
                        submitted[0].Event.Id,
                        TelemetryAckStatus.PermanentlyRejected,
                        "INVALID_EVENT_ENUM")
                },
                DateTime.UtcNow);

            Assert.That(fixture.Buffer.PendingCount, Is.EqualTo(0));
            Assert.That(fixture.Buffer.QuarantinedCount, Is.EqualTo(1));
            Assert.That(fixture.Buffer.Quarantined[0].Reason, Is.EqualTo("INVALID_EVENT_ENUM"));
        }

        [Test]
        public void BatchSender_RetryPreservesSerializedEventAndIdentity()
        {
            var fixture = new Fixture();
            fixture.Factory.BeginMatch();
            fixture.Emit(TelemetryEventTypes.MatchStarted, "start");
            var transport = new CapturingTransport();
            var sender = new TelemetryBatchSender(fixture.Buffer, transport, 10);

            Assert.That(sender.TryFlush(DateTime.UtcNow), Is.True);
            transport.CompleteFailure("OFFLINE");
            var firstJson = transport.LastBatchJson;
            var retryAt = fixture.Buffer.GetReadyBatch(10, DateTime.UtcNow.AddMinutes(1));
            Assert.That(retryAt.Count, Is.EqualTo(1));

            Assert.That(sender.TryFlush(DateTime.UtcNow.AddMinutes(1)), Is.True);
            Assert.That(transport.LastBatchJson, Is.EqualTo(firstJson));
        }

        [Test]
        public void BatchSender_DuplicateAcknowledgementRemovesPendingEvent()
        {
            var fixture = new Fixture();
            fixture.Factory.BeginMatch();
            fixture.Emit(TelemetryEventTypes.MatchStarted, "start");
            var transport = new CapturingTransport();
            var sender = new TelemetryBatchSender(fixture.Buffer, transport, 10);

            sender.TryFlush(DateTime.UtcNow);
            transport.CompleteWith(TelemetryAckStatus.DuplicateAlreadyAccepted);

            Assert.That(fixture.Buffer.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void MatchAdapter_EmitsCanonicalStartAndTerminalPayloads()
        {
            var fixture = new Fixture();
            fixture.Factory.BeginMatch();
            var adapter = new MatchTelemetryAdapter(fixture.Emitter);

            Assert.That(adapter.EmitMatchStarted(
                "match-start", DateTime.UtcNow, "RESEARCH_FACILITY", 4,
                "BUILD-1", "MAP-1", "WHITELIST-1", false,
                out var started, out var startFailure), Is.True);
            Assert.That(startFailure, Is.EqualTo(TelemetryBufferFailureReason.None));
            StringAssert.Contains("\"teamSize\":4", started.ContextJson);
            StringAssert.Contains("\"mapId\":\"RESEARCH_FACILITY\"", started.DataJson);

            Assert.That(adapter.EmitMatchEnded(
                "match-end", DateTime.UtcNow, "SUCCESS", 120.5, 3, "TEAM_ESCAPED",
                out var ended, out var endFailure), Is.True);
            Assert.That(endFailure, Is.EqualTo(TelemetryBufferFailureReason.None));
            StringAssert.Contains("\"phase\":\"MATCH_END\"", ended.ContextJson);
            StringAssert.Contains("\"survivorCount\":3", ended.DataJson);
            Assert.That(fixture.Allocator.IsTerminal, Is.True);
        }

        [Test]
        public void NoiseAdapter_CorrelatesRuntimeNoiseAndDeduplicatesByNoiseEventId()
        {
            var fixture = new Fixture();
            fixture.Factory.BeginMatch();
            StartMatch(fixture);
            var adapter = new NoiseTelemetryAdapter(fixture.Emitter);
            var userId = Guid.NewGuid();

            Assert.That(adapter.EmitAcceptedRuntimeNoise(
                "noise-42", DateTime.UtcNow, userId, "CORE_COLLECTION", "SPRINT", 0.7,
                new TelemetryPositionSnapshot(1, 2, 3), out var first, out _, 12), Is.True);
            Assert.That(adapter.EmitAcceptedRuntimeNoise(
                "noise-42", DateTime.UtcNow.AddSeconds(2), userId, "CORE_COLLECTION", "SPRINT", 0.9,
                new TelemetryPositionSnapshot(9, 9, 9), out var repeated, out _, 20), Is.True);

            Assert.That(repeated, Is.SameAs(first));
            Assert.That(fixture.Allocator.LastAllocatedSequence, Is.EqualTo(2));
            Assert.That(first.ReasonCode, Is.EqualTo("PLAYER_SPRINT"));
            StringAssert.Contains("\"noiseEventId\":\"noise-42\"", first.DataJson);
            StringAssert.Contains("\"position\":{\"x\":1", first.ContextJson);
        }

        [Test]
        public void PlayerAdapter_RejectsDownReasonMonsterMismatchBeforeEmission()
        {
            var fixture = new Fixture();
            fixture.Factory.BeginMatch();
            StartMatch(fixture);
            var adapter = new PlayerTelemetryAdapter(fixture.Emitter);

            Assert.Throws<ArgumentException>(() => adapter.EmitPlayerDowned(
                "down-1", DateTime.UtcNow, Guid.NewGuid(), "FINAL_HUNT", "LISTENER", "STALKER_ATTACK",
                out _, out _));
            Assert.That(fixture.Allocator.LastAllocatedSequence, Is.EqualTo(1));
        }

        [Test]
        public void MonsterAdapter_EmitsResearchAttackOnlyWhenCaptureEnabled()
        {
            var disabled = new Fixture(researchCaptureEnabled: false);
            disabled.Factory.BeginMatch();
            StartMatch(disabled);
            var disabledAdapter = new MonsterTelemetryAdapter(disabled.Emitter);
            Assert.Throws<InvalidOperationException>(() => disabledAdapter.EmitAttackResolved(
                "stalker-1:attack-1", DateTime.UtcNow, "FINAL_HUNT", "STALKER", "stalker-1",
                "attack-1", "HIT", out _, out _));

            var enabled = new Fixture(researchCaptureEnabled: true);
            enabled.Factory.BeginMatch();
            StartMatch(enabled);
            var enabledAdapter = new MonsterTelemetryAdapter(enabled.Emitter);
            Assert.That(enabledAdapter.EmitAttackResolved(
                "stalker-1:attack-1", DateTime.UtcNow, "FINAL_HUNT", "STALKER", "stalker-1",
                "attack-1", "HIT", out var telemetryEvent, out _), Is.True);

            StringAssert.Contains("\"researchCaptureEnabled\":true", telemetryEvent.ContextJson);
            StringAssert.Contains("\"attackEpisodeId\":\"attack-1\"", telemetryEvent.DataJson);
            Assert.That(telemetryEvent.UserId, Is.Null);
        }

        [Test]
        public void ObjectiveAdapter_SnapshotsCorePositionAndActingUser()
        {
            var fixture = new Fixture();
            fixture.Factory.BeginMatch();
            StartMatch(fixture);
            var adapter = new ObjectiveTelemetryAdapter(fixture.Emitter);
            var userId = Guid.NewGuid();

            Assert.That(adapter.EmitCoreTransition(
                "core-1-pickup-1", DateTime.UtcNow, TelemetryEventTypes.CorePickedUp,
                userId, "core-1", out var telemetryEvent, out _,
                new TelemetryPositionSnapshot(4, 5, 6)), Is.True);

            Assert.That(telemetryEvent.UserId, Is.EqualTo(userId));
            Assert.That(telemetryEvent.ReasonCode, Is.EqualTo("PLAYER_PICKUP"));
            StringAssert.Contains("\"coreId\":\"core-1\"", telemetryEvent.DataJson);
            StringAssert.Contains("\"position\":{\"x\":4", telemetryEvent.ContextJson);
        }

        [Test]
        public void ObjectiveAdapter_MapsDroppedAndPlacedAuthoritativeTransitions()
        {
            var fixture = new Fixture();
            fixture.Factory.BeginMatch();
            StartMatch(fixture);
            var adapter = new ObjectiveTelemetryAdapter(fixture.Emitter);
            var userId = Guid.NewGuid();

            Assert.That(adapter.EmitCoreTransition(
                "core-1-drop-1", DateTime.UtcNow, TelemetryEventTypes.CoreDropped,
                userId, "core-1", out var dropped, out _), Is.True);
            Assert.That(adapter.EmitCoreTransition(
                "core-1-place-1", DateTime.UtcNow, TelemetryEventTypes.CorePlaced,
                userId, "core-1", out var placed, out _), Is.True);

            Assert.That(dropped.ReasonCode, Is.EqualTo("PLAYER_DROP"));
            Assert.That(placed.ReasonCode, Is.EqualTo("CORE_OBJECTIVE_PLACED"));
        }

        [Test]
        public void MatchAdapter_EmitsInitialPhaseBetweenStartAndTerminal()
        {
            var fixture = new Fixture();
            fixture.Factory.BeginMatch();
            var adapter = new MatchTelemetryAdapter(fixture.Emitter);
            var now = DateTime.UtcNow;

            Assert.That(adapter.EmitMatchStarted(
                "match-start", now, "GAME", 2, "BUILD", "MAP", "WHITELIST", false,
                out var started, out _), Is.True);
            Assert.That(adapter.EmitPhaseStarted(
                "phase:core-collection:start:1", now, "CORE_COLLECTION",
                out var phaseStarted, out _), Is.True);
            Assert.That(adapter.EmitMatchEnded(
                "match-end:host-shutdown", now.AddSeconds(5), "ABORTED", 5, 2, "MATCH_ABORTED",
                out var ended, out _), Is.True);

            StringAssert.Contains("\"eventSequence\":1", started.ContextJson);
            StringAssert.Contains("\"eventSequence\":2", phaseStarted.ContextJson);
            StringAssert.Contains("\"eventSequence\":3", ended.ContextJson);
            Assert.That(ended.ReasonCode, Is.EqualTo("MATCH_ABORTED"));
        }

        [Test]
        public void FileLocalLog_AppendsOneJsonLineWithEventIdentity()
        {
            var directory = Path.Combine(Path.GetTempPath(), "echo-telemetry-tests", Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "telemetry.jsonl");
            var eventId = Guid.NewGuid();
            try
            {
                var log = new TelemetryFileLocalLog(path);
                log.Append("EVENT_CREATED", eventId, new TelemetryJsonObject().AddString("result", "OK").ToJson());

                var lines = File.ReadAllLines(path);
                Assert.That(lines, Has.Length.EqualTo(1));
                StringAssert.Contains("\"category\":\"EVENT_CREATED\"", lines[0]);
                StringAssert.Contains(eventId.ToString("D"), lines[0]);
                StringAssert.Contains("\"details\":{\"result\":\"OK\"}", lines[0]);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static void StartMatch(Fixture fixture)
        {
            var adapter = new MatchTelemetryAdapter(fixture.Emitter);
            adapter.EmitMatchStarted(
                "start", DateTime.UtcNow, "TEST_MAP", 1, "TEST_BUILD", "TEST_MAP_CONTENT",
                "TEST_WHITELIST", false, out _, out _);
        }

        private sealed class Fixture
        {
            public Fixture(
                bool hasAuthority = true,
                bool researchCaptureEnabled = false,
                int bufferCapacity = 16)
            {
                Authority = new FakeAuthority(Guid.NewGuid(), hasAuthority);
                Provenance = new FakeProvenance(researchCaptureEnabled);
                Allocator = new TelemetrySequenceAllocator();
                Factory = new TelemetryEventFactory(Allocator, Authority, Provenance);
                Buffer = new TelemetryBuffer(bufferCapacity);
                Emitter = new TelemetryEmitter(Factory, Buffer, Provenance);
            }

            public FakeAuthority Authority { get; }
            public FakeProvenance Provenance { get; }
            public TelemetrySequenceAllocator Allocator { get; }
            public TelemetryEventFactory Factory { get; }
            public TelemetryBuffer Buffer { get; }
            public TelemetryEmitter Emitter { get; }

            public TelemetryEmissionRequest Request(string eventType, string sourceKey)
            {
                return new TelemetryEmissionRequest
                {
                    SourceOccurrenceKey = sourceKey,
                    EventType = eventType,
                    OccurredAtUtc = DateTime.UtcNow,
                    Context = new TelemetryJsonObject().AddString("phase", "TEST"),
                    Data = new TelemetryJsonObject()
                };
            }

            public bool Emit(string eventType, string sourceKey)
            {
                return Emitter.TryEmit(Request(eventType, sourceKey), out _, out _);
            }
        }

        private sealed class FakeAuthority : ITelemetryAuthorityContext
        {
            private readonly Guid _matchId;

            public FakeAuthority(Guid matchId, bool hasAuthority)
            {
                _matchId = matchId;
                HasStateAuthority = hasAuthority;
            }

            public bool HasStateAuthority { get; }
            public long? AuthorityTick => 42;

            public bool TryGetMatchId(out Guid matchId)
            {
                matchId = _matchId;
                return true;
            }
        }

        private sealed class FakeProvenance : ITelemetryProvenanceProvider
        {
            private readonly bool _researchCaptureEnabled;

            public FakeProvenance(bool researchCaptureEnabled)
            {
                _researchCaptureEnabled = researchCaptureEnabled;
            }

            public TelemetryProvenanceSnapshot Capture()
            {
                return new TelemetryProvenanceSnapshot(
                    "TEST-SCENARIO-1",
                    "TEST-POLICY-1",
                    TelemetryConfigSource.Fixed,
                    _researchCaptureEnabled);
            }
        }

        private sealed class CapturingTransport : ITelemetryTransport
        {
            private Action<TelemetryTransportResult> _callback;
            private Guid _lastId;

            public string LastBatchJson { get; private set; }

            public void SendBatch(string batchJson, Action<TelemetryTransportResult> callback)
            {
                LastBatchJson = batchJson;
                _callback = callback;
                var marker = "\"id\":\"";
                var start = batchJson.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
                _lastId = Guid.Parse(batchJson.Substring(start, 36));
            }

            public void CompleteFailure(string error)
            {
                var callback = _callback;
                _callback = null;
                callback?.Invoke(new TelemetryTransportResult(false, null, error));
            }

            public void CompleteWith(TelemetryAckStatus status)
            {
                var callback = _callback;
                _callback = null;
                callback?.Invoke(new TelemetryTransportResult(
                    true,
                    new List<TelemetryAckItem> { new TelemetryAckItem(_lastId, status) }));
            }
        }
    }
}
