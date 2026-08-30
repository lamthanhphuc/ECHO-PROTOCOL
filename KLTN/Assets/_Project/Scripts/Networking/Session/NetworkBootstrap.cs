using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.Networking
{
    public enum NetworkSessionState { Disconnected, Connecting, InLobby, ShuttingDown, Failed }

    /// <summary>Single owner of the Photon Fusion runner and session lifecycle.</summary>
    public sealed class NetworkBootstrap : MonoBehaviour, INetworkRunnerCallbacks
    {
        public const string RunnerObjectName = "EchoNetworkRunner";
        public const string LobbySceneName = "Lobby";
        public const string BootstrapSceneName = "Bootstrap";

        [SerializeField] private NetworkRunner _runnerPrefab;
        [SerializeField] private string _hostSessionName = "EchoProtocol";
        [SerializeField, Min(2)] private int _maxPlayers = 4;

        private static NetworkBootstrap _instance;
        private bool _sessionOperationInProgress;
        private bool _callbacksRegistered;
        private NetworkObject _localInputOwner;
        private Func<NetworkPlayerInput> _localInputProvider;

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
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            UnregisterCallbacks();
        }

        public Task<bool> StartHost() => CreateRoomAsync(_hostSessionName, _maxPlayers);
        internal Task<bool> StartHost(string sessionName, int maxPlayers) => CreateRoomAsync(sessionName, maxPlayers);
        public Task<bool> JoinGame(string sessionName) => JoinRoomAsync(sessionName);

        public Task<bool> CreateRoomAsync(string sessionName, int maxPlayers = 4)
        {
            return maxPlayers < 2
                ? FailWithoutStarting("A room must allow at least 2 players.")
                : StartSessionAsync(GameMode.Host, sessionName, maxPlayers);
        }

        public Task<bool> JoinRoomAsync(string sessionName) => StartSessionAsync(GameMode.Client, sessionName, null);

        public async Task Shutdown()
        {
            ClearLocalInputProvider();
            if (Runner == null)
            {
                CurrentSessionName = string.Empty;
                SetState(NetworkSessionState.Disconnected, "Disconnected");
                return;
            }

            SetState(NetworkSessionState.ShuttingDown, "Leaving room...");
            var runner = Runner;
            Runner = null;
            _callbacksRegistered = false;

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
                SetState(NetworkSessionState.Disconnected, "Disconnected");
                ReturnToBootstrapScene();
            }
        }

        public Task ShutdownRunnerAsync() => Shutdown();

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
            if (Runner != null && !Runner.IsShutdown)
            {
                RegisterCallbacks();
                return Runner;
            }

            foreach (var existing in FindObjectsByType<NetworkRunner>(FindObjectsInactive.Include))
            {
                if (existing != null && !existing.IsShutdown)
                {
                    Runner = existing;
                    break;
                }
            }

            if (Runner == null)
            {
                Runner = _runnerPrefab != null
                    ? Instantiate(_runnerPrefab)
                    : new GameObject(RunnerObjectName).AddComponent<NetworkRunner>();
            }

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
            if (_sessionOperationInProgress) return await FailWithoutStarting("A create/join operation is already in progress.");
            if (HasRunningRunner) return await FailWithoutStarting($"Already connected to room '{CurrentSessionName}'.");

            _sessionOperationInProgress = true;
            LastError = string.Empty;
            SetState(NetworkSessionState.Connecting,
                gameMode == GameMode.Host ? $"Creating room '{normalizedName}'..." : $"Joining room '{normalizedName}'...");

            var runner = EnsureRunner();
            try
            {
                var args = new StartGameArgs
                {
                    GameMode = gameMode,
                    SessionName = normalizedName,
                    Scene = BuildSceneInfo(),
                    SceneManager = runner.GetComponent<INetworkSceneManager>(),
                    ObjectProvider = runner.GetComponent<INetworkObjectProvider>(),
                };
                if (playerCount.HasValue) args.PlayerCount = playerCount.Value;

                Debug.Log($"[NetworkSession] Starting {gameMode} for room '{normalizedName}'.");
                var result = await runner.StartGame(args);
                if (!result.Ok)
                {
                    await HandleStartFailureAsync(
                        $"Could not {(gameMode == GameMode.Host ? "create" : "join")} room '{normalizedName}': " +
                        $"{result.ShutdownReason}. {result.ErrorMessage}".Trim());
                    return false;
                }

                CurrentSessionName = normalizedName;
                SetState(NetworkSessionState.InLobby,
                    $"{(gameMode == GameMode.Host ? "Created" : "Joined")} room '{normalizedName}'.");
                Debug.Log($"[NetworkSession] Connected. Mode={gameMode}, Room='{normalizedName}', LocalPlayer={runner.LocalPlayer}.");

                if (runner.IsSceneAuthority && SceneManager.GetActiveScene().name != LobbySceneName)
                {
                    Debug.Log($"[NetworkSession] Loading network scene '{LobbySceneName}'.");
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
            ClearLocalInputProvider();
            LastError = message;
            Debug.LogError($"[NetworkSession] {message}");
            var failedRunner = Runner;
            Runner = null;
            _callbacksRegistered = false;

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
            Debug.Log($"[NetworkSession] State={state}. {message}");
            SessionStateChanged?.Invoke(state, message);
        }

        private void RegisterCallbacks()
        {
            if (Runner == null || _callbacksRegistered) return;
            Runner.AddCallbacks(this);
            _callbacksRegistered = true;
        }

        private void UnregisterCallbacks()
        {
            if (Runner != null && _callbacksRegistered) Runner.RemoveCallbacks(this);
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
            Debug.Log($"[NetworkSession] Player joined: {player}. Players={CountPlayers(runner)}.");
            PlayerJoined?.Invoke(player);
        }

        void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[NetworkSession] Player left: {player}. Players={CountPlayers(runner)}.");
            PlayerLeft?.Invoke(player);
        }

        void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason reason)
        {
            Debug.Log($"[NetworkSession] Runner shutdown: {reason}.");
            if (Runner == runner && State != NetworkSessionState.ShuttingDown)
            {
                CleanupUnexpectedTermination(runner, $"Session ended: {reason}");
            }
        }

        void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) => Debug.Log("[NetworkSession] Connected to Photon server.");

        void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            LastError = $"Disconnected from server: {reason}";
            Debug.LogWarning($"[NetworkSession] {LastError}");
            if (Runner == runner) CleanupUnexpectedTermination(runner, LastError);
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

        private void CleanupUnexpectedTermination(NetworkRunner runner, string message)
        {
            ClearLocalInputProvider();
            Runner = null;
            _callbacksRegistered = false;
            _sessionOperationInProgress = false;
            CurrentSessionName = string.Empty;
            LastError = message;
            SetState(NetworkSessionState.Failed, message);
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

        void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input)
        {
            if (_localInputProvider != null) input.Set(_localInputProvider());
        }
        void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner)
        {
            Debug.Log($"[NetworkSession] Scene load complete: {SceneManager.GetActiveScene().name}.");
            NetworkSceneLoadDone?.Invoke(runner);
        }
        void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) => Debug.Log("[NetworkSession] Network scene load started.");
        void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
        void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    }
}
