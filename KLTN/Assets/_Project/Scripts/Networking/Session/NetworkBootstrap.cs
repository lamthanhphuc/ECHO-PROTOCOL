using System;
using EchoProtocol.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using EchoProtocol.Auth;
using EchoProtocol.Gameplay;
using Fusion;
using Fusion.Matchmaking;
using Fusion.Sockets;
using EchoProtocol.Networking.Authority;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.Networking
{
    public enum NetworkSessionState { Disconnected, Connecting, InLobby, InMatch, ShuttingDown, Failed }

    /// <summary>Single owner of the Photon Fusion runner and session lifecycle.</summary>
    public sealed class NetworkBootstrap : MonoBehaviour, INetworkRunnerCallbacks
    {
        public const string RunnerObjectName = "EchoNetworkRunner";
        public const string LobbySceneName = "Lobby";
        public const string BootstrapSceneName = "Bootstrap";
        private const string HostMigrationTransitionSceneName =
            "__EchoHostMigrationTransition";

        [SerializeField] private NetworkRunner _runnerPrefab;
        [SerializeField] private string _hostSessionName = "EchoProtocol";
        [SerializeField, Min(2)] private int _maxPlayers = 4;

        private static NetworkBootstrap _instance;
        private bool _sessionOperationInProgress;
        private bool _callbacksRegistered;
        private NetworkObject _localInputOwner;
        private Func<NetworkPlayerInput> _localInputProvider;
        private Photon.Realtime.RealtimeClient _realtimeClient;
        private MatchAuthorityRuntime _matchAuthority;
        private string _reconnectSessionName;
        private bool _reconnectIdentityPending;
        private bool _completedMatchObserved;
        private int _reconnectGeneration;
        private Task _terminationTask;
        private Task _hostMigrationTask;
        private bool _hostMigrationInProgress;

        // Disconnected PlayerObjects must survive migration while a match
        // is running so their owners can reclaim the exact gameplay state.
        // Lobby players do not need that retention and keeping them there
        // produces ghost avatars/nameplates and stale input ownership.
        private bool _hostMigrationRetainDisconnectedPlayers;
        private bool _hostMigrationSceneFencePending;
        private const float HostMigrationSceneFenceTimeoutSeconds = 30f;

        // Old actor numbers for snapshot players that did not survive
        // Fusion migration as active participants.
        private readonly HashSet<int>
            _hostMigrationRetainedBackendActors = new();

        // A client transport timeout can arrive slightly before Fusion
        // delivers OnHostMigration. Do not destroy the old session during
        // that short race window.
        private const int HostMigrationDisconnectGraceMs = 15000;
        private bool _hostMigrationDisconnectPending;

        // V4.7A:
        // A transport disconnect can happen while Fusion is still
        // processing a heavy network scene load. Never shutdown or
        // replace that Runner until its scene coroutine has finished.
        private bool _sceneTransitionDisconnectPending;
        private NetDisconnectReason _sceneTransitionDisconnectReason;

        // Explicit checkpoints complement Fusion's periodic 10-second
        // Host Migration snapshot update.
        private bool _hostMigrationSnapshotPushInProgress;
        private bool _hostMigrationSnapshotPushQueued;
        private string _hostMigrationSnapshotQueuedReason;

        private CancellationTokenSource _reconnectCancellation;
        private float _reconnectDeadline;
        public bool IsReconnecting { get; private set; }
        public int ReconnectAttempt { get; private set; }
        public float ReconnectSecondsRemaining => Mathf.Max(0f, _reconnectDeadline - Time.realtimeSinceStartup);
        public bool IsSceneLoading { get; private set; }
        private float _nextReconnectProofRetryAt;
        private readonly Dictionary<PlayerRef, int> _actorIds = new();

        public event Action<NetworkSessionState, string> SessionStateChanged;
        public event Action<PlayerRef> PlayerJoined;
        public event Action<PlayerRef> PlayerLeft;
        public event Action<NetworkRunner> NetworkSceneLoadDone;

        public static NetworkBootstrap Instance => _instance;
        public NetworkRunner Runner { get; private set; }
        public NetworkSessionState State { get; private set; } = NetworkSessionState.Disconnected;
        public string CurrentSessionName { get; private set; } = string.Empty;
        public string LastError { get; private set; } = string.Empty;
        public bool HasRunningRunner => Runner != null && Runner.IsRunning;
        public bool IsBusy => _sessionOperationInProgress || State == NetworkSessionState.ShuttingDown;
        public bool ReconnectIdentityPending => _reconnectIdentityPending;
        public static bool IsHostMigrationResumeActive { get; private set; }

        private void Update()
        {
            if (State == NetworkSessionState.InMatch && NetworkMatchState.Instance != null
                && NetworkMatchState.Instance.IsEnded)
                _completedMatchObserved = true;
            if (!_reconnectIdentityPending || !HasRunningRunner
                || Time.unscaledTime < _nextReconnectProofRetryAt) return;
            _nextReconnectProofRetryAt = Time.unscaledTime + 2f;
            _matchAuthority?.TrySubmitLocalIdentity(force: true);
        }

        public void MarkReconnectProofSubmitted() => _reconnectIdentityPending = false;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[NetworkSession] Duplicate NetworkBootstrap destroyed.");
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            EchoProtocol.Voice.VoiceManager.EnsureExists();
            _matchAuthority = MatchAuthorityRuntime.EnsureExists(this);
        }

        private void OnDestroy()
        {
            _reconnectGeneration++;
            _reconnectCancellation?.Cancel();
            _reconnectCancellation?.Dispose();
            if (_instance == this) _instance = null;
            UnregisterCallbacks();
        }

        public Task<bool> StartHost() => CreateRoomAsync(_hostSessionName, _maxPlayers);
        internal Task<bool> StartHost(string sessionName, int maxPlayers) => CreateRoomAsync(sessionName, maxPlayers);
        public Task<bool> JoinGame(string sessionName) => JoinRoomAsync(sessionName);

        public Task<bool> CreateRoomAsync(
            string sessionName, int maxPlayers = 4,
            MatchDifficulty difficulty = MatchDifficulty.Normal)
        {
            if (maxPlayers is < 2 or > 4)
                return FailWithoutStarting("A room must allow between 2 and 4 players.");

            _matchAuthority ??= MatchAuthorityRuntime.EnsureExists(this);
            _matchAuthority.RequestDifficulty(difficulty);
            return StartSessionAsync(GameMode.Host, sessionName, maxPlayers);
        }

        public Task<bool> JoinRoomAsync(string sessionName) => StartSessionAsync(GameMode.Client, sessionName, null);

        public Task Shutdown() => ShutdownAsync(returnToLobby: false);

        private async Task ShutdownAsync(bool returnToLobby, string destinationScene = null)
        {
            if (State == NetworkSessionState.ShuttingDown) return;
            SetState(NetworkSessionState.ShuttingDown, "Leaving room...");
            _reconnectSessionName = null;
            IsReconnecting = false;
            IsSceneLoading = false;
            _sceneTransitionDisconnectPending = false;
            _reconnectIdentityPending = false;
            _completedMatchObserved = false;
            PlayerInteractionControlLock.ReleaseAll();
            ClearLocalInputProvider();
            try
            {
                if (_matchAuthority != null) await _matchAuthority.RelinquishHostAuthorityAsync();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[NetworkSession] Backend Host relinquish failed: {exception.Message}");
            }
            if (Runner == null)
            {
                CurrentSessionName = string.Empty;
                _actorIds.Clear();
                SetState(NetworkSessionState.Disconnected, "Disconnected");
                if (returnToLobby) RestoreLobbyCursor();
                if (destinationScene != null) await LoadDestinationAsync(destinationScene);
                return;
            }

            var runner = Runner;
            UnregisterCallbacks(runner);
            Runner = null;

            try
            {
                if (!runner.IsShutdown) await runner.Shutdown();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[NetworkSession] Runner shutdown failed: {exception}");
            }
            finally
            {
                if (runner != null) Destroy(runner.gameObject);
                CurrentSessionName = string.Empty;
                _actorIds.Clear();
                SetState(NetworkSessionState.Disconnected, "Disconnected");
                if (destinationScene != null) await LoadDestinationAsync(destinationScene);
                else if (returnToLobby) RestoreLobbyCursor();
                else ReturnToBootstrapScene();
            }
        }

        private static void RestoreLobbyCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (SceneManager.GetActiveScene().name != LobbySceneName)
                _ = SceneManager.LoadSceneAsync(LobbySceneName, LoadSceneMode.Single);
        }

        public Task ShutdownRunnerAsync() => ShutdownAsync(returnToLobby: true);

        public async Task LeaveToMainMenuAsync()
        {
            // Invalidate the retry loop before awaiting an in-flight join or cleanup.
            _reconnectGeneration++;
            _reconnectSessionName = null;
            _reconnectCancellation?.Cancel();
            IsReconnecting = false;
            if (_terminationTask != null) await _terminationTask;
            _reconnectCancellation?.Dispose();
            _reconnectCancellation = null;
            if (State == NetworkSessionState.ShuttingDown && !_sessionOperationInProgress)
                SetState(NetworkSessionState.Disconnected, string.Empty);
            await ShutdownAsync(false, EchoProtocol.Core.GameConstants.SceneMainMenu);
        }

        private static async Task LoadDestinationAsync(string sceneName)
        {
            var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation != null) while (!operation.isDone) await Task.Yield();
        }

        public bool RegisterLocalInputProvider(NetworkObject owner, Func<NetworkPlayerInput> provider)
        {
            if (owner == null || !owner.HasInputAuthority || provider == null) return false;
            if (_localInputOwner != null && _localInputOwner != owner)
            {
                Debug.LogError(
                    $"[NetworkSession] Input provider already belongs to {_localInputOwner.InputAuthority}; " +
                    $"rejecting {owner.InputAuthority}.");
                return false;
            }

            _localInputOwner = owner;
            _localInputProvider = provider;
            return true;
        }

        public void UnregisterLocalInputProvider(NetworkObject owner)
        {
            if (_localInputOwner != owner) return;
            _localInputOwner = null;
            _localInputProvider = null;
        }

        public NetworkRunner EnsureRunner()
        {
            if (Runner != null)
            {
                if (!Runner.IsShutdown)
                {
                    RegisterCallbacks();
                    return Runner;
                }

                Runner = null;
                _callbacksRegistered = false;
            }

            Runner = _runnerPrefab != null
                ? Instantiate(_runnerPrefab)
                : new GameObject(RunnerObjectName).AddComponent<NetworkRunner>();

            Runner.name = RunnerObjectName;
            DontDestroyOnLoad(Runner.gameObject);
            EnsureRunnerComponents(Runner);
            RegisterCallbacks();
            return Runner;
        }

        private async Task<bool> StartSessionAsync(GameMode gameMode, string sessionName, int? playerCount)
        {
            var normalizedName = sessionName?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedName)) return await FailWithoutStarting("Room name is required.");
            if (!Guid.TryParse(AuthSession.CurrentUserId, out var userId))
                return await FailWithoutStarting("An authenticated user is required to join a room.");
            if (_sessionOperationInProgress) return await FailWithoutStarting("A create/join operation is already in progress.");
            if (HasRunningRunner) return await FailWithoutStarting($"Already connected to room '{CurrentSessionName}'.");

            _sessionOperationInProgress = true;
            _completedMatchObserved = false;
            LastError = string.Empty;
            SetState(NetworkSessionState.Connecting,
                gameMode == GameMode.Host ? $"Creating room '{normalizedName}'..." : $"Joining room '{normalizedName}'...");

            _matchAuthority ??= MatchAuthorityRuntime.EnsureExists(this);
            if (gameMode == GameMode.Host
                && !await _matchAuthority.PrepareHostAsync(normalizedName, playerCount ?? 4))
            {
                _sessionOperationInProgress = false;
                return await FailWithoutStarting("Backend rejected the Host authority binding.");
            }

            var runner = EnsureRunner();
            try
            {
                _realtimeClient =
                    MatchmakingArgumentsExtensions.BuildRealtimeClient();
                var args = new StartGameArgs
                {
                    GameMode = gameMode,
                    SessionName = normalizedName,
                    Scene = BuildSceneInfo(),
                    SceneManager = runner.GetComponent<INetworkSceneManager>(),
                    ObjectProvider = runner.GetComponent<INetworkObjectProvider>(),
                    RealtimeClient = _realtimeClient,
                    StartGameCancellationToken = IsReconnecting && _reconnectCancellation != null
                        ? _reconnectCancellation.Token : CancellationToken.None,
                };
                var tokenKey = $"EchoProtocol.ConnectionToken.{userId:D}";
                if (!Guid.TryParse(PlayerPrefs.GetString(tokenKey), out var connectionId))
                {
                    connectionId = Guid.NewGuid();
                    PlayerPrefs.SetString(tokenKey, connectionId.ToString("D"));
                    PlayerPrefs.Save();
                }
                args.ConnectionToken = connectionId.ToByteArray();
                args.PlayerUniqueId = Math.Max(1L, BitConverter.ToInt64(args.ConnectionToken, 0) & long.MaxValue);
                if (playerCount.HasValue) args.PlayerCount = playerCount.Value;
                if (gameMode == GameMode.Host)
                {
                    args.SessionProperties = _matchAuthority.BuildHostSessionProperties();
                }

                RuntimeLog.Log(
                RuntimeLogCategory.NetworkSession,
                $"[NetworkSession] Starting {gameMode} for room '{normalizedName}'.");
                Application.targetFrameRate = 60;
                var result = await runner.StartGame(args);
                if (!result.Ok)
                {
                    await HandleStartFailureAsync(
                        $"Could not {(gameMode == GameMode.Host ? "create" : "join")} room '{normalizedName}': " +
                        $"{result.ShutdownReason}. {result.ErrorMessage}".Trim());
                    return false;
                }

                CurrentSessionName = normalizedName;
                if (!_matchAuthority.AttachJoinedSession(runner))
                {
                    await HandleStartFailureAsync("Fusion room has no valid backend match binding.");
                    return false;
                }
                var reconnecting = _reconnectSessionName == normalizedName;
                SetState(gameMode == GameMode.Client
                        && SceneManager.GetActiveScene().name == LobbyManager.GameSceneName
                        ? NetworkSessionState.InMatch : NetworkSessionState.InLobby,
                    $"{(gameMode == GameMode.Host ? "Created" : "Joined")} room '{normalizedName}'.");
                _reconnectSessionName = null;
                if (reconnecting) _matchAuthority.TrySubmitLocalIdentity(force: true);
                RuntimeLog.Log(
                RuntimeLogCategory.NetworkSession,
                $"[NetworkSession] Connected. Mode={gameMode}, Room='{normalizedName}', LocalPlayer={runner.LocalPlayer}.");

                if (runner.IsSceneAuthority && SceneManager.GetActiveScene().name != LobbySceneName)
                {
                    RuntimeLog.Log(
                RuntimeLogCategory.NetworkSession,
                $"[NetworkSession] Loading network scene '{LobbySceneName}'.");
                    _ = runner.LoadScene(LobbySceneName, LoadSceneMode.Single);
                }

                return true;
            }
            catch (Exception exception)
            {
                await HandleStartFailureAsync($"Connection to room '{normalizedName}' failed: {exception.Message}");
                Debug.LogException(exception);
                return false;
            }
            finally
            {
                _sessionOperationInProgress = false;
            }
        }

        private async Task HandleStartFailureAsync(string message)
        {
            IsSceneLoading = false;
            ClearLocalInputProvider();
            if (_matchAuthority != null) await _matchAuthority.EndAsync("FUSION_START_FAILED");
            LastError = message;
            Debug.LogError($"[NetworkSession] {message}");
            var failedRunner = Runner;
            UnregisterCallbacks(failedRunner);
            Runner = null;

            if (failedRunner != null)
            {
                try
                {
                    if (!failedRunner.IsShutdown) await failedRunner.Shutdown();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[NetworkSession] Cleanup after failure also failed: {exception.Message}");
                }
                Destroy(failedRunner.gameObject);
            }

            CurrentSessionName = string.Empty;
            SetState(NetworkSessionState.Failed, message);
        }

        private Task<bool> FailWithoutStarting(string message)
        {
            LastError = message;
            Debug.LogWarning($"[NetworkSession] {message}");
            SessionStateChanged?.Invoke(State, message);
            return Task.FromResult(false);
        }

        private void SetState(NetworkSessionState state, string message)
        {
            State = state;
            RuntimeLog.Log(
                RuntimeLogCategory.NetworkSession,
                $"[NetworkSession] State={state}. {message}");
            SessionStateChanged?.Invoke(state, message);
        }

        public bool CloseRoomForMatchStart()
        {
            if (Runner == null || !Runner.IsRunning || !Runner.IsServer || !Runner.SessionInfo.IsValid)
            {
                return false;
            }

            // Reconnects need an open session; OnConnectRequest admits only retained tokens.
            Runner.SessionInfo.IsOpen = true;
            Runner.SessionInfo.IsVisible = false;
            SetState(NetworkSessionState.InMatch, "Match started; new players blocked.");
            return true;
        }

        public bool UpdateRoomProperties(
            Dictionary<string, SessionProperty> properties)
        {
            return Runner != null
                && Runner.IsRunning
                && Runner.SessionInfo.IsValid
                && Runner.SessionInfo.UpdateCustomProperties(properties);
        }

        private void RegisterCallbacks()
        {
            if (Runner == null || _callbacksRegistered) return;
            Runner.AddCallbacks(this);
            _callbacksRegistered = true;
        }

        private void UnregisterCallbacks()
        {
            UnregisterCallbacks(Runner);
        }

        private void UnregisterCallbacks(NetworkRunner runner)
        {
            if (runner != null && _callbacksRegistered) runner.RemoveCallbacks(this);
            _callbacksRegistered = false;
        }

        private static NetworkSceneInfo BuildSceneInfo()
        {
            var sceneInfo = new NetworkSceneInfo();
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.buildIndex >= 0)
            {
                sceneInfo.AddSceneRef(SceneRef.FromIndex(activeScene.buildIndex), LoadSceneMode.Single);
            }
            return sceneInfo;
        }

        private static void EnsureRunnerComponents(NetworkRunner runner)
        {
            if (runner.GetComponent<INetworkSceneManager>() == null) runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
            if (runner.GetComponent<INetworkObjectProvider>() == null) runner.gameObject.AddComponent<NetworkObjectProviderDefault>();
        }

        void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (_hostMigrationInProgress
                || IsHostMigrationResumeActive)
            {
                RuntimeLog.Log(
                    RuntimeLogCategory.NetworkSession,
                    $"[NetworkSession] Player joined during Host Migration reconciliation; normal join side effects skipped. Player={player}.");
                return;
            }

            _actorIds[player] =
                runner.GetPlayerActorId(player)
                ?? player.PlayerId;

            RuntimeLog.Log(
                RuntimeLogCategory.NetworkSession,
                $"[NetworkSession] Player joined: {player}. Players={CountPlayers(runner)}.");

            PlayerJoined?.Invoke(player);

            if (player == runner.LocalPlayer)
            {
                _matchAuthority?.TrySubmitLocalIdentity();
            }
        }

        void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            RuntimeLog.Log(
                RuntimeLogCategory.NetworkSession,
                $"[NetworkSession] Player left: {player}. Players={CountPlayers(runner)}.");

            if (_hostMigrationInProgress
                || IsHostMigrationResumeActive)
            {
                RuntimeLog.Log(
                    RuntimeLogCategory.NetworkSession,
                    $"[NetworkSession] Player-left side effects deferred during Host Migration reconciliation. Player={player}.");
                return;
            }

            var actorNumber =
                _actorIds.TryGetValue(
                    player,
                    out var joinedActor)
                    ? joinedActor
                    : runner.GetPlayerActorId(player)
                        ?? player.PlayerId;

            _actorIds.Remove(player);

            if (runner.IsServer)
            {
                _matchAuthority?.MarkPlayerDisconnected(
                    actorNumber);

                NetworkMatchState.Instance
                    ?.ReleasePlayerOperationsAuthoritative(
                        player);

                foreach (var life in
                         FindObjectsByType<NetworkPlayerLifeState>())
                {
                    life.ReleaseDisconnectedReviverAuthoritative(
                        player);
                }
            }

            PlayerLeft?.Invoke(player);
        }

        void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason reason)
        {
            if (reason == ShutdownReason.HostMigration)
            {
                return;
            }

            // StartGame owns cleanup for failed / cancelled startup attempts.
            if (_sessionOperationInProgress
                && State != NetworkSessionState.InMatch
                && State != NetworkSessionState.InLobby)
            {
                return;
            }

            RuntimeLog.Log(
                RuntimeLogCategory.NetworkSession,
                $"[NetworkSession] Runner shutdown: {reason}.");

            if (Runner != runner
                || State == NetworkSessionState.ShuttingDown)
            {
                return;
            }

            // A disconnect/shutdown callback can arrive before Fusion
            // provides OnHostMigration. Any pending migration/scene fence
            // owns this Runner until it resolves.
            if (_hostMigrationInProgress
                || _hostMigrationSceneFencePending
                || _hostMigrationDisconnectPending
                || _sceneTransitionDisconnectPending)
            {
                RuntimeLog.Log(
                    RuntimeLogCategory.NetworkSession,
                    $"[NetworkSession] Shutdown deferred for Host Migration. Reason={reason}, State={State}, SceneLoading={IsSceneLoading}.");

                return;
            }

            // Lobby is a migratable session too. Do not immediately destroy
            // its Runner just because the shutdown callback won the race
            // against OnHostMigration.
            if (!_completedMatchObserved
                && (State == NetworkSessionState.InLobby
                    || State == NetworkSessionState.InMatch))
            {
                BeginHostMigrationGrace(
                    runner,
                    $"shutdown:{reason}");

                return;
            }

            if (reason == ShutdownReason.Ok
                && State != NetworkSessionState.InMatch)
            {
                CleanupTermination(runner);
                return;
            }

            CleanupUnexpectedTermination(
                runner,
                runner.IsClient
                    && State == NetworkSessionState.InMatch
                        ? "Host disconnected. Match ended. Create or join a new room."
                        : $"Session ended: {reason}");
        }

        void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) => RuntimeLog.Log(
                RuntimeLogCategory.NetworkSession,
                "[NetworkSession] Connected to Photon server.");

        void INetworkRunnerCallbacks.OnDisconnectedFromServer(
            NetworkRunner runner,
            NetDisconnectReason reason)
        {
            if (_hostMigrationSceneFencePending
                && Runner == runner)
            {
                return;
            }

            if (_hostMigrationInProgress
                && Runner != runner)
            {
                return;
            }

            if (_sessionOperationInProgress
                && State != NetworkSessionState.InMatch
                && State != NetworkSessionState.InLobby)
            {
                return;
            }

            if (State == NetworkSessionState.ShuttingDown)
            {
                return;
            }

            // NetworkSceneManagerDefault still owns this Runner until
            // OnSceneLoadDone. Never destroy it from a transport callback
            // while its scene coroutine is still active.
            if (Runner == runner
                && IsSceneLoading
                && !_completedMatchObserved)
            {
                if (!_sceneTransitionDisconnectPending)
                {
                    _sceneTransitionDisconnectPending = true;
                    _sceneTransitionDisconnectReason = reason;

                    Debug.LogWarning(
                        $"[NetworkSession] Transport disconnected while " +
                        $"network scene is loading: {reason}. " +
                        $"Deferring Host Migration decision until scene load completes.");
                }

                return;
            }

            // Both Lobby and gameplay are Host-Migratable sessions.
            // Fusion may report the transport loss immediately before
            // delivering OnHostMigration.
            if (Runner == runner
                && !_completedMatchObserved
                && (State == NetworkSessionState.InLobby
                    || State == NetworkSessionState.InMatch))
            {
                BeginHostMigrationGrace(
                    runner,
                    $"disconnect:{reason}");

                return;
            }

            LastError = _completedMatchObserved
                ? "Match finished. Create or join a new room."
                : State == NetworkSessionState.InMatch
                    ? "Host disconnected - Match aborted."
                    : $"Disconnected from server: {reason}";

            Debug.LogWarning(
                $"[NetworkSession] {LastError}");

            if (Runner == runner)
            {
                CleanupUnexpectedTermination(
                    runner,
                    LastError);
            }
        }

        private void BeginHostMigrationGrace(
            NetworkRunner runner,
            string trigger,
            bool forceReconnect = false)
        {
            if (Runner != runner
                || State == NetworkSessionState.ShuttingDown
                || _hostMigrationInProgress
                || _hostMigrationSceneFencePending
                || _hostMigrationDisconnectPending)
            {
                return;
            }

            _hostMigrationDisconnectPending = true;

            Debug.LogWarning(
                $"[NetworkSession] Waiting for Host Migration token. " +
                $"State={State}, Trigger={trigger}.");

            _ =
                WaitForHostMigrationOrCleanupAsync(
                    runner,
                    trigger,
                    forceReconnect);
        }

        private async Task WaitForHostMigrationOrCleanupAsync(
            NetworkRunner disconnectedRunner,
            string trigger,
            bool forceReconnect = false)
        {
            await Task.Delay(
                HostMigrationDisconnectGraceMs);

            // OnHostMigration already claimed the termination.
            if (!_hostMigrationDisconnectPending)
            {
                return;
            }

            // Migration now owns the session.
            if (_hostMigrationInProgress
                || _hostMigrationSceneFencePending)
            {
                _hostMigrationDisconnectPending = false;
                return;
            }

            // Another lifecycle path already replaced/closed this Runner.
            if (Runner != disconnectedRunner
                || State == NetworkSessionState.ShuttingDown)
            {
                _hostMigrationDisconnectPending = false;
                return;
            }

            _hostMigrationDisconnectPending = false;

            LastError =
                $"Host migration token was not received after {trigger}.";

            Debug.LogWarning(
                $"[NetworkSession] {LastError}");

            CleanupUnexpectedTermination(
                disconnectedRunner,
                LastError,
                forceReconnect: forceReconnect);
        }

        private async Task FinishSceneTransitionDisconnectAsync(
            NetworkRunner disconnectedRunner,
            NetDisconnectReason reason)
        {
            // Let NetworkSceneManagerDefault.OnSceneLoaded finish before
            // changing session ownership.
            await Task.Yield();

            if (!_sceneTransitionDisconnectPending
                || Runner != disconnectedRunner
                || State == NetworkSessionState.ShuttingDown
                || _hostMigrationInProgress
                || _hostMigrationSceneFencePending)
            {
                return;
            }

            _sceneTransitionDisconnectPending = false;

            RuntimeLog.Log(
                RuntimeLogCategory.NetworkSession,
                $"[NetworkSession] Scene-transition disconnect reached Host Migration grace. Reason={reason}, State={State}.");

            BeginHostMigrationGrace(
                disconnectedRunner,
                $"scene-transition:{reason}",
                forceReconnect: true);
        }

        public void RequestHostMigrationSnapshotCheckpoint(
            string reason)
        {
            var runner =
                Runner;

            if (runner == null
                || !runner.IsRunning
                || !runner.IsServer
                || State == NetworkSessionState.ShuttingDown
                || State == NetworkSessionState.Disconnected
                || _hostMigrationInProgress)
            {
                return;
            }

            if (_hostMigrationSnapshotPushInProgress)
            {
                _hostMigrationSnapshotPushQueued = true;
                _hostMigrationSnapshotQueuedReason =
                    string.IsNullOrWhiteSpace(reason)
                        ? "coalesced"
                        : reason;

                return;
            }

            _ =
                PushHostMigrationSnapshotCheckpointAsync(
                    runner,
                    reason);
        }

        private async Task PushHostMigrationSnapshotCheckpointAsync(
            NetworkRunner runner,
            string reason)
        {
            if (_hostMigrationSnapshotPushInProgress)
            {
                _hostMigrationSnapshotPushQueued = true;
                _hostMigrationSnapshotQueuedReason =
                    string.IsNullOrWhiteSpace(reason)
                        ? "coalesced"
                        : reason;

                return;
            }

            _hostMigrationSnapshotPushInProgress =
                true;

            var currentReason =
                string.IsNullOrWhiteSpace(reason)
                    ? "unspecified"
                    : reason;

            try
            {
                while (true)
                {
                    _hostMigrationSnapshotPushQueued =
                        false;

                    _hostMigrationSnapshotQueuedReason =
                        null;

                    // Finish authoritative mutations from the current
                    // callback before the snapshot is captured.
                    await Task.Yield();

                    if (Runner != runner
                        || !runner.IsRunning
                        || !runner.IsServer
                        || State == NetworkSessionState.ShuttingDown
                        || State == NetworkSessionState.Disconnected
                        || _hostMigrationInProgress)
                    {
                        return;
                    }

                    await runner
                        .PushHostMigrationSnapshot();

                    RuntimeLog.Log(
                        RuntimeLogCategory.NetworkSession,
                        $"[NetworkSession] Host Migration snapshot checkpoint pushed. Reason={currentReason}, State={State}, Scene={SceneManager.GetActiveScene().name}.");

                    if (!_hostMigrationSnapshotPushQueued)
                    {
                        break;
                    }

                    currentReason =
                        string.IsNullOrWhiteSpace(
                            _hostMigrationSnapshotQueuedReason)
                            ? "coalesced"
                            : _hostMigrationSnapshotQueuedReason;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[NetworkSession] Host Migration snapshot checkpoint failed. Reason={currentReason}, Error={exception.Message}");
            }
            finally
            {
                _hostMigrationSnapshotPushInProgress =
                    false;
            }
        }
        void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress address, NetConnectFailedReason reason)
        {
            LastError = $"Connection failed: {reason}";
            Debug.LogError($"[NetworkSession] {LastError} ({address}).");
            SetState(NetworkSessionState.Failed, LastError);
        }

        private static int CountPlayers(NetworkRunner runner)
        {
            var count = 0;
            foreach (var _ in runner.ActivePlayers) count++;
            return count;
        }

        private void CleanupUnexpectedTermination(
            NetworkRunner runner,
            string message,
            bool allowReconnect = true,
            bool forceReconnect = false)
        {
            if (Runner != runner) return;

            _hostMigrationDisconnectPending = false;
            _sceneTransitionDisconnectPending = false;

            var matchFinished = State == NetworkSessionState.InMatch
                && (_completedMatchObserved
                    || SceneManager.GetActiveScene().name == LobbySceneName
                    || NetworkMatchState.Instance != null && NetworkMatchState.Instance.IsEnded);
            if (matchFinished) message = "Match finished. Create or join a new room.";
            var reconnect =
                allowReconnect
                && runner.IsClient
                && (forceReconnect
                    || State == NetworkSessionState.InMatch)
                && !matchFinished;
            var reconnectName = reconnect ? CurrentSessionName : null;
            PlayerInteractionControlLock.ReleaseAll();
            ClearLocalInputProvider();
            // A transport loss must not end the backend match.
            // Stop local lease ownership and let the lease expire naturally
            // if Fusion cannot complete Host Migration.
            _matchAuthority?.ResetBinding();
            UnregisterCallbacks(runner);
            _sessionOperationInProgress = true;
            CurrentSessionName = string.Empty;
            _reconnectSessionName = reconnectName;
            _reconnectIdentityPending = reconnect;
            LastError = message;
            SetState(NetworkSessionState.ShuttingDown,
                matchFinished ? "Closing finished match..." : "Cleaning up disconnected session...");
            var graceSeconds = runner.GetComponent<FusionPlayerLifecycle>()?.ReconnectGraceSeconds ?? 60f;
            int generation = ++_reconnectGeneration;
            IsReconnecting = reconnect;
            ReconnectAttempt = 0;
            _reconnectDeadline = Time.realtimeSinceStartup + Mathf.Max(1f, graceSeconds - 5f);
            _reconnectCancellation?.Dispose();
            _reconnectCancellation = reconnect ? new CancellationTokenSource() : null;
            _reconnectCancellation?.CancelAfter(TimeSpan.FromSeconds(Mathf.Max(1f, graceSeconds - 5f)));
            IsSceneLoading = false;
            _terminationTask = FinishUnexpectedTerminationAsync(runner, message, generation);
        }

        private async Task FinishUnexpectedTerminationAsync(NetworkRunner runner, string message, int generation)
        {
            if (runner != null)
            {
                try
                {
                    if (!runner.IsShutdown) await runner.Shutdown();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[NetworkSession] Unexpected shutdown cleanup failed: {exception.Message}");
                }
                if (Runner == runner) Runner = null;
                if (runner != null) Destroy(runner.gameObject);
            }
            _callbacksRegistered = false;
            _sessionOperationInProgress = false;
            if (generation != _reconnectGeneration) return;
            if (!string.IsNullOrEmpty(_reconnectSessionName))
            {
                var sessionName = _reconnectSessionName;
                while (_reconnectSessionName == sessionName && Time.realtimeSinceStartup < _reconnectDeadline)
                {
                    await Task.Delay(2000);
                    if (generation != _reconnectGeneration || _reconnectSessionName != sessionName) return;
                    ReconnectAttempt++;
                    bool joined = await JoinRoomAsync(sessionName);
                    if (generation != _reconnectGeneration) return;
                    if (joined)
                    {
                        // The grace-period timer must not cancel an already restored session.
                        IsReconnecting = false;
                        _reconnectCancellation?.Dispose();
                        _reconnectCancellation = null;
                        return;
                    }
                }
                _reconnectSessionName = null;
            }
            _reconnectIdentityPending = false;
            IsReconnecting = false;
            _reconnectCancellation?.Dispose();
            _reconnectCancellation = null;
            if (message.StartsWith("Match finished", StringComparison.Ordinal))
            {
                LastError = string.Empty;
                SetState(NetworkSessionState.Disconnected, "Previous match closed. Ready for a new room.");
            }
            else if (message.StartsWith("Host disconnected", StringComparison.Ordinal))
            {
                LastError = "Host disconnected – Match aborted.";
                SetState(NetworkSessionState.Failed, LastError);
            }
            else
            {
                LastError = message;
                SetState(NetworkSessionState.Failed, message);
            }
            RestoreLobbyCursor();
        }

        private void CleanupTermination(NetworkRunner runner)
        {
            IsSceneLoading = false;
            ClearLocalInputProvider();
            UnregisterCallbacks(runner);
            Runner = null;
            _sessionOperationInProgress = false;
            CurrentSessionName = string.Empty;
            LastError = string.Empty;
            SetState(NetworkSessionState.Disconnected, "Disconnected");
            if (runner != null) Destroy(runner.gameObject);
            ReturnToBootstrapScene();
        }

        private void ClearLocalInputProvider()
        {
            _localInputOwner = null;
            _localInputProvider = null;
        }

        private static void ReturnToBootstrapScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.name != BootstrapSceneName)
            {
                _ = SceneManager.LoadSceneAsync(BootstrapSceneName, LoadSceneMode.Single);
            }
        }

        private async Task HandleHostMigrationAfterSceneReadyAsync(
            NetworkRunner oldRunner,
            HostMigrationToken hostMigrationToken)
        {
            try
            {
                if (Runner != oldRunner
                    || oldRunner == null
                    || hostMigrationToken == null)
                {
                    return;
                }

                if (IsSceneLoading)
                {
                    RuntimeLog.Log(
                        RuntimeLogCategory.NetworkSession,
                        "[NetworkSession] Host Migration token received during scene transition. Waiting for local scene load fence.");

                    var deadline =
                        Time.realtimeSinceStartup
                        + HostMigrationSceneFenceTimeoutSeconds;

                    while (Runner == oldRunner
                           && IsSceneLoading
                           && Time.realtimeSinceStartup
                               < deadline)
                    {
                        await Task.Delay(50);
                    }

                    if (Runner != oldRunner)
                    {
                        return;
                    }

                    if (IsSceneLoading)
                    {
                        Debug.LogWarning(
                            "[NetworkSession] Host Migration scene fence timed out; continuing with explicit P0-G scene teardown.");
                    }
                    else
                    {
                        RuntimeLog.Log(
                            RuntimeLogCategory.NetworkSession,
                            "[NetworkSession] Host Migration scene fence cleared.");
                    }
                }

                await HandleHostMigrationAsync(
                    oldRunner,
                    hostMigrationToken);
            }
            finally
            {
                _hostMigrationSceneFencePending =
                    false;
            }
        }
        private static async Task PrepareHostMigrationSceneReloadAsync(
            Scene sourceScene)
        {
            if (!sourceScene.IsValid()
                || !sourceScene.isLoaded
                || sourceScene.buildIndex < 0)
            {
                throw new InvalidOperationException(
                    "Host Migration source scene is not a valid build scene.");
            }

            var transitionScene =
                SceneManager.GetSceneByName(
                    HostMigrationTransitionSceneName);

            if (!transitionScene.IsValid()
                || !transitionScene.isLoaded)
            {
                transitionScene =
                    SceneManager.CreateScene(
                        HostMigrationTransitionSceneName);
            }

            if (!SceneManager.SetActiveScene(
                    transitionScene))
            {
                throw new InvalidOperationException(
                    "Could not activate Host Migration transition scene.");
            }

            RuntimeLog.Log(
                RuntimeLogCategory.NetworkSession,
                $"[NetworkSession] Host Migration unloading network scene '{sourceScene.name}' before Runner restart.");

            var unload =
                SceneManager.UnloadSceneAsync(
                    sourceScene);

            if (unload == null)
            {
                throw new InvalidOperationException(
                    $"Could not unload Host Migration source scene '{sourceScene.name}'.");
            }

            while (!unload.isDone)
            {
                await Task.Yield();
            }

            RuntimeLog.Log(
                RuntimeLogCategory.NetworkSession,
                $"[NetworkSession] Host Migration network scene unloaded. TransitionScene='{HostMigrationTransitionSceneName}'.");
        }
        private async Task HandleHostMigrationAsync(
            NetworkRunner oldRunner,
            HostMigrationToken hostMigrationToken)
        {
            if (oldRunner == null
                || hostMigrationToken == null
                || Runner != oldRunner
                || _hostMigrationInProgress)
            {
                return;
            }

            _hostMigrationInProgress = true;
            _hostMigrationDisconnectPending = false;
            _sessionOperationInProgress = true;

            var previousState = State;
            var previousSessionName =
                CurrentSessionName;

            var migrationSourceScene =
                SceneManager.GetActiveScene();

            var migrationSceneInfo =
                BuildSceneInfo();

            _hostMigrationRetainDisconnectedPlayers =
                previousState ==
                NetworkSessionState.InMatch;

            _hostMigrationRetainedBackendActors.Clear();

            RuntimeLog.Log(
                RuntimeLogCategory.NetworkSession,
                $"[NetworkSession] Host migration started. Session='{previousSessionName}', Mode={hostMigrationToken.GameMode}, PreviousState={previousState}, Scene='{migrationSourceScene.name}', RetainDisconnected={_hostMigrationRetainDisconnectedPlayers}.");

            LastError = string.Empty;
            SetState(
                NetworkSessionState.Connecting,
                "Host disconnected. Migrating session...");

            PlayerInteractionControlLock.ReleaseAll();
            ClearLocalInputProvider();

            // The old Runner must never execute the normal unexpected-
            // termination path because that path ends backend authority.
            UnregisterCallbacks(oldRunner);

            if (Runner == oldRunner)
            {
                Runner = null;
            }

            try
            {
                if (!oldRunner.IsShutdown)
                {
                    await oldRunner.Shutdown(
                        shutdownReason:
                            ShutdownReason.HostMigration);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[NetworkSession] Old Runner Host Migration shutdown warning: {exception.Message}");
            }

            if (oldRunner != null)
            {
                Destroy(oldRunner.gameObject);
            }

            _callbacksRegistered = false;

            NetworkRunner migrationRunner = null;

            try
            {
                await PrepareHostMigrationSceneReloadAsync(
                    migrationSourceScene);

                migrationRunner = EnsureRunner();

                _realtimeClient =
                    MatchmakingArgumentsExtensions
                        .BuildRealtimeClient();

                var args = new StartGameArgs
                {
                    GameMode = hostMigrationToken.GameMode,
                    HostMigrationToken = hostMigrationToken,
                    HostMigrationResume =
                        ResumeHostMigration,
                    Scene = migrationSceneInfo,
                    SceneManager =
                        migrationRunner.GetComponent<INetworkSceneManager>(),
                    ObjectProvider =
                        migrationRunner.GetComponent<INetworkObjectProvider>(),
                    RealtimeClient = _realtimeClient
                };

                if (Guid.TryParse(
                        AuthSession.CurrentUserId,
                        out var userId))
                {
                    var tokenKey =
                        $"EchoProtocol.ConnectionToken.{userId:D}";

                    if (!Guid.TryParse(
                            PlayerPrefs.GetString(tokenKey),
                            out var connectionId))
                    {
                        connectionId = Guid.NewGuid();

                        PlayerPrefs.SetString(
                            tokenKey,
                            connectionId.ToString(
                                "D"));

                        PlayerPrefs.Save();
                    }

                    args.ConnectionToken =
                        connectionId.ToByteArray();

                    args.PlayerUniqueId =
                        Math.Max(
                            1L,
                            BitConverter.ToInt64(
                                args.ConnectionToken,
                                0)
                            & long.MaxValue);
                }

                // Spawned() callbacks can run while StartGame restores the
                // Host Migration snapshot. Keep new-match initialization
                // disabled for that entire resume window.
                IsHostMigrationResumeActive = true;

                var result =
                    await migrationRunner.StartGame(args);

                IsHostMigrationResumeActive = false;

                if (!result.Ok)
                {
                    throw new InvalidOperationException(
                        $"Host Migration StartGame failed: {result.ShutdownReason}. {result.ErrorMessage}".Trim());
                }

                Runner = migrationRunner;
                CurrentSessionName =
                    !string.IsNullOrWhiteSpace(previousSessionName)
                        ? previousSessionName
                        : migrationRunner.SessionInfo.Name;

                _matchAuthority ??=
                    MatchAuthorityRuntime.EnsureExists(this);

                if (migrationRunner.IsServer)
                {
                    // Fusion authority is already restored. Keep gameplay
                    // running while backend takeover waits for the previous
                    // Host lease to expire.
                    SetState(
                        previousState ==
                            NetworkSessionState.InMatch
                            ? NetworkSessionState.InMatch
                            : NetworkSessionState.InLobby,
                        "Host migration restored. Waiting for backend Host authority...");

                    bool takeoverAccepted =
                        await _matchAuthority
                            .TakeoverHostAuthorityAsync(
                                migrationRunner);

                    if (!takeoverAccepted)
                    {
                        throw new InvalidOperationException(
                            "Backend rejected migrated Host authority.");
                    }

                    if (_hostMigrationRetainDisconnectedPlayers
                        && _hostMigrationRetainedBackendActors.Count > 0)
                    {
                        bool rosterReconciled =
                            await _matchAuthority
                                .ReconcileMigratedDisconnectedActorsAsync(
                                    _hostMigrationRetainedBackendActors);

                        if (!rosterReconciled)
                        {
                            Debug.LogWarning(
                                "[NetworkSession] Host Migration completed with one or more backend roster disconnects still pending.");
                        }
                    }

                    // Publish an immediate fresh snapshot after the new
                    // Host has successfully assumed backend authority.
                    await migrationRunner
                        .PushHostMigrationSnapshot();
                }
                else
                {
                    if (!_matchAuthority
                            .AttachJoinedSession(
                                migrationRunner))
                    {
                        throw new InvalidOperationException(
                            "Migrated Fusion session has no valid backend match binding.");
                    }
                }

                _actorIds.Clear();

                foreach (var player in
                         migrationRunner.ActivePlayers)
                {
                    _actorIds[player] =
                        migrationRunner
                            .GetPlayerActorId(player)
                        ?? player.PlayerId;
                }

                _reconnectSessionName = null;
                _reconnectIdentityPending = true;
                _completedMatchObserved = false;

                SetState(
                    previousState ==
                        NetworkSessionState.InMatch
                        ? NetworkSessionState.InMatch
                        : NetworkSessionState.InLobby,
                    migrationRunner.IsServer
                        ? "Host migration complete. This player is the new Host."
                        : "Host migration complete. Reconnected to new Host.");

                _matchAuthority
                    .TrySubmitLocalIdentity(
                        force: true);

                RuntimeLog.Log(
                    RuntimeLogCategory.NetworkSession,
                    $"[NetworkSession] Host migration completed. IsServer={migrationRunner.IsServer}, LocalPlayer={migrationRunner.LocalPlayer}, Session='{CurrentSessionName}'.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);

                LastError =
                    $"Host migration failed: {exception.Message}";

                if (migrationRunner != null)
                {
                    try
                    {
                        if (!migrationRunner.IsShutdown)
                        {
                            await migrationRunner.Shutdown();
                        }
                    }
                    catch (Exception cleanupException)
                    {
                        Debug.LogWarning(
                            $"[NetworkSession] Host migration cleanup warning: {cleanupException.Message}");
                    }

                    if (Runner == migrationRunner)
                    {
                        Runner = null;
                    }

                    Destroy(
                        migrationRunner.gameObject);
                }

                CurrentSessionName = string.Empty;

                SetState(
                    NetworkSessionState.Failed,
                    LastError);

                ReturnToBootstrapScene();
            }
            finally
            {
                IsHostMigrationResumeActive = false;
                _hostMigrationRetainDisconnectedPlayers = false;
                _hostMigrationRetainedBackendActors.Clear();

                _callbacksRegistered =
                    Runner != null
                    && !Runner.IsShutdown;

                _sessionOperationInProgress = false;
                _hostMigrationInProgress = false;
            }
        }

        private void ResumeHostMigration(
            NetworkRunner migrationRunner)
        {
            if (migrationRunner == null)
            {
                return;
            }

            var oldPlayerObjects =
                migrationRunner
                    .GetResumeSnapshotNetworkObjectPlayerObjects();

            var snapshotPlayerObjectIds =
                new HashSet<NetworkId>(
                    oldPlayerObjects.Values);

            var activePlayers =
                new HashSet<PlayerRef>(
                    migrationRunner.ActivePlayers);

            var restoredObjects =
                new Dictionary<NetworkId, NetworkObject>();

            foreach (var resumeObject in
                     migrationRunner
                         .GetResumeSnapshotNetworkObjects())
            {
                if (resumeObject == null)
                {
                    continue;
                }

                bool hasTransformState =
                    resumeObject.TryGetBehaviour<NetworkTRSP>(
                        out var trsp);

                Vector3 position =
                    hasTransformState
                        ? trsp.Data.Position
                        : Vector3.zero;

                Quaternion rotation =
                    hasTransformState
                        ? trsp.Data.Rotation
                        : Quaternion.identity;

                var previousId =
                    resumeObject.Id;

                bool isSnapshotPlayerObject =
                    snapshotPlayerObjectIds.Contains(
                        previousId);

                var restored =
                    migrationRunner.Spawn(
                        resumeObject,
                        position:
                            position,
                        rotation:
                            rotation,
                        inputAuthority:
                            isSnapshotPlayerObject
                                ? PlayerRef.None
                                : resumeObject.InputAuthority,
                        onBeforeSpawned:
                            (_, newObject) =>
                            {
                                newObject.CopyStateFrom(
                                    resumeObject);

                                if (isSnapshotPlayerObject)
                                {
                                    newObject.AssignInputAuthority(
                                        PlayerRef.None);
                                }
                            });

                if (restored != null)
                {
                    restoredObjects[previousId] =
                        restored;
                }
            }

            var mappedPlayerObjectIds =
                new HashSet<NetworkId>();

            bool hasLocalBackendUser =
                Guid.TryParse(
                    AuthSession.CurrentUserId,
                    out var localBackendUserId);

            foreach (var pair in
                     oldPlayerObjects)
            {
                if (!activePlayers.Contains(
                        pair.Key))
                {
                    continue;
                }

                if (!restoredObjects.TryGetValue(
                        pair.Value,
                        out var restoredPlayer)
                    || restoredPlayer == null
                    || !restoredPlayer.IsValid)
                {
                    continue;
                }

                if (pair.Key == migrationRunner.LocalPlayer
                    && hasLocalBackendUser
                    && restoredPlayer.TryGetComponent<LobbyPlayerState>(
                        out var snapshotLocalState)
                    && snapshotLocalState.HasVerifiedBackendIdentity
                    && Guid.TryParse(
                        snapshotLocalState.BackendUserId.ToString(),
                        out var snapshotBackendUserId)
                    && snapshotBackendUserId != localBackendUserId)
                {
                    continue;
                }

                migrationRunner.SetPlayerObject(
                    pair.Key,
                    restoredPlayer);

                restoredPlayer.AssignInputAuthority(
                    pair.Key);

                mappedPlayerObjectIds.Add(
                    pair.Value);
            }

            if ((!migrationRunner.TryGetPlayerObject(
                        migrationRunner.LocalPlayer,
                        out var localPlayerObject)
                    || localPlayerObject == null
                    || !localPlayerObject.IsValid)
                && hasLocalBackendUser)
            {
                foreach (var previousPlayerId in
                         snapshotPlayerObjectIds)
                {
                    if (!restoredObjects.TryGetValue(
                            previousPlayerId,
                            out var candidate)
                        || candidate == null
                        || !candidate.IsValid
                        || mappedPlayerObjectIds.Contains(
                            previousPlayerId)
                        || !candidate.TryGetComponent<LobbyPlayerState>(
                            out var candidateState)
                        || !candidateState.HasVerifiedBackendIdentity
                        || !Guid.TryParse(
                            candidateState.BackendUserId.ToString(),
                            out var candidateBackendUserId)
                        || candidateBackendUserId != localBackendUserId)
                    {
                        continue;
                    }

                    migrationRunner.SetPlayerObject(
                        migrationRunner.LocalPlayer,
                        candidate);

                    candidate.AssignInputAuthority(
                        migrationRunner.LocalPlayer);

                    mappedPlayerObjectIds.Add(
                        previousPlayerId);

                    RuntimeLog.Log(
                        RuntimeLogCategory.NetworkSession,
                        $"[NetworkSession] Migrated local PlayerObject reconciled by backend identity. Player={migrationRunner.LocalPlayer}, Object={candidate.Id}.");

                    break;
                }
            }

            var lifecyclePlayerObjects =
                new Dictionary<PlayerRef, NetworkObject>();

            // First register all PlayerObjects that Fusion already mapped
            // to currently active players.
            foreach (var player in
                     migrationRunner.ActivePlayers)
            {
                if (migrationRunner.TryGetPlayerObject(
                        player,
                        out var activePlayerObject)
                    && activePlayerObject != null
                    && activePlayerObject.IsValid)
                {
                    lifecyclePlayerObjects[player] =
                        activePlayerObject;
                }
            }

            var retainedPlayers = 0;
            var conflictingPlayersRemoved = 0;
            var departedLobbyPlayersRemoved = 0;

            // A snapshot PlayerObject is NOT stale merely because its
            // previous PlayerRef is absent from ActivePlayers. The client
            // may still reconnect after the new Host is established.
            foreach (var pair in
                     oldPlayerObjects)
            {
                // This snapshot object was already mapped to an active
                // Fusion player above.
                if (mappedPlayerObjectIds.Contains(
                        pair.Value))
                {
                    continue;
                }

                if (!restoredObjects.TryGetValue(
                        pair.Value,
                        out var retainedPlayer)
                    || retainedPlayer == null
                    || !retainedPlayer.IsValid)
                {
                    continue;
                }

                // Lobby does not preserve disconnected PlayerObjects.
                // Their loadout can be recreated when the account joins
                // again, while removing the old snapshot avoids ghost
                // avatars/nameplates and stale InputAuthority.
                if (!_hostMigrationRetainDisconnectedPlayers)
                {
                    migrationRunner.Despawn(
                        retainedPlayer);

                    restoredObjects.Remove(
                        pair.Value);

                    departedLobbyPlayersRemoved++;

                    continue;
                }

                // InMatch keeps the exact PlayerObject for bounded
                // reconnect so HP, movement, inventory and other
                // Networked gameplay state can be reclaimed.
                //
                // We cannot keep two logical players under the same
                // PlayerRef. Remove only this genuine collision case.
                if (lifecyclePlayerObjects.ContainsKey(
                        pair.Key))
                {
                    Debug.LogWarning(
                        $"[NetworkSession] Conflicting migrated PlayerObject removed. Player={pair.Key}, Object={retainedPlayer.Id}.");

                    migrationRunner.Despawn(
                        retainedPlayer);

                    restoredObjects.Remove(
                        pair.Value);

                    conflictingPlayersRemoved++;

                    continue;
                }

                if (retainedPlayer.TryGetComponent<LobbyPlayerState>(
                        out var retainedLobbyState)
                    && retainedLobbyState.FusionActorNumber > 0)
                {
                    _hostMigrationRetainedBackendActors.Add(
                        retainedLobbyState.FusionActorNumber);
                }

                retainedPlayer
                    .RemoveInputAuthority();

                lifecyclePlayerObjects[pair.Key] =
                    retainedPlayer;

                retainedPlayers++;
            }

            foreach (var sceneObject in
                     migrationRunner
                         .GetResumeSnapshotNetworkSceneObjects())
            {
                sceneObject.Item1.CopyStateFrom(
                    sceneObject.Item2);
            }

            var lifecycle =
                migrationRunner.GetComponent<FusionPlayerLifecycle>();

            if (lifecycle == null
                || !lifecycle.RebuildAfterHostMigration(
                    migrationRunner,
                    lifecyclePlayerObjects))
            {
                throw new InvalidOperationException(
                    "Failed to rebuild migrated player identity registries.");
            }
            if (migrationRunner.IsServer
                && SceneManager.GetActiveScene().name ==
                    LobbyManager.GameSceneName)
            {
                var playerSpawner =
                    FindAnyObjectByType<PlayerSpawner>();

                if (playerSpawner == null
                    || !playerSpawner
                        .AdoptMigratedAuthoritativeWorldState(
                            migrationRunner))
                {
                    throw new InvalidOperationException(
                        "Failed to adopt migrated authoritative gameplay world.");
                }
            }

            RuntimeLog.Log(
                RuntimeLogCategory.NetworkSession,
                $"[NetworkSession] Host migration snapshot restored. DynamicObjects={restoredObjects.Count}, PlayerObjects={lifecyclePlayerObjects.Count}, RetainedPlayers={retainedPlayers}, LobbyDepartedRemoved={departedLobbyPlayersRemoved}, ConflictsRemoved={conflictingPlayersRemoved}.");
        }

        void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input)
        {
            if (_localInputProvider != null) input.Set(_localInputProvider());
        }
        void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            if (!runner.IsServer) return;
            if (State != NetworkSessionState.InMatch)
            {
                request.Accept();
                return;
            }
            var lifecycle = runner.GetComponent<FusionPlayerLifecycle>();
            if (lifecycle != null && lifecycle.CanReconnect(token)) request.Accept();
            else request.Refuse();
        }
        void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        void INetworkRunnerCallbacks.OnHostMigration(
            NetworkRunner runner,
            HostMigrationToken hostMigrationToken)
        {
            if (Runner != runner
                || _hostMigrationInProgress
                || _hostMigrationSceneFencePending)
            {
                return;
            }

            // A valid HostMigrationToken wins over all pending
            // disconnect cleanup paths.
            _sceneTransitionDisconnectPending = false;
            _hostMigrationDisconnectPending = false;

            RuntimeLog.Log(
                RuntimeLogCategory.NetworkSession,
                $"[NetworkSession] Host Migration token received. State={State}, SceneLoading={IsSceneLoading}, Session='{CurrentSessionName}'.");

            _hostMigrationSceneFencePending =
                true;

            _hostMigrationTask =
                HandleHostMigrationAfterSceneReadyAsync(
                    runner,
                    hostMigrationToken);
        }
        void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner)
        {
            var transitionDisconnectPending =
                Runner == runner
                && _sceneTransitionDisconnectPending;

            var transitionDisconnectReason =
                _sceneTransitionDisconnectReason;

            if (Runner == runner)
            {
                IsSceneLoading = false;
            }

            var sceneName = SceneManager.GetActiveScene().name;
            RuntimeLog.Log(
                RuntimeLogCategory.NetworkSession,
                $"[NetworkSession] Scene load complete: {sceneName}.");
            if (Runner == runner
                && sceneName == LobbySceneName
                && State == NetworkSessionState.InMatch)
            {
                PlayerInteractionControlLock.ReleaseAll();
                ClearLocalInputProvider();
                _completedMatchObserved = false;
                _reconnectIdentityPending = false;

                if (runner.IsServer && runner.SessionInfo.IsValid)
                {
                    runner.SessionInfo.IsOpen = true;
                    runner.SessionInfo.IsVisible = true;
                }

                SetState(
                    NetworkSessionState.InLobby,
                    "Match finished; room retained.");
            }
            if (Runner == runner && sceneName == LobbyManager.GameSceneName
                && State == NetworkSessionState.InLobby)
                SetState(NetworkSessionState.InMatch, "Match scene loaded.");
            NetworkSceneLoadDone?.Invoke(runner);

            if (Runner == runner)
            {
                RequestHostMigrationSnapshotCheckpoint(
                    $"scene-ready:{sceneName}");
            }

            if (Runner == runner
                && _reconnectIdentityPending
                && runner.IsClient)
            {
                _matchAuthority
                    ?.TrySubmitLocalIdentity(
                        force: true);
            }

            if (transitionDisconnectPending
                && Runner == runner)
            {
                _ =
                    FinishSceneTransitionDisconnectAsync(
                        runner,
                        transitionDisconnectReason);
            }
        }
        void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner)
        {
            if (Runner == runner) IsSceneLoading = true;
            RuntimeLog.Log(
                RuntimeLogCategory.NetworkSession,
                "[NetworkSession] Network scene load started.");
        }
        void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
        void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    }
}
