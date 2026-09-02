using System;
using System.IO;
using System.Linq;
using System.Reflection;
using EchoProtocol.AI.Listener.Memory;
using EchoProtocol.AI.Listener.Noise;
using EchoProtocol.AI.Listener.Perception;
using EchoProtocol.Networking;
using PlayerRef = Fusion.PlayerRef;
using Object = UnityEngine.Object;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.AI.Listener.Tests
{
    public sealed class RuntimeNoiseAndHearingTests
    {
        [Test]
        public void LIS001_A_CanonicalNoiseTypeSetIsExact()
        {
            Assert.That(
                Enum.GetNames(typeof(RuntimeNoiseType)),
                Is.EquivalentTo(new[]
                {
                    "SPRINT",
                    "INTERACTION",
                    "CORE_CARRY",
                    "CORE_DROP",
                    "NOISE_MAKER"
                }));
        }

        [Test]
        public void LIS001_F_CanonicalNoiseAndHearingReasonSetsAreExact()
        {
            Assert.That(
                Enum.GetNames(typeof(NoiseValidationRejectReason)),
                Is.EquivalentTo(new[]
                {
                    "None",
                    "NotStateAuthority",
                    "UnknownNoiseType",
                    "InvalidDefinition",
                    "InvalidPosition",
                    "InvalidLoudness",
                    "InvalidHearingRadius",
                    "InvalidExpiry",
                    "DuplicateEmission",
                    "SourceActionRejected"
                }));
            Assert.That(
                Enum.GetNames(typeof(NoiseSystemDiagnosticReason)),
                Is.EquivalentTo(new[]
                {
                    "None",
                    "CapacityEvicted",
                    "DedupRetentionInvariantViolation",
                    "SubsystemUnavailable"
                }));
            Assert.That(
                Enum.GetNames(typeof(ListenerHearingRejectReason)),
                Is.EquivalentTo(new[]
                {
                    "None",
                    "Expired",
                    "OutsideRange",
                    "BelowThreshold",
                    "OccludedBelowThreshold",
                    "OcclusionQueryFailed",
                    "InvalidEvent"
                }));
            Assert.That(
                Enum.GetNames(typeof(PendingHearingDiagnosticReason)),
                Is.EquivalentTo(new[]
                {
                    "ExpiredBeforeCommit",
                    "CapacityEvicted",
                    "ConsumedByStatePolicy"
                }));
        }

        [Test]
        public void LIS001_ProductionOccurrenceKeyHelpersAreDeterministicAndMovementStreamIsPerPlayer()
        {
            var sprint = RuntimeNoiseSourceOccurrenceKey.ForMovement("player-1", RuntimeNoiseType.SPRINT, 42);
            var carry = RuntimeNoiseSourceOccurrenceKey.ForMovement("player-1", RuntimeNoiseType.CORE_CARRY, 42);
            var interaction = RuntimeNoiseSourceOccurrenceKey.ForInteraction("player-1", 7);
            var tool = RuntimeNoiseSourceOccurrenceKey.ForTeamTool("player-1", "NOISE_MAKER", 7);
            var drop = RuntimeNoiseSourceOccurrenceKey.ForCoreDrop("core-1", 3);

            Assert.That(sprint.StreamKey, Is.EqualTo(carry.StreamKey));
            Assert.That(sprint.Sequence, Is.EqualTo(carry.Sequence));
            Assert.That(interaction.StreamKey, Is.EqualTo("interaction:player-1"));
            Assert.That(tool.StreamKey, Is.EqualTo("team-tool:player-1:NOISE_MAKER"));
            Assert.That(drop.StreamKey, Is.EqualTo("core-drop:core-1"));
        }

        [Test]
        public void LIS001_MovementWatermarkPreventsSprintAndCoreCarryFromSameAuthoritativeOpportunity()
        {
            var system = new RuntimeNoiseSystem();
            var matchId = Guid.NewGuid();
            var now = Now();
            var sprint = RuntimeNoiseSourceOccurrenceKey.ForMovement("player-1", RuntimeNoiseType.SPRINT, 42);
            var carry = RuntimeNoiseSourceOccurrenceKey.ForMovement("player-1", RuntimeNoiseType.CORE_CARRY, 42);

            Assert.That(system.TryAccept(
                matchId,
                Request(RuntimeNoiseType.SPRINT, sprint.StreamKey, sprint.Sequence, Vector3.zero, now, 42),
                out _), Is.EqualTo(RuntimeNoiseAcceptStatus.Accepted));
            Assert.That(system.TryAccept(
                matchId,
                Request(RuntimeNoiseType.CORE_CARRY, carry.StreamKey, carry.Sequence, Vector3.zero, now, 42),
                out _), Is.EqualTo(RuntimeNoiseAcceptStatus.Duplicate));
        }

        [Test]
        public void LIS001_B_InvalidUnsupportedNoiseTypeCannotCreateAuthoritativeEvent()
        {
            var system = new RuntimeNoiseSystem();

            var status = system.TryAccept(
                Guid.NewGuid(),
                Request((RuntimeNoiseType)999, "bad-type", 1, Vector3.zero, Now(), 1),
                out _);

            Assert.That(status, Is.EqualTo(RuntimeNoiseAcceptStatus.Rejected));
            Assert.That(system.LastRejectReason, Is.EqualTo(NoiseValidationRejectReason.UnknownNoiseType));
            Assert.That(system.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void LIS001_C_D_E_N_AcceptedLogicalEmissionDeduplicatesEventPublication()
        {
            var system = new RuntimeNoiseSystem();
            var matchId = Guid.NewGuid();
            var publishCount = 0;
            RuntimeNoiseEvent published = default;
            system.RuntimeNoiseAccepted += noiseEvent =>
            {
                publishCount++;
                published = noiseEvent;
            };

            var first = system.TryAccept(
                matchId,
                Request(RuntimeNoiseType.INTERACTION, "interaction:player:terminal", 10, new Vector3(1, 2, 3), Now(), 10),
                out var firstEvent);
            var duplicate = system.TryAccept(
                matchId,
                Request(RuntimeNoiseType.INTERACTION, "interaction:player:terminal", 10, new Vector3(9, 9, 9), Now(), 10),
                out var duplicateEvent);

            Assert.That(first, Is.EqualTo(RuntimeNoiseAcceptStatus.Accepted));
            Assert.That(duplicate, Is.EqualTo(RuntimeNoiseAcceptStatus.Duplicate));
            Assert.That(duplicateEvent.NoiseEventId, Is.Null);
            Assert.That(firstEvent.WorldPosition, Is.EqualTo(new Vector3(1, 2, 3)));
            Assert.That(publishCount, Is.EqualTo(1));
            Assert.That(published.NoiseEventId, Is.EqualTo(firstEvent.NoiseEventId));
        }

        [Test]
        public void LIS001_F_G_RuntimeNoiseEventIsImmutableSnapshotWithoutLiveReferences()
        {
            var system = new RuntimeNoiseSystem();
            var emittedAt = Now();
            var sourcePosition = new Vector3(4, 5, 6);

            Assert.That(system.TryAccept(
                Guid.NewGuid(),
                Request(RuntimeNoiseType.CORE_DROP, "core-drop:core", 1, sourcePosition, emittedAt, 2),
                out var noiseEvent), Is.EqualTo(RuntimeNoiseAcceptStatus.Accepted));
            sourcePosition = new Vector3(99, 99, 99);

            Assert.That(noiseEvent.WorldPosition, Is.EqualTo(new Vector3(4, 5, 6)));
            Assert.That(noiseEvent.EmittedAtUtc, Is.EqualTo(emittedAt));
            Assert.That(
                typeof(RuntimeNoiseEvent).GetFields().Select(field => field.FieldType),
                Has.No.Member(typeof(Transform)));
            Assert.That(
                typeof(RuntimeNoiseEvent).GetProperties().Select(property => property.PropertyType),
                Has.No.Member(typeof(GameObject)));
            Assert.That(
                typeof(RuntimeNoiseEvent).GetProperties().Select(property => property.Name),
                Has.No.Member("SourcePlayerId"));
            Assert.That(
                typeof(RuntimeNoiseEvent).GetProperties().Select(property => property.Name),
                Has.No.Member("SourceEntityId"));
            Assert.That(
                typeof(RuntimeNoiseEvent).GetProperties().Select(property => property.Name),
                Has.No.Member("SourceOccurrenceKey"));
        }

        [Test]
        public void LIS001_H_I_J_K_L_ActiveAndDedupStorageAreBoundedExpireAndReset()
        {
            var system = new RuntimeNoiseSystem(activeCapacity: 2, dedupCapacity: 2);
            var matchId = Guid.NewGuid();
            var now = Now();

            system.TryAccept(matchId, Request(RuntimeNoiseType.SPRINT, "movement:p1", 1, Vector3.zero, now, 1), out var one);
            system.TryAccept(matchId, Request(RuntimeNoiseType.SPRINT, "movement:p1", 2, Vector3.right, now, 2), out _);
            system.TryAccept(matchId, Request(RuntimeNoiseType.SPRINT, "movement:p1", 3, Vector3.left, now, 3), out _);

            Assert.That(system.ActiveCount, Is.EqualTo(2));
            Assert.That(system.DedupCount, Is.EqualTo(1));
            Assert.That(system.GetActiveEvents(now).Select(item => item.NoiseEventId), Has.No.Member(one.NoiseEventId));
            Assert.That(system.LastDiagnosticReason, Is.EqualTo(NoiseSystemDiagnosticReason.CapacityEvicted));

            system.Expire(now.AddSeconds(10));
            Assert.That(system.ActiveCount, Is.EqualTo(0));
            system.ResetForMatch();
            Assert.That(system.DedupCount, Is.EqualTo(0));
        }

        [Test]
        public void LIS001_LongRecurringMovementStreamsUseCompactWatermarksWithoutForgottenDuplicates()
        {
            var system = new RuntimeNoiseSystem(activeCapacity: 8, dedupCapacity: 4);
            var matchId = Guid.NewGuid();
            var now = Now();

            for (var sequence = 1; sequence <= 1000; sequence++)
            {
                Assert.That(system.TryAccept(
                    matchId,
                    Request(RuntimeNoiseType.SPRINT, "movement:player-a", sequence, Vector3.zero, now, sequence),
                    out _), Is.EqualTo(RuntimeNoiseAcceptStatus.Accepted));
            }

            for (var sequence = 1; sequence <= 1000; sequence++)
            {
                Assert.That(system.TryAccept(
                    matchId,
                    Request(RuntimeNoiseType.CORE_CARRY, "movement:player-b", sequence, Vector3.zero, now, sequence),
                    out _), Is.EqualTo(RuntimeNoiseAcceptStatus.Accepted));
            }

            Assert.That(system.DedupCount, Is.EqualTo(2));
            Assert.That(system.TryAccept(
                matchId,
                Request(RuntimeNoiseType.SPRINT, "movement:player-a", 1, Vector3.zero, now, 1),
                out _), Is.EqualTo(RuntimeNoiseAcceptStatus.Duplicate));
            Assert.That(system.LastRejectReason, Is.EqualTo(NoiseValidationRejectReason.DuplicateEmission));
            Assert.That(system.TryAccept(
                matchId,
                Request(RuntimeNoiseType.CORE_CARRY, "movement:player-b", 500, Vector3.zero, now, 500),
                out _), Is.EqualTo(RuntimeNoiseAcceptStatus.Duplicate));
        }

        [Test]
        public void LIS001_DedupStreamCapacityFailsSafeWithoutRetiringExistingWatermark()
        {
            var system = new RuntimeNoiseSystem(dedupCapacity: 1);
            var matchId = Guid.NewGuid();
            var now = Now();

            Assert.That(system.TryAccept(
                matchId,
                Request(RuntimeNoiseType.SPRINT, "movement:player-a", 1, Vector3.zero, now, 1),
                out _), Is.EqualTo(RuntimeNoiseAcceptStatus.Accepted));
            Assert.That(system.TryAccept(
                matchId,
                Request(RuntimeNoiseType.SPRINT, "movement:player-b", 1, Vector3.zero, now, 1),
                out _), Is.EqualTo(RuntimeNoiseAcceptStatus.Rejected));

            Assert.That(system.LastDiagnosticReason, Is.EqualTo(NoiseSystemDiagnosticReason.DedupRetentionInvariantViolation));
            Assert.That(system.TryAccept(
                matchId,
                Request(RuntimeNoiseType.SPRINT, "movement:player-a", 1, Vector3.zero, now, 1),
                out _), Is.EqualTo(RuntimeNoiseAcceptStatus.Duplicate));
            Assert.That(system.DedupCount, Is.EqualTo(1));
        }

        [Test]
        public void LIS001_MatchLifecycleBeginIsIdempotentAndNewMatchOrResetClearsRuntimeNoise()
        {
            var gameObject = new GameObject("runtime-noise-lifecycle-test");
            try
            {
                var service = gameObject.AddComponent<EchoProtocol.Networking.Authority.HostRuntimeNoiseService>();
                var system = (RuntimeNoiseSystem)typeof(EchoProtocol.Networking.Authority.HostRuntimeNoiseService)
                    .GetField("_noiseSystem", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(service);
                var firstMatch = Guid.NewGuid();
                var secondMatch = Guid.NewGuid();
                var now = Now();

                service.BeginMatch(firstMatch);
                Assert.That(system.TryAccept(
                    firstMatch,
                    Request(RuntimeNoiseType.SPRINT, "movement:player-a", 1, Vector3.zero, now, 1),
                    out _), Is.EqualTo(RuntimeNoiseAcceptStatus.Accepted));
                service.BeginMatch(firstMatch);
                Assert.That(system.TryAccept(
                    firstMatch,
                    Request(RuntimeNoiseType.SPRINT, "movement:player-a", 1, Vector3.zero, now, 1),
                    out _), Is.EqualTo(RuntimeNoiseAcceptStatus.Duplicate));

                service.BeginMatch(secondMatch);
                Assert.That(system.DedupCount, Is.EqualTo(0));
                service.ResetForMatch();
                Assert.That(system.DedupCount, Is.EqualTo(0));
                Assert.That(system.ActiveCount, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void LIS001_InteractionOccurrenceKeyStaysOnOneStreamAcrossTargetsAndSequences()
        {
            var first = RuntimeNoiseSourceOccurrenceKey.ForInteraction("player-1", 1);
            var second = RuntimeNoiseSourceOccurrenceKey.ForInteraction("player-1", 12);
            var third = RuntimeNoiseSourceOccurrenceKey.ForInteraction("player-1", 13);

            Assert.That(first.StreamKey, Is.EqualTo("interaction:player-1"));
            Assert.That(second.StreamKey, Is.EqualTo(first.StreamKey));
            Assert.That(second.Sequence, Is.EqualTo(12));
            Assert.That(third.Sequence, Is.EqualTo(13));
            Assert.That(first.Sequence, Is.EqualTo(1));
        }

        [Test]
        public void LIS001_SensorMatchLifecycleUsesPerMatchWatermarking()
        {
            var sensor = new ListenerHearingSensor(new StaticListenerOcclusionResolver(ListenerOcclusionClass.CLEAR));
            var matchA = Guid.NewGuid();
            var matchB = Guid.NewGuid();
            var now = Now();
            var eventA = CreateNoise(RuntimeNoiseType.INTERACTION, "noise-a", 1, Vector3.zero, now, 100);

            sensor.BeginMatch(matchA);
            Assert.That(sensor.TryEvaluate(eventA, Vector3.zero, now, out _, out _), Is.True);
            sensor.BeginMatch(matchA);
            Assert.That(sensor.TryEvaluate(eventA, Vector3.zero, now.AddMilliseconds(1), out _, out var repeatReject), Is.False);
            Assert.That(repeatReject, Is.EqualTo(ListenerHearingRejectReason.None));
            Assert.That(sensor.LastEvaluationStatus, Is.EqualTo(ListenerHearingEvaluationStatus.AlreadyEvaluated));

            sensor.EndMatch();
            Assert.That(sensor.TryEvaluate(eventA, Vector3.zero, now.AddMilliseconds(2), out _, out var unboundReject), Is.False);
            Assert.That(unboundReject, Is.EqualTo(ListenerHearingRejectReason.None));
            Assert.That(sensor.LastEvaluationStatus, Is.EqualTo(ListenerHearingEvaluationStatus.NotMatchBound));

            sensor.BeginMatch(matchB);
            var eventB = CreateNoise(RuntimeNoiseType.INTERACTION, "noise-b", 1, Vector3.zero, now, 50);
            Assert.That(sensor.TryEvaluate(eventB, Vector3.zero, now.AddMilliseconds(3), out _, out _), Is.True);
        }

        [Test]
        public void LIS001_SameMatchLowerTickHigherPublicationOrdinalStillReevaluates()
        {
            var matchId = Guid.NewGuid();
            var now = Now();
            var system = new RuntimeNoiseSystem();
            Assert.That(system.TryAccept(
                matchId,
                Request(RuntimeNoiseType.INTERACTION, "interaction:player-1", 1, Vector3.zero, now, 100),
                out var eventA), Is.EqualTo(RuntimeNoiseAcceptStatus.Accepted));
            Assert.That(system.TryAccept(
                matchId,
                Request(RuntimeNoiseType.INTERACTION, "interaction:player-1", 2, Vector3.zero, now.AddSeconds(1), 50),
                out var eventB), Is.EqualTo(RuntimeNoiseAcceptStatus.Accepted));

            var resolver = new CountingResolver(ListenerOcclusionClass.CLEAR);
            var sensor = new ListenerHearingSensor(resolver, new ListenerHearingPolicy(0.01d, 0.5d, 0.25d));
            sensor.BeginMatch(matchId);

            Assert.That(sensor.TryEvaluate(eventA, Vector3.zero, now.AddMilliseconds(100), out _, out _), Is.True);
            Assert.That(sensor.TryEvaluate(eventB, Vector3.zero, now.AddMilliseconds(1100), out _, out _), Is.True);
            Assert.That(resolver.CallCount, Is.EqualTo(2));

            Assert.That(sensor.TryEvaluate(eventA, Vector3.zero, now.AddMilliseconds(500), out _, out var aAgain), Is.False);
            Assert.That(aAgain, Is.EqualTo(ListenerHearingRejectReason.None));
            Assert.That(sensor.TryEvaluate(eventB, Vector3.zero, now.AddMilliseconds(1800), out _, out var bAgain), Is.False);
            Assert.That(bAgain, Is.EqualTo(ListenerHearingRejectReason.None));
            Assert.That(resolver.CallCount, Is.EqualTo(2));
        }

        [Test]
        public void LIS002_T_U_V_W_X_Y_Z_HearingUsesCanonicalDistanceAndOcclusionFormula()
        {
            var now = Now();
            var open = CreateNoise(RuntimeNoiseType.NOISE_MAKER, "noise-open", 1, Vector3.zero, now, 1);
            var policy = new ListenerHearingPolicy(0.1d, 0.5d, 0.25d);

            var clearSensor = new ListenerHearingSensor(
                new StaticListenerOcclusionResolver(ListenerOcclusionClass.CLEAR),
                policy);
            clearSensor.BeginMatch(Guid.NewGuid());
            Assert.That(clearSensor.TryEvaluate(
                open,
                new Vector3(0, 0, 10),
                now,
                out var clearObservation,
                out var clearReject), Is.True);
            Assert.That(clearReject, Is.EqualTo(ListenerHearingRejectReason.None));
            Assert.That(clearObservation.EffectiveIntensity, Is.EqualTo(0.5d).Within(0.0001d));

            var wallSensor = new ListenerHearingSensor(
                new StaticListenerOcclusionResolver(ListenerOcclusionClass.SOLID_WALL),
                policy);
            wallSensor.BeginMatch(Guid.NewGuid());
            Assert.That(wallSensor.TryEvaluate(
                open,
                new Vector3(0, 0, 10),
                now,
                out var wallObservation,
                out _), Is.True);
            Assert.That(wallObservation.EffectiveIntensity, Is.EqualTo(0.125d).Within(0.0001d));

            var closedDoorSensor = new ListenerHearingSensor(
                new StaticListenerOcclusionResolver(ListenerOcclusionClass.CLOSED_DOOR),
                policy);
            closedDoorSensor.BeginMatch(Guid.NewGuid());
            Assert.That(closedDoorSensor.TryEvaluate(
                open,
                new Vector3(0, 0, 10),
                now,
                out var doorObservation,
                out _), Is.True);
            Assert.That(doorObservation.EffectiveIntensity, Is.GreaterThan(wallObservation.EffectiveIntensity));
            Assert.That(
                ListenerOcclusionClassifier.Strongest(
                    ListenerOcclusionClass.OPEN_DOOR,
                    ListenerOcclusionClass.SOLID_WALL),
                Is.EqualTo(ListenerOcclusionClass.SOLID_WALL));
            Assert.That(
                ListenerOcclusionClassifier.Strongest(
                    ListenerOcclusionClass.CLOSED_DOOR,
                    ListenerOcclusionClass.OPEN_DOOR),
                Is.EqualTo(ListenerOcclusionClass.CLOSED_DOOR));

            var outOfRangeSensor = new ListenerHearingSensor(
                new StaticListenerOcclusionResolver(ListenerOcclusionClass.CLEAR),
                policy);
            outOfRangeSensor.BeginMatch(Guid.NewGuid());
            Assert.That(outOfRangeSensor.TryEvaluate(
                open,
                new Vector3(0, 0, 21),
                now,
                out _,
                out var outOfRange), Is.False);
            Assert.That(outOfRange, Is.EqualTo(ListenerHearingRejectReason.OutsideRange));
        }

        [Test]
        public void LIS002_V_W_X_HearingRejectReasonsSeparateThresholdOcclusionAndQueryFailure()
        {
            var now = Now();
            var noiseEvent = CreateNoise(RuntimeNoiseType.INTERACTION, "threshold", 1, Vector3.zero, now, 1);

            var clearSensor = new ListenerHearingSensor(
                new StaticListenerOcclusionResolver(ListenerOcclusionClass.CLEAR),
                new ListenerHearingPolicy(0.99d, 0.5d, 0.25d));
            clearSensor.BeginMatch(Guid.NewGuid());
            Assert.That(clearSensor.TryEvaluate(
                noiseEvent,
                new Vector3(0, 0, 5),
                now,
                out _,
                out var clearReason), Is.False);
            Assert.That(clearReason, Is.EqualTo(ListenerHearingRejectReason.BelowThreshold));

            var occluded = CreateNoise(RuntimeNoiseType.INTERACTION, "threshold-occluded", 1, Vector3.zero, now, 2);
            var occludedSensor = new ListenerHearingSensor(
                new StaticListenerOcclusionResolver(ListenerOcclusionClass.SOLID_WALL),
                new ListenerHearingPolicy(0.2d, 0.5d, 0.1d));
            occludedSensor.BeginMatch(Guid.NewGuid());
            Assert.That(occludedSensor.TryEvaluate(
                occluded,
                new Vector3(0, 0, 5),
                now,
                out _,
                out var occludedReason), Is.False);
            Assert.That(occludedReason, Is.EqualTo(ListenerHearingRejectReason.OccludedBelowThreshold));

            var queryFailed = CreateNoise(RuntimeNoiseType.INTERACTION, "query-failed", 1, Vector3.zero, now, 3);
            var queryFailedSensor = new ListenerHearingSensor(
                new StaticListenerOcclusionResolver(ListenerOcclusionClass.QUERY_FAILED));
            queryFailedSensor.BeginMatch(Guid.NewGuid());
            Assert.That(queryFailedSensor.TryEvaluate(
                queryFailed,
                Vector3.zero,
                now,
                out _,
                out var queryReason), Is.False);
            Assert.That(queryReason, Is.EqualTo(ListenerHearingRejectReason.OcclusionQueryFailed));
        }

        [Test]
        public void LIS002_AA_AB_AC_AD_AE_AF_HearingIsOneTimeImmutableAndNotRetroactive()
        {
            var now = Now();
            var noiseEvent = CreateNoise(RuntimeNoiseType.INTERACTION, "door", 1, Vector3.zero, now, 10);
            var resolver = new MutableResolver(ListenerOcclusionClass.CLOSED_DOOR);
            var sensor = new ListenerHearingSensor(resolver, new ListenerHearingPolicy(0.01d, 0.5d, 0.25d));
            sensor.BeginMatch(Guid.NewGuid());

            Assert.That(sensor.TryEvaluate(
                noiseEvent,
                new Vector3(0, 0, 3),
                now,
                out var observation,
                out _), Is.True);
            resolver.OcclusionClass = ListenerOcclusionClass.CLEAR;

            Assert.That(sensor.TryEvaluate(
                noiseEvent,
                Vector3.zero,
                now.AddMilliseconds(1),
                out _,
                out var duplicateReason), Is.False);
            Assert.That(duplicateReason, Is.EqualTo(ListenerHearingRejectReason.None));
            Assert.That(sensor.LastEvaluationStatus, Is.EqualTo(ListenerHearingEvaluationStatus.AlreadyEvaluated));
            Assert.That(observation.OcclusionClass, Is.EqualTo(ListenerOcclusionClass.CLOSED_DOOR));
            Assert.That(observation.Distance, Is.EqualTo(3d).Within(0.0001d));
            Assert.That(
                typeof(HearingObservation).GetProperties().Select(property => property.Name),
                Has.No.Member("SourcePlayerId"));
            Assert.That(
                typeof(HearingObservation).GetProperties().Select(property => property.Name),
                Has.No.Member("SourceEntityId"));
            Assert.That(
                typeof(HearingObservation).GetProperties().Select(property => property.Name),
                Has.No.Member("SourceOccurrenceKey"));
            Assert.That(
                typeof(HearingObservation).GetProperties().Select(property => property.Name),
                Has.No.Member("AuthoritativeEmissionKey"));
            Assert.That(
                typeof(RuntimeNoiseEvent).GetProperties().Select(property => property.Name),
                Has.No.Member("AuthoritativeEmissionKey"));
            Assert.That(
                typeof(RuntimeNoiseEvent).GetProperties().Select(property => property.Name),
                Has.No.Member("SourcePlayerId"));
            Assert.That(
                typeof(RuntimeNoiseEvent).GetProperties().Select(property => property.Name),
                Has.No.Member("SourceEntityId"));
            Assert.That(
                typeof(RuntimeNoiseEvent).GetProperties().Select(property => property.Name),
                Has.No.Member("SourceOccurrenceKey"));

            var expired = CreateNoise(RuntimeNoiseType.INTERACTION, "expired", 1, Vector3.zero, now.AddSeconds(-10), 11);
            var expiredSensor = new ListenerHearingSensor(new StaticListenerOcclusionResolver(ListenerOcclusionClass.CLEAR));
            expiredSensor.BeginMatch(Guid.NewGuid());
            Assert.That(expiredSensor.TryEvaluate(
                expired,
                Vector3.zero,
                now,
                out _,
                out var expiredReason), Is.False);
            Assert.That(expiredReason, Is.EqualTo(ListenerHearingRejectReason.Expired));

            var retroactiveSensor = new ListenerHearingSensor(
                new StaticListenerOcclusionResolver(ListenerOcclusionClass.CLEAR));
            retroactiveSensor.BeginMatch(Guid.NewGuid());
            var outside = CreateNoise(RuntimeNoiseType.NOISE_MAKER, "retro", 1, Vector3.zero, now, 12);
            Assert.That(retroactiveSensor.TryEvaluate(
                outside,
                new Vector3(0, 0, 21),
                now,
                out _,
                out var outsideReason), Is.False);
            Assert.That(outsideReason, Is.EqualTo(ListenerHearingRejectReason.OutsideRange));
            Assert.That(retroactiveSensor.TryEvaluate(
                outside,
                Vector3.zero,
                now.AddMilliseconds(1),
                out _,
                out var replayReason), Is.False);
            Assert.That(replayReason, Is.EqualTo(ListenerHearingRejectReason.None));
            Assert.That(retroactiveSensor.LastEvaluationStatus, Is.EqualTo(ListenerHearingEvaluationStatus.AlreadyEvaluated));
        }

        [Test]
        public void LIS002_AG_AH_AI_AJ_AK_AL_AM_AN_AO_AP_PendingInboxStoresFrozenObservationsDeterministically()
        {
            var now = Now();
            var inbox = new PendingHearingInbox(capacity: 2);
            var low = Observation("low", 1, 0.2d, 5d, now);
            var high = Observation("high", 2, 0.8d, 10d, now);
            var medium = Observation("medium", 3, 0.5d, 2d, now);

            Assert.That(inbox.TryAdd(low, now), Is.True);
            Assert.That(inbox.TryAdd(high, now), Is.True);
            Assert.That(inbox.TryAdd(low, now), Is.False);
            Assert.That(inbox.TryAdd(medium, now), Is.True);

            Assert.That(inbox.Count, Is.EqualTo(2));
            Assert.That(inbox.Observations.Select(item => item.NoiseEventId), Is.EquivalentTo(new[] { "high", "medium" }));
            Assert.That(inbox.LastDiagnosticReason, Is.EqualTo(PendingHearingDiagnosticReason.CapacityEvicted));
            Assert.That(inbox.TryTakeBest(now, out var selected), Is.True);
            Assert.That(selected.NoiseEventId, Is.EqualTo("high"));
            Assert.That(selected.EffectiveIntensity, Is.EqualTo(0.8d));
            Assert.That(inbox.LastDiagnosticReason, Is.EqualTo(PendingHearingDiagnosticReason.CapacityEvicted));
            inbox.Clear();
            Assert.That(inbox.LastDiagnosticReason, Is.Null);
            Assert.That(inbox.TryAdd(Observation("expired", 4, 1d, 1d, now.AddSeconds(-3)), now), Is.False);
            Assert.That(inbox.LastDiagnosticReason, Is.EqualTo(PendingHearingDiagnosticReason.ExpiredBeforeCommit));
            Assert.That(inbox.RemoveExpired(now.AddSeconds(5)), Is.EqualTo(0));
        }

        [Test]
        public void LIS002_E_PendingRankingUsesNewerEmittedAtBeforeShorterDistance()
        {
            var now = Now();
            var inbox = new PendingHearingInbox(capacity: 2);
            var olderCloser = Observation("older-closer", 1, 0.5d, 1d, now.AddSeconds(-1), now);
            var newerFarther = Observation("newer-farther", 2, 0.5d, 10d, now, now);

            Assert.That(inbox.TryAdd(olderCloser, now), Is.True);
            Assert.That(inbox.TryAdd(newerFarther, now), Is.True);
            Assert.That(inbox.TryTakeBest(now, out var selected), Is.True);

            Assert.That(selected.NoiseEventId, Is.EqualTo("newer-farther"));
        }

        [Test]
        public void LIS002_UnityOcclusionResolverIgnoresSelfAndIgnoredSourceColliders()
        {
            var listenerRoot = new GameObject("listener-root");
            listenerRoot.transform.position = new Vector3(0f, 0f, 10f);
            var listenerCollider = listenerRoot.AddComponent<BoxCollider>();
            listenerCollider.size = new Vector3(0.5f, 0.5f, 0.5f);

            var source = new GameObject("source");
            source.transform.position = new Vector3(0f, 0f, 0f);
            var sourceCollider = source.AddComponent<BoxCollider>();
            sourceCollider.size = new Vector3(0.2f, 0.2f, 0.2f);

            var wall = new GameObject("wall");
            wall.transform.position = new Vector3(0f, 0f, 5f);
            var wallCollider = wall.AddComponent<BoxCollider>();
            wallCollider.size = new Vector3(1f, 1f, 1f);

            try
            {
                var resolver = new UnityListenerOcclusionResolver(
                    LayerMask.GetMask("Default"),
                    listenerRoot.transform,
                    ignoredListenerColliders: new[] { listenerCollider },
                    maxHits: 8,
                    ignoredSourceColliders: new[] { sourceCollider });

                Physics.SyncTransforms();
                Assert.That(resolver.Classify(new Vector3(0f, 0f, 10f), new Vector3(0f, 0f, 0f)), Is.EqualTo(ListenerOcclusionClass.SOLID_WALL));

                Object.DestroyImmediate(wall);
                Physics.SyncTransforms();
                Assert.That(resolver.Classify(new Vector3(0f, 0f, 10f), new Vector3(0f, 0f, 0f)), Is.EqualTo(ListenerOcclusionClass.CLEAR));
            }
            finally
            {
                Object.DestroyImmediate(listenerRoot);
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(wall);
            }
        }

        [Test]
        public void LIS002_ListenerOcclusionClassifierMapsDoorStateToOcclusionClass()
        {
            Assert.That(ListenerOcclusionClassifier.ClassifyDoorState(NetworkDoorState.Open), Is.EqualTo(ListenerOcclusionClass.OPEN_DOOR));
            Assert.That(ListenerOcclusionClassifier.ClassifyDoorState(NetworkDoorState.Closed), Is.EqualTo(ListenerOcclusionClass.CLOSED_DOOR));
            Assert.That(ListenerOcclusionClassifier.ClassifyDoorState(NetworkDoorState.Locked), Is.EqualTo(ListenerOcclusionClass.CLOSED_DOOR));
        }

        [Test]
        public void LIS002_ListenerOcclusionResolverReturnsQueryFailedWhenSaturated()
        {
            var listener = new GameObject("listener");
            var source = new GameObject("source");
            var blockerOne = new GameObject("blocker-one");
            var blockerTwo = new GameObject("blocker-two");
            blockerOne.transform.position = new Vector3(0f, 0f, 2f);
            blockerTwo.transform.position = new Vector3(0f, 0f, 4f);
            blockerOne.AddComponent<BoxCollider>();
            blockerTwo.AddComponent<BoxCollider>();
            source.transform.position = new Vector3(0f, 0f, 0f);
            listener.transform.position = new Vector3(0f, 0f, 10f);

            try
            {
                var resolver = new UnityListenerOcclusionResolver(
                    LayerMask.GetMask("Default"),
                    listener.transform,
                    maxHits: 1);
                Physics.SyncTransforms();
                Assert.That(resolver.Classify(listener.transform.position, source.transform.position), Is.EqualTo(ListenerOcclusionClass.QUERY_FAILED));
            }
            finally
            {
                Object.DestroyImmediate(listener);
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(blockerOne);
                Object.DestroyImmediate(blockerTwo);
            }
        }

        [Test]
        public void LIS002_ListenerHearingSensorRequiresMatchBindingBeforePhysicalEvaluation()
        {
            var resolver = new CountingResolver(ListenerOcclusionClass.CLEAR);
            var sensor = new ListenerHearingSensor(resolver);
            var noise = CreateNoise(RuntimeNoiseType.INTERACTION, "noise-bound", 1, Vector3.zero, Now(), 1);

            Assert.That(sensor.TryEvaluate(noise, Vector3.zero, Now(), out _, out var reject), Is.False);
            Assert.That(reject, Is.EqualTo(ListenerHearingRejectReason.None));
            Assert.That(sensor.LastEvaluationStatus, Is.EqualTo(ListenerHearingEvaluationStatus.NotMatchBound));
            Assert.That(resolver.CallCount, Is.EqualTo(0));

            sensor.BeginMatch(Guid.NewGuid());
            Assert.That(sensor.TryEvaluate(noise, Vector3.zero, Now(), out _, out _), Is.True);
            Assert.That(resolver.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void LIS002_DoorAuthoringIsInheritedAndNotClassForced()
        {
            var door = new GameObject("door").AddComponent<NetworkDoor>();
            try
            {
                Assert.That(door.EmitsRuntimeInteractionNoise, Is.False);
                typeof(NetworkInteractable)
                    .GetField("_emitsRuntimeInteractionNoise", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(door, true);
                Assert.That(door.EmitsRuntimeInteractionNoise, Is.True);
                typeof(NetworkInteractable)
                    .GetField("_emitsRuntimeInteractionNoise", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(door, false);
                Assert.That(door.EmitsRuntimeInteractionNoise, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(door.gameObject);
            }
        }

        [Test]
        public void LIS001_RejectReasonPrecisionMarksAuthorityAndSourceContextInvalidPath()
        {
            var service = new GameObject("noise-service").AddComponent<EchoProtocol.Networking.Authority.HostRuntimeNoiseService>();
            service.BeginMatch(Guid.NewGuid());
            Assert.That(service.TryAccept(default, RuntimeNoiseType.INTERACTION, default, Vector3.zero, out _), Is.False);
            Assert.That(service.LastRejectReason, Is.EqualTo(NoiseValidationRejectReason.NotStateAuthority));
            Assert.That(service.LastDiagnosticReason, Is.EqualTo(NoiseSystemDiagnosticReason.SubsystemUnavailable));

            var method = typeof(EchoProtocol.Networking.Authority.HostRuntimeNoiseService)
                .GetMethod("TryAccept", new[]
                {
                    typeof(PlayerRef),
                    typeof(RuntimeNoiseType),
                    typeof(RuntimeNoiseSourceOccurrenceKey),
                    typeof(Vector3),
                    typeof(RuntimeNoiseEvent).MakeByRefType()
                });
            Assert.That(method, Is.Not.Null);
            var sourceText = File.ReadAllText("Assets/_Project/Scripts/Networking/Authority/HostRuntimeNoiseService.cs");
            StringAssert.Contains("if (!actor.IsValid)", sourceText);
            StringAssert.Contains("SourceActionRejected", sourceText);
            Object.DestroyImmediate(service.gameObject);
        }

        [Test]
        public void LIS001_LIS002_Q_R_S_AQ_AR_AS_ProductionContractsKeepNoiseTelemetryAndStalkerSeparated()
        {
            var hostSource = File.ReadAllText("Assets/_Project/Scripts/Networking/Authority/HostRuntimeNoiseService.cs");
            var movementSource = File.ReadAllText("Assets/_Project/Scripts/Networking/Player/NetworkPlayerMovement.cs");
            var interactionSource = File.ReadAllText("Assets/_Project/Scripts/Networking/Interaction/NetworkPlayerInteractor.cs");
            var pickupSource = File.ReadAllText("Assets/_Project/Scripts/Networking/Interaction/NetworkPickupItem.cs");
            var matchAuthoritySource = File.ReadAllText("Assets/_Project/Scripts/Networking/Authority/MatchAuthorityRuntime.cs");
            var interactableSource = File.ReadAllText("Assets/_Project/Scripts/Networking/Interaction/NetworkInteractable.cs");
            var doorSource = File.ReadAllText("Assets/_Project/Scripts/Networking/Interaction/NetworkDoor.cs");
            var toggleSource = File.ReadAllText("Assets/_Project/Scripts/Networking/Interaction/NetworkToggleInteractable.cs");
            var listenerRoot = "Assets/Scripts/AI/Listener";

            StringAssert.Contains("RuntimeNoiseAccepted", hostSource);
            StringAssert.Contains("BeginMatch(Guid matchId)", hostSource);
            StringAssert.Contains("_runtimeNoise?.BeginMatch(MatchId)", matchAuthoritySource);
            StringAssert.Contains("RecordRuntimeNoise", hostSource);
            StringAssert.Contains("noiseEvent.NoiseEventId", hostSource);
            StringAssert.Contains("noiseEvent.EmittedAtUtc", hostSource);
            StringAssert.Contains("EmitAcceptedRuntimeNoise", matchAuthoritySource);
            StringAssert.Contains("emittedAtUtc", matchAuthoritySource);
            StringAssert.DoesNotContain("position.x", hostSource);
            StringAssert.DoesNotContain("double loudness", hostSource);
            StringAssert.Contains("RuntimeNoiseSourceOccurrenceKey.ForMovement", movementSource);
            StringAssert.Contains("RuntimeNoiseSourceOccurrenceKey.ForInteraction", interactionSource);
            StringAssert.Contains("RuntimeNoiseSourceOccurrenceKey.ForTeamTool", interactionSource);
            StringAssert.Contains("target.EmitsRuntimeInteractionNoise", interactionSource);
            StringAssert.Contains("_emitsRuntimeInteractionNoise", interactableSource);
            StringAssert.DoesNotContain("public override bool EmitsRuntimeInteractionNoise => true", doorSource);
            StringAssert.DoesNotContain("EmitsRuntimeInteractionNoise => true", toggleSource);
            StringAssert.Contains("RuntimeNoiseSourceOccurrenceKey.ForCoreDrop", pickupSource);
            Assert.That(Directory.GetFiles(listenerRoot, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText)
                .Any(source => source.Contains("TelemetryEvent")), Is.False);
            Assert.That(Directory.GetFiles(listenerRoot, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText)
                .Any(source => source.Contains("EchoProtocol.AI.Stalker")), Is.False);
        }

        private static RuntimeNoiseEmissionRequest Request(
            RuntimeNoiseType noiseType,
            string streamKey,
            long sequence,
            Vector3 position,
            DateTime emittedAtUtc,
            long authoritativeTick)
        {
            return new RuntimeNoiseEmissionRequest(
                new RuntimeNoiseSourceOccurrenceKey(streamKey, sequence),
                noiseType,
                position,
                emittedAtUtc,
                authoritativeTick);
        }

        private static RuntimeNoiseEvent CreateNoise(
            RuntimeNoiseType noiseType,
            string sourceKey,
            long sequence,
            Vector3 position,
            DateTime emittedAtUtc,
            long authoritativeTick)
        {
            var system = new RuntimeNoiseSystem();
            Assert.That(system.TryAccept(
                Guid.NewGuid(),
                Request(noiseType, sourceKey, sequence, position, emittedAtUtc, authoritativeTick),
                out var noiseEvent), Is.EqualTo(RuntimeNoiseAcceptStatus.Accepted));
            return noiseEvent;
        }

        private static HearingObservation Observation(
            string noiseEventId,
            long ordinal,
            double intensity,
            double distance,
            DateTime nowUtc)
        {
            return Observation(noiseEventId, ordinal, intensity, distance, nowUtc, nowUtc);
        }

        private static HearingObservation Observation(
            string noiseEventId,
            long ordinal,
            double intensity,
            double distance,
            DateTime emittedAtUtc,
            DateTime heardAtUtc)
        {
            return new HearingObservation(
                noiseEventId,
                new RuntimeNoiseEventOrderKey(1, (ulong)ordinal),
                RuntimeNoiseType.INTERACTION,
                new Vector3((float)distance, 0, 0),
                emittedAtUtc,
                heardAtUtc,
                heardAtUtc.AddSeconds(2),
                distance,
                1d,
                intensity,
                ListenerOcclusionClass.CLEAR);
        }

        private static DateTime Now()
        {
            return DateTime.UtcNow;
        }

        private sealed class CountingResolver : IListenerOcclusionResolver
        {
            private readonly ListenerOcclusionClass _default;

            public CountingResolver(ListenerOcclusionClass @default)
            {
                _default = @default;
            }

            public int CallCount { get; private set; }

            public ListenerOcclusionClass Classify(Vector3 listenerPosition, Vector3 noisePosition)
            {
                CallCount++;
                return _default;
            }
        }

        private sealed class MutableResolver : IListenerOcclusionResolver
        {
            public MutableResolver(ListenerOcclusionClass occlusionClass)
            {
                OcclusionClass = occlusionClass;
            }

            public ListenerOcclusionClass OcclusionClass { get; set; }

            public ListenerOcclusionClass Classify(Vector3 listenerPosition, Vector3 noisePosition)
            {
                return OcclusionClass;
            }
        }
    }
}
