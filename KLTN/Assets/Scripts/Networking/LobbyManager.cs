using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.Networking
{
    /// <summary>
    /// Host-mode lobby/session operations backed by Photon Fusion <see cref="NetworkRunner"/>.
    /// </summary>
    public class LobbyManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        public const string PlayerReadyPropertyKey = "echo_ready";

        [SerializeField] private NetworkBootstrap _bootstrap;

        private bool _localReady;
        private string _currentRoomName;

        public event Action<RoomInfoViewModel> OnRoomUpdated;
        public event Action<string> OnLobbyError;

        public bool IsInRoom => _bootstrap != null && _bootstrap.HasRunningRunner;

        private void Awake()
        {
            if (_bootstrap == null)
            {
                _bootstrap = FindAnyObjectByType<NetworkBootstrap>();
            }
        }

        private void OnDestroy()
        {
            if (_bootstrap?.Runner != null)
            {
                _bootstrap.Runner.RemoveCallbacks(this);
            }
        }

        public async void CreateRoom(string roomName, int maxPlayers = 4)
        {
            if (!ValidateRoomName(roomName))
            {
                return;
            }

            if (maxPlayers < 2)
            {
                ReportError("Room must allow at least 2 players.");
                return;
            }

            await StartSessionAsync(GameMode.Host, roomName, maxPlayers);
        }

        public async void JoinRoom(string roomName)
        {
            if (!ValidateRoomName(roomName))
            {
                return;
            }

            await StartSessionAsync(GameMode.Client, roomName, playerCount: null);
        }

        public async void LeaveRoom()
        {
            _localReady = false;
            _currentRoomName = null;
            await _bootstrap.ShutdownRunnerAsync();
            NotifyRoomUpdated();
        }

        public void SetReady(bool isReady)
        {
            if (_bootstrap == null || !_bootstrap.HasRunningRunner)
            {
                ReportError("Cannot set ready — not connected to a room.");
                return;
            }

            _localReady = isReady;
            PublishLocalReadyState(isReady);
            NotifyRoomUpdated();
        }

        private async Task StartSessionAsync(GameMode mode, string roomName, int? playerCount)
        {
            if (_bootstrap == null)
            {
                ReportError("NetworkBootstrap is not assigned.");
                return;
            }

            try
            {
                var runner = _bootstrap.EnsureRunner();
                runner.AddCallbacks(this);

                if (runner.IsRunning)
                {
                    ReportError("Already connected to a session. Leave the current room first.");
                    return;
                }

                var sceneManager = runner.GetComponent<INetworkSceneManager>();
                var objectProvider = runner.GetComponent<INetworkObjectProvider>();
                var sceneInfo = BuildSceneInfo();

                var args = new StartGameArgs
                {
                    GameMode = mode,
                    SessionName = roomName,
                    Scene = sceneInfo,
                    SceneManager = sceneManager,
                    ObjectProvider = objectProvider,
                    OnGameStarted = OnGameStarted,
                };

                if (playerCount.HasValue)
                {
                    args.PlayerCount = playerCount.Value;
                }

                var result = await runner.StartGame(args);
                if (!result.Ok)
                {
                    var action = mode == GameMode.Host ? "create" : "join";
                    ReportError($"Failed to {action} room '{roomName}': {result.ShutdownReason}");
                    return;
                }

                _currentRoomName = roomName;
                _localReady = false;
                NotifyRoomUpdated();
            }
            catch (Exception ex)
            {
                ReportError(ex.Message);
            }
        }

        private static NetworkSceneInfo BuildSceneInfo()
        {
            var sceneInfo = new NetworkSceneInfo();
            var activeScene = SceneManager.GetActiveScene();

            if (!activeScene.IsValid()
                || activeScene.buildIndex < 0
                || activeScene.buildIndex >= SceneManager.sceneCountInBuildSettings)
            {
                return sceneInfo;
            }

            sceneInfo.AddSceneRef(SceneRef.FromIndex(activeScene.buildIndex), LoadSceneMode.Single);
            return sceneInfo;
        }

        private void OnGameStarted(NetworkRunner runner)
        {
            NotifyRoomUpdated();
        }

        private void PublishLocalReadyState(bool isReady)
        {
            var runner = _bootstrap.Runner;
            if (runner?.SessionInfo == null || !runner.SessionInfo.IsValid)
            {
                return;
            }

            var actorId = runner.GetPlayerActorId(runner.LocalPlayer);
            var propertyKey = $"{PlayerReadyPropertyKey}_{actorId}";
            var properties = new Dictionary<string, SessionProperty>
            {
                { propertyKey, isReady },
            };

            runner.SessionInfo.UpdateCustomProperties(properties);
        }

        private void NotifyRoomUpdated()
        {
            OnRoomUpdated?.Invoke(BuildRoomInfo());
        }

        private RoomInfoViewModel BuildRoomInfo()
        {
            var runner = _bootstrap?.Runner;
            var isRunning = runner != null && runner.IsRunning;

            if (!isRunning)
            {
                return new RoomInfoViewModel
                {
                    RoomName = string.Empty,
                    MaxPlayers = 0,
                    CurrentPlayers = 0,
                    IsHost = false,
                    IsReady = false,
                };
            }

            var session = runner.SessionInfo;
            return new RoomInfoViewModel
            {
                RoomName = string.IsNullOrEmpty(_currentRoomName) ? session.Name : _currentRoomName,
                MaxPlayers = session.IsValid ? session.MaxPlayers : 0,
                CurrentPlayers = runner.ActivePlayers != null ? GetActivePlayerCount(runner) : 0,
                IsHost = runner.IsServer,
                IsReady = _localReady,
            };
        }

        private static int GetActivePlayerCount(NetworkRunner runner)
        {
            var count = 0;
            foreach (var _ in runner.ActivePlayers)
            {
                count++;
            }

            return count;
        }

        private bool ValidateRoomName(string roomName)
        {
            if (string.IsNullOrWhiteSpace(roomName))
            {
                ReportError("Room name is required.");
                return false;
            }

            return true;
        }

        private void ReportError(string message)
        {
            Debug.LogWarning($"[LobbyManager] {message}");
            OnLobbyError?.Invoke(message);
        }

        void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player) => NotifyRoomUpdated();

        void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player) => NotifyRoomUpdated();

        void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            _localReady = false;
            _currentRoomName = null;
            NotifyRoomUpdated();
        }

        void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) => NotifyRoomUpdated();

        void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            _localReady = false;
            _currentRoomName = null;
            NotifyRoomUpdated();
        }

        void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            ReportError($"Connection failed: {reason}");
        }

        void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input) { }

        void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

        void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

        void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

        void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

        void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

        void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner) { }

        void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) { }

        void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }

        void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    }
}
