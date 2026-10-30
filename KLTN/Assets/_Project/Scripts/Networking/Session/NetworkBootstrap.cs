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
using EchoProtocol.UI.Result;
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
        // A successful StartGame is NOT proof that Fusion has invoked HostMigrationResume.
        // Keep the world initialization fence until the authoritative resume has
        // fully copied snapshot objects and adopted the gameplay scene.
        private TaskCompletionSource<bool> _hostMigrationResumeCompletion;
        private NetworkRunner _hostMigrationExpectedResumeRunner;
        private const int HostMigrationRestoreTimeoutMs = 90000;
        private bool _matchStartPending;

        // Disconnected PlayerObjects must survive migration while a match
        // is running so their owners can reclaim the exact gameplay state.
        // Lobby players do not need that retention and keeping them there
        // produces ghost avatars/nameplates and stale input ownership.
        private bool _hostMigrationRetainDisconnectedPlayers;
        private NetworkId _hostMigrationAuthoritativeMatchStateId;
        private bool _hostMigrationSceneFencePending;
        private const float HostMigrationSceneFenceWarningSeconds = 30f;

        // Old actor numbers for snapshot players that did not survive
        // Fusion migration as active participants.
        private readonly Dictionary<int, Guid>
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
        // Single writer for EXPLICIT snapshot uploads (mission, scene, graceful Leave).
        // AutoUpdate is Fusion-owned; keep it enabled for sudden network loss.
        private readonly SemaphoreSlim _hostMigrationSnapshotGate = new(1, 1);
        private const float HostMigrationCheckpointIntervalSeconds = 10f;
        private float _hostMigrationSnapshotNextCheckpointAt;

        private CancellationTokenSource _reconnectCancellation;
        private float _reconnectDeadline;
        public bool IsReconnecting { get; private set; }
        public int ReconnectAttempt { get; private set; }
        public float ReconnectSecondsRemaining => Mathf.Max(0f, _reconnectDeadline - Time.realtimeSinceStartup);
        public bool IsSceneLoading { get; private set; }
        private float _nextReconnectProofRetryAt;
        private float _nextLocalInputRecoveryAt;
        private readonly Dictionary<PlayerRef, int> _actorIds = new();
        // Host Migration may deliver join callbacks before the recovered
        // PlayerObject roster is authoritative. Replay only verified peers.
        private readonly HashSet<PlayerRef> _migrationDeferredPlayerJoins = new();

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
        public bool IsMatchStartPending => _matchStartPending;
        public bool ReconnectIdentityPending => _reconnectIdentityPending;
        public static bool IsHostMigrationResumeActive { get; private set; }
        // The Fusion scene callback may fire before HostMigrationResume has
        // restored snapshot objects. Treat the entire Runner handoff as an
        // atomic reconciliation window, not only the Spawn callback window.
        public static bool IsHostMigrationReconciliationPending =>
            IsHostMigrationResumeActive
            || (_instance != null && (
                _instance._hostMigrationInProgress
                || _instance._hostMigrationSceneFencePending
                || _instance._hostMigrationDisconnectPending
                || _instance._sceneTransitionDisconnectPending));

        // Fallback for authority events missed while a migration runner is starting.
        // Never create another PlayerObject; rebind input on the current one only.
        private void RecoverLocalInputProvider()
        {
            var runner = Runner;
            if (runner == null || !runner.IsRunning
                || (State != NetworkSessionState.InLobby
                    && State != NetworkSessionState.InMatch)) return;

            if (!runner.TryGetPlayerObject(runner.LocalPlayer, out var localObject)
                || localObject == null
                || !localObject.IsValid
                || !localObject.HasInputAuthority) return;

            if (_localInputOwner == localObject && _localInputProvider != null) return;

            if (localObject.TryGetComponent<NetworkPlayerMovement>(out var movement))
            {
                movement.InputAuthorityGained();
                if (_localInputOwner == localObject && _localInputProvider != null)
                {
                    RuntimeLog.Log(RuntimeLogCategory.NetworkSession,
                        "[NetworkSession] Local input provider recovered for current PlayerObject.");
                }
            }
        }

        private void Update()
        {
            if (State != NetworkSessionState.InMatch)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (HasRunningRunner
                && (State == NetworkSessionState.InLobby
                    || State == NetworkSessionState.InMatch)
                && Time.unscaledTime >= _nextLocalInputRecoveryAt)
            {
                _nextLocalInputRecoveryAt = Time.unscaledTime + 1f;
                RecoverLocalInputProvider();
            }

            if (State == NetworkSessionState.InMatch && NetworkMatchState.Instance != null
                && NetworkMatchState.Instance.IsEnded)
                _completedMatchObserved = true;
            if (!_reconnectIdentityPending || !HasRunningRunner
                || Time.unscaledTime < _nextReconnectProofRetryAt) return;
            _nextReconnectProofRetryAt = Time.unscaledTime + 2f;
            _matchAuthority?.TrySubmitLocalIdentity(force: true);
        }

        public void MarkReconnectProofSubmitted()
        {
            if (!_reconnectIdentityPending) return;
            _reconnectIdentityPending = false;
            RuntimeLog.Log(
                RuntimeLogCategory.MatchAuthority,
                "MA|CLIENT_ROSTER_REBIND_ACK");
        }

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
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            EchoProtocol.Voice.VoiceManager.EnsureExists();
            _matchAuthority = MatchAuthorityRuntime.EnsureExists(this);
            MatchLoadingCoordinator.EnsureExists(this);
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

        private int _requestedGameMode;
        public Task<bool> CreateRoomAsync(
            string sessionName, int maxPlayers = 4,
            MatchDifficulty difficulty = MatchDifficulty.Normal, int gameMode = 0)
        {
            if (maxPlayers is < 2 or > 4)
                return FailWithoutStarting("A room must allow between 2 and 4 players.");

            if (gameMode < 0 || gameMode > 1) return FailWithoutStarting("Invalid game mode.");
            _requestedGameMode = gameMode;
            _matchAuthority ??= MatchAuthorityRuntime.EnsureExists(this);
            _matchAuthority.RequestDifficulty((MatchDifficulty)EchoProtocol.UI.LobbySelectionMemory.Read("difficulty", (int)difficulty));
            return StartSessionAsync(GameMode.Host, sessionName, maxPlayers);
        }

        public Task<bool> JoinRoomAsync(string sessionName) => StartSessionAsync(GameMode.Client, sessionName, null);

        public Task Shutdown() => ShutdownAsync(returnToLobby: false);

        private async Task ShutdownAsync(bool returnToLobby, string destinationScene = null, bool abortFailedLoading = false)
        {
            if (State == NetworkSessionState.ShuttingDown) return;
            ResultSession.Clear();
            EchoProtocol.UI.LobbySelectionMemory.SaveBeforeLeaving();
            var previousState = State;
            SetState(NetworkSessionState.ShuttingDown, "Leaving room...");

            var sceneRunner =
                Runner;

            if (abortFailedLoading)
            {
                // A failed loading attempt has no valid world snapshot to migrate. Still never
                // tear down a runner while Fusion owns an unfinished scene operation.
                float sceneIdleDeadline = Time.realtimeSinceStartup + 12f;
                while (Runner == sceneRunner && sceneRunner != null && !sceneRunner.IsShutdown
                    && (IsSceneLoading || sceneRunner.IsSceneManagerBusy)
                    && Time.realtimeSinceStartup < sceneIdleDeadline)
                    await Task.Delay(50);
                if (Runner != sceneRunner || (sceneRunner != null && !sceneRunner.IsShutdown
                    && (IsSceneLoading || sceneRunner.IsSceneManagerBusy)))
                {
                    if (Runner == sceneRunner) SetState(previousState, "Loading recovery is waiting for Fusion's scene operation.");
                    throw new TimeoutException("Scene loading is still finishing. Wait a moment, then return again.");
                }
            }
            else
                await WaitForRunnerSceneIdleAsync(sceneRunner, "explicit shutdown");

            var gracefulHostMigration =
                !abortFailedLoading
                && sceneRunner != null
                && sceneRunner.IsRunning
                && sceneRunner.IsServer
                && (previousState == NetworkSessionState.InLobby
                    || previousState == NetworkSessionState.InMatch)
                && CountPlayers(sceneRunner) > 1;

            if (gracefulHostMigration)
            {
                try
                {
                    var snapshotPushed =
                        await PushGracefulHostMigrationSnapshotAsync(
                            sceneRunner);

                    if (!snapshotPushed)
                    {
                        LastError = "Host handoff aborted: fresh migration snapshot was not saved.";
                        Debug.LogWarning($"[NetworkSession] {LastError}");
                        SetState(previousState, LastError);
                        return;
                    }
                    else
                    {
                        RuntimeLog.Log(
                            RuntimeLogCategory.NetworkSession,
                            $"[NetworkSession] Graceful Host handoff snapshot pushed. State={previousState}, Players={CountPlayers(sceneRunner)}.");
                    }
                }
                catch (Exception exception)
                {
                    LastError = $"Host handoff aborted: snapshot upload failed. {exception.Message}";
                    Debug.LogWarning($"[NetworkSession] {LastError}");
                    SetState(previousState, LastError);
                    return;
                }
            }

            _reconnectSessionName = null;
            IsReconnecting = false;
            IsSceneLoading = false;
            _sceneTransitionDisconnectPending = false;
            _reconnectIdentityPending = false;
            _completedMatchObserved = false;
            _matchStartPending = false;
            PlayerInteractionControlLock.ReleaseAll();
            ClearLocalInputProvider();
            try
            {
                if (_matchAuthority != null)
                {
                    if (abortFailedLoading && sceneRunner != null && sceneRunner.IsServer)
                        await _matchAuthority.EndAsync("MATCH_LOADING_FAILED");
                    else await _matchAuthority.RelinquishHostAuthorityAsync();
                }
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
                if (!runner.IsShutdown)
                {
                    await runner.Shutdown(
                        shutdownReason: gracefulHostMigration
                            ? ShutdownReason.HostMigration
                            : ShutdownReason.Ok);
                }
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

        private async Task<bool> PushGracefulHostMigrationSnapshotAsync(
            NetworkRunner runner)
        {
            // A graceful Leave needs a FRESH upload, not a cached success.
            // The gate also prevents a pending mission checkpoint from
            // writing into the same Fusion snapshot buffer concurrently.
            if (!await WaitForSnapshotWorldReadyAsync(
                    runner, allowShuttingDown: true))
            {
                RuntimeLog.Log(RuntimeLogCategory.NetworkSession,
                    $"HM_SNAPSHOT_NOT_READY|kind=leave|{SnapshotDiagnostic(runner)}");
                return false;
            }

            await _hostMigrationSnapshotGate.WaitAsync();
            try
            {
                Exception lastException = null;
                for (var attempt = 1; attempt <= 3; attempt++)
                {
                    if (!IsSnapshotWorldReady(runner, allowShuttingDown: true))
                    {
                        RuntimeLog.Log(RuntimeLogCategory.NetworkSession,
                            $"HM_SNAPSHOT_NOT_READY|kind=leave|attempt={attempt}|{SnapshotDiagnostic(runner)}");
                        return false;
                    }

                    try
                    {
                        RuntimeLog.Log(RuntimeLogCategory.NetworkSession,
                            $"HM_SNAPSHOT_PUSH_BEGIN|kind=leave|attempt={attempt}|{SnapshotDiagnostic(runner)}");
                        if (await runner.PushHostMigrationSnapshot())
                        {
                            RuntimeLog.Log(RuntimeLogCategory.NetworkSession,
                                $"HM_SNAPSHOT_PUSH_OK|kind=leave|attempt={attempt}");
                            return true;
                        }
                        RuntimeLog.Log(RuntimeLogCategory.NetworkSession,
                            $"HM_SNAPSHOT_PUSH_REJECTED|kind=leave|attempt={attempt}|{SnapshotDiagnostic(runner)}");
                    }
                    catch (Exception exception)
                    {
                        lastException = exception;
                        Debug.LogWarning(
                            $"[NetworkSession] HM_SNAPSHOT_PUSH_ERROR|kind=leave|attempt={attempt}|{exception}");
                    }

                    // Retry a temporary Photon upload rejection, but never
                    // treat a failed upload as permission to abandon the Host.
                    if (attempt < 3)
                        await Task.Delay(1000);
                }

                if (lastException != null)
                    throw new InvalidOperationException(
                        "Could not push the graceful Host Migration snapshot.",
                        lastException);
                return false;
            }
            finally
            {
                _hostMigrationSnapshotGate.Release();
            }
        }

        private static void RestoreLobbyCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (SceneManager.GetActiveScene().name != LobbySceneName)
            {
                EchoProtocol.UI.GameUIFeedback.Instance.BeginSceneTransition();
                _ = SceneManager.LoadSceneAsync(LobbySceneName, LoadSceneMode.Single);
            }
        }

        public Task ShutdownRunnerAsync() => ShutdownAsync(returnToLobby: true);

        public Task AbortFailedMatchLoadingAsync(NetworkRunner expectedRunner, string attemptId)
        {
            var loading = MatchLoadingCoordinator.Instance;
            if (loading == null || !loading.IsFailedAttempt(expectedRunner, attemptId)
                || (Runner != null && Runner != expectedRunner))
                throw new InvalidOperationException("This failed loading attempt no longer owns the session.");
            if (IsHostMigrationReconciliationPending || IsReconnecting)
                throw new InvalidOperationException("Session recovery is still in progress. Wait for it to finish before returning.");
            _reconnectGeneration++;
            _reconnectSessionName = null;
            _reconnectCancellation?.Cancel();
            return ShutdownAsync(false, EchoProtocol.Core.GameConstants.SceneMainMenu, abortFailedLoading: true);
        }

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
            if (SceneManager.GetActiveScene().name != sceneName)
                EchoProtocol.UI.GameUIFeedback.Instance.BeginSceneTransition();
            try
            {
                var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                if (operation != null) while (!operation.isDone) await Task.Yield();
            }
            finally
            {
                EchoProtocol.UI.GameUIFeedback.Instance.CancelSceneTransition();
            }
        }

        public bool RegisterLocalInputProvider(NetworkObject owner, Func<NetworkPlayerInput> provider)
        {
            if (owner == null || !owner.HasInputAuthority || provider == null) return false;
            if (_localInputOwner != null && _localInputOwner != owner)
            {
                if (!_localInputOwner.IsValid
                    || !_localInputOwner.HasInputAuthority)
                {
                    RuntimeLog.Log(
                        RuntimeLogCategory.NetworkSession,
                        $"[NetworkSession] Replacing stale local input provider with {owner.InputAuthority}.");

                    ClearLocalInputProvider();
                }
                else
                {
                    Debug.LogError(
                        $"[NetworkSession] Input provider already belongs to {_localInputOwner.InputAuthority}; " +
                        $"rejecting {owner.InputAuthority}.");
                    return false;
                }
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
            _matchStartPending = false;
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
                    args.SessionProperties[LobbyMapCatalog.GameModeKey] = _requestedGameMode;
                    args.SessionProperties[LobbyMapCatalog.PropertyKey] = _requestedGameMode == 1 ? 2
                        : Mathf.Clamp(EchoProtocol.UI.LobbySelectionMemory.Read("map", 0), 0, 1);
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
                // A deliberate Leave followed by a manual Join does not use
                // _reconnectSessionName. A snapshot-restored PlayerObject can
                // still carry BackendUserId although its backend roster row
                // was marked disconnected. Every fresh CLIENT StartGame must
                // therefore obtain a new join proof and backend bind ACK.
                if (gameMode == GameMode.Client)
                {
                    _reconnectIdentityPending = true;
                    _nextReconnectProofRetryAt = Time.unscaledTime;
                    RuntimeLog.Log(
                        RuntimeLogCategory.MatchAuthority,
                        $"MA|CLIENT_JOIN_REBIND_PENDING|match={_matchAuthority.MatchId:D}|reconnect={reconnecting}");
                }
                SetState(gameMode == GameMode.Client
                        && LobbyMapCatalog.IsGameplayScene(SceneManager.GetActiveScene().name)
                        ? NetworkSessionState.InMatch : NetworkSessionState.InLobby,
                    $"{(gameMode == GameMode.Host ? "Created" : "Joined")} room '{normalizedName}'.");
                _reconnectSessionName = null;
                if (gameMode == GameMode.Client)
                {
                    // The retry loop remains active until the state-authority
                    // RPC confirms a SUCCESSFUL backend BindPlayerAsync.
                    // Replicated BackendUserId alone must not clear this gate.
                    _matchAuthority.TrySubmitLocalIdentity(force: true);
                }
                RuntimeLog.Log(
                RuntimeLogCategory.NetworkSession,
                $"[NetworkSession] Connected. Mode={gameMode}, Room='{normalizedName}', LocalPlayer={runner.LocalPlayer}.");

                if (runner.IsSceneAuthority && SceneManager.GetActiveScene().name != LobbySceneName)
                {
                    RuntimeLog.Log(
                RuntimeLogCategory.NetworkSession,
                $"[NetworkSession] Loading network scene '{LobbySceneName}'.");
                    await EchoProtocol.UI.GameUIFeedback.LoadNetworkSceneAsync(runner, LobbySceneName);
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
            _matchStartPending = false;
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
                var candidate = Runner;
                Debug.LogError(
                    $"[NETDIAG][CloseRoomRejected] " +
                    $"State={State}; MatchGate={_matchStartPending}; " +
                    $"RunnerPresent={candidate != null}; " +
                    $"Running={candidate != null && candidate.IsRunning}; " +
                    $"Server={candidate != null && candidate.IsServer}; " +
                    $"SceneAuthority={candidate != null && candidate.IsSceneAuthority}; " +
                    $"SessionValid={candidate != null && candidate.SessionInfo.IsValid}; " +
                    $"SceneLoading={IsSceneLoading}; Migration={_hostMigrationInProgress}");
                return false;
            }

            // Reconnects need an open session; OnConnectRequest admits only retained tokens.
            Runner.SessionInfo.IsOpen = true;
            Runner.SessionInfo.IsVisible = false;
            _matchStartPending = false;
            SetState(NetworkSessionState.InMatch, "Match started; new players blocked.");
            return true;
        }

        public bool TryBeginMatchStartGate()
        {
            if (_matchStartPending
                || Runner == null
                || !Runner.IsRunning
                || !Runner.IsServer
                || State != NetworkSessionState.InLobby
                || !Runner.SessionInfo.IsValid)
            {
                return false;
            }

            _matchStartPending = true;

            RuntimeLog.Log(
                RuntimeLogCategory.NetworkSession,
                "[NetworkSession] Match-start local admission gate armed; new connections refused while backend start is confirmed.");

            return true;
        }

        public void CancelMatchStartGate()
        {
            if (!_matchStartPending) return;

            _matchStartPending = false;


            RuntimeLog.Log(
                RuntimeLogCategory.NetworkSession,
                "[NetworkSession] Match-start local admission gate released.");
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
            var sceneManager = runner.GetComponent<INetworkSceneManager>();
            if (sceneManager is NetworkSceneManagerDefault stock && stock.GetType() == typeof(NetworkSceneManagerDefault))
            {
                // This runner has not been started. Replace only Fusion's stock manager with
                // the observing subclass, preserving its options and every base loading operation.
                bool takeover = stock.IsSceneTakeOverEnabled, logErrors = stock.LogSceneLoadErrors;
                DestroyImmediate(stock);
                var observed = runner.gameObject.AddComponent<MatchLoadingSceneManager>();
                observed.IsSceneTakeOverEnabled = takeover;
                observed.LogSceneLoadErrors = logErrors;
            }
            else if (sceneManager == null) runner.gameObject.AddComponent<MatchLoadingSceneManager>();
            if (runner.GetComponent<INetworkObjectProvider>() == null) runner.gameObject.AddComponent<NetworkObjectProviderDefault>();
        }

        public void ReportSceneLoadFailure(NetworkRunner runner, string message)
        {
            if (Runner != runner) return;
            // Called only after the observed scene operation ended, never from a watchdog.
            IsSceneLoading = false;
            LastError = message;
            if (_sceneTransitionDisconnectPending)
                _ = FinishSceneTransitionDisconnectAsync(runner, _sceneTransitionDisconnectReason);
        }

        void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (_hostMigrationInProgress
                || IsHostMigrationResumeActive)
            {
                if (runner == Runner || runner == _hostMigrationExpectedResumeRunner)
                    _migrationDeferredPlayerJoins.Add(player);
                RuntimeLog.Log(
                    RuntimeLogCategory.NetworkSession,
                    $"HM_ADMISSION_JOIN_DEFERRED|player={player}|runnerCurrent={runner == Runner}");
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
            // A replaced Runner must never mutate the current roster.
            if (runner == null || runner != Runner)
            {
                RuntimeLog.Log(
                    RuntimeLogCategory.NetworkSession,
                    $"NET|STALE_PLAYER_LEFT_IGNORED|player={player}");
                return;
            }

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
            if (Runner == runner) MatchLoadingCoordinator.Instance?.YieldToNetworkRecovery(runner);
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
            if (Runner == runner) MatchLoadingCoordinator.Instance?.YieldToNetworkRecovery(runner);
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
                || SceneManager.GetActiveScene().name == EchoProtocol.Core.GameConstants.SceneResult
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

        // Do not serialize while a Fusion scene operation or gameplay world
        // initialization is still in flight. OnSceneLoadDone fires during
        // the scene manager callback, not necessarily after all post-load work.
        private bool IsSnapshotWorldReady(
            NetworkRunner runner, bool allowShuttingDown)
        {
            return runner != null
                && Runner == runner
                && runner.IsRunning
                && runner.IsServer
                && State != NetworkSessionState.Disconnected
                && State != NetworkSessionState.Failed
                && (allowShuttingDown || State != NetworkSessionState.ShuttingDown)
                && !_hostMigrationInProgress
                && !_hostMigrationSceneFencePending
                && !IsHostMigrationResumeActive
                && !IsSceneLoading
                && !runner.IsSceneManagerBusy
                && (State != NetworkSessionState.InMatch
                    || SceneManager.GetActiveScene().name == LobbyManager.GameSceneName);
        }

        private async Task<bool> WaitForSnapshotWorldReadyAsync(
            NetworkRunner runner, bool allowShuttingDown)
        {
            // Bounded wait: never deadlock Leave because a scene did not finish.
            var deadline = Time.realtimeSinceStartup + 12f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (IsSnapshotWorldReady(runner, allowShuttingDown))
                {
                    // The simulation/scene callback may still be in the same
                    // Unity frame. Require a second stable observation.
                    await Task.Delay(150);
                    if (IsSnapshotWorldReady(runner, allowShuttingDown))
                        return true;
                }

                if (runner == null || Runner != runner || !runner.IsRunning
                    || (!allowShuttingDown && State == NetworkSessionState.ShuttingDown))
                    return false;
                await Task.Delay(100);
            }
            return false;
        }

        private string SnapshotDiagnostic(NetworkRunner runner)
        {
            return $"state={State}|scene={SceneManager.GetActiveScene().name}"
                + $"|runnerMatch={Runner == runner}|running={runner != null && runner.IsRunning}"
                + $"|server={runner != null && runner.IsServer}"
                + $"|sceneLoading={IsSceneLoading}|sceneBusy={runner != null && runner.IsSceneManagerBusy}"
                + $"|migration={_hostMigrationInProgress}|queued={_hostMigrationSnapshotPushQueued}";
        }

        public void RequestHostMigrationSnapshotCheckpoint(
            string reason)
        {
            var runner = Runner;
            if (runner == null
                || !runner.IsRunning
                || !runner.IsServer
                || State == NetworkSessionState.ShuttingDown
                || State == NetworkSessionState.Disconnected
                || _hostMigrationInProgress)
                return;

            // Coalesce bursty player/milestone/scene events. A request made
            // during scene load waits in this worker until the world is stable.
            if (_hostMigrationSnapshotPushInProgress)
            {
                _hostMigrationSnapshotPushQueued = true;
                _hostMigrationSnapshotQueuedReason =
                    string.IsNullOrWhiteSpace(reason) ? "coalesced" : reason;
                return;
            }

            _ = PushHostMigrationSnapshotCheckpointAsync(runner, reason);
        }

        private async Task PushHostMigrationSnapshotCheckpointAsync(
            NetworkRunner runner, string reason)
        {
            if (_hostMigrationSnapshotPushInProgress)
            {
                _hostMigrationSnapshotPushQueued = true;
                _hostMigrationSnapshotQueuedReason =
                    string.IsNullOrWhiteSpace(reason) ? "coalesced" : reason;
                return;
            }

            _hostMigrationSnapshotPushInProgress = true;
            var currentReason = string.IsNullOrWhiteSpace(reason)
                ? "unspecified" : reason;
            try
            {
                while (true)
                {
                    _hostMigrationSnapshotPushQueued = false;
                    _hostMigrationSnapshotQueuedReason = null;

                    if (!await WaitForSnapshotWorldReadyAsync(
                            runner, allowShuttingDown: false))
                    {
                        RuntimeLog.Log(RuntimeLogCategory.NetworkSession,
                            $"HM_SNAPSHOT_NOT_READY|kind=checkpoint|reason={currentReason}|{SnapshotDiagnostic(runner)}");
                        return;
                    }

                    // Fusion already performs periodic AutoUpdate (10 s).
                    // Throttle EXTRA manual requests rather than storming the
                    // Photon upload path on player verification/scene load.
                    var cooldown = _hostMigrationSnapshotNextCheckpointAt
                        - Time.realtimeSinceStartup;
                    if (cooldown > 0f)
                    {
                        await Task.Delay(Mathf.CeilToInt(cooldown * 1000f));
                    }

                    await _hostMigrationSnapshotGate.WaitAsync();
                    try
                    {
                        if (!IsSnapshotWorldReady(runner, allowShuttingDown: false))
                        {
                            // If Leave started or the Runner changed, do not
                            // interfere with its fresh, serialized upload.
                            return;
                        }

                        // Requests received during the readiness/cooldown wait
                        // describe state that THIS upload is about to capture.
                        // Do not schedule a second upload for those requests.
                        if (_hostMigrationSnapshotPushQueued)
                        {
                            currentReason = string.IsNullOrWhiteSpace(
                                _hostMigrationSnapshotQueuedReason)
                                    ? "coalesced" : _hostMigrationSnapshotQueuedReason;
                            _hostMigrationSnapshotPushQueued = false;
                            _hostMigrationSnapshotQueuedReason = null;
                        }

                        RuntimeLog.Log(RuntimeLogCategory.NetworkSession,
                            $"HM_SNAPSHOT_PUSH_BEGIN|kind=checkpoint|reason={currentReason}|{SnapshotDiagnostic(runner)}");
                        bool pushed = await runner.PushHostMigrationSnapshot();
                        // Even a refusal should be rate-limited: repeated
                        // retries on an unhealthy Fusion buffer are harmful.
                        _hostMigrationSnapshotNextCheckpointAt =
                            Time.realtimeSinceStartup + HostMigrationCheckpointIntervalSeconds;
                        if (!pushed)
                        {
                            Debug.LogWarning(
                                $"[NetworkSession] HM_SNAPSHOT_PUSH_REJECTED|kind=checkpoint|reason={currentReason}|{SnapshotDiagnostic(runner)}");
                            return;
                        }
                        RuntimeLog.Log(RuntimeLogCategory.NetworkSession,
                            $"HM_SNAPSHOT_PUSH_OK|kind=checkpoint|reason={currentReason}");
                    }
                    finally
                    {
                        _hostMigrationSnapshotGate.Release();
                    }

                    if (!_hostMigrationSnapshotPushQueued)
                        return;

                    currentReason = string.IsNullOrWhiteSpace(
                        _hostMigrationSnapshotQueuedReason)
                            ? "coalesced" : _hostMigrationSnapshotQueuedReason;
                }
            }
            catch (Exception exception)
            {
                // Preserve the original stack trace, and back off on an
                // exception as well as a returned false result.
                _hostMigrationSnapshotNextCheckpointAt =
                    Time.realtimeSinceStartup + HostMigrationCheckpointIntervalSeconds;
                Debug.LogWarning(
                    $"[NetworkSession] HM_SNAPSHOT_PUSH_ERROR|kind=checkpoint|reason={currentReason}|{exception}");
            }
            finally
            {
                _hostMigrationSnapshotPushInProgress = false;
                _hostMigrationSnapshotPushQueued = false;
                _hostMigrationSnapshotQueuedReason = null;
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

        private async Task WaitForRunnerSceneIdleAsync(
            NetworkRunner runner,
            string context)
        {
            if (runner == null) return;

            var waitingLogged = false;
            var sceneIdleDeadline = Time.realtimeSinceStartup + 90f;
            var nextWarningAt =
                Time.realtimeSinceStartup
                + HostMigrationSceneFenceWarningSeconds;

            while (Runner == runner
                   && !runner.IsShutdown
                   && (IsSceneLoading
                       || runner.IsSceneManagerBusy))
            {
                if (!waitingLogged)
                {
                    RuntimeLog.Log(
                        RuntimeLogCategory.NetworkSession,
                        $"[NetworkSession] Waiting for Fusion scene manager before {context}. CallbackLoading={IsSceneLoading}, ManagerBusy={runner.IsSceneManagerBusy}.");

                    waitingLogged = true;
                }

                await Task.Delay(50);

                if (Time.realtimeSinceStartup >= sceneIdleDeadline)
                {
                    Debug.LogError($"[NetworkSession] Scene cleanup timed out before {context}; forcing runner shutdown.");
                    IsSceneLoading = false;
                    return;
                }

                if (Time.realtimeSinceStartup
                    >= nextWarningAt)
                {
                    Debug.LogWarning(
                        $"[NetworkSession] Fusion scene manager is still busy before {context}; Runner teardown remains fenced.");

                    nextWarningAt =
                        Time.realtimeSinceStartup
                        + HostMigrationSceneFenceWarningSeconds;
                }
            }

            if (waitingLogged
                && Runner == runner)
            {
                RuntimeLog.Log(
                    RuntimeLogCategory.NetworkSession,
                    $"[NetworkSession] Fusion scene manager became idle before {context}.");
            }
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
            _matchStartPending = false;

            var matchFinished = State == NetworkSessionState.InMatch
                && (_completedMatchObserved
                    || SceneManager.GetActiveScene().name == EchoProtocol.Core.GameConstants.SceneResult
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
                    await WaitForRunnerSceneIdleAsync(
                        runner,
                        "unexpected disconnect cleanup");

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
            _matchStartPending = false;
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
                EchoProtocol.UI.GameUIFeedback.Instance.BeginSceneTransition();
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

                await WaitForRunnerSceneIdleAsync(
                    oldRunner,
                    "Host Migration Runner replacement");

                if (Runner != oldRunner)
                {
                    return;
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
            _migrationDeferredPlayerJoins.Clear();
            _hostMigrationDisconnectPending = false;
            _sessionOperationInProgress = true;

            var previousState = State;
            var previousSessionName =
                CurrentSessionName;

            var migrationSourceScene =
                SceneManager.GetActiveScene();

            // OnSceneLoadDone can lag behind Unity activating SciFi. A
            // Host migration during that gap must preserve the ongoing
            // match rather than treating its snapshot as a Lobby snapshot.
            if (previousState == NetworkSessionState.InLobby
                && migrationSourceScene.name == LobbyManager.GameSceneName)
            {
                previousState = NetworkSessionState.InMatch;
                RuntimeLog.Log(
                    RuntimeLogCategory.NetworkSession,
                    "[NetworkSession] Migration scene/state race corrected: SciFi scene was already active while state was InLobby.");
            }

            var migrationSceneInfo =
                BuildSceneInfo();

            _hostMigrationRetainDisconnectedPlayers =
                previousState ==
                NetworkSessionState.InMatch;

            _hostMigrationAuthoritativeMatchStateId =
                _hostMigrationRetainDisconnectedPlayers
                && NetworkMatchState.Instance != null
                && NetworkMatchState.Instance.Object != null
                && NetworkMatchState.Instance.Object.IsValid
                    ? NetworkMatchState.Instance.Object.Id
                    : default;

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

            // Re-check the Fusion-owned scene operation immediately before
            // unregistering callbacks and shutting down the old Runner. The
            // callback flag alone has a one-frame race with the default
            // scene manager coroutine.
            await WaitForRunnerSceneIdleAsync(
                oldRunner,
                "Host Migration shutdown");

            if (Runner != oldRunner)
            {
                _sessionOperationInProgress = false;
                _hostMigrationInProgress = false;
                return;
            }

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
                _hostMigrationExpectedResumeRunner = migrationRunner;
                _hostMigrationResumeCompletion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                _realtimeClient =
                    MatchmakingArgumentsExtensions
                        .BuildRealtimeClient();

                var args = new StartGameArgs
                {
                    GameMode = hostMigrationToken.GameMode,
                    HostMigrationToken = hostMigrationToken,
                    HostMigrationResume =
                        ResumeHostMigrationTracked,
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

                if (!result.Ok)
                {
                    throw new InvalidOperationException(
                        $"Host Migration StartGame failed: {result.ShutdownReason}. {result.ErrorMessage}".Trim());
                }

                Runner = migrationRunner;

                if (migrationRunner.IsServer)
                {
                    // On some Fusion runners StartGame completes before the
                    // snapshot callback runs. Finishing here would allow
                    // scene-ready to spawn default MatchState / four new Cores.
                    var resumeTask = _hostMigrationResumeCompletion.Task;
                    RuntimeLog.Log(RuntimeLogCategory.NetworkSession,
                        "HM_RESUME_BARRIER_WAIT|startGame=Ok|waiting_for_snapshot_callback");
                    var completed = await Task.WhenAny(
                        resumeTask,
                        Task.Delay(HostMigrationRestoreTimeoutMs));
                    if (completed != resumeTask)
                    {
                        throw new TimeoutException(
                            "Host Migration resume callback did not finish; refusing to create a fresh gameplay world.");
                    }

                    // Propagate errors from preflight, CopyStateFrom, registry
                    // rebuild and PlayerSpawner adoption. Never claim success.
                    await resumeTask;

                    // The resume callback already calls
                    // PlayerSpawner.AdoptMigratedAuthoritativeWorldState(),
                    // which rejects missing/duplicate authoritative MatchState
                    // and invalid Core snapshots before it returns success.
                    // Re-enumerating NetworkObjects here, after an async
                    // continuation, can access Fusion's transient object
                    // registry and throw ArgumentOutOfRangeException.
                    // Preserve the successful callback as the validation gate.
                    if (_hostMigrationRetainDisconnectedPlayers
                        && previousState == NetworkSessionState.InMatch)
                    {
                        if (SceneManager.GetActiveScene().name
                            != LobbyManager.GameSceneName)
                        {
                            throw new InvalidOperationException(
                                "Host Migration callback completed outside the gameplay scene.");
                        }

                        RuntimeLog.Log(RuntimeLogCategory.NetworkSession,
                            "HM_RESUME_WORLD_VALID|validated_by_authoritative_scene_adoption");
                    }
                    RuntimeLog.Log(RuntimeLogCategory.NetworkSession,
                        "HM_RESUME_BARRIER_PASSED|snapshot_restored_before_takeover");
                }

                // Only the authoritative server can restore Fusion snapshot
                // objects. Client scene initialization remains proxy-only.
                IsHostMigrationResumeActive = false;
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

                    // A player may connect/disconnect between Fusion's
                    // resume callback and completion of backend takeover.
                    // Reconcile that window using snapshot token identity,
                    // never by issuing a new gameplay spawn in-match.
                    var lifecycleAfterTakeover =
                        migrationRunner.GetComponent<FusionPlayerLifecycle>();
                    if (lifecycleAfterTakeover == null
                        || !lifecycleAfterTakeover.ReconcilePostMigrationRoster(
                            migrationRunner))
                    {
                        throw new InvalidOperationException(
                            "Host Migration late-join/leave reconciliation failed.");
                    }

                    var bindingRebuildDeadline =
                        Time.realtimeSinceStartup + 5f;

                    var bindingsRebuilt = false;

                    while (!(bindingsRebuilt =
                                 _matchAuthority
                                     .RebuildMigratedPlayerBindings(
                                         migrationRunner))
                           && Time.realtimeSinceStartup
                               < bindingRebuildDeadline)
                    {
                        await Task.Delay(50);
                    }

                    if (!bindingsRebuilt)
                    {
                        throw new InvalidOperationException(
                            "Failed to rebuild the migrated Host backend binding before timeout.");
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

                // Bootstrap callbacks were deliberately suppressed during
                // the snapshot copy. Emit only joins whose PlayerObject has
                // been verified/attached; never create objects from events.
                if (migrationRunner.IsServer)
                {
                    var connected = new HashSet<PlayerRef>(migrationRunner.ActivePlayers);
                    foreach (var joined in _migrationDeferredPlayerJoins)
                    {
                        if (!connected.Contains(joined)
                            || !migrationRunner.TryGetPlayerObject(joined, out var attached)
                            || attached == null
                            || !attached.Id.IsValid
                            || !migrationRunner.TryFindObject(attached.Id, out var registeredAttached)
                            || registeredAttached != attached)
                            continue;
                        PlayerJoined?.Invoke(joined);
                        RuntimeLog.Log(RuntimeLogCategory.NetworkSession,
                            $"HM_ADMISSION_JOIN_REPLAYED|player={joined}|object={attached.Id}");
                    }
                }

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
                _hostMigrationResumeCompletion = null;
                _hostMigrationExpectedResumeRunner = null;
                _hostMigrationRetainDisconnectedPlayers = false;
                _hostMigrationAuthoritativeMatchStateId = default;
                _hostMigrationRetainedBackendActors.Clear();
                _migrationDeferredPlayerJoins.Clear();

                _callbacksRegistered =
                    Runner != null
                    && !Runner.IsShutdown;

                _sessionOperationInProgress = false;
                _hostMigrationInProgress = false;
            }
        }

        private void ResumeHostMigrationTracked(NetworkRunner migrationRunner)
        {
            var completion = _hostMigrationResumeCompletion;
            if (completion == null || migrationRunner != _hostMigrationExpectedResumeRunner)
            {
                // A timed-out/old Runner callback must never restore objects
                // into a different or already-shutdown session.
                Debug.LogError("HM_RESUME_LATE_OR_FOREIGN_CALLBACK_IGNORED");
                return;
            }
            if (completion.Task.IsCompleted)
            {
                RuntimeLog.Log(RuntimeLogCategory.NetworkSession,
                    "HM_RESUME_DUPLICATE_CALLBACK_IGNORED");
                return;
            }

            try
            {
                ResumeHostMigration(migrationRunner);
                completion.TrySetResult(true);
                RuntimeLog.Log(RuntimeLogCategory.NetworkSession,
                    "HM_RESUME_CALLBACK_COMPLETE|restore_and_adoption_succeeded");
            }
            catch (Exception exception)
            {
                // The async owner handles cleanup of the new Runner. Do not
                // let a failing Fusion callback masquerade as a good restore.
                completion.TrySetException(exception);
                Debug.LogError($"HM_RESUME_CALLBACK_FAILED|error={exception.Message}");
            }
        }

        private void ResumeHostMigration(
            NetworkRunner migrationRunner)
        {
            // Fusion exposes resume-snapshot access only to the peer that
            // became the authoritative Host. Clients reconnect normally and
            // receive the objects restored by that Host.
            if (migrationRunner == null
                || !migrationRunner.IsServer)
            {
                if (migrationRunner != null)
                {
                    RuntimeLog.Log(
                        RuntimeLogCategory.NetworkSession,
                        "[NetworkSession] Client Host Migration resume skipped; waiting for authoritative replication.");
                }

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

            var resumeObjects =
                new List<NetworkObject>();

            NetworkId canonicalMatchStateId = default;
            var snapshotMatchStateCount = 0;

            foreach (var resumeObject in
                     migrationRunner
                         .GetResumeSnapshotNetworkObjects())
            {
                if (resumeObject == null)
                {
                    continue;
                }

                resumeObjects.Add(
                    resumeObject);

                if (!resumeObject.TryGetBehaviour<NetworkMatchState>(
                        out _))
                {
                    continue;
                }

                snapshotMatchStateCount++;

                if (_hostMigrationAuthoritativeMatchStateId.IsValid
                    && resumeObject.Id ==
                        _hostMigrationAuthoritativeMatchStateId)
                {
                    canonicalMatchStateId =
                        resumeObject.Id;
                }
                else if (!canonicalMatchStateId.IsValid
                         && !_hostMigrationAuthoritativeMatchStateId.IsValid)
                {
                    canonicalMatchStateId =
                        resumeObject.Id;
                }
            }

            if (_hostMigrationRetainDisconnectedPlayers
                && (!canonicalMatchStateId.IsValid
                    || (_hostMigrationAuthoritativeMatchStateId.IsValid
                        && canonicalMatchStateId !=
                            _hostMigrationAuthoritativeMatchStateId)))
            {
                throw new InvalidOperationException(
                    "Host Migration snapshot does not contain the previously authoritative NetworkMatchState.");
            }

            if (_hostMigrationRetainDisconnectedPlayers
                && snapshotMatchStateCount > 1)
            {
                Debug.LogWarning(
                    $"[NetworkSession] Host Migration snapshot contains {snapshotMatchStateCount} NetworkMatchState objects; restoring canonical NetworkId={canonicalMatchStateId} only.");
            }

            var missingSnapshotPlayerObjects = 0;

            foreach (var expectedId in snapshotPlayerObjectIds)
            {
                var found = false;

                foreach (var snapshotObject in resumeObjects)
                {
                    if (snapshotObject != null
                        && snapshotObject.Id == expectedId)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    missingSnapshotPlayerObjects++;

                    Debug.LogError(
                        $"[NetworkSession] HM_PREFLIGHT_MISSING_PLAYER|oldId={expectedId}");
                }
            }

            RuntimeLog.Log(
                RuntimeLogCategory.NetworkSession,
                $"[NetworkSession] HM_PREFLIGHT|scene={SceneManager.GetActiveScene().name}|inMatch={_hostMigrationRetainDisconnectedPlayers}|snapshotObjects={resumeObjects.Count}|snapshotMatchStates={snapshotMatchStateCount}|playerMappings={oldPlayerObjects.Count}|uniquePlayerObjects={snapshotPlayerObjectIds.Count}|activePlayers={activePlayers.Count}|missingPlayers={missingSnapshotPlayerObjects}");

            if (_hostMigrationRetainDisconnectedPlayers
                && snapshotPlayerObjectIds.Count == 0)
            {
                throw new InvalidOperationException(
                    "Host Migration gameplay snapshot contains no PlayerObjects. Refusing to initialize an empty gameplay world.");
            }

            if (snapshotPlayerObjectIds.Count != oldPlayerObjects.Count
                || missingSnapshotPlayerObjects > 0)
            {
                throw new InvalidOperationException(
                    "Host Migration snapshot PlayerObject mapping is inconsistent. Refusing partial restore.");
            }

            foreach (var resumeObject in
                     resumeObjects)
            {
                if (resumeObject.TryGetBehaviour<NetworkMatchState>(
                        out _)
                    && (!_hostMigrationRetainDisconnectedPlayers
                        || resumeObject.Id != canonicalMatchStateId))
                {
                    Debug.LogWarning(
                        $"[NetworkSession] Stale migrated NetworkMatchState snapshot ignored. NetworkId={resumeObject.Id}, Canonical={canonicalMatchStateId}, PreviousState={(_hostMigrationRetainDisconnectedPlayers ? "InMatch" : "Lobby")}.");

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
                            },
                        flags: isSnapshotPlayerObject
                            ? NetworkSpawnFlags.DontDestroyOnLoad
                            : default);

                if (restored != null)
                {
                    restoredObjects[previousId] =
                        restored;

                    if (isSnapshotPlayerObject)
                    {
                        RuntimeLog.Log(
                            RuntimeLogCategory.NetworkSession,
                            $"[NetworkSession] HM_PLAYER_RESTORE|oldId={previousId}|newId={restored.Id}|hasTRSP={hasTransformState}|snapshotPos={position}|spawnPos={restored.transform.position}");
                    }
                }
            }

            foreach (var expectedId in snapshotPlayerObjectIds)
            {
                if (!restoredObjects.TryGetValue(
                        expectedId,
                        out var restoredPlayer)
                    || restoredPlayer == null
                    || !restoredPlayer.IsValid)
                {
                    throw new InvalidOperationException(
                        $"Host Migration failed to spawn snapshot PlayerObject {expectedId}. Refusing partial restore.");
                }
            }

            RuntimeLog.Log(
                RuntimeLogCategory.NetworkSession,
                $"[NetworkSession] HM_POSTFLIGHT|restoredObjects={restoredObjects.Count}|restoredPlayerObjects={snapshotPlayerObjectIds.Count}|result=VALIDATED");

            var mappedPlayerObjectIds =
                new HashSet<NetworkId>();

            bool hasLocalBackendUser =
                Guid.TryParse(
                    AuthSession.CurrentUserId,
                    out var localBackendUserId);

            // PlayerRef values are transport-scoped and can be reassigned
            // when a new Host is elected. Reclaim every active player's
            // snapshot object by its stable connection-token hash before
            // considering the old PlayerRef mapping.
            foreach (var activePlayer in
                     activePlayers)
            {
                var connectionToken =
                    migrationRunner.GetPlayerConnectionToken(
                        activePlayer);

                if (connectionToken == null
                    || connectionToken.Length == 0)
                {
                    continue;
                }

                foreach (var previousPlayerId in
                         snapshotPlayerObjectIds)
                {
                    if (mappedPlayerObjectIds.Contains(
                            previousPlayerId)
                        || !restoredObjects.TryGetValue(
                            previousPlayerId,
                            out var candidate)
                        || candidate == null
                        || !candidate.IsValid
                        || !candidate.TryGetComponent<LobbyPlayerState>(
                            out var candidateState)
                        || !FusionPlayerLifecycle.ReconnectTokenMatches(
                            candidateState,
                            connectionToken))
                    {
                        continue;
                    }

                    migrationRunner.SetPlayerObject(
                        activePlayer,
                        candidate);

                    candidate.AssignInputAuthority(
                        activePlayer);

                    mappedPlayerObjectIds.Add(
                        previousPlayerId);

                    RuntimeLog.Log(
                        RuntimeLogCategory.NetworkSession,
                        $"[NetworkSession] Migrated PlayerObject reconciled by connection token. Player={activePlayer}, Object={candidate.Id}.");

                    break;
                }
            }

            foreach (var pair in
                     oldPlayerObjects)
            {
                if (!activePlayers.Contains(
                        pair.Key)
                    || mappedPlayerObjectIds.Contains(
                        pair.Value)
                    || migrationRunner.TryGetPlayerObject(
                        pair.Key,
                        out var alreadyMappedObject)
                    && alreadyMappedObject != null
                    && alreadyMappedObject.IsValid)
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

                // PlayerRef is transport-scoped and may point to a different
                // person after multiple Host migrations. Never reassign a
                // snapshot identity when the new connection token disagrees.
                var currentConnectionToken =
                    migrationRunner.GetPlayerConnectionToken(pair.Key);

                // PlayerRef is never proof of identity, especially on the
                // second/third Host migration. Missing token/hash must not
                // bypass snapshot identity verification.
                if (currentConnectionToken == null
                    || currentConnectionToken.Length == 0
                    || !restoredPlayer.TryGetComponent<LobbyPlayerState>(
                        out var previousLobbyState)
                    || previousLobbyState.ReconnectTokenHash.Length == 0
                    || !FusionPlayerLifecycle.ReconnectTokenMatches(
                        previousLobbyState,
                        currentConnectionToken))
                {
                    Debug.LogWarning(
                        $"HM_ADMISSION_PLAYERREF_FALLBACK_DENIED|player={pair.Key}|oldObject={pair.Value}|reason=MissingOrMismatchedToken");
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
                    if (retainedLobbyState.HasVerifiedBackendIdentity
                        && Guid.TryParse(
                            retainedLobbyState.BackendUserId.ToString(),
                            out var retainedBackendUserId))
                    {
                        var oldActor = retainedLobbyState.FusionActorNumber;
                        if (_hostMigrationRetainedBackendActors.TryGetValue(
                                oldActor, out var existingUserId)
                            && existingUserId != retainedBackendUserId)
                        {
                            throw new InvalidOperationException(
                                $"Conflicting retained backend identities for actor {oldActor}.");
                        }

                        _hostMigrationRetainedBackendActors[oldActor] =
                            retainedBackendUserId;
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"[NetworkSession] Retained player actor {retainedLobbyState.FusionActorNumber} has no verified backend identity; refusing unsafe disconnect.");
                    }
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
            if (migrationRunner.IsServer)
            {
                var playerSpawner =
                    FindAnyObjectByType<PlayerSpawner>();

                bool sceneReconciled =
                    playerSpawner != null
                    && (SceneManager.GetActiveScene().name ==
                            LobbyManager.GameSceneName
                        ? playerSpawner
                            .AdoptMigratedAuthoritativeWorldState(
                                migrationRunner)
                        : SceneManager.GetActiveScene().name ==
                            LobbySceneName
                            && playerSpawner
                                .RebuildMigratedLobbyPlacementAuthoritative(
                                    migrationRunner));

                if (!sceneReconciled)
                {
                    throw new InvalidOperationException(
                        "Failed to reconcile the migrated authoritative scene.");
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
            if (Runner != runner
                || runner == null
                || !runner.IsRunning
                || _localInputOwner == null
                || !_localInputOwner.IsValid
                || !_localInputOwner.HasInputAuthority
                || _localInputProvider == null)
            {
                return;
            }

            var localInput = _localInputProvider();
            if (MatchLoadingCoordinator.OwnsPresentation)
            {
                // Cover the first tick after scene-unload locks reset while preserving
                // the spawn/camera orientation and existing crouch pose.
                localInput.Move = Vector2.zero;
                localInput.JumpPressed = false;
                localInput.SprintHeld = false;
                localInput.ResonanceHeld = false;
                localInput.CrouchHeld = _localInputOwner.TryGetComponent<NetworkPlayerMovement>(out var movement)
                    && movement.IsCrouching;
            }
            input.Set(localInput);
        }
        void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            if (!runner.IsServer) return;

            // Ignore callbacks from the Runner that is being replaced.
            if (runner != Runner)
            {
                RuntimeLog.Log(RuntimeLogCategory.NetworkSession,
                    "HM_ADMISSION_DENIED|reason=StaleRunner");
                request.Refuse();
                return;
            }

            if (_hostMigrationInProgress || IsHostMigrationResumeActive)
            {
                if (_hostMigrationExpectedResumeRunner != runner)
                {
                    RuntimeLog.Log(RuntimeLogCategory.NetworkSession,
                        "HM_ADMISSION_DENIED|reason=UnexpectedMigrationRunner");
                    request.Refuse();
                    return;
                }
                if (token == null || token.Length == 0)
                {
                    RuntimeLog.Log(RuntimeLogCategory.NetworkSession,
                        "HM_ADMISSION_DENIED|reason=MissingTokenDuringMigration");
                    request.Refuse();
                    return;
                }

                // Fusion can attempt to reconnect peers before the snapshot
                // resume callback. Temporarily admit token-bearing peers;
                // snapshot restoration verifies their identity BEFORE
                // granting any PlayerObject input authority.
                var snapshotReady = _hostMigrationResumeCompletion != null
                    && _hostMigrationResumeCompletion.Task.Status == TaskStatus.RanToCompletion;
                if (_hostMigrationRetainDisconnectedPlayers && snapshotReady)
                {
                    var restoredLifecycle = runner.GetComponent<FusionPlayerLifecycle>();
                    if (restoredLifecycle == null || !restoredLifecycle.CanReconnect(token))
                    {
                        RuntimeLog.Log(RuntimeLogCategory.NetworkSession,
                            "HM_ADMISSION_DENIED|reason=NoMatchingRetainedIdentity");
                        request.Refuse();
                        return;
                    }
                }
                RuntimeLog.Log(RuntimeLogCategory.NetworkSession,
                    snapshotReady ? "HM_ADMISSION_VERIFIED_REJOIN" : "HM_ADMISSION_PROVISIONAL_WAITING_FOR_SNAPSHOT");
                request.Accept();
                return;
            }

            if (_matchStartPending)
            {
                RuntimeLog.Log(
                    RuntimeLogCategory.NetworkSession,
                    "[NetworkSession] Join refused: match start is pending.");
                request.Refuse();
                return;
            }

            if (State != NetworkSessionState.InMatch)
            {
                request.Accept();
                return;
            }

            var lifecycle = runner.GetComponent<FusionPlayerLifecycle>();
            if (lifecycle != null && lifecycle.CanReconnect(token))
            {
                request.Accept();
            }
            else
            {
                RuntimeLog.Log(
                    RuntimeLogCategory.NetworkSession,
                    $"[NetworkSession] InMatch join refused: no disconnected PlayerObject matches the supplied token. " +
                    (lifecycle != null ? lifecycle.GetReconnectAdmissionDiagnostic(token) : "reason=LifecycleMissing") +
                    $"|migrationInProgress={_hostMigrationInProgress}|resumeActive={IsHostMigrationResumeActive}.");
                request.Refuse();
            }
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
            MatchLoadingCoordinator.Instance?.YieldToNetworkRecovery(runner);
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
                ResultSession.Clear();
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
            if (Runner == runner
                && sceneName == EchoProtocol.Core.GameConstants.SceneResult
                && State == NetworkSessionState.InMatch)
            {
                PlayerInteractionControlLock.ReleaseAll();
                ClearLocalInputProvider();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (Runner == runner && LobbyMapCatalog.IsGameplayScene(sceneName)
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
            ResultSession.Capture(NetworkMatchState.Instance);
            RuntimeLog.Log(
                RuntimeLogCategory.NetworkSession,
                "[NetworkSession] Network scene load started.");
        }
        void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data)
            => MatchLoadingCoordinator.Instance?.ReceiveClientMessage(runner, player, key, data);
        void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    }
}
