using System;
using EchoProtocol.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using EchoProtocol.Networking.Authority;
using UnityEngine;

namespace EchoProtocol.Networking
{
    /// <summary>
    /// Projects Fusion's replicated ActivePlayers collection into lobby UI state.
    /// The projection is never used as the authoritative member store.
    /// </summary>
    public sealed class LobbyManager : MonoBehaviour
    {
        public const string GameSceneName = "SciFi";

        [SerializeField] private NetworkBootstrap _bootstrap;
        private bool _matchStartInProgress;
        private string _localOperatorName = string.Empty;
        private LobbyPlayerState _namedPlayer;

        public string LocalOperatorName => _localOperatorName;

        public bool SetLocalOperatorName(string name)
        {
            var normalized = LobbyPlayerState.NormalizeOperatorName(name);
            if (normalized.Length == 0) return false;
            _localOperatorName = normalized;
            _namedPlayer = null;
            return true;
        }

        private void Update()
        {
            // Player objects can spawn after StartGame completes. Submit once per owned object.
            if (_localOperatorName.Length == 0 || !IsInRoom) return;
            if (!TryGetLocalPlayerState(out var player, false) || player == _namedPlayer) return;
            if (player.RequestOperatorName(_localOperatorName)) _namedPlayer = player;
        }

        public event Action<RoomInfoViewModel> OnRoomUpdated;
        public event Action<string> OnLobbyError;
        public event Action<LobbySelectionResult> OnSelectionRequestCompleted;

        public RoomInfoViewModel CurrentState { get; private set; } = new RoomInfoViewModel();
        public bool IsInRoom => _bootstrap != null && _bootstrap.HasRunningRunner;

        private void Awake()
        {
            if (_bootstrap == null) _bootstrap = FindAnyObjectByType<NetworkBootstrap>();
        }

        private void OnEnable()
        {
            if (_bootstrap == null) _bootstrap = FindAnyObjectByType<NetworkBootstrap>();
            if (_bootstrap == null) return;

            _bootstrap.PlayerJoined += HandlePlayerChanged;
            _bootstrap.PlayerLeft += HandlePlayerChanged;
            _bootstrap.SessionStateChanged += HandleSessionStateChanged;
            LobbyPlayerState.AnyStateChanged += RefreshFromRunner;
            LobbyPlayerState.LocalSelectionRequestCompleted += HandleSelectionResult;
            RefreshFromRunner();
        }

        private void OnDisable()
        {
            if (_bootstrap == null) return;
            _bootstrap.PlayerJoined -= HandlePlayerChanged;
            _bootstrap.PlayerLeft -= HandlePlayerChanged;
            _bootstrap.SessionStateChanged -= HandleSessionStateChanged;
            LobbyPlayerState.AnyStateChanged -= RefreshFromRunner;
            LobbyPlayerState.LocalSelectionRequestCompleted -= HandleSelectionResult;
        }

        public async void CreateRoom(string roomName, int maxPlayers = 4)
        {
            if (!ValidateRoomName(roomName)) return;
            var created = await _bootstrap.CreateRoomAsync(roomName, maxPlayers);
            if (!created) ReportError(_bootstrap.LastError);
        }

        public async void JoinRoom(string roomName)
        {
            if (!ValidateRoomName(roomName)) return;
            var joined = await _bootstrap.JoinRoomAsync(roomName);
            if (!joined) ReportError(_bootstrap.LastError);
        }

        public async void LeaveRoom()
        {
            if (_bootstrap == null || _bootstrap.IsBusy) return;
            await _bootstrap.ShutdownRunnerAsync();
            RefreshFromRunner();
        }

        public void SetReady(bool isReady)
        {
            if (!IsInRoom)
            {
                ReportError("Cannot set ready - not connected to a room.");
                return;
            }

            var runner = _bootstrap.Runner;
            if (!runner.TryGetPlayerObject(runner.LocalPlayer, out var playerObject)
                || !playerObject.TryGetComponent<LobbyPlayerState>(out var playerState))
            {
                ReportError("Local lobby player state is not available yet.");
                return;
            }

            if (!playerState.RequestReady(isReady))
            {
                ReportError("Ready request rejected: local player does not own this network object.");
            }
        }

        public bool RequestTeam(int teamId)
        {
            if (!TryGetLocalPlayerState(out var state)) return false;
            return state.RequestTeam(teamId);
        }

        public bool RequestTool(int toolId)
        {
            if (!TryGetLocalPlayerState(out var state)) return false;
            return state.RequestTool(toolId);
        }

        public bool TryGetLocalPlayerState(out LobbyPlayerState state, bool reportError = true)
        {
            state = null;
            var runner = _bootstrap?.Runner;
            if (runner == null || !runner.IsRunning
                || !runner.TryGetPlayerObject(runner.LocalPlayer, out var playerObject)
                || !playerObject.TryGetComponent(out state))
            {
                if (reportError) ReportError("Local lobby player state is not available yet.");
                return false;
            }
            return true;
        }

        public bool TryStartMatch()
        {
            if (_matchStartInProgress)
            {
                Debug.LogWarning("[LobbyManager] Start match ignored: a match start is already in progress.");
                return false;
            }

            if (_bootstrap == null || _bootstrap.State != NetworkSessionState.InLobby)
            {
                ReportError("Start rejected: session is not in the lobby.");
                return false;
            }

            var runner = _bootstrap.Runner;
            if (runner == null || !runner.IsRunning || !runner.IsServer || !runner.IsSceneAuthority)
            {
                ReportError("Only the authoritative host can start the match.");
                return false;
            }

            var state = BuildStateFromRunner();
            if (!state.CanStartMatch)
            {
                ReportError("Start rejected: at least 1 player is required and every player must be ready.");
                return false;
            }

            _matchStartInProgress = true;

            RuntimeLog.Log(
                RuntimeLogCategory.Lobby,
                $"[LobbyManager] Host validated {state.CurrentPlayers} ready players. Confirming backend authority.");

            _ = StartMatchAsync(runner);
            return true;
        }

        private async Task StartMatchAsync(NetworkRunner runner)
        {
            try
            {
                var (accepted, error) = await MatchAuthorityRuntime.EnsureExists(_bootstrap).StartMatchAsync();
                if (!accepted)
                {
                    ReportError($"Backend rejected match start: {error}");
                    return;
                }

                if (runner == null || runner != _bootstrap?.Runner || !runner.IsRunning
                    || !runner.IsServer || !runner.IsSceneAuthority
                    || _bootstrap.State != NetworkSessionState.InLobby)
                {
                    ReportError("Match start aborted: authoritative runner is no longer available.");
                    if (_bootstrap != null) await _bootstrap.ShutdownRunnerAsync();
                    return;
                }

                if (!_bootstrap.CloseRoomForMatchStart())
                {
                    ReportError("Could not close the Fusion room before match start.");
                    await _bootstrap.ShutdownRunnerAsync();
                    return;
                }

                RuntimeLog.Log(
                RuntimeLogCategory.Lobby,
                $"[LobbyManager] Backend confirmed match. Loading '{GameSceneName}'.");
                await runner.LoadScene(
                    GameSceneName,
                    UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                ReportError($"Match scene load failed: {exception.Message}");
                Debug.LogException(exception);
                if (_bootstrap != null) await _bootstrap.ShutdownRunnerAsync();
            }
            finally
            {
                _matchStartInProgress = false;
            }
        }

        /// <summary>Rebuilds the display snapshot exclusively from Fusion ActivePlayers.</summary>
        public void RefreshFromRunner()
        {
            CurrentState = BuildStateFromRunner();
            OnRoomUpdated?.Invoke(CurrentState);
        }

        private RoomInfoViewModel BuildStateFromRunner()
        {
            var runner = _bootstrap?.Runner;
            if (runner == null || !runner.IsRunning)
            {
                return new RoomInfoViewModel();
            }

            var members = new List<LobbyMemberViewModel>();
            var localReady = false;
            foreach (var player in runner.ActivePlayers)
            {
                var isReady = false;
                var teamId = 0;
                var toolId = 0;
                var operatorName = string.Empty;
                if (runner.TryGetPlayerObject(player, out var playerObject)
                    && playerObject.TryGetComponent<LobbyPlayerState>(out var playerState))
                {
                    isReady = playerState.IsReady;
                    teamId = playerState.TeamId;
                    toolId = playerState.ToolId;
                    operatorName = playerState.OperatorName.ToString();
                }

                var isLocal = player == runner.LocalPlayer;
                if (isLocal) localReady = isReady;
                members.Add(new LobbyMemberViewModel
                {
                    PlayerRef = player,
                    ActorId = runner.GetPlayerActorId(player) ?? player.PlayerId,
                    IsLocal = isLocal,
                    IsReady = isReady,
                    TeamId = teamId,
                    ToolId = toolId,
                    OperatorName = operatorName,
                });
            }
            members.Sort((left, right) => left.ActorId.CompareTo(right.ActorId));

            var session = runner.SessionInfo;
            return new RoomInfoViewModel
            {
                RoomName = session.IsValid ? session.Name : _bootstrap.CurrentSessionName,
                MaxPlayers = session.IsValid ? session.MaxPlayers : 0,
                CurrentPlayers = members.Count,
                IsHost = runner.IsServer,
                IsReady = localReady,
                CanStartMatch = runner.IsServer && members.Count >= 1 && members.TrueForAll(member => member.IsReady),
                Members = members,
            };
        }

        private void HandlePlayerChanged(PlayerRef player) => RefreshFromRunner();

        private void HandleSelectionResult(LobbySelectionResult result)
        {
            OnSelectionRequestCompleted?.Invoke(result);
            RefreshFromRunner();
        }

        private void HandleSessionStateChanged(NetworkSessionState state, string message)
        {
            if (state == NetworkSessionState.Disconnected || state == NetworkSessionState.Failed)
            {
                // A disconnected session always rebuilds to an empty, not-ready state.
            }
            RefreshFromRunner();
        }

        private bool ValidateRoomName(string roomName)
        {
            if (!string.IsNullOrWhiteSpace(roomName)) return true;
            ReportError("Room name is required.");
            return false;
        }

        private void ReportError(string message)
        {
            Debug.LogWarning($"[LobbyManager] {message}");
            OnLobbyError?.Invoke(message);
        }
    }
}
