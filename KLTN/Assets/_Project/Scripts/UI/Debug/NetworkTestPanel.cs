using System;
using EchoProtocol.Networking;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.UI.Debugging
{
    /// <summary>
    /// Host Mode test controls with a read-only preview in the Lobby Game view.
    /// </summary>
    [ExecuteAlways]
    public sealed class NetworkTestPanel : MonoBehaviour
    {
        private const int WindowWidth = 360;
        private const int WindowHeight = 720;
        private const int WindowId = 0x4543484F;

        [SerializeField] private NetworkBootstrap _bootstrap;
        [SerializeField] private LobbyManager _lobbyManager;
        [SerializeField] private string _sessionName = "echo-test";
        [SerializeField, Min(1)] private int _maxPlayers = 4;

        private static NetworkTestPanel _instance;
        private bool _isBusy;
        private string _status = "Disconnected";
        private RoomInfoViewModel _lobbyState = new RoomInfoViewModel();
        private Rect _windowRect = new Rect(20, 20, WindowWidth, WindowHeight);

        private void OnEnable()
        {
            // Edit mode only draws the preview; never initialize networking or
            // change object lifetime (including when editing a prefab).
            if (!Application.IsPlaying(gameObject)) return;

            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            // Keep legacy Bootstrap scenes working until the editor migration is run.
            // A panel authored in Lobby belongs to that scene and is unloaded with it.
            if (gameObject.scene.name != NetworkBootstrap.LobbySceneName)
            {
                DontDestroyOnLoad(gameObject);
            }

            if (_bootstrap == null)
            {
                _bootstrap = FindAnyObjectByType<NetworkBootstrap>();
            }

            if (_lobbyManager == null)
            {
                _lobbyManager = FindAnyObjectByType<LobbyManager>();
            }

            if (_bootstrap != null)
            {
                _bootstrap.SessionStateChanged += OnSessionStateChanged;
                _bootstrap.PlayerJoined += OnPlayerChanged;
                _bootstrap.PlayerLeft += OnPlayerChanged;
            }

            if (_lobbyManager != null)
            {
                _lobbyManager.OnRoomUpdated += OnRoomUpdated;
                _lobbyManager.OnLobbyError += OnLobbyError;
                _lobbyManager.OnSelectionRequestCompleted += OnSelectionRequestCompleted;
                _lobbyState = _lobbyManager.CurrentState;
            }
        }

        private void OnDisable()
        {
            if (_bootstrap != null)
            {
                _bootstrap.SessionStateChanged -= OnSessionStateChanged;
                _bootstrap.PlayerJoined -= OnPlayerChanged;
                _bootstrap.PlayerLeft -= OnPlayerChanged;
            }

            if (_lobbyManager != null)
            {
                _lobbyManager.OnRoomUpdated -= OnRoomUpdated;
                _lobbyManager.OnLobbyError -= OnLobbyError;
                _lobbyManager.OnSelectionRequestCompleted -= OnSelectionRequestCompleted;
            }

            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnGUI()
        {
            if (SceneManager.GetActiveScene().name != NetworkBootstrap.LobbySceneName)
            {
                return;
            }

            _windowRect = GUI.Window(
                WindowId,
                _windowRect,
                DrawWindow,
                "Fusion Network Test");
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.Space(6);
            GUILayout.Label("Session name");
            var sessionName = GUILayout.TextField(_sessionName, 32);
            if (Application.IsPlaying(gameObject)) _sessionName = sessionName;

            GUILayout.Space(8);
            using (new GUIEnabledScope(!_isBusy && !IsConnected))
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Create Room", GUILayout.Height(34)) && Application.IsPlaying(gameObject))
                {
                    StartHost();
                }

                if (GUILayout.Button("Join Room", GUILayout.Height(34)) && Application.IsPlaying(gameObject))
                {
                    JoinSession();
                }
                GUILayout.EndHorizontal();
            }

            using (new GUIEnabledScope(Application.IsPlaying(gameObject) && !_isBusy && _bootstrap != null && _bootstrap.Runner != null))
            {
                if (GUILayout.Button(_lobbyState.IsReady ? "Set Not Ready" : "Set Ready", GUILayout.Height(30)))
                {
                    _lobbyManager?.SetReady(!_lobbyState.IsReady);
                }

                if (_lobbyState.IsHost)
                {
                    using (new GUIEnabledScope(_lobbyState.CanStartMatch))
                    {
                        if (GUILayout.Button("Start Match", GUILayout.Height(30)))
                        {
                            _lobbyManager?.TryStartMatch();
                        }
                    }
                }

                if (GUILayout.Button("Leave Room", GUILayout.Height(30)))
                {
                    ShutdownSession();
                }
            }

            GUILayout.Space(8);
            GUILayout.Label($"Status: {GetStatusText()}");
            DrawMemberList();
            GUI.DragWindow(new Rect(0, 0, WindowWidth, 24));
        }

        private bool IsConnected => _bootstrap != null && _bootstrap.HasRunningRunner;

        private async void StartHost()
        {
            EchoProtocol.Audio.GameAudioRuntime.UI("ui/click");
            if (!ValidateSessionName())
            {
                return;
            }

            _isBusy = true;
            _status = "Starting host...";

            try
            {
                var started = await _bootstrap.CreateRoomAsync(_sessionName.Trim(), _maxPlayers);
                if (!started) _status = _bootstrap.LastError;
                EchoProtocol.Audio.GameAudioRuntime.UI(started ? "ui/confirm" : "ui/error");
            }
            catch (Exception exception)
            {
                _status = "Host failed - check Console";
                UnityEngine.Debug.LogError($"[NetworkTestPanel] Host failed: {exception}");
            }
            finally
            {
                _isBusy = false;
            }
        }

        private async void JoinSession()
        {
            EchoProtocol.Audio.GameAudioRuntime.UI("ui/click");
            if (!ValidateSessionName())
            {
                return;
            }

            _isBusy = true;
            _status = "Joining...";

            try
            {
                var joined = await _bootstrap.JoinRoomAsync(_sessionName.Trim());
                if (!joined) _status = _bootstrap.LastError;
                EchoProtocol.Audio.GameAudioRuntime.UI(joined ? "ui/confirm" : "ui/error");
            }
            catch (Exception exception)
            {
                _status = "Join failed - check Console";
                UnityEngine.Debug.LogError($"[NetworkTestPanel] Join failed: {exception}");
            }
            finally
            {
                _isBusy = false;
            }
        }

        private async void ShutdownSession()
        {
            _isBusy = true;
            _status = "Shutting down...";

            try
            {
                await _bootstrap.Shutdown();
                _status = "Disconnected";
            }
            finally
            {
                _isBusy = false;
            }
        }

        private bool ValidateSessionName()
        {
            if (_bootstrap == null)
            {
                _status = "NetworkBootstrap not found";
                UnityEngine.Debug.LogError("[NetworkTestPanel] NetworkBootstrap was not found.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_sessionName))
            {
                _status = "Enter a session name";
                return false;
            }

            return true;
        }

        private string GetStatusText()
        {
            if (_isBusy || !IsConnected)
            {
                return _status;
            }

            var runner = _bootstrap.Runner;
            var mode = runner.IsServer ? "Host" : "Client";
            var session = runner.SessionInfo.IsValid ? runner.SessionInfo.Name : _sessionName;
            return $"{mode} / {session} / {CountPlayers(runner)} player(s)";
        }

        private void OnSessionStateChanged(NetworkSessionState state, string message)
        {
            _status = $"{state}: {message}";
        }

        private void OnPlayerChanged(PlayerRef player)
        {
            if (_bootstrap != null && _bootstrap.Runner != null)
            {
                _status = $"{_bootstrap.State}: {CountPlayers(_bootstrap.Runner)} player(s)";
            }
        }

        private void OnRoomUpdated(RoomInfoViewModel state)
        {
            _lobbyState = state ?? new RoomInfoViewModel();
        }

        private void OnLobbyError(string message)
        {
            EchoProtocol.Audio.GameAudioRuntime.UI("ui/error");
            _status = message;
        }

        private void OnSelectionRequestCompleted(LobbySelectionResult result)
        {
            _status = result.Accepted
                ? $"{result.Kind} {result.RequestedId} selected"
                : $"{result.Kind} {result.RequestedId} rejected: {result.Error}";
        }

        private void DrawMemberList()
        {
            GUILayout.Space(10);
            GUILayout.Label($"Lobby Members ({_lobbyState.CurrentPlayers}/{_lobbyState.MaxPlayers})");

            if (_lobbyState.Members == null || _lobbyState.Members.Count == 0)
            {
                GUILayout.Label("No connected members");
                return;
            }

            foreach (var member in _lobbyState.Members)
            {
                var localMarker = member.IsLocal ? (_lobbyState.IsHost ? " (You, Host)" : " (You)") : string.Empty;
                var readyLabel = member.IsReady ? "READY" : "NOT READY";
                GUILayout.Label(
                    $"- {member.DisplayName} [PlayerRef {member.PlayerRef.RawEncoded}] " +
                    $"Tool:{member.ToolId} {readyLabel}{localMarker}");
            }
        }

        private static int CountPlayers(NetworkRunner runner)
        {
            var count = 0;
            foreach (var _ in runner.ActivePlayers)
            {
                count++;
            }

            return count;
        }

        private readonly struct GUIEnabledScope : IDisposable
        {
            private readonly bool _wasEnabled;

            public GUIEnabledScope(bool enabled)
            {
                _wasEnabled = GUI.enabled;
                GUI.enabled = _wasEnabled && enabled;
            }

            public void Dispose()
            {
                GUI.enabled = _wasEnabled;
            }
        }
    }
}
