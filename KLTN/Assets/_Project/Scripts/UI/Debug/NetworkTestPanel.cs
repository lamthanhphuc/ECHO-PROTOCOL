using System;
using EchoProtocol.Networking;
using Fusion;
using UnityEngine;

namespace EchoProtocol.UI.Debugging
{
    /// <summary>
    /// Runtime-only controls for manually verifying Host Mode sessions.
    /// </summary>
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

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            if (_bootstrap == null)
            {
                _bootstrap = FindAnyObjectByType<NetworkBootstrap>();
            }

            if (_lobbyManager == null)
            {
                _lobbyManager = GetComponent<LobbyManager>();
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

        private void OnDestroy()
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
            _windowRect = GUI.Window(WindowId, _windowRect, DrawWindow, "Fusion Network Test");
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.Space(6);
            GUILayout.Label("Session name");
            _sessionName = GUILayout.TextField(_sessionName, 32);

            GUILayout.Space(8);
            using (new GUIEnabledScope(!_isBusy && !IsConnected))
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Create Room", GUILayout.Height(34)))
                {
                    StartHost();
                }

                if (GUILayout.Button("Join Room", GUILayout.Height(34)))
                {
                    JoinSession();
                }
                GUILayout.EndHorizontal();
            }

            using (new GUIEnabledScope(!_isBusy && _bootstrap != null && _bootstrap.Runner != null))
            {
                DrawSelectionControls();

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
            _status = message;
        }

        private void OnSelectionRequestCompleted(LobbySelectionResult result)
        {
            _status = result.Accepted
                ? $"{result.Kind} {result.RequestedId} selected"
                : $"{result.Kind} {result.RequestedId} rejected: {result.Error}";
        }

        private void DrawSelectionControls()
        {
            if (_lobbyManager == null || !_lobbyManager.TryGetLocalPlayerState(out var playerState, false)) return;

            var localMember = GetLocalMember();
            var selectedTeam = localMember?.TeamId ?? 0;
            var selectedTool = localMember?.ToolId ?? 0;

            GUILayout.Space(8);
            GUILayout.Label("Team");
            GUILayout.BeginHorizontal();
            for (var teamId = 0; teamId <= playerState.TeamCount; teamId++)
            {
                var capturedId = teamId;
                var label = teamId == 0 ? "None" : $"Team {teamId}";
                if (selectedTeam == teamId) label = $"[{label}]";
                if (GUILayout.Button(label)) _lobbyManager.RequestTeam(capturedId);
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("Tool");
            if (GUILayout.Button(selectedTool == 0 ? "[None]" : "None")) _lobbyManager.RequestTool(0);
            foreach (var tool in playerState.ToolDefinitions)
            {
                if (tool == null) continue;
                var claimedByOther = tool.IsUnique && IsToolClaimedByOther(tool.Id);
                var label = tool.DisplayName;
                if (selectedTool == tool.Id) label = $"[{label}]";
                else if (claimedByOther) label = $"{label} (Taken)";

                using (new GUIEnabledScope(!claimedByOther))
                {
                    if (GUILayout.Button(label)) _lobbyManager.RequestTool(tool.Id);
                }
            }
        }

        private LobbyMemberViewModel GetLocalMember()
        {
            if (_lobbyState.Members == null) return null;
            return _lobbyState.Members.Find(member => member.IsLocal);
        }

        private bool IsToolClaimedByOther(int toolId)
        {
            return _lobbyState.Members != null
                && _lobbyState.Members.Exists(member => !member.IsLocal && member.ToolId == toolId);
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
                    $"T:{member.TeamId} Tool:{member.ToolId} {readyLabel}{localMarker}");
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
                GUI.enabled = enabled;
            }

            public void Dispose()
            {
                GUI.enabled = _wasEnabled;
            }
        }
    }
}
