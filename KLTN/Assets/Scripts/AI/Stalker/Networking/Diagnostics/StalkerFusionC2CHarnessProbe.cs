using System;
using System.Collections.Generic;
using System.Text;
using EchoProtocol.AI.Common;
using EchoProtocol.Networking;
using EchoProtocol.Networking.Diagnostics;
using EchoProtocol.Player;
using Fusion;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace EchoProtocol.AI.Stalker.Networking.Diagnostics
{
    public sealed class StalkerFusionC2CHarnessProbe : MonoBehaviour
    {
        private const string NoCheatArg = "--stk2-no-cheat-autostart=";
        private const string Phase4AttackArg = "--stk4-attack-autostart=";
        private const float RuntimeResolveIntervalSeconds = 0.25f;
        private const float NavMeshSpawnSampleRadius = 0.10f;
        private const float NoCheatPositionTolerance = 0.05f;
        private const int RequiredHiddenVerificationSimulationDelta = 3;
        private const int AttackStabilitySimulationDelta = 8;
        private const int ExpectedTargetEpisodeWaitSimulationDelta = 180;
        private const float AttackWaitLogIntervalSeconds = 2f;
        private static readonly List<PlayerId> s_sharedPlayerIds = new List<PlayerId>();

        [SerializeField] private FusionC2CHarnessController controller;
        [SerializeField] private NetworkObject stalkerPrefab;
        [SerializeField] private NavMeshSurface navMeshSurface;
        [SerializeField] private Transform stalkerSpawnPoint;
        [SerializeField] private Transform visiblePlayerMarker;
        [SerializeField] private Transform hiddenPlayerMarker;
        [SerializeField] private GameObject noCheatOccluder;
        [SerializeField] private float logIntervalSeconds = 1f;
        [SerializeField] private bool noCheatAutostart;
        [SerializeField] private bool phase4AttackAutostart;

        private readonly List<PlayerId> _activePlayerIds = new List<PlayerId>();
        private readonly StringBuilder _playerIdBuilder = new StringBuilder();
        private NetworkObject _spawnedStalker;
        private StalkerFusionRuntime _cachedRuntime;
        private string _lastStalkerSignature = string.Empty;
        private string _lastTopologySignature = string.Empty;
        private float _nextLogTime;
        private float _nextRuntimeResolveTime;
        private bool _navMeshAttempted;
        private bool _navMeshReady;
        private bool _navMeshFailureLogged;
        private NoCheatStage _noCheatStage;
        private PlayerId _noCheatPositionedPlayerId;
        private PlayerId _noCheatTargetId;
        private Vector3 _baselineTargetPosition;
        private Vector3 _baselineLastKnownPosition;
        private Vector3 _hiddenTargetPosition;
        private Vector3 _navMeshSpawnPosition;
        private int _baselineSimulationCount;
        private int _baselineRunnerTick;
        private Stk4AttackStage _attackStage;
        private StalkerDiagnosticAttackConsequenceSink _hostAttackSink;
        private StalkerDiagnosticAttackConsequenceSink _clientAttackSink;
        private PlayerId _attackTargetId;
        private StalkerAttackEpisodeId _attackEpisodeId;
        private StalkerAttackEpisodeId _attackBaselineEpisodeId;
        private PlayerId _attackBaselineTargetId;
        private StalkerState _attackBaselineState;
        private int _attackResolutionBaseline;
        private int _attackConsequenceBaseline;
        private int _attackBaselineSimulationCount;
        private int _attackBaselineRunnerTick;
        private int _attackPositionedSimulationCount;
        private int _attackPositionedRunnerTick;
        private int _attackResolvedSimulationCount;
        private bool _clientAttackProxyLogged;
        private string _lastAttackWaitLogKey = string.Empty;
        private float _nextAttackWaitLogTime;

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponent<FusionC2CHarnessController>();
            }

            noCheatAutostart = noCheatAutostart || ResolveBooleanArg(NoCheatArg);
            phase4AttackAutostart = phase4AttackAutostart || ResolveBooleanArg(Phase4AttackArg);
        }

        private void OnDisable()
        {
            _spawnedStalker = null;
            _cachedRuntime = null;
            _lastStalkerSignature = string.Empty;
            _lastTopologySignature = string.Empty;
            _noCheatStage = NoCheatStage.None;
            _noCheatPositionedPlayerId = PlayerId.Invalid;
            _noCheatTargetId = PlayerId.Invalid;
            _attackStage = Stk4AttackStage.None;
            _hostAttackSink = null;
            _clientAttackSink = null;
            _attackTargetId = PlayerId.Invalid;
            _attackEpisodeId = StalkerAttackEpisodeId.Invalid;
            _attackBaselineEpisodeId = StalkerAttackEpisodeId.Invalid;
            _attackBaselineTargetId = PlayerId.Invalid;
            _clientAttackProxyLogged = false;
            _lastAttackWaitLogKey = string.Empty;
        }

        private void Update()
        {
            var runner = controller != null ? controller.Runner : null;
            if (runner == null || !runner.IsRunning)
            {
                return;
            }

            if (runner.IsServer)
            {
                if (!EnsureHostNavMeshReady())
                {
                    CaptureAndLogIfChangedOrDue(runner);
                    return;
                }

                EnsureHostStalkerSpawned(runner);
                TickNoCheatScenario(runner);
                TickPhase4AttackScenario(runner);
            }
            else
            {
                TickPhase4ClientObservation(runner);
            }

            CaptureAndLogIfChangedOrDue(runner);
        }

        [ContextMenu("STK2 No-Cheat/Start")]
        public void StartNoCheatScenario()
        {
            _noCheatStage = NoCheatStage.PositionVisible;
            if (noCheatOccluder != null)
            {
                noCheatOccluder.SetActive(false);
            }

            _noCheatPositionedPlayerId = PlayerId.Invalid;
            _noCheatTargetId = PlayerId.Invalid;
            _baselineTargetPosition = default;
            _baselineLastKnownPosition = default;
            _hiddenTargetPosition = default;
            _baselineSimulationCount = 0;
            _baselineRunnerTick = 0;
        }

        [ContextMenu("STK4 Attack/Start Live 2P Acceptance")]
        public void StartPhase4AttackScenario()
        {
            _attackStage = Stk4AttackStage.WaitForClientPlayer;
            _attackTargetId = PlayerId.Invalid;
            _attackEpisodeId = StalkerAttackEpisodeId.Invalid;
            _attackBaselineEpisodeId = StalkerAttackEpisodeId.Invalid;
            _attackBaselineTargetId = PlayerId.Invalid;
            _attackResolutionBaseline = 0;
            _attackConsequenceBaseline = 0;
            _attackBaselineSimulationCount = 0;
            _attackBaselineRunnerTick = 0;
            _attackPositionedSimulationCount = 0;
            _attackPositionedRunnerTick = 0;
            _attackResolvedSimulationCount = 0;
            _lastAttackWaitLogKey = string.Empty;
            _nextAttackWaitLogTime = 0f;
            _hostAttackSink = new StalkerDiagnosticAttackConsequenceSink();
            Debug.Log("STK4|ATTACK|role=Host|stage=StartRequested");
        }

        [ContextMenu("STK2 No-Cheat/Occlude And Move Current Target")]
        public void OccludeAndMoveCurrentTarget()
        {
            var runner = controller != null ? controller.Runner : null;
            if (runner == null || !runner.IsRunning || !runner.IsServer)
            {
                Debug.LogWarning("STK2|NO_CHEAT|action=OccludeAndMoveCurrentTarget|result=Skipped|reason=HostRunnerRequired");
                return;
            }

            if (!TryGetPrimaryStalkerRuntime(runner, out var runtime)
                || !TryCaptureNoCheatBaseline(runner, runtime))
            {
                Debug.LogWarning("STK2|NO_CHEAT|stage=OccludeAndMove|result=Skipped|reason=MissingLockedTarget");
                return;
            }

            _noCheatStage = NoCheatStage.OccludeAndMove;
            TickNoCheatScenario(runner);
        }

        private bool EnsureHostNavMeshReady()
        {
            if (_navMeshReady)
            {
                return true;
            }

            if (_navMeshAttempted)
            {
                return false;
            }

            _navMeshAttempted = true;

            if (navMeshSurface == null)
            {
                navMeshSurface = GetComponentInChildren<NavMeshSurface>(true);
            }

            if (navMeshSurface == null)
            {
                LogNavMeshFailedOnce("MissingNavMeshSurface");
                return false;
            }

            if (stalkerSpawnPoint == null)
            {
                LogNavMeshFailedOnce("MissingStalkerSpawnPoint");
                return false;
            }

            try
            {
                navMeshSurface.BuildNavMesh();
                if (navMeshSurface.navMeshData == null)
                {
                    LogNavMeshFailedOnce("NoNavMeshData");
                    return false;
                }

                LogNavMeshDiagnostics();

                if (!NavMesh.SamplePosition(stalkerSpawnPoint.position, out var hit, NavMeshSpawnSampleRadius, NavMesh.AllAreas))
                {
                    LogNavMeshFailedOnce("SpawnPointNotOnNavMesh");
                    return false;
                }

                _navMeshSpawnPosition = hit.position;
                _navMeshReady = true;
                Debug.Log($"STK2|NAVMESH|role=Host|result=Ready|spawn={FormatVector(_navMeshSpawnPosition)}|sampleRadius={NavMeshSpawnSampleRadius:0.###}");
                return true;
            }
            catch (Exception ex)
            {
                LogNavMeshFailedOnce($"Exception:{Sanitize(ex.GetType().Name)}");
                return false;
            }
        }

        private void LogNavMeshDiagnostics()
        {
            var spawn = stalkerSpawnPoint != null ? stalkerSpawnPoint.position : Vector3.zero;
            var triangulation = NavMesh.CalculateTriangulation();
            var vertexCount = triangulation.vertices != null ? triangulation.vertices.Length : 0;
            var indexCount = triangulation.indices != null ? triangulation.indices.Length : 0;
            var r005 = NavMesh.SamplePosition(spawn, out var hit005, 0.05f, NavMesh.AllAreas);
            var r025 = NavMesh.SamplePosition(spawn, out var hit025, 0.25f, NavMesh.AllAreas);
            var r050 = NavMesh.SamplePosition(spawn, out var hit050, 0.50f, NavMesh.AllAreas);
            var r100 = NavMesh.SamplePosition(spawn, out var hit100, 1.00f, NavMesh.AllAreas);
            var nearest = r005
                ? FormatVector(hit005.position)
                : r025
                    ? FormatVector(hit025.position)
                    : r050
                        ? FormatVector(hit050.position)
                        : r100
                            ? FormatVector(hit100.position)
                            : "none";

            Debug.Log($"STK2|NAVMESH_DIAG|vertices={vertexCount}|indices={indexCount}|spawn={FormatVector(spawn)}|r005={r005}|r025={r025}|r050={r050}|r100={r100}|nearest={nearest}");
        }

        private void LogNavMeshFailedOnce(string reason)
        {
            if (_navMeshFailureLogged)
            {
                return;
            }

            _navMeshFailureLogged = true;
            Debug.LogError($"STK2|NAVMESH|role=Host|result=Failed|reason={reason}");
        }

        private static string Sanitize(string value)
        {
            return string.IsNullOrEmpty(value) ? "Unknown" : value.Replace('|', '_').Replace(' ', '_');
        }

        private void ExecuteOccludeAndMove(NetworkRunner runner)
        {
            if (noCheatOccluder != null)
            {
                noCheatOccluder.SetActive(true);
            }

            if (hiddenPlayerMarker == null)
            {
                LogNoCheatFailure("MissingHiddenMarker");
                return;
            }

            if (!TryResolveHostTargetIdentity(runner, _noCheatTargetId, out var identity))
            {
                LogNoCheatFailure("TargetNotResolvableBeforeMove");
                return;
            }

            if (!TryTeleportTarget(identity, hiddenPlayerMarker.position, out var reason))
            {
                LogNoCheatFailure($"HiddenTeleportFailed:{reason}");
                return;
            }

            _hiddenTargetPosition = GetTargetSamplePosition(identity);
            LogNoCheatSnapshot(runner, "OccludedMoved");
            _noCheatStage = NoCheatStage.WaitForHiddenVerification;
        }

        private void EnsureHostStalkerSpawned(NetworkRunner runner)
        {
            if (_spawnedStalker != null && _spawnedStalker.IsValid)
            {
                return;
            }

            if (TryGetPrimaryStalkerRuntime(runner, out var existingRuntime)
                && existingRuntime.Object != null
                && existingRuntime.Object.HasStateAuthority)
            {
                _spawnedStalker = existingRuntime.Object;
                _cachedRuntime = existingRuntime;
                return;
            }

            if (stalkerPrefab == null)
            {
                Debug.LogError("STK2|SPAWN_FAIL|reason=MissingStalkerPrefab");
                return;
            }

            var position = _navMeshReady ? _navMeshSpawnPosition : stalkerSpawnPoint != null ? stalkerSpawnPoint.position : Vector3.zero;
            var rotation = stalkerSpawnPoint != null ? stalkerSpawnPoint.rotation : Quaternion.identity;
            _spawnedStalker = runner.Spawn(stalkerPrefab, position, rotation, PlayerRef.None);

            if (_spawnedStalker == null)
            {
                Debug.LogError("STK2|SPAWN_FAIL|reason=RunnerSpawnReturnedNull");
                return;
            }

            _cachedRuntime = _spawnedStalker.GetComponent<StalkerFusionRuntime>();
            Debug.Log($"STK2|SPAWN|role=Host|objectId={_spawnedStalker.Id}|stateAuth={_spawnedStalker.HasStateAuthority}|server={runner.IsServer}");
        }

        private void TickNoCheatScenario(NetworkRunner runner)
        {
            if ((!noCheatAutostart && _noCheatStage == NoCheatStage.None)
                || _noCheatStage == NoCheatStage.Complete)
            {
                return;
            }

            if (_noCheatStage == NoCheatStage.None)
            {
                StartNoCheatScenario();
            }

            if (_noCheatStage == NoCheatStage.PositionVisible)
            {
                if (visiblePlayerMarker == null)
                {
                    LogNoCheatFailure("MissingVisibleMarker");
                    return;
                }

                if (!TryResolveFirstHostPlayerIdentity(runner, out var playerId, out var identity))
                {
                    LogNoCheatFailure("NoPositionablePlayer");
                    return;
                }

                if (!TryTeleportTarget(identity, visiblePlayerMarker.position, out var reason))
                {
                    LogNoCheatFailure($"VisibleTeleportFailed:{reason}");
                    return;
                }

                _noCheatPositionedPlayerId = playerId;
                LogNoCheatSnapshot(runner, "VisiblePositioned");
                _noCheatStage = NoCheatStage.WaitForLock;
                return;
            }

            if (_noCheatStage == NoCheatStage.WaitForLock
                && TryGetPrimaryStalkerRuntime(runner, out var runtime)
                && TryCaptureNoCheatBaseline(runner, runtime))
            {
                LogNoCheatSnapshot(runner, "LockCaptured");
                _noCheatStage = NoCheatStage.OccludeAndMove;
                return;
            }

            if (_noCheatStage == NoCheatStage.OccludeAndMove)
            {
                ExecuteOccludeAndMove(runner);
                return;
            }

            if (_noCheatStage == NoCheatStage.WaitForHiddenVerification)
            {
                VerifyHiddenNoCheatAfterDelay(runner);
            }
        }

        private void TickPhase4AttackScenario(NetworkRunner runner)
        {
            if ((!phase4AttackAutostart && _attackStage == Stk4AttackStage.None)
                || _attackStage == Stk4AttackStage.Complete
                || _attackStage == Stk4AttackStage.Failed)
            {
                return;
            }

            if (_attackStage == Stk4AttackStage.None)
            {
                StartPhase4AttackScenario();
            }

            if (!TryGetPrimaryStalkerRuntime(runner, out var runtime))
            {
                LogAttackHostWaiting("MissingStalkerRuntime");
                return;
            }

            var controllerComponent = runtime.GetComponent<StalkerController>();
            if (controllerComponent == null)
            {
                LogAttackHostFailure("MissingStalkerController");
                return;
            }

            if (_hostAttackSink == null)
            {
                _hostAttackSink = new StalkerDiagnosticAttackConsequenceSink();
            }

            controllerComponent.ConfigurePhase4AttackAcceptanceDiagnostics(
                0.05f,
                100f,
                2f,
                0.10f,
                1.00f,
                _hostAttackSink);

            if (_attackStage == Stk4AttackStage.WaitForClientPlayer)
            {
                if (!TryResolveFirstRemoteHostPlayerIdentity(runner, out var playerId, out var identity))
                {
                    LogAttackHostWaiting("NoRemotePlayerObject");
                    return;
                }

                if (!TryResolveHostLocalPlayerIdentity(runner, out var localPlayerId, out var localIdentity))
                {
                    LogAttackHostFailure("NoLocalPlayerObject");
                    return;
                }

                if (!TryComputeAttackTargetPosition(runtime, out var attackPosition))
                {
                    LogAttackHostFailure("NoAttackPositionOnNavMesh");
                    return;
                }

                if (!TryComputeHostAwayPosition(runtime, out var localAwayPosition))
                {
                    LogAttackHostFailure("NoLocalAwayPositionOnNavMesh");
                    return;
                }

                _attackTargetId = playerId;
                LogAttackTargetCompetition(runtime, localPlayerId, localIdentity, playerId, identity, "BeforePositioning");
                if (!TryTeleportTarget(localIdentity, localAwayPosition, out var localReason))
                {
                    LogAttackHostFailure($"LocalAwayTeleportFailed:{localReason}");
                    return;
                }

                Physics.SyncTransforms();
                LogAttackTargetCompetition(runtime, localPlayerId, localIdentity, playerId, identity, "AfterLocalAway");

                if (controllerComponent.ActiveAttackEpisodeId.IsValid && !controllerComponent.HitMomentResolved)
                {
                    LogAttackHostWaiting($"WaitingForPreExistingEpisodeToResolve:episode={controllerComponent.ActiveAttackEpisodeId.Value}:target={controllerComponent.AttackTargetId}");
                    _attackStage = Stk4AttackStage.WaitForBaselineSettled;
                    return;
                }

                CaptureAttackBaseline(runner, runtime, controllerComponent);
                if (!PositionExpectedAttackTarget(runner, runtime, controllerComponent, identity))
                {
                    return;
                }

                return;
            }

            if (_attackStage == Stk4AttackStage.WaitForBaselineSettled)
            {
                if (controllerComponent.ActiveAttackEpisodeId.IsValid && !controllerComponent.HitMomentResolved)
                {
                    LogAttackHostWaiting($"WaitingForPreExistingEpisodeToResolve:episode={controllerComponent.ActiveAttackEpisodeId.Value}:target={controllerComponent.AttackTargetId}");
                    return;
                }

                if (!TryResolveHostTargetIdentity(runner, _attackTargetId, out var identity))
                {
                    LogAttackHostFailure("RemoteTargetLostBeforePositioning");
                    return;
                }

                CaptureAttackBaseline(runner, runtime, controllerComponent);
                if (!PositionExpectedAttackTarget(runner, runtime, controllerComponent, identity))
                {
                    return;
                }

                return;
            }

            if (_attackStage == Stk4AttackStage.WaitForEpisode)
            {
                var episode = controllerComponent.ActiveAttackEpisode;
                if (!episode.EpisodeId.IsValid)
                {
                    CheckExpectedEpisodeTimeout(runtime, runner, controllerComponent);
                    return;
                }

                if (!IsExpectedAttackEpisode(episode))
                {
                    LogUnexpectedAttackEpisodeOnce(episode, controllerComponent);
                    CheckExpectedEpisodeTimeout(runtime, runner, controllerComponent);
                    return;
                }

                _attackEpisodeId = episode.EpisodeId;
                Debug.Log($"STK4|ATTACK|role=Host|stage=EpisodeStarted|server={runner.IsServer}|stateAuth={(runtime.Object != null && runtime.Object.HasStateAuthority)}|episode={_attackEpisodeId.Value}|baselineEpisode={FormatEpisode(_attackBaselineEpisodeId)}|target={episode.TargetIdAtEntry}|expectedTarget={_attackTargetId}|targetMatches={episode.TargetIdAtEntry == _attackTargetId}|startedTick={episode.StartedTick}|baselineRunnerTick={_attackBaselineRunnerTick}|positionedRunnerTick={_attackPositionedRunnerTick}|state={controllerComponent.CurrentState}|hitResolved={controllerComponent.HitMomentResolved}|outcome={controllerComponent.AttackOutcome}|resolutionDelta={controllerComponent.AttackResolutionCount - _attackResolutionBaseline}|consequenceDelta={_hostAttackSink.CallCount - _attackConsequenceBaseline}|simCount={runtime.AuthoritativeSimulationCount}|runnerTick={runner.Tick.Raw}");
                _attackStage = Stk4AttackStage.WaitForResolution;
                return;
            }

            if (_attackStage == Stk4AttackStage.WaitForResolution)
            {
                if (!controllerComponent.HitMomentResolved)
                {
                    return;
                }

                if (controllerComponent.ActiveAttackEpisodeId != _attackEpisodeId
                    || controllerComponent.AttackTargetId != _attackTargetId)
                {
                    LogAttackHostFailure($"ResolvedUnexpectedEpisode:episode={FormatEpisode(controllerComponent.ActiveAttackEpisodeId)}:target={controllerComponent.AttackTargetId}");
                    return;
                }

                var resolutionDelta = controllerComponent.AttackResolutionCount - _attackResolutionBaseline;
                var consequenceDelta = _hostAttackSink.CallCount - _attackConsequenceBaseline;
                Debug.Log($"STK4|ATTACK|role=Host|stage=Resolved|server={runner.IsServer}|stateAuth={(runtime.Object != null && runtime.Object.HasStateAuthority)}|episode={_attackEpisodeId.Value}|target={controllerComponent.AttackTargetId}|hitResolved={controllerComponent.HitMomentResolved}|outcome={controllerComponent.AttackOutcome}|resolutionDelta={resolutionDelta}|consequenceDelta={consequenceDelta}|state={controllerComponent.CurrentState}|lastResult={controllerComponent.AttackResolutionResult}|simCount={runtime.AuthoritativeSimulationCount}|runnerTick={runner.Tick.Raw}");
                _attackResolvedSimulationCount = runtime.AuthoritativeSimulationCount;
                _attackStage = Stk4AttackStage.StabilityWindow;
                return;
            }

            if (_attackStage == Stk4AttackStage.StabilityWindow)
            {
                var simDelta = runtime.AuthoritativeSimulationCount - _attackResolvedSimulationCount;
                if (simDelta < AttackStabilitySimulationDelta)
                {
                    return;
                }

                var resolutionDelta = controllerComponent.AttackResolutionCount - _attackResolutionBaseline;
                var consequenceDelta = _hostAttackSink.CallCount - _attackConsequenceBaseline;
                var passed = controllerComponent.HitMomentResolved
                    && controllerComponent.AttackOutcome == StalkerAttackOutcome.Hit
                    && controllerComponent.CurrentState == StalkerState.RECOVER
                    && controllerComponent.ActiveAttackEpisodeId == _attackEpisodeId
                    && resolutionDelta == 1
                    && consequenceDelta == 1
                    && controllerComponent.AttackTargetId == _attackTargetId;

                Debug.Log($"STK4|ATTACK|role=Host|stage=ExactlyOnceVerified|result={(passed ? "Passed" : "Failed")}|server={runner.IsServer}|stateAuth={(runtime.Object != null && runtime.Object.HasStateAuthority)}|episode={_attackEpisodeId.Value}|target={controllerComponent.AttackTargetId}|outcome={controllerComponent.AttackOutcome}|resolutionDelta={resolutionDelta}|consequenceDelta={consequenceDelta}|state={controllerComponent.CurrentState}|simDelta={simDelta}|simCount={runtime.AuthoritativeSimulationCount}|runnerTick={runner.Tick.Raw}");
                _attackStage = passed ? Stk4AttackStage.Complete : Stk4AttackStage.Failed;
            }
        }

        private void TickPhase4ClientObservation(NetworkRunner runner)
        {
            if (!phase4AttackAutostart || _clientAttackProxyLogged)
            {
                return;
            }

            if (!TryGetPrimaryStalkerRuntime(runner, out var runtime))
            {
                return;
            }

            var controllerComponent = runtime.GetComponent<StalkerController>();
            if (controllerComponent == null)
            {
                return;
            }

            if (_clientAttackSink == null)
            {
                _clientAttackSink = new StalkerDiagnosticAttackConsequenceSink();
                controllerComponent.ConfigurePhase4AttackAcceptanceDiagnostics(
                    0.05f,
                    100f,
                    2f,
                    0.10f,
                    1.00f,
                    _clientAttackSink);
            }

            Debug.Log($"STK4|ATTACK|role=Client|stage=ProxyObserved|server={runner.IsServer}|stateAuth={(runtime.Object != null && runtime.Object.HasStateAuthority)}|objectId={(runtime.Object != null ? runtime.Object.Id.ToString() : "none")}|simCount={runtime.AuthoritativeSimulationCount}|episodeValid={controllerComponent.ActiveAttackEpisodeId.IsValid}|resolutionCount={controllerComponent.AttackResolutionCount}|consequenceCount={_clientAttackSink.CallCount}|state={controllerComponent.CurrentState}|runnerTick={runner.Tick.Raw}");
            _clientAttackProxyLogged = true;
        }

        private void CaptureAttackBaseline(
            NetworkRunner runner,
            StalkerFusionRuntime runtime,
            StalkerController controllerComponent)
        {
            _attackBaselineEpisodeId = controllerComponent.ActiveAttackEpisodeId;
            _attackBaselineTargetId = controllerComponent.AttackTargetId;
            _attackBaselineState = controllerComponent.CurrentState;
            _attackResolutionBaseline = controllerComponent.AttackResolutionCount;
            _attackConsequenceBaseline = _hostAttackSink != null ? _hostAttackSink.CallCount : 0;
            _attackBaselineSimulationCount = runtime.AuthoritativeSimulationCount;
            _attackBaselineRunnerTick = runner.Tick.Raw;
            Debug.Log($"STK4|ATTACK|role=Host|stage=BaselineCaptured|episode={FormatEpisode(_attackBaselineEpisodeId)}|target={_attackBaselineTargetId}|state={_attackBaselineState}|resolutionCount={_attackResolutionBaseline}|consequenceCount={_attackConsequenceBaseline}|simCount={_attackBaselineSimulationCount}|runnerTick={_attackBaselineRunnerTick}");
        }

        private bool PositionExpectedAttackTarget(
            NetworkRunner runner,
            StalkerFusionRuntime runtime,
            StalkerController controllerComponent,
            PlayerRuntimeIdentity identity)
        {
            if (!TryComputeAttackTargetPosition(runtime, out var attackPosition))
            {
                LogAttackHostFailure("NoAttackPositionOnNavMesh");
                return false;
            }

            if (!TryTeleportTarget(identity, attackPosition, out var reason))
            {
                LogAttackHostFailure($"AttackTeleportFailed:{reason}");
                return false;
            }

            _attackPositionedSimulationCount = runtime.AuthoritativeSimulationCount;
            _attackPositionedRunnerTick = runner.Tick.Raw;
            Physics.SyncTransforms();
            Debug.Log($"STK4|ATTACK|role=Host|stage=PlayerPositioned|server={runner.IsServer}|stateAuth={(runtime.Object != null && runtime.Object.HasStateAuthority)}|expectedTarget={_attackTargetId}|playerId={_attackTargetId}|playerObject={identity.EntityRoot.name}|replicatedTeleport=True|position={FormatVector(GetTargetSamplePosition(identity))}|baselineEpisode={FormatEpisode(_attackBaselineEpisodeId)}|baselineTarget={_attackBaselineTargetId}|baselineState={_attackBaselineState}|baselineResolutionCount={_attackResolutionBaseline}|baselineConsequenceCount={_attackConsequenceBaseline}|simCount={_attackPositionedSimulationCount}|runnerTick={_attackPositionedRunnerTick}");
            _attackStage = Stk4AttackStage.WaitForEpisode;
            return true;
        }

        private bool IsExpectedAttackEpisode(StalkerAttackEpisode episode)
        {
            return episode.EpisodeId.IsValid
                && episode.EpisodeId != _attackBaselineEpisodeId
                && episode.TargetIdAtEntry == _attackTargetId
                && episode.StartedTick > _attackBaselineRunnerTick
                && episode.StartedTick >= _attackPositionedRunnerTick;
        }

        private void LogUnexpectedAttackEpisodeOnce(
            StalkerAttackEpisode episode,
            StalkerController controllerComponent)
        {
            var key = $"{episode.EpisodeId.Value}:{episode.TargetIdAtEntry}:{episode.HitMomentResolved}:{controllerComponent.CurrentState}";
            if (string.Equals(key, _lastAttackWaitLogKey, StringComparison.Ordinal))
            {
                return;
            }

            _lastAttackWaitLogKey = key;
            Debug.Log($"STK4|ATTACK|role=Host|stage=EpisodeWait|result=Rejected|reason=UnexpectedEpisode|episode={FormatEpisode(episode.EpisodeId)}|baselineEpisode={FormatEpisode(_attackBaselineEpisodeId)}|target={episode.TargetIdAtEntry}|expectedTarget={_attackTargetId}|targetMatches={episode.TargetIdAtEntry == _attackTargetId}|startedTick={episode.StartedTick}|baselineRunnerTick={_attackBaselineRunnerTick}|positionedRunnerTick={_attackPositionedRunnerTick}|hitResolved={episode.HitMomentResolved}|state={controllerComponent.CurrentState}");
        }

        private void CheckExpectedEpisodeTimeout(
            StalkerFusionRuntime runtime,
            NetworkRunner runner,
            StalkerController controllerComponent)
        {
            var simDelta = runtime.AuthoritativeSimulationCount - _attackPositionedSimulationCount;
            if (simDelta < ExpectedTargetEpisodeWaitSimulationDelta)
            {
                return;
            }

            Debug.LogWarning($"STK4|ATTACK|role=Host|stage=EpisodeWait|result=Failed|reason=ExpectedTargetEpisodeNotObserved|expectedTarget={_attackTargetId}|baselineEpisode={FormatEpisode(_attackBaselineEpisodeId)}|activeEpisode={FormatEpisode(controllerComponent.ActiveAttackEpisodeId)}|activeTarget={controllerComponent.AttackTargetId}|state={controllerComponent.CurrentState}|simDelta={simDelta}|simCount={runtime.AuthoritativeSimulationCount}|runnerTick={runner.Tick.Raw}");
            _attackStage = Stk4AttackStage.Failed;
        }

        private void LogAttackTargetCompetition(
            StalkerFusionRuntime runtime,
            PlayerId localPlayerId,
            PlayerRuntimeIdentity localIdentity,
            PlayerId remotePlayerId,
            PlayerRuntimeIdentity remoteIdentity,
            string stage)
        {
            var stalkerPosition = runtime.transform.position;
            var localPosition = GetTargetSamplePosition(localIdentity);
            var remotePosition = GetTargetSamplePosition(remoteIdentity);
            Debug.Log($"STK4|ATTACK|role=Host|stage=TargetCompetition|sample={stage}|localPlayer={localPlayerId}|localPosition={FormatVector(localPosition)}|localDistance={Vector3.Distance(stalkerPosition, localPosition):0.###}|remotePlayer={remotePlayerId}|remotePosition={FormatVector(remotePosition)}|remoteDistance={Vector3.Distance(stalkerPosition, remotePosition):0.###}|tieBreakLowerPlayerIdWins=True");
        }

        private void CaptureAndLogIfChangedOrDue(NetworkRunner runner)
        {
            var stalkerSignature = CreateStalkerSignature(runner);
            var topologySignature = CreateTopologySignature(runner);
            var due = Time.realtimeSinceStartup >= _nextLogTime;

            if (!due
                && string.Equals(stalkerSignature, _lastStalkerSignature, StringComparison.Ordinal)
                && string.Equals(topologySignature, _lastTopologySignature, StringComparison.Ordinal))
            {
                return;
            }

            _lastStalkerSignature = stalkerSignature;
            _lastTopologySignature = topologySignature;
            _nextLogTime = Time.realtimeSinceStartup + Mathf.Max(0.25f, logIntervalSeconds);

            Debug.Log(stalkerSignature);
            Debug.Log(topologySignature);
        }

        private string CreateStalkerSignature(NetworkRunner runner)
        {
            var role = runner.IsServer ? "Host" : "Client";
            if (!TryGetPrimaryStalkerRuntime(runner, out var runtime))
            {
                return $"STK2|STALKER|role={role}|exists=False|server={runner.IsServer}";
            }

            var obj = runtime.Object;
            var controllerComponent = runtime.GetComponent<StalkerController>();
            var hasStep = runtime.HasLastAuthoritativeStep;
            var step = runtime.LastAuthoritativeStep;
            var consequenceCount = 0;
            if (controllerComponent != null)
            {
                if (runner.IsServer && _hostAttackSink != null)
                {
                    consequenceCount = _hostAttackSink.CallCount;
                }
                else if (!runner.IsServer && _clientAttackSink != null)
                {
                    consequenceCount = _clientAttackSink.CallCount;
                }
            }

            return $"STK2|STALKER|role={role}|exists=True|objectId={(obj != null ? obj.Id.ToString() : "none")}|stateAuth={(obj != null && obj.HasStateAuthority)}|server={runner.IsServer}|simCount={runtime.AuthoritativeSimulationCount}|legacySuppressed={(controllerComponent != null && controllerComponent.SuppressLegacyUpdateSimulation)}|hasStep={hasStep}|runnerTick={runner.Tick.Raw}|runnerTime={runner.SimulationTime}|runnerDelta={runner.DeltaTime}|stepTick={(hasStep ? step.Time.Tick.ToString() : "none")}|stepTime={(hasStep ? step.Time.Seconds.ToString() : "none")}|stepDelta={(hasStep ? step.DeltaSeconds.ToString() : "none")}|state={(controllerComponent != null ? controllerComponent.CurrentState.ToString() : "none")}|detection={(controllerComponent != null ? controllerComponent.DetectionTargetId.ToString() : "none")}|current={(controllerComponent != null ? controllerComponent.CurrentTargetId.ToString() : "none")}|attackEpisode={(controllerComponent != null && controllerComponent.ActiveAttackEpisodeId.IsValid ? controllerComponent.ActiveAttackEpisodeId.Value.ToString() : "none")}|attackTarget={(controllerComponent != null ? controllerComponent.AttackTargetId.ToString() : "none")}|hitResolved={(controllerComponent != null && controllerComponent.HitMomentResolved)}|attackOutcome={(controllerComponent != null ? controllerComponent.AttackOutcome.ToString() : "none")}|attackResolutionCount={(controllerComponent != null ? controllerComponent.AttackResolutionCount.ToString() : "none")}|attackConsequenceCount={consequenceCount}";
        }

        private string CreateTopologySignature(NetworkRunner runner)
        {
            var role = runner.IsServer ? "Host" : "Client";
            var lifecycle = runner.GetComponent<FusionPlayerLifecycle>();
            if (lifecycle == null)
            {
                return $"STK2|TOPOLOGY|role={role}|reason=MissingFusionPlayerLifecycle";
            }

            lifecycle.IdentityRegistry.CollectActivePlayerIds(_activePlayerIds);
            _playerIdBuilder.Clear();
            for (var i = 0; i < _activePlayerIds.Count; i++)
            {
                if (i > 0)
                {
                    _playerIdBuilder.Append(',');
                }

                _playerIdBuilder.Append(_activePlayerIds[i].Value);
            }

            var runtime = TryGetPrimaryStalkerRuntime(runner, out var foundRuntime) ? foundRuntime : null;
            var controllerComponent = runtime != null ? runtime.GetComponent<StalkerController>() : null;
            return $"STK2|TOPOLOGY|role={role}|active={_activePlayerIds.Count}|playerIds={_playerIdBuilder}|entityRegistry={lifecycle.EntityRegistry.Count}|simCount={(runtime != null ? runtime.AuthoritativeSimulationCount.ToString() : "none")}|state={(controllerComponent != null ? controllerComponent.CurrentState.ToString() : "none")}|detection={(controllerComponent != null ? controllerComponent.DetectionTargetId.ToString() : "none")}|current={(controllerComponent != null ? controllerComponent.CurrentTargetId.ToString() : "none")}";
        }

        private void LogNoCheatSnapshot(NetworkRunner runner, string stage)
        {
            if (!TryGetPrimaryStalkerRuntime(runner, out var runtime))
            {
                Debug.Log($"STK2|NO_CHEAT|stage={stage}|reason=MissingStalker");
                return;
            }

            var controllerComponent = runtime.GetComponent<StalkerController>();
            if (controllerComponent == null)
            {
                Debug.Log($"STK2|NO_CHEAT|stage={stage}|reason=MissingStalkerController");
                return;
            }

            var targetPosition = "none";
            if (TryResolveHostTargetIdentity(runner, controllerComponent.CurrentTargetId, out var identity))
            {
                targetPosition = FormatVector(GetTargetSamplePosition(identity));
            }

            Debug.Log($"STK2|NO_CHEAT|stage={stage}|current={controllerComponent.CurrentTargetId}|targetPosition={targetPosition}|lastKnown={FormatVector(controllerComponent.LastKnownPosition)}|state={controllerComponent.CurrentState}");
        }

        private void VerifyHiddenNoCheatAfterDelay(NetworkRunner runner)
        {
            if (!TryGetPrimaryStalkerRuntime(runner, out var runtime))
            {
                return;
            }

            var simDelta = runtime.AuthoritativeSimulationCount - _baselineSimulationCount;
            if (simDelta < RequiredHiddenVerificationSimulationDelta)
            {
                return;
            }

            var controllerComponent = runtime.GetComponent<StalkerController>();
            if (controllerComponent == null || !controllerComponent.CurrentTargetId.IsValid)
            {
                LogNoCheatFailure("TargetNoLongerValid");
                return;
            }

            if (controllerComponent.CurrentTargetId != _noCheatTargetId)
            {
                LogNoCheatFailure("CurrentTargetChanged");
                return;
            }

            if (!TryResolveHostTargetIdentity(runner, _noCheatTargetId, out var identity))
            {
                LogNoCheatFailure("TargetNotResolvableForVerification");
                return;
            }

            var targetPosition = GetTargetSamplePosition(identity);
            var lastKnown = controllerComponent.LastKnownPosition;
            var movedFromBaseline = Vector3.Distance(targetPosition, _baselineTargetPosition) > NoCheatPositionTolerance;
            var reachedHiddenMarker = Vector3.Distance(targetPosition, _hiddenTargetPosition) <= NoCheatPositionTolerance;
            var frozen = Vector3.Distance(lastKnown, _baselineLastKnownPosition) <= NoCheatPositionTolerance;
            var passed = movedFromBaseline
                && reachedHiddenMarker
                && frozen
                && simDelta >= RequiredHiddenVerificationSimulationDelta
                && controllerComponent.CurrentTargetId == _noCheatTargetId;

            Debug.Log($"STK2|NO_CHEAT|stage=HiddenVerified|result={(passed ? "Passed" : "Failed")}|target={_noCheatTargetId}|baselineTargetPosition={FormatVector(_baselineTargetPosition)}|targetPosition={FormatVector(targetPosition)}|hiddenTargetPosition={FormatVector(_hiddenTargetPosition)}|baselineLastKnown={FormatVector(_baselineLastKnownPosition)}|lastKnown={FormatVector(lastKnown)}|moved={movedFromBaseline}|reachedHidden={reachedHiddenMarker}|frozen={frozen}|simDelta={simDelta}|state={controllerComponent.CurrentState}|baselineRunnerTick={_baselineRunnerTick}|runnerTick={runner.Tick.Raw}");
            _noCheatStage = NoCheatStage.Complete;
        }

        private void LogNoCheatFailure(string reason)
        {
            Debug.LogWarning($"STK2|NO_CHEAT|stage={_noCheatStage}|result=Failed|reason={reason}|target={_noCheatTargetId}");
            _noCheatStage = NoCheatStage.Complete;
        }

        private bool TryCaptureNoCheatBaseline(NetworkRunner runner, StalkerFusionRuntime runtime)
        {
            var controllerComponent = runtime != null ? runtime.GetComponent<StalkerController>() : null;
            if (controllerComponent == null || !controllerComponent.CurrentTargetId.IsValid)
            {
                return false;
            }

            if (!_noCheatPositionedPlayerId.IsValid || controllerComponent.CurrentTargetId != _noCheatPositionedPlayerId)
            {
                return false;
            }

            if (!TryResolveHostTargetIdentity(runner, controllerComponent.CurrentTargetId, out var identity))
            {
                return false;
            }

            var targetPosition = GetTargetSamplePosition(identity);
            var lastKnown = controllerComponent.LastKnownPosition;
            if (Vector3.Distance(targetPosition, lastKnown) > NoCheatPositionTolerance)
            {
                return false;
            }

            _noCheatTargetId = controllerComponent.CurrentTargetId;
            _baselineTargetPosition = targetPosition;
            _baselineLastKnownPosition = controllerComponent.LastKnownPosition;
            _baselineSimulationCount = runtime.AuthoritativeSimulationCount;
            _baselineRunnerTick = runner.Tick.Raw;
            return true;
        }

        private bool TryGetPrimaryStalkerRuntime(NetworkRunner runner, out StalkerFusionRuntime runtime)
        {
            if (_cachedRuntime != null)
            {
                if (_cachedRuntime.Object != null && _cachedRuntime.Object.IsValid && RuntimeBelongsToRunner(_cachedRuntime, runner))
                {
                    runtime = _cachedRuntime;
                    return true;
                }

                _cachedRuntime = null;
            }

            runtime = null;
            if (Time.realtimeSinceStartup < _nextRuntimeResolveTime)
            {
                return false;
            }

            _nextRuntimeResolveTime = Time.realtimeSinceStartup + RuntimeResolveIntervalSeconds;
            var found = FindObjectsByType<StalkerFusionRuntime>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < found.Length; i++)
            {
                if (found[i] == null
                    || found[i].Object == null
                    || !found[i].Object.IsValid
                    || !RuntimeBelongsToRunner(found[i], runner))
                {
                    continue;
                }

                runtime = found[i];
                _cachedRuntime = runtime;
                return true;
            }

            return false;
        }

        private static bool RuntimeBelongsToRunner(StalkerFusionRuntime runtime, NetworkRunner runner)
        {
            return runtime != null && runner != null && runtime.Runner == runner;
        }

        private static bool TryResolveFirstHostPlayerIdentity(NetworkRunner runner, out PlayerId playerId, out PlayerRuntimeIdentity identity)
        {
            playerId = PlayerId.Invalid;
            identity = null;
            var lifecycle = runner.GetComponent<FusionPlayerLifecycle>();
            if (lifecycle == null)
            {
                return false;
            }

            lifecycle.IdentityRegistry.CollectActivePlayerIds(s_sharedPlayerIds);
            for (var i = 0; i < s_sharedPlayerIds.Count; i++)
            {
                if (lifecycle.EntityRegistry.TryGetEntity(s_sharedPlayerIds[i], out identity)
                    && identity != null
                    && identity.EntityRoot != null)
                {
                    playerId = s_sharedPlayerIds[i];
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveFirstRemoteHostPlayerIdentity(NetworkRunner runner, out PlayerId playerId, out PlayerRuntimeIdentity identity)
        {
            playerId = PlayerId.Invalid;
            identity = null;

            if (runner == null)
            {
                return false;
            }

            var lifecycle = runner.GetComponent<FusionPlayerLifecycle>();
            if (lifecycle == null)
            {
                return false;
            }

            foreach (var player in runner.ActivePlayers)
            {
                if (!player.IsRealPlayer || player == runner.LocalPlayer)
                {
                    continue;
                }

                if (!lifecycle.IdentityRegistry.TryGetPlayerId(player, out var candidateId)
                    || !candidateId.IsValid
                    || !lifecycle.EntityRegistry.TryGetEntity(candidateId, out var candidateIdentity)
                    || candidateIdentity == null
                    || candidateIdentity.EntityRoot == null)
                {
                    continue;
                }

                playerId = candidateId;
                identity = candidateIdentity;
                return true;
            }

            return false;
        }

        private static bool TryResolveHostLocalPlayerIdentity(NetworkRunner runner, out PlayerId playerId, out PlayerRuntimeIdentity identity)
        {
            playerId = PlayerId.Invalid;
            identity = null;

            if (runner == null || !runner.LocalPlayer.IsRealPlayer)
            {
                return false;
            }

            var lifecycle = runner.GetComponent<FusionPlayerLifecycle>();
            if (lifecycle == null
                || !lifecycle.IdentityRegistry.TryGetPlayerId(runner.LocalPlayer, out playerId)
                || !playerId.IsValid)
            {
                playerId = PlayerId.Invalid;
                return false;
            }

            return lifecycle.EntityRegistry.TryGetEntity(playerId, out identity)
                && identity != null
                && identity.EntityRoot != null;
        }

        private static bool TryComputeAttackTargetPosition(StalkerFusionRuntime runtime, out Vector3 position)
        {
            position = default;
            if (runtime == null)
            {
                return false;
            }

            var origin = runtime.transform.position;
            var forward = runtime.transform.forward;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }

            var desired = origin + forward.normalized * 1.2f;
            desired.y = origin.y;
            if (NavMesh.SamplePosition(desired, out var hit, 0.75f, NavMesh.AllAreas))
            {
                position = hit.position;
                return true;
            }

            position = desired;
            return true;
        }

        private static bool TryComputeHostAwayPosition(StalkerFusionRuntime runtime, out Vector3 position)
        {
            position = default;
            if (runtime == null)
            {
                return false;
            }

            var origin = runtime.transform.position;
            var forward = runtime.transform.forward;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }

            var desired = origin - forward.normalized * 5f;
            desired.y = origin.y;
            if (NavMesh.SamplePosition(desired, out var hit, 1.5f, NavMesh.AllAreas))
            {
                position = hit.position;
                return true;
            }

            position = desired;
            return true;
        }

        private static bool TryResolveHostTargetIdentity(
            NetworkRunner runner,
            PlayerId targetId,
            out PlayerRuntimeIdentity identity)
        {
            identity = null;
            var lifecycle = runner.GetComponent<FusionPlayerLifecycle>();
            if (lifecycle == null || !targetId.IsValid)
            {
                return false;
            }

            return lifecycle.EntityRegistry.TryGetEntity(targetId, out identity)
                && identity != null
                && identity.EntityRoot != null;
        }

        private static bool TryTeleportTarget(PlayerRuntimeIdentity identity, Vector3 position, out string reason)
        {
            if (identity == null || identity.EntityRoot == null)
            {
                reason = "MissingIdentity";
                return false;
            }

            var networkObject = identity.EntityRoot.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                reason = "MissingNetworkObject";
                return false;
            }

            if (!networkObject.HasStateAuthority)
            {
                reason = "MissingStateAuthority";
                return false;
            }

            if (identity.EntityRoot.TryGetComponent<NetworkCharacterController>(out var characterController))
            {
                characterController.Teleport(position);
                Physics.SyncTransforms();
                reason = string.Empty;
                return true;
            }

            if (identity.EntityRoot.TryGetComponent<NetworkTransform>(out var networkTransform))
            {
                networkTransform.Teleport(position);
                Physics.SyncTransforms();
                reason = string.Empty;
                return true;
            }

            reason = "MissingReplicatedMovementComponent";
            return false;
        }

        private static Vector3 GetTargetSamplePosition(PlayerRuntimeIdentity identity)
        {
            return identity.VisionTargetPoint != null
                ? identity.VisionTargetPoint.position
                : identity.EntityRoot.position;
        }

        private static bool ResolveBooleanArg(string prefix)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg != null && arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return bool.TryParse(arg.Substring(prefix.Length), out var parsed) && parsed;
                }
            }

            return false;
        }

        private static string FormatVector(Vector3 value)
        {
            return $"{value.x:0.###},{value.y:0.###},{value.z:0.###}";
        }

        private static string FormatEpisode(StalkerAttackEpisodeId episodeId)
        {
            return episodeId.IsValid ? episodeId.Value.ToString() : "none";
        }

        private void LogAttackHostWaiting(string reason)
        {
            var key = $"{_attackStage}:{reason}";
            if (!string.Equals(key, _lastAttackWaitLogKey, StringComparison.Ordinal)
                || Time.realtimeSinceStartup >= _nextAttackWaitLogTime)
            {
                _lastAttackWaitLogKey = key;
                _nextAttackWaitLogTime = Time.realtimeSinceStartup + AttackWaitLogIntervalSeconds;
                Debug.Log($"STK4|ATTACK|role=Host|stage={_attackStage}|result=Waiting|reason={Sanitize(reason)}");
            }
        }

        private void LogAttackHostFailure(string reason)
        {
            Debug.LogWarning($"STK4|ATTACK|role=Host|stage={_attackStage}|result=Failed|reason={Sanitize(reason)}|target={_attackTargetId}");
            _attackStage = Stk4AttackStage.Failed;
        }

        private enum NoCheatStage
        {
            None,
            PositionVisible,
            WaitForLock,
            OccludeAndMove,
            WaitForHiddenVerification,
            Complete
        }

        private enum Stk4AttackStage
        {
            None,
            WaitForClientPlayer,
            WaitForBaselineSettled,
            WaitForEpisode,
            WaitForResolution,
            StabilityWindow,
            Complete,
            Failed
        }
    }
}
