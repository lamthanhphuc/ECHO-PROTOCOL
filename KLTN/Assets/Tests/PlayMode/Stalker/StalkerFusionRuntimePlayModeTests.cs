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
    public sealed class StalkerFusionRuntimePlayModeTests
    {
        private const string RuntimeTypeName = "EchoProtocol.AI.Stalker.Networking.StalkerFusionRuntime";
        private const string ControllerTypeName = "EchoProtocol.AI.Stalker.StalkerController";
        private const string SensorTypeName = "EchoProtocol.AI.Stalker.StalkerVisionSensor";
        private const string LifecycleTypeName = "EchoProtocol.Networking.FusionPlayerLifecycle";
        private const string PlayerRuntimeIdentityTypeName = "EchoProtocol.Player.PlayerRuntimeIdentity";
        private const string PlayerIdTypeName = "EchoProtocol.AI.Common.PlayerId";
        private const string PlayerRefTypeName = "Fusion.PlayerRef";
        private const string AiSimulationTimeTypeName = "EchoProtocol.AI.Common.AiSimulationTime";
        private const string AiSimulationStepTypeName = "EchoProtocol.AI.Common.AiSimulationStep";
        private const string VisionObservationTypeName = "EchoProtocol.AI.Stalker.VisionObservation";
        private const string DiagnosticAttackSinkTypeName = "EchoProtocol.AI.Stalker.StalkerDiagnosticAttackConsequenceSink";
        private const string SearchOutcomeTypeName = "EchoProtocol.AI.Stalker.Telemetry.StalkerSearchTerminalOutcome";
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
        public IEnumerator RUNTIME_01_NoAuthority_DoesNotExecuteStalkerSimulation()
        {
            var fixture = CreateRuntimeFixture();

            InvokeInstanceMethod(fixture.Runtime, "FixedUpdateNetwork", Type.EmptyTypes, Array.Empty<object>());

            Assert.That(GetIntProperty(fixture.Runtime, "AuthoritativeSimulationCount"), Is.EqualTo(0));
            AssertState(fixture.Controller, "PATROL");
            yield return null;
        }

        [UnityTest]
        public IEnumerator RUNTIME_02_InvalidOrNotRunningRunner_DoesNotExecuteSimulation()
        {
            var fixture = CreateRuntimeFixture();
            InjectLifecycle(fixture.Runtime, CreateLifecycle());

            InvokeInstanceMethod(fixture.Runtime, "FixedUpdateNetwork", Type.EmptyTypes, Array.Empty<object>());

            Assert.That(GetIntProperty(fixture.Runtime, "AuthoritativeSimulationCount"), Is.EqualTo(0));
            AssertInvalidPlayerId(GetProperty(fixture.Controller, "DetectionTargetId"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RUNTIME_PIPELINE_03_ValidTypedFrameExecutesOneSimulation()
        {
            var fixture = CreateRuntimeFixture();
            var lifecycle = CreateLifecycle();
            InjectLifecycle(fixture.Runtime, lifecycle);
            RegisterActivePlayer(lifecycle, 1, 1, new Vector3(0f, 1f, 4f));

            Assert.That(RunPipeline(fixture.Runtime, 21L, 2d, 0.1f), Is.True);

            Assert.That(GetIntProperty(fixture.Runtime, "AuthoritativeSimulationCount"), Is.EqualTo(1));
            AssertState(fixture.Controller, "DETECT");
            AssertPlayerIdValue(GetProperty(fixture.Controller, "DetectionTargetId"), 1);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RUNTIME_PIPELINE_04_SuppliedSimulationStepStampsCandidateObservation()
        {
            var fixture = CreateRuntimeFixture();
            var lifecycle = CreateLifecycle();
            InjectLifecycle(fixture.Runtime, lifecycle);
            RegisterActivePlayer(lifecycle, 1, 1, new Vector3(0f, 1f, 4f));

            Assert.That(RunPipeline(fixture.Runtime, 42L, 3.5d, 0.1f), Is.True);

            var observation = GetProperty(GetMemory(fixture.Controller), "LastDetectionTargetObservation");
            Assert.That(GetProperty(observation, "ObservedAt"), Is.EqualTo(CreateSimulationTime(42L, 3.5d)));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RUNTIME_PIPELINE_05_CandidateObservationTimeEqualsSuppliedStepTime()
        {
            var fixture = CreateRuntimeFixture();
            var lifecycle = CreateLifecycle();
            InjectLifecycle(fixture.Runtime, lifecycle);
            RegisterActivePlayer(lifecycle, 1, 1, new Vector3(0f, 1f, 4f));

            Assert.That(RunPipeline(fixture.Runtime, 77L, 8.25d, 0.1f), Is.True);

            var observedAt = GetProperty(GetProperty(GetMemory(fixture.Controller), "LastDetectionTargetObservation"), "ObservedAt");
            Assert.That(observedAt, Is.EqualTo(CreateSimulationTime(77L, 8.25d)));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RUNTIME_06_EmptyActivePlayerFrameIsTypedAndDoesNotUseLegacyFallback()
        {
            var fixture = CreateRuntimeFixture();
            InjectLifecycle(fixture.Runtime, CreateLifecycle());
            SetLegacySensorCandidate(fixture.Sensor, CreatePlayerObject("RUNTIME_LegacyCandidate", new Vector3(0f, 1f, 3f)).transform);

            Assert.That(RunPipeline(fixture.Runtime, 1L, 1d, 0.1f), Is.True);

            AssertState(fixture.Controller, "PATROL");
            AssertInvalidPlayerId(GetProperty(fixture.Controller, "DetectionTargetId"));
            Assert.That(GetProperty(fixture.Controller, "DetectionTarget"), Is.Null);
            Assert.That(GetPrivateListCount(fixture.Runtime, "_perceptionSnapshots"), Is.EqualTo(0));
            Assert.That(GetPrivateListCount(fixture.Runtime, "_targetStatuses"), Is.EqualTo(0));
            Assert.That(GetPrivateListCount(fixture.Runtime, "_visibleCandidates"), Is.EqualTo(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RUNTIME_11_FrameBuilderIntegrityFailureDoesNotInvokeControllerSimulation()
        {
            var fixture = CreateRuntimeFixture();
            var lifecycle = CreateLifecycle();
            InjectLifecycle(fixture.Runtime, lifecycle);
            RegisterLogicalPlayer(lifecycle, 1);

            Assert.That(RunPipeline(fixture.Runtime, 1L, 1d, 0.1f), Is.False);

            Assert.That(GetIntProperty(fixture.Runtime, "AuthoritativeSimulationCount"), Is.EqualTo(0));
            AssertState(fixture.Controller, "PATROL");
            Assert.That(GetPrivateListCount(fixture.Runtime, "_perceptionSnapshots"), Is.EqualTo(0));
            Assert.That(GetPrivateListCount(fixture.Runtime, "_targetStatuses"), Is.EqualTo(0));
            Assert.That(GetPrivateListCount(fixture.Runtime, "_visibleCandidates"), Is.EqualTo(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RUNTIME_07_DisconnectedLockedTargetStatusAllowsReselectingVisibleEligiblePlayer()
        {
            var fixture = CreateRuntimeFixture();
            var lifecycle = CreateLifecycle();
            InjectLifecycle(fixture.Runtime, lifecycle);
            SetCurrentTarget(fixture.Controller, 1, new Vector3(0f, 1f, 4f));
            RegisterLogicalPlayer(lifecycle, 1);
            UnregisterPlayer(lifecycle, 1);
            RegisterActivePlayer(lifecycle, 2, 2, new Vector3(0f, 1f, 3f));

            Assert.That(RunPipeline(fixture.Runtime, 2L, 2d, 0.1f), Is.True);

            AssertState(fixture.Controller, "DETECT");
            AssertInvalidPlayerId(GetProperty(fixture.Controller, "CurrentTargetId"));
            AssertPlayerIdValue(GetProperty(fixture.Controller, "DetectionTargetId"), 2);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RUNTIME_08_NoRunnerOrAuthorityDoesNotSimulate()
        {
            var fixture = CreateRuntimeFixture();
            var lifecycle = CreateLifecycle();
            InjectLifecycle(fixture.Runtime, lifecycle);
            RegisterActivePlayer(lifecycle, 1, 1, new Vector3(0f, 1f, 4f));

            InvokeInstanceMethod(fixture.Runtime, "FixedUpdateNetwork", Type.EmptyTypes, Array.Empty<object>());

            AssertState(fixture.Controller, "PATROL");
            AssertInvalidPlayerId(GetProperty(fixture.Controller, "DetectionTargetId"));
            Assert.That(GetIntProperty(fixture.Runtime, "AuthoritativeSimulationCount"), Is.EqualTo(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RUNTIME_09_RepeatedFrameBuildsDoNotCarryStalePlayerOrCandidateData()
        {
            var fixture = CreateRuntimeFixture();
            var lifecycle = CreateLifecycle();
            InjectLifecycle(fixture.Runtime, lifecycle);
            RegisterActivePlayer(lifecycle, 1, 1, new Vector3(0f, 1f, 4f));

            Assert.That(RunPipeline(fixture.Runtime, 1L, 1d, 0.1f), Is.True);
            AssertState(fixture.Controller, "DETECT");

            UnregisterPlayer(lifecycle, 1);
            Assert.That(RunPipeline(fixture.Runtime, 2L, 2d, 0.1f), Is.True);

            AssertState(fixture.Controller, "PATROL");
            AssertInvalidPlayerId(GetProperty(fixture.Controller, "DetectionTargetId"));
            Assert.That(GetIntProperty(fixture.Runtime, "AuthoritativeSimulationCount"), Is.EqualTo(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RUNTIME_10_LateActivePlayerBecomesVisibleToNextFrameWithoutRuntimeRecreation()
        {
            var fixture = CreateRuntimeFixture();
            var lifecycle = CreateLifecycle();
            InjectLifecycle(fixture.Runtime, lifecycle);

            Assert.That(RunPipeline(fixture.Runtime, 1L, 1d, 0.1f), Is.True);
            AssertState(fixture.Controller, "PATROL");

            RegisterActivePlayer(lifecycle, 1, 1, new Vector3(0f, 1f, 4f));
            Assert.That(RunPipeline(fixture.Runtime, 2L, 2d, 0.1f), Is.True);

            AssertState(fixture.Controller, "DETECT");
            AssertPlayerIdValue(GetProperty(fixture.Controller, "DetectionTargetId"), 1);
            Assert.That(GetIntProperty(fixture.Runtime, "AuthoritativeSimulationCount"), Is.EqualTo(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RUNTIME_12_SpawnedNetworkRuntimeSuppressesLegacyUpdate()
        {
            var fixture = CreateRuntimeFixture();

            InvokeInstanceMethod(fixture.Runtime, "Spawned", Type.EmptyTypes, Array.Empty<object>());

            Assert.That(GetBoolProperty(fixture.Controller, "SuppressLegacyUpdateSimulation"), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RUNTIME_13_DisabledWhileNetworkOwned_KeepsLegacySuppressed()
        {
            var fixture = CreateRuntimeFixture();
            InvokeInstanceMethod(fixture.Runtime, "Spawned", Type.EmptyTypes, Array.Empty<object>());
            Assert.That(GetBoolProperty(fixture.Controller, "SuppressLegacyUpdateSimulation"), Is.True);
            InvokeInstanceMethod(fixture.Runtime, "OnDisable", Type.EmptyTypes, Array.Empty<object>());

            Assert.That(GetBoolProperty(fixture.Controller, "SuppressLegacyUpdateSimulation"), Is.True);

            InvokeInstanceMethod(fixture.Runtime, "OnEnable", Type.EmptyTypes, Array.Empty<object>());

            Assert.That(GetBoolProperty(fixture.Controller, "SuppressLegacyUpdateSimulation"), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RUNTIME_14_NonSpawnedEnabledComponentDoesNotSuppressOfflineLegacyUpdate()
        {
            var fixture = CreateRuntimeFixture();
            SetPrivateField(fixture.Controller, "<SuppressLegacyUpdateSimulation>k__BackingField", false);

            InvokeInstanceMethod(fixture.Runtime, "OnEnable", Type.EmptyTypes, Array.Empty<object>());

            Assert.That(GetBoolProperty(fixture.Controller, "SuppressLegacyUpdateSimulation"), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RUNTIME_15_DespawnedNetworkRuntime_RestoresLegacyUpdate()
        {
            var fixture = CreateRuntimeFixture();
            InvokeInstanceMethod(fixture.Runtime, "Spawned", Type.EmptyTypes, Array.Empty<object>());
            Assert.That(GetBoolProperty(fixture.Controller, "SuppressLegacyUpdateSimulation"), Is.True);

            InvokeInstanceMethod(
                fixture.Runtime,
                "Despawned",
                new[] { ResolveType("Fusion.NetworkRunner"), typeof(bool) },
                new object[] { null, false });

            Assert.That(GetBoolProperty(fixture.Controller, "SuppressLegacyUpdateSimulation"), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RUNTIME_ATTACK_16_HostPipeline_ResolvesTypedAttackExactlyOnce()
        {
            var fixture = CreateRuntimeFixture();
            var lifecycle = CreateLifecycle();
            var sink = Activator.CreateInstance(ResolveType(DiagnosticAttackSinkTypeName));
            InjectLifecycle(fixture.Runtime, lifecycle);
            RegisterActivePlayer(lifecycle, 1, 1, new Vector3(0f, 0f, 1f));
            SetPublicProperty(fixture.Controller, "AttackConsequenceSink", sink);
            SetCurrentTarget(fixture.Controller, 1, new Vector3(0f, 0f, 1f));
            SetPrivateField(fixture.Controller, "attackRange", 2f);
            SetPrivateField(fixture.Controller, "attackWindup", 0.1f);
            SetPrivateField(fixture.Controller, "currentTarget", null);

            Assert.That(RunPipeline(fixture.Runtime, 10L, 1d, 0.1f), Is.True);
            AssertState(fixture.Controller, "ATTACK");
            Assert.That(GetBoolProperty(fixture.Controller, "HitMomentResolved"), Is.False);

            Assert.That(RunPipeline(fixture.Runtime, 11L, 1.1d, 0.1f), Is.True);

            AssertState(fixture.Controller, "RECOVER");
            Assert.That(GetBoolProperty(fixture.Controller, "HitMomentResolved"), Is.True);
            Assert.That(GetIntProperty(fixture.Controller, "AttackResolutionCount"), Is.EqualTo(1));
            Assert.That(GetIntProperty(sink, "CallCount"), Is.EqualTo(1));
            var presentationState = GetProperty(fixture.Runtime, "LastAuthoritativePresentationState");
            Assert.That(GetProperty(presentationState, "AttackHitMomentResolved"), Is.EqualTo(true));
            Assert.That(GetProperty(presentationState, "AttackPhase").ToString(), Is.EqualTo("Recover"));
            Assert.That(GetProperty(presentationState, "AttackOutcome").ToString(), Is.EqualTo("Hit"));

            Assert.That(RunPipeline(fixture.Runtime, 12L, 1.2d, 0.1f), Is.True);

            Assert.That(GetIntProperty(fixture.Controller, "AttackResolutionCount"), Is.EqualTo(1));
            Assert.That(GetIntProperty(sink, "CallCount"), Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RUNTIME_ATTACK_17_ClientProxyFixedUpdate_CannotCreateOrResolveAttack()
        {
            var fixture = CreateRuntimeFixture();
            var sink = Activator.CreateInstance(ResolveType(DiagnosticAttackSinkTypeName));
            SetPublicProperty(fixture.Controller, "AttackConsequenceSink", sink);
            SetCurrentTarget(fixture.Controller, 1, new Vector3(0f, 0f, 1f));
            SetPrivateField(fixture.Controller, "currentTarget", null);

            InvokeInstanceMethod(fixture.Runtime, "FixedUpdateNetwork", Type.EmptyTypes, Array.Empty<object>());

            Assert.That(GetIntProperty(fixture.Runtime, "AuthoritativeSimulationCount"), Is.EqualTo(0));
            Assert.That(GetBoolProperty(GetProperty(fixture.Controller, "ActiveAttackEpisodeId"), "IsValid"), Is.False);
            Assert.That(GetIntProperty(fixture.Controller, "AttackResolutionCount"), Is.EqualTo(0));
            Assert.That(GetIntProperty(sink, "CallCount"), Is.EqualTo(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RUNTIME_ATTACK_18_RecoverPresentationUsesRecoveryProgress()
        {
            var fixture = CreateRuntimeFixture();
            var lifecycle = CreateLifecycle();
            var sink = Activator.CreateInstance(ResolveType(DiagnosticAttackSinkTypeName));
            InjectLifecycle(fixture.Runtime, lifecycle);
            RegisterActivePlayer(lifecycle, 1, 1, new Vector3(0f, 0f, 1f));
            SetPublicProperty(fixture.Controller, "AttackConsequenceSink", sink);
            SetCurrentTarget(fixture.Controller, 1, new Vector3(0f, 0f, 1f));
            SetPrivateField(fixture.Controller, "attackRange", 2f);
            SetPrivateField(fixture.Controller, "attackWindup", 0.1f);
            SetPrivateField(fixture.Controller, "attackRecovery", 1f);
            SetPrivateField(fixture.Controller, "currentTarget", null);

            Assert.That(RunPipeline(fixture.Runtime, 20L, 2d, 0.1f), Is.True);
            Assert.That(RunPipeline(fixture.Runtime, 21L, 2.1d, 0.1f), Is.True);
            AssertState(fixture.Controller, "RECOVER");
            Assert.That(RunPipeline(fixture.Runtime, 22L, 2.2d, 0.25f), Is.True);

            var presentationState = GetProperty(fixture.Runtime, "LastAuthoritativePresentationState");
            Assert.That(GetProperty(presentationState, "AttackPhase").ToString(), Is.EqualTo("Recover"));
            Assert.That((float)GetProperty(presentationState, "AttackProgressSeconds"), Is.EqualTo(0.25f).Within(VectorTolerance));
            Assert.That((float)GetProperty(presentationState, "AttackProgressSeconds"), Is.Not.EqualTo(0.1f).Within(VectorTolerance));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RUNTIME_ATTACK_19_ResolvedEpisodeIsNotCurrentPresentationAfterRecoverExit()
        {
            var fixture = CreateRuntimeFixture();
            var lifecycle = CreateLifecycle();
            var sink = Activator.CreateInstance(ResolveType(DiagnosticAttackSinkTypeName));
            InjectLifecycle(fixture.Runtime, lifecycle);
            RegisterActivePlayer(lifecycle, 1, 1, new Vector3(0f, 0f, 1f));
            SetPublicProperty(fixture.Controller, "AttackConsequenceSink", sink);
            SetCurrentTarget(fixture.Controller, 1, new Vector3(0f, 0f, 1f));
            SetPrivateField(fixture.Controller, "attackRange", 2f);
            SetPrivateField(fixture.Controller, "attackWindup", 0.1f);
            SetPrivateField(fixture.Controller, "attackRecovery", 0.1f);
            SetPrivateField(fixture.Controller, "currentTarget", null);

            Assert.That(RunPipeline(fixture.Runtime, 30L, 3d, 0.1f), Is.True);
            Assert.That(RunPipeline(fixture.Runtime, 31L, 3.1d, 0.1f), Is.True);
            Assert.That(GetBoolProperty(fixture.Controller, "HasCommittedAttackResolutionFact"), Is.True);
            var committedFact = GetProperty(fixture.Controller, "LastCommittedAttackResolutionFact");

            Assert.That(RunPipeline(fixture.Runtime, 32L, 3.2d, 0.1f), Is.True);

            var presentationState = GetProperty(fixture.Runtime, "LastAuthoritativePresentationState");
            Assert.That(GetBoolProperty(GetProperty(presentationState, "AttackEpisodeId"), "IsValid"), Is.False);
            Assert.That(GetProperty(presentationState, "AttackPhase").ToString(), Is.EqualTo("None"));
            Assert.That(GetBoolProperty(fixture.Controller, "HasCommittedAttackResolutionFact"), Is.True);
            Assert.That(GetProperty(fixture.Controller, "LastCommittedAttackResolutionFact"), Is.EqualTo(committedFact));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RUNTIME_SEARCH_20_SameTargetReacquiredCommitsOneTerminalFact()
        {
            var fixture = CreateRuntimeFixture();
            var lifecycle = CreateLifecycle();
            InjectLifecycle(fixture.Runtime, lifecycle);
            RegisterActivePlayer(lifecycle, 1, 1, new Vector3(0f, 1f, 4f));
            BeginSearch(fixture.Controller, 1, new Vector3(0f, 1f, 4f), 10f);

            Assert.That(RunPipeline(fixture.Runtime, 40L, 4d, 0.1f), Is.True);

            AssertSearchFact(fixture.Controller, 1L, "SAME_TARGET_REACQUIRED");
            AssertSearchExitedAndCleared(fixture.Controller, "CHASE");
            Assert.That(RunPipeline(fixture.Runtime, 41L, 4.1d, 0.1f), Is.True);
            AssertSearchFact(fixture.Controller, 1L, "SAME_TARGET_REACQUIRED");
            yield return null;
        }

        [UnityTest]
        public IEnumerator RUNTIME_SEARCH_21_NewEligibleTargetObservedCommitsFromReplacementTransition()
        {
            var fixture = CreateRuntimeFixture();
            var lifecycle = CreateLifecycle();
            InjectLifecycle(fixture.Runtime, lifecycle);
            RegisterLogicalPlayer(lifecycle, 1);
            UnregisterPlayer(lifecycle, 1);
            RegisterActivePlayer(lifecycle, 2, 2, new Vector3(0f, 1f, 4f));
            BeginSearch(fixture.Controller, 1, new Vector3(0f, 1f, 8f), 10f);

            Assert.That(RunPipeline(fixture.Runtime, 50L, 5d, 0.1f), Is.True);

            AssertSearchFact(fixture.Controller, 1L, "NEW_ELIGIBLE_TARGET_OBSERVED");
            AssertSearchExitedAndCleared(fixture.Controller, "DETECT");
            AssertState(fixture.Controller, "DETECT");
            AssertPlayerIdValue(GetProperty(fixture.Controller, "DetectionTargetId"), 2);
            Assert.That(RunPipeline(fixture.Runtime, 51L, 5.1d, 0.1f), Is.True);
            AssertSearchFact(fixture.Controller, 1L, "NEW_ELIGIBLE_TARGET_OBSERVED");
            yield return null;
        }

        [UnityTest]
        public IEnumerator RUNTIME_SEARCH_22_TimeoutCommitsOneTerminalFact()
        {
            var fixture = CreateRuntimeFixture();
            var lifecycle = CreateLifecycle();
            InjectLifecycle(fixture.Runtime, lifecycle);
            RegisterActivePlayer(lifecycle, 1, 1, new Vector3(100f, 1f, 100f));
            BeginSearch(fixture.Controller, 1, new Vector3(0f, 1f, 4f), 0f);

            Assert.That(RunPipeline(fixture.Runtime, 60L, 6d, 0.1f), Is.True);

            AssertSearchFact(fixture.Controller, 1L, "TIMEOUT");
            AssertSearchExitedAndCleared(fixture.Controller, "PATROL");
            Assert.That(RunPipeline(fixture.Runtime, 61L, 6.1d, 0.1f), Is.True);
            AssertSearchFact(fixture.Controller, 1L, "TIMEOUT");
            yield return null;
        }

        [UnityTest]
        public IEnumerator RUNTIME_SEARCH_23_CurrentTargetInvalidNoReplacementCommitsOneTerminalFact()
        {
            var fixture = CreateRuntimeFixture();
            var lifecycle = CreateLifecycle();
            InjectLifecycle(fixture.Runtime, lifecycle);
            RegisterLogicalPlayer(lifecycle, 1);
            UnregisterPlayer(lifecycle, 1);
            BeginSearch(fixture.Controller, 1, new Vector3(0f, 1f, 4f), 10f);

            Assert.That(RunPipeline(fixture.Runtime, 70L, 7d, 0.1f), Is.True);

            AssertSearchFact(fixture.Controller, 1L, "CURRENT_TARGET_INVALID_NO_REPLACEMENT");
            AssertSearchExitedAndCleared(fixture.Controller, "PATROL");
            Assert.That(RunPipeline(fixture.Runtime, 71L, 7.1d, 0.1f), Is.True);
            AssertSearchFact(fixture.Controller, 1L, "CURRENT_TARGET_INVALID_NO_REPLACEMENT");
            yield return null;
        }

        [UnityTest]
        public IEnumerator RUNTIME_SEARCH_24_SearchEpisodeCannotCommitSecondTerminalResult()
        {
            var fixture = CreateRuntimeFixture();
            InjectLifecycle(fixture.Runtime, CreateLifecycle());
            BeginSearch(fixture.Controller, 1, new Vector3(0f, 1f, 4f), 10f);
            SetPrivateField(fixture.Controller, "_currentSimulationStep", CreateSimulationStep(80L, 8d, 0.1f));

            InvokeInstanceMethod(
                fixture.Controller,
                "CommitSearchEnded",
                new[] { ResolveType(SearchOutcomeTypeName) },
                new[] { Enum.Parse(ResolveType(SearchOutcomeTypeName), "TIMEOUT") });
            InvokeInstanceMethod(
                fixture.Controller,
                "CommitSearchEnded",
                new[] { ResolveType(SearchOutcomeTypeName) },
                new[] { Enum.Parse(ResolveType(SearchOutcomeTypeName), "CURRENT_TARGET_INVALID_NO_REPLACEMENT") });

            AssertSearchFact(fixture.Controller, 1L, "TIMEOUT");
            yield return null;
        }

        private RuntimeFixture CreateRuntimeFixture()
        {
            var root = new GameObject("RUNTIME_Stalker");
            _createdObjects.Add(root);
            var origin = new GameObject("RUNTIME_Origin");
            origin.transform.SetParent(root.transform, false);
            origin.transform.localPosition = new Vector3(0f, 1f, 0f);

            var controller = (Component)root.AddComponent(ResolveType(ControllerTypeName));
            var sensor = (Component)root.AddComponent(ResolveType(SensorTypeName));
            var runtime = (Component)root.AddComponent(ResolveType(RuntimeTypeName));
            var agent = root.GetComponent<NavMeshAgent>();
            Assert.That(agent, Is.Not.Null);
            agent.enabled = false;

            ((Behaviour)controller).enabled = false;
            SetPrivateField(controller, "visionSensor", sensor);
            SetPrivateField(sensor, "visionOrigin", origin.transform);
            SetPrivateField(sensor, "visionDistance", 20f);
            SetPrivateField(sensor, "visionAngle", 120f);
            SetPrivateField(sensor, "losBlockerMask", default(LayerMask));
            SetPrivateField(runtime, "controller", controller);
            SetPrivateField(runtime, "visionSensor", sensor);
            return new RuntimeFixture(runtime, controller, sensor);
        }

        private Component CreateLifecycle()
        {
            var root = new GameObject("RUNTIME_Lifecycle");
            _createdObjects.Add(root);
            return (Component)root.AddComponent(ResolveType(LifecycleTypeName));
        }

        private Component RegisterActivePlayer(Component lifecycle, int playerRefIndex, int playerIdValue, Vector3 position)
        {
            RegisterLogicalPlayer(lifecycle, playerRefIndex);

            var identity = CreatePlayerObject($"RUNTIME_Player_{playerIdValue}", position).AddComponent(ResolveType(PlayerRuntimeIdentityTypeName));
            Assert.That(InvokeInstanceMethod(
                identity,
                "TryBind",
                new[] { ResolveType(PlayerIdTypeName) },
                new[] { CreatePlayerId(playerIdValue) }),
                Is.EqualTo(true));

            var entityRegistry = GetProperty(lifecycle, "EntityRegistry");
            Assert.That(InvokeInstanceMethod(
                entityRegistry,
                "TryRegister",
                new[] { ResolveType(PlayerRuntimeIdentityTypeName) },
                new object[] { identity }),
                Is.EqualTo(true));

            Physics.SyncTransforms();
            return (Component)identity;
        }

        private static void RegisterLogicalPlayer(Component lifecycle, int playerRefIndex)
        {
            var identityRegistry = GetProperty(lifecycle, "IdentityRegistry");
            var playerRef = CreatePlayerRef(playerRefIndex);
            var args = new[] { playerRef, null };
            Assert.That(InvokeInstanceMethod(
                identityRegistry,
                "TryRegister",
                new[] { ResolveType(PlayerRefTypeName), ResolveType(PlayerIdTypeName).MakeByRefType() },
                args),
                Is.EqualTo(true));
        }

        private GameObject CreatePlayerObject(string name, Vector3 position)
        {
            var player = new GameObject(name);
            player.transform.position = position;
            _createdObjects.Add(player);
            return player;
        }

        private static void UnregisterPlayer(Component lifecycle, int playerIdValue)
        {
            var entityRegistry = GetProperty(lifecycle, "EntityRegistry");
            InvokeInstanceMethod(
                entityRegistry,
                "Unregister",
                new[] { ResolveType(PlayerIdTypeName) },
                new[] { CreatePlayerId(playerIdValue) });

            var identityRegistry = GetProperty(lifecycle, "IdentityRegistry");
            InvokeInstanceMethod(
                identityRegistry,
                "Unregister",
                new[] { ResolveType(PlayerRefTypeName) },
                new[] { CreatePlayerRef(playerIdValue) });
        }

        private static bool RunPipeline(Component runtime, long tick, double seconds, float deltaSeconds)
        {
            var step = Activator.CreateInstance(ResolveType(AiSimulationStepTypeName), CreateSimulationTime(tick, seconds), deltaSeconds);
            var result = InvokeInstanceMethod(
                runtime,
                "RunAuthoritativePipeline",
                new[] { ResolveType(AiSimulationStepTypeName) },
                new[] { step });
            return (bool)result;
        }

        private static object CreateSimulationTime(long tick, double seconds)
        {
            return Activator.CreateInstance(ResolveType(AiSimulationTimeTypeName), tick, seconds);
        }

        private static object CreateSimulationStep(long tick, double seconds, float deltaSeconds)
        {
            return Activator.CreateInstance(ResolveType(AiSimulationStepTypeName), CreateSimulationTime(tick, seconds), deltaSeconds);
        }

        private static void InjectLifecycle(Component runtime, Component lifecycle)
        {
            SetPrivateField(runtime, "lifecycle", lifecycle);
        }

        private static void SetLegacySensorCandidate(Component sensor, Transform target)
        {
            SetPrivateField(sensor, "candidate", target);
        }

        private static void SetCurrentTarget(Component controller, int playerId, Vector3 lastKnownPosition)
        {
            SetPrivateField(controller, "currentState", Enum.Parse(ResolveType("EchoProtocol.AI.Stalker.StalkerState"), "CHASE"));
            SetPrivateField(controller, "lastKnownPosition", lastKnownPosition);
            SetPrivateField(controller, "attackRange", 0.1f);
            var memory = GetMemory(controller);
            InvokeInstanceMethod(memory, "SetCurrentTarget", new[] { ResolveType(PlayerIdTypeName) }, new[] { CreatePlayerId(playerId) });
            InvokeInstanceMethod(memory, "TryAcceptCurrentTargetObservation", new[] { ResolveType(VisionObservationTypeName) }, new[] { CreateObservation(playerId, lastKnownPosition) });
        }

        private static void BeginSearch(Component controller, int playerId, Vector3 lastKnownPosition, float searchDuration)
        {
            SetCurrentTarget(controller, playerId, lastKnownPosition);
            SetPrivateField(controller, "searchDuration", searchDuration);
            InvokeInstanceMethod(controller, "EnterSearch", Type.EmptyTypes, Array.Empty<object>());
        }

        private static object CreateObservation(int playerId, Vector3 position)
        {
            return Activator.CreateInstance(
                ResolveType(VisionObservationTypeName),
                CreatePlayerId(playerId),
                position,
                position.normalized,
                CreateSimulationTime(1L, 1d),
                position.magnitude);
        }

        private static object CreatePlayerRef(int index)
        {
            return ResolveType(PlayerRefTypeName)
                .GetMethod("FromIndex", BindingFlags.Static | BindingFlags.Public)
                .Invoke(null, new object[] { index });
        }

        private static object CreatePlayerId(int value)
        {
            return Activator.CreateInstance(ResolveType(PlayerIdTypeName), value);
        }

        private static object GetMemory(Component controller)
        {
            var field = controller.GetType().GetField("_memory", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return field.GetValue(controller);
        }

        private static int GetPrivateListCount(Component target, string fieldName)
        {
            var value = GetPrivateField(target, fieldName);
            Assert.That(value, Is.Not.Null);
            return (int)value.GetType().GetProperty("Count").GetValue(value);
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}' on '{target.GetType().FullName}'.");
            return field.GetValue(target);
        }

        private static object InvokeInstanceMethod(object target, string methodName, Type[] parameterTypes, object[] args)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                parameterTypes,
                null);
            Assert.That(method, Is.Not.Null, $"Missing method '{methodName}' on '{target.GetType().FullName}'.");
            return method.Invoke(target, args);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}' on '{target.GetType().FullName}'.");
            field.SetValue(target, value);
        }

        private static void SetPublicProperty(object target, string propertyName, object value)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Missing public property '{propertyName}' on '{target.GetType().FullName}'.");
            property.SetValue(target, value);
        }

        private static object GetProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Missing public property '{propertyName}' on '{target.GetType().FullName}'.");
            return property.GetValue(target);
        }

        private static int GetIntProperty(object target, string propertyName)
        {
            return (int)GetProperty(target, propertyName);
        }

        private static bool GetBoolProperty(object target, string propertyName)
        {
            return (bool)GetProperty(target, propertyName);
        }

        private static void AssertState(Component controller, string expected)
        {
            Assert.That(GetProperty(controller, "CurrentState").ToString(), Is.EqualTo(expected));
        }

        private static void AssertPlayerIdValue(object playerId, int expected)
        {
            Assert.That(GetBoolProperty(playerId, "IsValid"), Is.True);
            Assert.That(GetProperty(playerId, "Value"), Is.EqualTo(expected));
        }

        private static void AssertInvalidPlayerId(object playerId)
        {
            Assert.That(GetBoolProperty(playerId, "IsValid"), Is.False);
        }

        private static void AssertSearchFact(Component controller, long expectedEpisodeId, string expectedOutcome)
        {
            Assert.That(GetBoolProperty(controller, "HasCommittedSearchEndedFact"), Is.True);
            var fact = GetProperty(controller, "LastCommittedSearchEndedFact");
            var episodeId = GetProperty(fact, "EpisodeId");
            var endedAt = GetProperty(fact, "EndedAt");
            Assert.That(GetProperty(episodeId, "Value"), Is.EqualTo(expectedEpisodeId));
            Assert.That(GetProperty(fact, "Outcome").ToString(), Is.EqualTo(expectedOutcome));
            Assert.That(GetBoolProperty(endedAt, "IsValid"), Is.True);
        }

        private static void AssertSearchExitedAndCleared(Component controller, string expectedState)
        {
            AssertState(controller, expectedState);
            Assert.That(GetProperty(controller, "ActiveSearchContext"), Is.Null);
            AssertInvalidSearchEpisodeId(GetProperty(controller, "ActiveSearchEpisodeId"));
        }

        private static void AssertInvalidSearchEpisodeId(object episodeId)
        {
            Assert.That(GetBoolProperty(episodeId, "IsValid"), Is.False);
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

            Assert.Fail($"Could not find type '{fullTypeName}' in the loaded Unity AppDomain.");
            return null;
        }

        private readonly struct RuntimeFixture
        {
            public RuntimeFixture(Component runtime, Component controller, Component sensor)
            {
                Runtime = runtime;
                Controller = controller;
                Sensor = sensor;
            }

            public Component Runtime { get; }

            public Component Controller { get; }

            public Component Sensor { get; }
        }
    }
}
