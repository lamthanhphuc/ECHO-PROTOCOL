using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EchoProtocol.Networking.Loading;
using EchoProtocol.UI.Loading;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.Networking
{
    /// <summary>Presentation/readiness barrier only; backend authority and Fusion own match/scene loading.</summary>
    [DisallowMultipleComponent]
    public sealed class MatchLoadingCoordinator : MonoBehaviour
    {
        private const int Channel = 0x4543484C; // ECHL
        private const int Version = 1;
        private const float AnnouncementTimeout = 12f;
        private const float AuthorizationTimeout = 65f;
        private const float LoadingTimeout = 180f;
        private const float ReadinessTimeout = 60f;
        private readonly MatchLoadingBarrier _barrier = new MatchLoadingBarrier();
        private readonly HashSet<int> _announced = new HashSet<int>();
        private readonly HashSet<int> _pendingReady = new HashSet<int>();
        private readonly int _streamNonce = Guid.NewGuid().GetHashCode();
        private NetworkBootstrap _bootstrap;
        private NetworkRunner _runner;
        private bool _subscribed, _releasePending, _recovering;
        private int _sequence, _lastHostSequence, _messageId, _sceneDoneFrame, _attemptBeginFrame;
        private float _nextStatus, _nextAck, _phaseStartedAt;
        private MatchLoadingPhase _timedPhase;
        private Task _recoveryTask;
        private string _recoveryError = string.Empty;

        [Serializable]
        private sealed class Envelope
        {
            public int v = Version;
            public string id, scene;
            public int host, seq, kind;
            public int[] members, ready;
            public bool auth, load, release;
            public string error;
        }

        public static MatchLoadingCoordinator Instance { get; private set; }
        public static bool OwnsPresentation => Instance != null && Instance._barrier.IsVisible;
        public MatchLoadingSnapshot Snapshot
        {
            get
            {
                var state = _barrier.Snapshot;
                return string.IsNullOrEmpty(_recoveryError) ? state : new MatchLoadingSnapshot(
                    state.IsVisible, state.Phase, state.SceneName, state.SceneProgress, state.LocalReady,
                    state.ReadyPlayers, state.RequiredPlayers, _recoveryError);
            }
        }
        public string AttemptId => _barrier.AttemptId;
        public bool RecoveryReturnsToLobby => !_barrier.BackendAuthorized && !_barrier.SceneStarted
            && _bootstrap != null && _bootstrap.State == NetworkSessionState.InLobby && _bootstrap.HasRunningRunner;
        public bool IsAttemptActive => _barrier.IsVisible
            && _barrier.Phase != MatchLoadingPhase.Failed && _barrier.Phase != MatchLoadingPhase.Complete;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        public static MatchLoadingCoordinator EnsureExists(NetworkBootstrap bootstrap)
        {
            if (Instance == null) new GameObject("MatchLoadingCoordinator").AddComponent<MatchLoadingCoordinator>();
            Instance.Bind(bootstrap);
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CinematicMatchLoadingScreen.EnsureExists();
        }

        private void Bind(NetworkBootstrap bootstrap)
        {
            if (_bootstrap == bootstrap && _subscribed) return;
            Unsubscribe();
            _bootstrap = bootstrap;
            if (_bootstrap == null || !isActiveAndEnabled) return;
            _bootstrap.SessionStateChanged += SessionChanged;
            _bootstrap.PlayerLeft += PlayerLeft;
            _bootstrap.NetworkSceneLoadDone += NetworkSceneDone;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (_subscribed && _bootstrap != null)
            {
                _bootstrap.SessionStateChanged -= SessionChanged;
                _bootstrap.PlayerLeft -= PlayerLeft;
                _bootstrap.NetworkSceneLoadDone -= NetworkSceneDone;
            }
            _subscribed = false;
        }

        private void OnEnable() { if (_bootstrap != null) Bind(_bootstrap); }
        private void OnDisable() { Unsubscribe(); Dismiss(); }
        private void OnDestroy() { Unsubscribe(); if (Instance == this) Instance = null; }

        public string BeginHost(NetworkRunner runner, string sceneName)
        {
            if (runner == null || !runner.IsRunning || !runner.IsServer || !runner.IsSceneAuthority)
                throw new InvalidOperationException("Only the authoritative host can announce match loading.");
            AdoptRunner(runner);
            var roster = runner.ActivePlayers.Where(p => p.IsRealPlayer).Select(p => p.RawEncoded).ToArray();
            _barrier.Begin(Guid.NewGuid().ToString("N"), sceneName, runner.LocalPlayer.RawEncoded,
                roster, Time.realtimeSinceStartupAsDouble);
            ResetAttempt();
            _announced.Add(runner.LocalPlayer.RawEncoded);
            BroadcastStatus();
            return _barrier.AttemptId;
        }

        public async Task<bool> WaitForAnnouncementsAsync(NetworkRunner runner, string attempt)
        {
            float deadline = Time.realtimeSinceStartup + AnnouncementTimeout;
            while (IsCurrent(runner, attempt) && Time.realtimeSinceStartup < deadline)
            {
                if (Time.frameCount > _attemptBeginFrame + 1
                    && _barrier.RequiredPlayers.All(_announced.Contains)) return true;
                await Task.Delay(50);
            }
            if (IsCurrent(runner, attempt)) Fail("A player did not acknowledge match loading. Check the connection and retry.");
            return false;
        }

        public bool IsCurrent(NetworkRunner runner, string attempt) => IsAttemptActive && runner != null
            && runner == _runner && runner == _bootstrap?.Runner && runner.IsRunning
            && _barrier.AttemptId == attempt && !NetworkBootstrap.IsHostMigrationReconciliationPending;

        public void BackendAuthorized(NetworkRunner runner, string attempt)
        {
            if (!IsCurrent(runner, attempt)) return;
            _barrier.Authorize();
            BroadcastStatus();
        }

        public void BeginSceneTransition(NetworkRunner runner, string attempt)
        {
            if (!IsCurrent(runner, attempt)) return;
            _barrier.StartScene();
            BroadcastStatus();
        }

        public void AbortHost(NetworkRunner runner, string message)
        {
            if (runner != _runner || !IsAttemptActive) return;
            Fail(message);
        }

        public void SceneLoadStarted(NetworkRunner runner, string sceneName)
        {
            if (!MatchesScene(runner, sceneName)) return;
            _barrier.StartScene();
        }

        public void SceneLoadProgress(NetworkRunner runner, string sceneName, float progress)
        {
            if (MatchesScene(runner, sceneName)) _barrier.SetSceneProgress(progress);
        }

        public void SceneLoadCompleted(NetworkRunner runner, string sceneName)
        {
            if (!MatchesScene(runner, sceneName) || _barrier.LocalSceneLoaded) return;
            _barrier.SceneLoaded();
            _sceneDoneFrame = Time.frameCount;
        }

        public void SceneLoadFailed(NetworkRunner runner, string sceneName, Exception exception)
        {
            if (MatchesScene(runner, sceneName)) Fail($"Could not load {sceneName}: {exception.Message}");
        }

        private bool MatchesScene(NetworkRunner runner, string sceneName) => IsAttemptActive
            && runner == _runner && runner == _bootstrap?.Runner && _barrier.SceneName == sceneName;

        private void NetworkSceneDone(NetworkRunner runner) =>
            SceneLoadCompleted(runner, SceneManager.GetActiveScene().name);

        private void AdoptRunner(NetworkRunner runner)
        {
            if (_runner == runner) return;
            _runner = runner;
            _lastHostSequence = 0;
            _sequence = 0;
            _messageId = 0;
        }

        private void ResetAttempt()
        {
            _announced.Clear();
            _pendingReady.Clear();
            _releasePending = false;
            _sceneDoneFrame = int.MaxValue;
            _attemptBeginFrame = Time.frameCount;
            _recoveryError = string.Empty;
            _recoveryTask = null;
            _nextStatus = _nextAck = 0f;
            _timedPhase = _barrier.Phase;
            _phaseStartedAt = Time.realtimeSinceStartup;
        }

        // Called only by the static RPC after authenticating RpcInfo.Source == None.
        internal void ReceiveHostStatus(NetworkRunner runner, string json)
        {
            if (runner == null || runner != _bootstrap?.Runner || !runner.IsClient || runner.IsServer
                || NetworkBootstrap.IsHostMigrationReconciliationPending || string.IsNullOrEmpty(json)
                || json.Length > 1600 || (_recoveryTask != null && !_recoveryTask.IsCompleted)) return;
            Envelope packet;
            try { packet = JsonUtility.FromJson<Envelope>(json); }
            catch (ArgumentException) { return; }
            if (!ValidPacket(packet)) return;
            AdoptRunner(runner);
            if (packet.seq <= _lastHostSequence) return;
            _lastHostSequence = packet.seq;

            if (!_barrier.Matches(packet.id, packet.scene))
            {
                if (!packet.members.Contains(runner.LocalPlayer.RawEncoded)
                    || _bootstrap.State != NetworkSessionState.InLobby) return;
                _barrier.Begin(packet.id, packet.scene, packet.host, packet.members, Time.realtimeSinceStartupAsDouble);
                ResetAttempt();
            }
            if (packet.auth) _barrier.Authorize();
            if (packet.load) _barrier.StartScene();
            if (packet.ready != null)
                foreach (int player in packet.ready) _barrier.Acknowledge(packet.id, packet.scene, player);
            _releasePending |= packet.release;
            if (!string.IsNullOrEmpty(packet.error)) _barrier.Fail(packet.error);
            TryAcceptRelease();
        }

        private static bool ValidPacket(Envelope packet) => packet != null && packet.v == Version
            && Guid.TryParseExact(packet.id, "N", out _) && LobbyMapCatalog.IsGameplayScene(packet.scene)
            && packet.members != null && packet.members.Length >= 1 && packet.members.Length <= 4
            && packet.members.Distinct().Count() == packet.members.Length && packet.members.Contains(packet.host)
            && (packet.ready == null || packet.ready.Length <= packet.members.Length);

        internal void ReceiveClientMessage(NetworkRunner runner, PlayerRef sender, ReliableKey key, ReadOnlySpan<byte> bytes)
        {
            key.GetInts(out int channel, out int version, out _, out _);
            if (channel != Channel || version != Version || runner == null || !runner.IsServer
                || runner != _runner || runner != _bootstrap?.Runner || !IsAttemptActive
                || !sender.IsRealPlayer || !_barrier.RequiredPlayers.Contains(sender.RawEncoded)
                || bytes.Length == 0 || bytes.Length > 1600) return;
            Envelope packet;
            try { packet = JsonUtility.FromJson<Envelope>(Encoding.UTF8.GetString(bytes.ToArray())); }
            catch (ArgumentException) { return; }
            if (packet == null || packet.v != Version || !_barrier.Matches(packet.id, packet.scene)) return;
            // On the server Fusion resolves sender from the authenticated client connection.
            switch (packet.kind)
            {
                case 1: _announced.Add(sender.RawEncoded); break;
                case 2:
                    if (_barrier.BackendAuthorized && _barrier.SceneStarted) _pendingReady.Add(sender.RawEncoded);
                    break;
                case 3: Fail($"A player could not finish loading. {LimitError(packet.error)}"); break;
            }
        }

        private void Update()
        {
            // An error from an older runner must not cover a successfully recovered/new session.
            if (_barrier.IsVisible && _barrier.Phase == MatchLoadingPhase.Failed
                && _bootstrap != null && _bootstrap.Runner != null && _bootstrap.Runner != _runner)
            {
                Dismiss();
                return;
            }
            if (!IsAttemptActive) return;
            if (_bootstrap != null && (NetworkBootstrap.IsHostMigrationReconciliationPending || _bootstrap.IsReconnecting))
            {
                YieldToNetworkRecovery(_runner);
                return;
            }
            if (_bootstrap == null || _runner == null || _bootstrap.Runner != _runner)
            {
                Fail("The network session changed while loading.");
                return;
            }
            if (!_runner.IsRunning)
            {
                Fail("The host connection was lost while loading.");
                return;
            }

            if (LocalGameplayReady())
            {
                _barrier.SetLocalReady();
                if (_barrier.LocalReady && _runner.IsServer)
                    _barrier.Acknowledge(_barrier.AttemptId, _barrier.SceneName, _runner.LocalPlayer.RawEncoded);
            }
            if (_runner.IsServer)
            {
                foreach (int player in _pendingReady)
                    if (AuthoritativeSpawnReady(PlayerRef.FromEncoded(player)))
                        _barrier.Acknowledge(_barrier.AttemptId, _barrier.SceneName, player);
                if (_barrier.ReadyPlayers.Count == _barrier.RequiredPlayers.Count
                    && LocalGameplayReady()
                    && _barrier.RequiredPlayers.All(player => IsConnected(player)
                        && AuthoritativeSpawnReady(PlayerRef.FromEncoded(player))))
                    _barrier.TryRelease(_barrier.AttemptId, _barrier.SceneName, _barrier.HostPlayer);
                if (Time.unscaledTime >= _nextStatus || _barrier.ReleaseReceived)
                {
                    _nextStatus = Time.unscaledTime + 1f;
                    BroadcastStatus();
                }
            }
            else
            {
                if (Time.frameCount > _attemptBeginFrame + 1 && Time.unscaledTime >= _nextAck)
                {
                    _nextAck = Time.unscaledTime + 1f;
                    SendClientMessage(_barrier.LocalReady ? 2 : 1);
                }
                TryAcceptRelease();
            }
            CheckDeadline();
        }

        private bool LocalGameplayReady()
        {
            if (!_barrier.LocalSceneLoaded || Time.frameCount <= _sceneDoneFrame || _bootstrap.IsSceneLoading
                || _runner.IsSceneManagerBusy || SceneManager.GetActiveScene().name != _barrier.SceneName
                || !AuthoritativeSpawnReady(_runner.LocalPlayer)) return false;
            var match = NetworkMatchState.Instance;
            if (match == null || match.Object == null || !match.Object.IsValid || match.Runner != _runner) return false;
            if (!_runner.TryGetPlayerObject(_runner.LocalPlayer, out var player) || !player.HasInputAuthority) return false;
            var camera = FindAnyObjectByType<PlayerCamera>();
            return camera != null && camera.Target == player.transform;
        }

        private bool AuthoritativeSpawnReady(PlayerRef player) => _runner != null
            && _runner.TryGetPlayerObject(player, out var obj) && obj != null && obj.IsValid
            && obj.TryGetComponent<LobbyPlayerState>(out var state) && state.IsGameplayPlayer && state.GameplaySpawnReady;

        private bool IsConnected(int encoded) => _runner.ActivePlayers.Any(player => player.RawEncoded == encoded);

        private void TryAcceptRelease()
        {
            if (_releasePending && LocalGameplayReady())
                _barrier.TryRelease(_barrier.AttemptId, _barrier.SceneName, _barrier.HostPlayer);
        }

        private void CheckDeadline()
        {
            if (!IsAttemptActive) return;
            if (_timedPhase != _barrier.Phase)
            {
                _timedPhase = _barrier.Phase;
                _phaseStartedAt = Time.realtimeSinceStartup;
            }
            float timeout = _barrier.Phase == MatchLoadingPhase.SceneLoading ? LoadingTimeout
                : _barrier.Phase == MatchLoadingPhase.GameplayReadiness ? ReadinessTimeout : AuthorizationTimeout;
            if (Time.realtimeSinceStartup - _phaseStartedAt >= timeout)
                Fail($"{_barrier.Phase} timed out. The match was not marked ready. Return and retry the session.");
        }

        private void BroadcastStatus()
        {
            if (_runner == null || !_runner.IsRunning || !_runner.IsServer || !_barrier.IsVisible) return;
            var packet = new Envelope
            {
                id = _barrier.AttemptId, scene = _barrier.SceneName, host = _barrier.HostPlayer,
                members = _barrier.RequiredPlayers.ToArray(), ready = _barrier.ReadyPlayers.ToArray(),
                auth = _barrier.BackendAuthorized, load = _barrier.SceneStarted,
                release = _barrier.ReleaseReceived, error = LimitError(_barrier.Error), seq = ++_sequence
            };
            try { MatchLoadingRpc.RPC_LoadingStatus(_runner, JsonUtility.ToJson(packet)); }
            catch (Exception error) { Debug.LogWarning($"[MatchLoading] Host announcement failed: {error.Message}"); }
        }

        private void SendClientMessage(int kind)
        {
            if (_runner == null || !_runner.IsRunning || !_runner.IsClient || _runner.IsServer) return;
            var packet = new Envelope { id = _barrier.AttemptId, scene = _barrier.SceneName,
                kind = kind, error = kind == 3 ? LimitError(_barrier.Error) : null };
            try
            {
                var key = ReliableKey.FromInts(Channel, Version, _streamNonce, ++_messageId);
                _runner.SendReliableDataToServer(key, Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet)));
            }
            catch (Exception error) { Debug.LogWarning($"[MatchLoading] Client acknowledgement failed: {error.Message}"); }
        }

        private static string LimitError(string error) => string.IsNullOrEmpty(error) ? string.Empty
            : error.Substring(0, Math.Min(80, error.Length));

        private void Fail(string message)
        {
            if (!IsAttemptActive) return;
            _barrier.Fail(message);
            Debug.LogWarning($"[MatchLoading] {message}");
            if (_runner != null && _runner.IsServer) BroadcastStatus();
            else SendClientMessage(3);
        }

        private void SessionChanged(NetworkSessionState state, string message)
        {
            if (!IsAttemptActive || _recovering) return;
            if (state == NetworkSessionState.Failed || state == NetworkSessionState.Disconnected)
                Fail(string.IsNullOrWhiteSpace(message) ? "The session ended while loading." : message);
            else if (state == NetworkSessionState.ShuttingDown) Dismiss();
        }

        private void PlayerLeft(PlayerRef player)
        {
            if (IsAttemptActive && _barrier.RequiredPlayers.Contains(player.RawEncoded))
                Fail("A required player disconnected while loading. Return to the lobby and start again.");
        }

        public void YieldToNetworkRecovery(NetworkRunner runner)
        {
            if (runner == _runner && IsAttemptActive) Dismiss();
        }

        public void Dismiss() => _barrier.Dismiss();

        internal bool IsFailedAttempt(NetworkRunner runner, string attempt) => _barrier.IsVisible
            && _barrier.Phase == MatchLoadingPhase.Failed && _barrier.AttemptId == attempt
            && ReferenceEquals(runner, _runner);

        public async Task RecoverAsync()
        {
            if (_recovering || !_barrier.IsVisible) return;
            if (_bootstrap == null) { Dismiss(); return; }
            if (_bootstrap.Runner != null && _bootstrap.Runner != _runner) { Dismiss(); return; }
            // A backend rejection leaves the current lobby usable; no networking restart is needed.
            if (RecoveryReturnsToLobby)
            {
                Dismiss();
                return;
            }
            _recovering = true;
            try
            {
                // One in-flight cleanup survives a UI watchdog timeout and repeated button presses.
                var cleanup = _recoveryTask ??= RecoverSessionAsync(_runner, _barrier.AttemptId);
                if (await Task.WhenAny(cleanup, Task.Delay(15000)) != cleanup)
                {
                    // Do not destroy a runner still owned by Fusion's scene coroutine.
                    // Keep recovery actionable instead of an endless disabled button/spinner.
                    _recoveryError = "Network cleanup is still in progress. Scene and session operations must finish before returning safely. You can retry this action.";
                    return;
                }
                await cleanup;
            }
            catch (Exception error)
            {
                _recoveryTask = null;
                _recoveryError = error.Message;
                Debug.LogException(error);
            }
            finally { _recovering = false; }
        }

        private async Task RecoverSessionAsync(NetworkRunner runner, string attempt)
        {
            await _bootstrap.AbortFailedMatchLoadingAsync(runner, attempt);
            if (_bootstrap.HasRunningRunner || SceneManager.GetActiveScene().name != EchoProtocol.Core.GameConstants.SceneMainMenu)
                throw new InvalidOperationException("The session could not be closed yet. Wait for network recovery and try again.");
            if (_barrier.AttemptId == attempt) Dismiss();
        }
    }
}
