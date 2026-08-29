using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerAuthoritativeTargetCorePlayModeTests
    {
        private const string PlayerIdTypeName = "EchoProtocol.AI.Common.PlayerId";
        private const string AiSimulationTimeTypeName = "EchoProtocol.AI.Common.AiSimulationTime";
        private const string AiSimulationStepTypeName = "EchoProtocol.AI.Common.AiSimulationStep";
        private const string VisionObservationTypeName = "EchoProtocol.AI.Stalker.VisionObservation";
        private const string StalkerControllerTypeName = "EchoProtocol.AI.Stalker.StalkerController";
        private const string StalkerVisionSensorTypeName = "EchoProtocol.AI.Stalker.StalkerVisionSensor";
        private const string StalkerSimulationInputTypeName = "EchoProtocol.AI.Stalker.StalkerSimulationInput";
        private const string StalkerTargetCandidateTypeName = "EchoProtocol.AI.Stalker.StalkerTargetCandidate";
        private const string StalkerTargetStatusTypeName = "EchoProtocol.AI.Stalker.StalkerTargetStatus";
        private const string EligibilityResultTypeName = "EchoProtocol.AI.Stalker.StalkerTargetEligibilityResult";
        private const string EligibilityReasonTypeName = "EchoProtocol.AI.Stalker.StalkerTargetEligibilityReason";
        private const float FloatTolerance = 0.0001f;
        private const float VectorTolerance = 0.001f;

        private readonly List<GameObject> _createdObjects = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (var i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                {
                    UnityEngine.Object.Destroy(_createdObjects[i]);
                }
            }

            _createdObjects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_AUTH_PATROL_SelectsNearestEligibleVisiblePlayerId()
        {
            var fixture = CreateFixture();
            var candidates = CreateCandidateList(
                CreateCandidate(1, new Vector3(0f, 1f, 6f), 6f, true),
                CreateCandidate(2, new Vector3(0f, 1f, 2f), 2f, true),
                CreateCandidate(3, new Vector3(0f, 1f, 4f), 4f, true));

            Assert.That(Simulate(fixture.Controller, 0.1f, candidates, null), Is.True);

            AssertState(fixture.Controller, "DETECT");
            AssertPlayerIdValue(GetProperty(GetMemory(fixture.Controller), "DetectionTargetId"), 2);
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_AUTH_PATROL_DeterministicTieUsesStablePlayerId()
        {
            var fixture = CreateFixture();
            var candidates = CreateCandidateList(
                CreateCandidate(5, new Vector3(0f, 1f, 2f), 2f, true),
                CreateCandidate(2, new Vector3(1f, 1f, 2f), 2f, true));

            Assert.That(Simulate(fixture.Controller, 0.1f, candidates, null), Is.True);

            AssertState(fixture.Controller, "DETECT");
            AssertPlayerIdValue(GetProperty(GetMemory(fixture.Controller), "DetectionTargetId"), 2);
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_AUTH_PATROL_NearerIneligibleSelectsFartherEligible()
        {
            var fixture = CreateFixture();
            var candidates = CreateCandidateList(
                CreateCandidate(1, new Vector3(0f, 1f, 1f), 1f, false),
                CreateCandidate(2, new Vector3(0f, 1f, 3f), 3f, true));

            Assert.That(Simulate(fixture.Controller, 0.1f, candidates, null), Is.True);

            AssertState(fixture.Controller, "DETECT");
            AssertPlayerIdValue(GetProperty(GetMemory(fixture.Controller), "DetectionTargetId"), 2);
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_AUTH_PATROL_ToDetectRecordsDetectionTargetId()
        {
            var fixture = CreateFixture();
            var candidates = CreateCandidateList(CreateCandidate(7, new Vector3(0f, 1f, 3f), 3f, true));

            Assert.That(Simulate(fixture.Controller, 0.1f, candidates, null), Is.True);

            AssertState(fixture.Controller, "DETECT");
            AssertPlayerIdValue(GetProperty(GetMemory(fixture.Controller), "DetectionTargetId"), 7);
            Assert.That(GetFloatProperty(fixture.Controller, "DetectionMeter"), Is.EqualTo(0f).Within(FloatTolerance));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_AUTH_PATROL_ToDetectDoesNotTickDetectMeterSameSimulate()
        {
            var fixture = CreateFixture();
            SetPrivateField(fixture.Controller, "detectionFillRate", 100f);
            var candidates = CreateCandidateList(CreateCandidate(1, new Vector3(0f, 1f, 3f), 3f, true));

            Assert.That(Simulate(fixture.Controller, 0.25f, candidates, CreateStatusList(CreateStatus(1, true))), Is.True);

            AssertState(fixture.Controller, "DETECT");
            Assert.That(GetFloatProperty(fixture.Controller, "DetectionMeter"), Is.EqualTo(0f).Within(FloatTolerance));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_AUTH_DETECT_VisibleLockedTargetFillsWithExplicitDelta()
        {
            var fixture = CreateFixture();
            SetDetectLock(fixture.Controller, 1, 0f);
            SetPrivateField(fixture.Controller, "detectionFillRate", 2f);
            SetPrivateField(fixture.Controller, "detectionMeterFull", 10f);

            Assert.That(Simulate(fixture.Controller, 0.25f, CreateCandidateList(CreateCandidate(1, new Vector3(0f, 1f, 3f), 3f, true)), CreateStatusList(CreateStatus(1, true))), Is.True);

            AssertState(fixture.Controller, "DETECT");
            Assert.That(GetFloatProperty(fixture.Controller, "DetectionMeter"), Is.EqualTo(0.5f).Within(FloatTolerance));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_AUTH_DETECT_RejectedObservationDoesNotFillMeter()
        {
            var fixture = CreateFixture();
            SetDetectLock(fixture.Controller, 1, 0f);
            AcceptDetectionObservation(fixture.Controller, 1, new Vector3(0f, 1f, 4f), 20L);
            SetPrivateField(fixture.Controller, "detectionFillRate", 100f);

            Assert.That(Simulate(fixture.Controller, 0.25f, CreateCandidateList(CreateCandidate(1, new Vector3(0f, 1f, 3f), 3f, true)), CreateStatusList(CreateStatus(1, true))), Is.True);

            AssertState(fixture.Controller, "PATROL");
            Assert.That(GetFloatProperty(fixture.Controller, "DetectionMeter"), Is.EqualTo(0f).Within(FloatTolerance));
            AssertInvalidPlayerId(GetProperty(GetMemory(fixture.Controller), "DetectionTargetId"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_AUTH_DETECT_DoesNotSwitchToAnotherVisiblePlayerWhileLocked()
        {
            var fixture = CreateFixture();
            SetDetectLock(fixture.Controller, 1, 1f);
            SetPrivateField(fixture.Controller, "detectionDecayRate", 1f);

            Assert.That(Simulate(fixture.Controller, 0.25f, CreateCandidateList(CreateCandidate(2, new Vector3(0f, 1f, 2f), 2f, true)), CreateStatusList(CreateStatus(1, true), CreateStatus(2, true))), Is.True);

            AssertState(fixture.Controller, "DETECT");
            AssertPlayerIdValue(GetProperty(GetMemory(fixture.Controller), "DetectionTargetId"), 1);
            Assert.That(GetFloatProperty(fixture.Controller, "DetectionMeter"), Is.EqualTo(0.75f).Within(FloatTolerance));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_AUTH_DETECT_HiddenEligibleDecaysAndDoesNotUpdateLastKnownPosition()
        {
            var fixture = CreateFixture();
            var oldPosition = new Vector3(4f, 1f, 4f);
            SetDetectLock(fixture.Controller, 1, 1f);
            AcceptDetectionObservation(fixture.Controller, 1, oldPosition, 1L);
            SetPrivateField(fixture.Controller, "lastKnownPosition", oldPosition);
            SetPrivateField(fixture.Controller, "detectionDecayRate", 2f);

            Assert.That(Simulate(fixture.Controller, 0.25f, CreateCandidateList(), CreateStatusList(CreateStatus(1, true))), Is.True);

            AssertState(fixture.Controller, "DETECT");
            Assert.That(GetFloatProperty(fixture.Controller, "DetectionMeter"), Is.EqualTo(0.5f).Within(FloatTolerance));
            AssertVectorNear(GetVector3Property(fixture.Controller, "LastKnownPosition"), oldPosition);
            AssertVectorNear(GetVector3Property(GetMemory(fixture.Controller), "LastKnownPosition"), oldPosition);
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_AUTH_DETECT_HiddenIneligibleInvalidatesAndReturnsPatrol()
        {
            var fixture = CreateFixture();
            SetDetectLock(fixture.Controller, 1, 1f);

            Assert.That(Simulate(fixture.Controller, 0.1f, CreateCandidateList(), CreateStatusList(CreateStatus(1, false))), Is.True);

            AssertState(fixture.Controller, "PATROL");
            AssertInvalidPlayerId(GetProperty(GetMemory(fixture.Controller), "DetectionTargetId"));
            Assert.That(GetFloatProperty(fixture.Controller, "DetectionMeter"), Is.EqualTo(0f).Within(FloatTolerance));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_AUTH_DETECT_MissingOrDuplicateStatusFailsClosed()
        {
            var fixture = CreateFixture();
            SetDetectLock(fixture.Controller, 1, 1f);

            Assert.That(Simulate(fixture.Controller, 0.1f, CreateCandidateList(), CreateStatusList()), Is.True);
            AssertState(fixture.Controller, "PATROL");
            AssertInvalidPlayerId(GetProperty(GetMemory(fixture.Controller), "DetectionTargetId"));

            fixture = CreateFixture();
            SetDetectLock(fixture.Controller, 1, 1f);
            Assert.That(Simulate(fixture.Controller, 0.1f, CreateCandidateList(), CreateStatusList(CreateStatus(1, true), CreateStatus(1, true))), Is.True);
            AssertState(fixture.Controller, "PATROL");
            AssertInvalidPlayerId(GetProperty(GetMemory(fixture.Controller), "DetectionTargetId"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_AUTH_DETECT_FullPromotesCorrectPlayerIdToCurrentTargetId()
        {
            var fixture = CreateFixture();
            SetDetectLock(fixture.Controller, 1, 0.9f);
            SetPrivateField(fixture.Controller, "detectionFillRate", 1f);
            SetPrivateField(fixture.Controller, "detectionMeterFull", 1f);

            Assert.That(Simulate(fixture.Controller, 0.2f, CreateCandidateList(CreateCandidate(1, new Vector3(0f, 1f, 3f), 3f, true)), CreateStatusList(CreateStatus(1, true))), Is.True);

            AssertState(fixture.Controller, "CHASE");
            AssertInvalidPlayerId(GetProperty(GetMemory(fixture.Controller), "DetectionTargetId"));
            AssertPlayerIdValue(GetProperty(GetMemory(fixture.Controller), "CurrentTargetId"), 1);
            Assert.That((bool)GetProperty(GetMemory(fixture.Controller), "HasLastKnownPosition"), Is.True);
            AssertVectorNear(GetVector3Property(GetMemory(fixture.Controller), "LastKnownPosition"), new Vector3(0f, 1f, 3f));
            Assert.That((bool)GetProperty(GetMemory(fixture.Controller), "HasLastSeenDirection"), Is.True);
            Assert.That((bool)GetProperty(GetMemory(fixture.Controller), "HasTargetLastSeenTime"), Is.True);
            Assert.That(GetProperty(GetMemory(fixture.Controller), "TargetLastSeenTime"), Is.EqualTo(Activator.CreateInstance(ResolveType(AiSimulationTimeTypeName), 10L, 1d)));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_AUTH_DETECT_ToChaseDoesNotTickChaseOrAttackSameSimulate()
        {
            var fixture = CreateFixture();
            SetDetectLock(fixture.Controller, 1, 0.9f);
            SetPrivateField(fixture.Controller, "detectionFillRate", 1f);
            SetPrivateField(fixture.Controller, "detectionMeterFull", 1f);
            SetPrivateField(fixture.Controller, "attackRange", 100f);

            Assert.That(Simulate(fixture.Controller, 0.2f, CreateCandidateList(CreateCandidate(1, new Vector3(0f, 1f, 1f), 1f, true)), CreateStatusList(CreateStatus(1, true))), Is.True);

            AssertState(fixture.Controller, "CHASE");
            Assert.That(GetFloatProperty(fixture.Controller, "AttackElapsedTime"), Is.EqualTo(0f).Within(FloatTolerance));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_AUTH_CHASE_VisibleCurrentTargetUpdatesLastKnownPosition()
        {
            var fixture = CreateFixture();
            SetCurrentTarget(fixture.Controller, 1, new Vector3(0f, 1f, 4f));
            var newPosition = new Vector3(3f, 1f, 5f);

            Assert.That(Simulate(fixture.Controller, 0.1f, CreateCandidateList(CreateCandidate(1, newPosition, 5f, true)), CreateStatusList(CreateStatus(1, true))), Is.True);

            AssertState(fixture.Controller, "CHASE");
            AssertVectorNear(GetVector3Property(fixture.Controller, "LastKnownPosition"), newPosition);
            AssertVectorNear(GetVector3Property(GetMemory(fixture.Controller), "LastKnownPosition"), newPosition);
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_AUTH_CHASE_IgnoresCloserVisiblePlayerWhileLocked()
        {
            var fixture = CreateFixture();
            SetCurrentTarget(fixture.Controller, 1, new Vector3(0f, 1f, 4f));
            var lockedPosition = new Vector3(5f, 1f, 5f);
            var closerOther = new Vector3(0f, 1f, 1f);

            Assert.That(Simulate(fixture.Controller, 0.1f, CreateCandidateList(CreateCandidate(2, closerOther, 1f, true), CreateCandidate(1, lockedPosition, 5f, true)), CreateStatusList(CreateStatus(1, true), CreateStatus(2, true))), Is.True);

            AssertState(fixture.Controller, "CHASE");
            AssertVectorNear(GetVector3Property(fixture.Controller, "LastKnownPosition"), lockedPosition);
            AssertPlayerIdValue(GetProperty(GetMemory(fixture.Controller), "CurrentTargetId"), 1);
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_AUTH_CHASE_HiddenEligibleFreezesLastKnownPosition()
        {
            var fixture = CreateFixture();
            var oldPosition = new Vector3(0f, 1f, 4f);
            SetCurrentTarget(fixture.Controller, 1, oldPosition);

            Assert.That(Simulate(fixture.Controller, 0.1f, CreateCandidateList(), CreateStatusList(CreateStatus(1, true))), Is.True);

            AssertState(fixture.Controller, "SEARCH");
            Assert.That(GetFloatProperty(fixture.Controller, "SearchElapsedTime"), Is.EqualTo(0f).Within(FloatTolerance));
            AssertVectorNear(GetVector3Property(fixture.Controller, "LastKnownPosition"), oldPosition);
            AssertVectorNear(GetVector3Property(GetMemory(fixture.Controller), "LastKnownPosition"), oldPosition);
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_AUTH_CHASE_HiddenEligibleDoesNotReadHiddenTransformPosition()
        {
            var fixture = CreateFixture();
            var oldPosition = new Vector3(0f, 1f, 4f);
            var hiddenObject = new GameObject("STK_AUTH_HiddenLegacyTransform");
            hiddenObject.transform.position = new Vector3(99f, 1f, 99f);
            _createdObjects.Add(hiddenObject);
            SetCurrentTarget(fixture.Controller, 1, oldPosition);
            SetPrivateField(fixture.Controller, "currentTarget", hiddenObject.transform);

            Assert.That(Simulate(fixture.Controller, 0.1f, CreateCandidateList(), CreateStatusList(CreateStatus(1, true))), Is.True);

            AssertState(fixture.Controller, "SEARCH");
            Assert.That(Vector3.Distance(GetVector3Property(fixture.Controller, "LastKnownPosition"), hiddenObject.transform.position), Is.GreaterThan(1f));
            AssertVectorNear(GetVector3Property(fixture.Controller, "LastKnownPosition"), oldPosition);
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_AUTH_CHASE_HiddenEligibleWithoutRememberedPosition_FailsClosed()
        {
            var fixture = CreateFixture();
            SetCurrentTargetWithoutObservation(fixture.Controller, 1);
            SetPrivateField(fixture.Controller, "lastKnownPosition", new Vector3(99f, 1f, 99f));
            SetPrivateField(fixture.Controller, "searchDuration", 10f);

            Assert.That(Simulate(fixture.Controller, 0.1f, CreateCandidateList(), CreateStatusList(CreateStatus(1, true))), Is.True);

            AssertState(fixture.Controller, "PATROL");
            AssertInvalidPlayerId(GetProperty(GetMemory(fixture.Controller), "CurrentTargetId"));
            Assert.That((bool)GetProperty(GetMemory(fixture.Controller), "HasLastKnownPosition"), Is.False);
            Assert.That(GetFloatProperty(fixture.Controller, "SearchElapsedTime"), Is.EqualTo(0f).Within(FloatTolerance));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_AUTH_CHASE_IneligibleCurrentTargetInvalidatesToPatrol()
        {
            var fixture = CreateFixture();
            SetCurrentTarget(fixture.Controller, 1, new Vector3(0f, 1f, 4f));

            Assert.That(Simulate(fixture.Controller, 0.1f, CreateCandidateList(), CreateStatusList(CreateStatus(1, false))), Is.True);

            AssertState(fixture.Controller, "PATROL");
            AssertInvalidPlayerId(GetProperty(GetMemory(fixture.Controller), "CurrentTargetId"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_AUTH_SEARCH_HiddenEligibleTarget_RemainsSearching()
        {
            var fixture = CreateFixture();
            var oldPosition = new Vector3(0f, 1f, 4f);
            SetCurrentTarget(fixture.Controller, 1, oldPosition);

            Assert.That(Simulate(fixture.Controller, 0.1f, CreateCandidateList(), CreateStatusList(CreateStatus(1, true))), Is.True);
            AssertState(fixture.Controller, "SEARCH");

            SetPrivateField(fixture.Controller, "searchDuration", 10f);
            Assert.That(Simulate(fixture.Controller, 0.25f, CreateCandidateList(), CreateStatusList(CreateStatus(1, true))), Is.True);

            AssertState(fixture.Controller, "SEARCH");
            Assert.That(GetProperty(fixture.Controller, "CurrentTarget"), Is.Null);
            AssertPlayerIdValue(GetProperty(GetMemory(fixture.Controller), "CurrentTargetId"), 1);
            AssertVectorNear(GetVector3Property(fixture.Controller, "LastKnownPosition"), oldPosition);
            AssertVectorNear(GetVector3Property(GetMemory(fixture.Controller), "LastKnownPosition"), oldPosition);
            Assert.That(GetFloatProperty(fixture.Controller, "SearchElapsedTime"), Is.EqualTo(0.25f).Within(FloatTolerance));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_AUTH_SEARCH_HiddenEligibleTarget_DoesNotReadHiddenTransform()
        {
            var fixture = CreateFixture();
            var oldPosition = new Vector3(0f, 1f, 4f);
            var hiddenObject = new GameObject("STK_AUTH_SearchHiddenLegacyTransform");
            hiddenObject.transform.position = new Vector3(8f, 1f, 8f);
            _createdObjects.Add(hiddenObject);
            SetCurrentTarget(fixture.Controller, 1, oldPosition);
            SetState(fixture.Controller, "SEARCH");
            SetPrivateField(fixture.Controller, "currentTarget", hiddenObject.transform);

            hiddenObject.transform.position = new Vector3(99f, 1f, 99f);
            Assert.That(Simulate(fixture.Controller, 0.25f, CreateCandidateList(), CreateStatusList(CreateStatus(1, true))), Is.True);

            AssertState(fixture.Controller, "SEARCH");
            Assert.That(Vector3.Distance(GetVector3Property(fixture.Controller, "LastKnownPosition"), hiddenObject.transform.position), Is.GreaterThan(1f));
            AssertVectorNear(GetVector3Property(fixture.Controller, "LastKnownPosition"), oldPosition);
            AssertVectorNear(GetVector3Property(GetMemory(fixture.Controller), "LastKnownPosition"), oldPosition);
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_AUTH_SEARCH_ReacquiresSamePlayerIdToChase()
        {
            var fixture = CreateFixture();
            var oldPosition = new Vector3(0f, 1f, 4f);
            var reacquiredPosition = new Vector3(3f, 1f, 5f);
            var otherPosition = new Vector3(0f, 1f, 1f);
            SetCurrentTarget(fixture.Controller, 1, oldPosition);
            SetState(fixture.Controller, "SEARCH");

            Assert.That(Simulate(
                fixture.Controller,
                0.1f,
                CreateCandidateList(
                    CreateCandidate(2, otherPosition, 1f, true),
                    CreateCandidate(1, reacquiredPosition, 5f, true)),
                CreateStatusList(CreateStatus(1, true), CreateStatus(2, true))),
                Is.True);

            AssertState(fixture.Controller, "CHASE");
            AssertPlayerIdValue(GetProperty(GetMemory(fixture.Controller), "CurrentTargetId"), 1);
            AssertVectorNear(GetVector3Property(fixture.Controller, "LastKnownPosition"), reacquiredPosition);
            AssertVectorNear(GetVector3Property(GetMemory(fixture.Controller), "LastKnownPosition"), reacquiredPosition);
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_AUTH_SEARCH_InvalidTargetStatus_ReturnsPatrol()
        {
            var fixture = CreateFixture();
            SetCurrentTarget(fixture.Controller, 1, new Vector3(0f, 1f, 4f));
            SetState(fixture.Controller, "SEARCH");

            Assert.That(Simulate(fixture.Controller, 0.1f, CreateCandidateList(), CreateStatusList(CreateStatus(1, false))), Is.True);

            AssertState(fixture.Controller, "PATROL");
            AssertInvalidPlayerId(GetProperty(GetMemory(fixture.Controller), "CurrentTargetId"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_AUTH_SEARCH_Timeout_ReturnsPatrol()
        {
            var fixture = CreateFixture();
            SetCurrentTarget(fixture.Controller, 1, new Vector3(0f, 1f, 4f));
            SetState(fixture.Controller, "SEARCH");
            SetPrivateField(fixture.Controller, "searchDuration", 0.5f);

            Assert.That(Simulate(fixture.Controller, 0.5f, CreateCandidateList(), CreateStatusList(CreateStatus(1, true))), Is.True);

            AssertState(fixture.Controller, "PATROL");
            AssertInvalidPlayerId(GetProperty(GetMemory(fixture.Controller), "CurrentTargetId"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_AUTH_CHASE_MissingOrDuplicateStatusFailsClosed()
        {
            var fixture = CreateFixture();
            SetCurrentTarget(fixture.Controller, 1, new Vector3(0f, 1f, 4f));

            Assert.That(Simulate(fixture.Controller, 0.1f, CreateCandidateList(), CreateStatusList()), Is.True);
            AssertState(fixture.Controller, "PATROL");
            AssertInvalidPlayerId(GetProperty(GetMemory(fixture.Controller), "CurrentTargetId"));

            fixture = CreateFixture();
            SetCurrentTarget(fixture.Controller, 1, new Vector3(0f, 1f, 4f));
            Assert.That(Simulate(fixture.Controller, 0.1f, CreateCandidateList(), CreateStatusList(CreateStatus(1, true), CreateStatus(1, true))), Is.True);
            AssertState(fixture.Controller, "PATROL");
            AssertInvalidPlayerId(GetProperty(GetMemory(fixture.Controller), "CurrentTargetId"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_AUTH_EmptyTypedVisibleList_DoesNotUseLegacySensorFallback()
        {
            var fixture = CreateFixture();
            var legacyTarget = new GameObject("STK_AUTH_LegacyCandidate");
            legacyTarget.transform.position = new Vector3(0f, 1f, 3f);
            _createdObjects.Add(legacyTarget);
            SetSensorFields(fixture.VisionSensor, fixture.Origin, legacyTarget.transform, 10f, 90f, 0);
            Physics.SyncTransforms();

            Assert.That(Simulate(fixture.Controller, 0.1f, CreateCandidateList(), null), Is.True);

            AssertState(fixture.Controller, "PATROL");
            Assert.That(GetProperty(fixture.Controller, "DetectionTarget"), Is.Null);
            AssertInvalidPlayerId(GetProperty(GetMemory(fixture.Controller), "DetectionTargetId"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_AUTH_NullTypedLists_UseLegacyCompatibilitySensorPath()
        {
            var fixture = CreateFixture();
            var legacyTarget = new GameObject("STK_AUTH_LegacyCandidate");
            legacyTarget.transform.position = new Vector3(0f, 1f, 3f);
            _createdObjects.Add(legacyTarget);
            SetSensorFields(fixture.VisionSensor, fixture.Origin, legacyTarget.transform, 10f, 90f, 0);
            Physics.SyncTransforms();

            Assert.That(Simulate(fixture.Controller, 0.1f, null, null), Is.True);

            AssertState(fixture.Controller, "DETECT");
            Assert.That(GetProperty(fixture.Controller, "DetectionTarget"), Is.SameAs(legacyTarget.transform));
            yield return null;
        }

        private StalkerFixture CreateFixture()
        {
            var controllerType = ResolveType(StalkerControllerTypeName);
            var sensorType = ResolveType(StalkerVisionSensorTypeName);
            var stalker = new GameObject("STK_AUTH_Stalker");
            _createdObjects.Add(stalker);

            var origin = new GameObject("STK_AUTH_Origin");
            origin.transform.SetParent(stalker.transform, false);
            origin.transform.localPosition = new Vector3(0f, 1f, 0f);

            var sensor = (Component)stalker.AddComponent(sensorType);
            var controller = (Component)stalker.AddComponent(controllerType);
            var navMeshAgent = stalker.GetComponent<NavMeshAgent>();
            Assert.That(navMeshAgent, Is.Not.Null);
            navMeshAgent.enabled = false;
            ((Behaviour)controller).enabled = false;
            SetPrivateField(controller, "visionSensor", sensor);
            return new StalkerFixture(controller, sensor, origin.transform);
        }

        private static void SetDetectLock(Component controller, int playerId, float meter)
        {
            SetState(controller, "DETECT");
            InvokeMethod(GetMemory(controller), "SetDetectionTarget", new[] { ResolveType(PlayerIdTypeName) }, new[] { CreatePlayerId(playerId) });
            InvokeMethod(GetMemory(controller), "SetDetectionMeter", new[] { typeof(float) }, new object[] { meter });
            SetPrivateField(controller, "detectionMeter", meter);
        }

        private static void SetCurrentTarget(Component controller, int playerId, Vector3 lastKnownPositionValue)
        {
            SetState(controller, "CHASE");
            var memory = GetMemory(controller);
            InvokeMethod(memory, "SetCurrentTarget", new[] { ResolveType(PlayerIdTypeName) }, new[] { CreatePlayerId(playerId) });
            InvokeMethod(memory, "TryAcceptCurrentTargetObservation", new[] { ResolveType(VisionObservationTypeName) }, new[] { CreateObservation(playerId, lastKnownPositionValue, 1L, 1d, 4f) });
            SetPrivateField(controller, "lastKnownPosition", lastKnownPositionValue);
            SetPrivateField(controller, "attackRange", 0.1f);
        }

        private static void SetCurrentTargetWithoutObservation(Component controller, int playerId)
        {
            SetState(controller, "CHASE");
            InvokeMethod(GetMemory(controller), "SetCurrentTarget", new[] { ResolveType(PlayerIdTypeName) }, new[] { CreatePlayerId(playerId) });
            SetPrivateField(controller, "attackRange", 0.1f);
        }

        private static void AcceptDetectionObservation(Component controller, int playerId, Vector3 position, long tick)
        {
            InvokeMethod(
                GetMemory(controller),
                "TryAcceptDetectionTargetObservation",
                new[] { ResolveType(VisionObservationTypeName) },
                new[] { CreateObservation(playerId, position, tick, (double)tick, 4f) });
        }

        private static bool Simulate(Component controller, float deltaSeconds, object candidates, object statuses)
        {
            var input = Activator.CreateInstance(
                ResolveType(StalkerSimulationInputTypeName),
                Activator.CreateInstance(
                    ResolveType(AiSimulationStepTypeName),
                    Activator.CreateInstance(ResolveType(AiSimulationTimeTypeName), 10L, 1d),
                    deltaSeconds),
                candidates,
                statuses);
            var result = InvokeMethod(controller, "Simulate", new[] { ResolveType(StalkerSimulationInputTypeName) }, new[] { input });
            Assert.That(result, Is.TypeOf<bool>());
            return (bool)result;
        }

        private static object CreateCandidate(int playerId, Vector3 position, float distance, bool eligible)
        {
            return Activator.CreateInstance(
                ResolveType(StalkerTargetCandidateTypeName),
                CreateObservation(playerId, position, 10L, 1d, distance),
                eligible ? CreateEligibleResult() : CreateIneligibleResult("Downed"));
        }

        private static object CreateObservation(int playerId, Vector3 position, long tick, double seconds, float distance)
        {
            return Activator.CreateInstance(
                ResolveType(VisionObservationTypeName),
                CreatePlayerId(playerId),
                position,
                (position - Vector3.zero).normalized,
                Activator.CreateInstance(ResolveType(AiSimulationTimeTypeName), tick, seconds),
                distance);
        }

        private static object CreateStatus(int playerId, bool eligible)
        {
            return Activator.CreateInstance(
                ResolveType(StalkerTargetStatusTypeName),
                CreatePlayerId(playerId),
                eligible ? CreateEligibleResult() : CreateIneligibleResult("Downed"));
        }

        private static object CreateCandidateList(params object[] candidates)
        {
            return CreateTypedList(StalkerTargetCandidateTypeName, candidates);
        }

        private static object CreateStatusList(params object[] statuses)
        {
            return CreateTypedList(StalkerTargetStatusTypeName, statuses);
        }

        private static object CreateTypedList(string typeName, params object[] values)
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(ResolveType(typeName)));
            for (var i = 0; i < values.Length; i++)
            {
                list.Add(values[i]);
            }

            return list;
        }

        private static object CreateEligibleResult()
        {
            return ResolveType(EligibilityResultTypeName)
                .GetMethod("EligibleTarget", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, Array.Empty<object>());
        }

        private static object CreateIneligibleResult(string reasonName)
        {
            var reason = Enum.Parse(ResolveType(EligibilityReasonTypeName), reasonName);
            return ResolveType(EligibilityResultTypeName)
                .GetMethod("Ineligible", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new[] { reason });
        }

        private static object CreatePlayerId(int value)
        {
            return Activator.CreateInstance(ResolveType(PlayerIdTypeName), value);
        }

        private static object GetMemory(Component controller)
        {
            var field = controller.GetType().GetField("_memory", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing private StalkerController._memory field.");
            return field.GetValue(controller);
        }

        private static void SetSensorFields(Component sensor, Transform origin, Transform candidate, float distance, float angle, int maskValue)
        {
            var mask = default(LayerMask);
            mask.value = maskValue;
            SetPrivateField(sensor, "visionOrigin", origin);
            SetPrivateField(sensor, "candidate", candidate);
            SetPrivateField(sensor, "visionDistance", distance);
            SetPrivateField(sensor, "visionAngle", angle);
            SetPrivateField(sensor, "losBlockerMask", mask);
        }

        private static void SetState(Component controller, string stateName)
        {
            SetPrivateField(controller, "currentState", Enum.Parse(ResolveType("EchoProtocol.AI.Stalker.StalkerState"), stateName));
        }

        private static object InvokeMethod(object target, string methodName, Type[] parameterTypes, object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public, null, parameterTypes, null);
            Assert.That(method, Is.Not.Null, $"Missing public method '{methodName}' on '{target.GetType().FullName}'.");
            return method.Invoke(target, args);
        }

        private static object GetProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Missing public property '{propertyName}' on '{target.GetType().FullName}'.");
            return property.GetValue(target);
        }

        private static float GetFloatProperty(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.TypeOf<float>());
            return (float)value;
        }

        private static Vector3 GetVector3Property(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.TypeOf<Vector3>());
            return (Vector3)value;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}' on '{target.GetType().FullName}'.");
            field.SetValue(target, value);
        }

        private static void AssertState(Component controller, string expected)
        {
            Assert.That(GetProperty(controller, "CurrentState").ToString(), Is.EqualTo(expected));
        }

        private static void AssertPlayerIdValue(object playerId, int expected)
        {
            Assert.That((bool)GetProperty(playerId, "IsValid"), Is.True);
            Assert.That((int)GetProperty(playerId, "Value"), Is.EqualTo(expected));
        }

        private static void AssertInvalidPlayerId(object playerId)
        {
            Assert.That((bool)GetProperty(playerId, "IsValid"), Is.False);
        }

        private static void AssertVectorNear(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThanOrEqualTo(VectorTolerance));
        }

        private static Type ResolveType(string fullTypeName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                var type = assemblies[i].GetType(fullTypeName, false);
                if (type != null)
                {
                    return type;
                }
            }

            Assert.Fail($"Could not find production type '{fullTypeName}' in the loaded Unity AppDomain.");
            return null;
        }

        private readonly struct StalkerFixture
        {
            public StalkerFixture(Component controller, Component visionSensor, Transform origin)
            {
                Controller = controller;
                VisionSensor = visionSensor;
                Origin = origin;
            }

            public Component Controller { get; }

            public Component VisionSensor { get; }

            public Transform Origin { get; }
        }
    }
}
