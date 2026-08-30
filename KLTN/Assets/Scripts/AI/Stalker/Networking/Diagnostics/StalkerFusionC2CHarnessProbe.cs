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
        private const float RuntimeResolveIntervalSeconds = 0.25f;
        private const float NavMeshSpawnSampleRadius = 0.10f;
        private const float NoCheatPositionTolerance = 0.05f;
        private const int RequiredHiddenVerificationSimulationDelta = 3;
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

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponent<FusionC2CHarnessController>();
            }

            noCheatAutostart = noCheatAutostart || ResolveBooleanArg(NoCheatArg);
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

            return $"STK2|STALKER|role={role}|exists=True|objectId={(obj != null ? obj.Id.ToString() : "none")}|stateAuth={(obj != null && obj.HasStateAuthority)}|server={runner.IsServer}|simCount={runtime.AuthoritativeSimulationCount}|legacySuppressed={(controllerComponent != null && controllerComponent.SuppressLegacyUpdateSimulation)}|hasStep={hasStep}|runnerTick={runner.Tick.Raw}|runnerTime={runner.SimulationTime}|runnerDelta={runner.DeltaTime}|stepTick={(hasStep ? step.Time.Tick.ToString() : "none")}|stepTime={(hasStep ? step.Time.Seconds.ToString() : "none")}|stepDelta={(hasStep ? step.DeltaSeconds.ToString() : "none")}|state={(controllerComponent != null ? controllerComponent.CurrentState.ToString() : "none")}|detection={(controllerComponent != null ? controllerComponent.DetectionTargetId.ToString() : "none")}|current={(controllerComponent != null ? controllerComponent.CurrentTargetId.ToString() : "none")}";
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

            var networkTransform = identity.EntityRoot.GetComponent<NetworkTransform>();
            if (networkTransform == null)
            {
                reason = "MissingNetworkTransform";
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

            networkTransform.Teleport(position);
            Physics.SyncTransforms();
            reason = string.Empty;
            return true;
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

        private enum NoCheatStage
        {
            None,
            PositionVisible,
            WaitForLock,
            OccludeAndMove,
            WaitForHiddenVerification,
            Complete
        }
    }
}
