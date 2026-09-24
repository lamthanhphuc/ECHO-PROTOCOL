using System;
using System.Text;
using EchoProtocol.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI
{
    /// <summary>Canvas presentation only; session ownership remains in NetworkBootstrap.</summary>
    public sealed class NetworkLobbyUI : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private TMP_InputField playerNameInput;
        [SerializeField] private TMP_InputField sessionNameInput;
        [Header("Buttons")]
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button readyButton;
        [SerializeField] private Button startButton;
        [SerializeField] private Button leaveButton;
        [Header("Status")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text memberCountText;
        [SerializeField] private TMP_Text memberListText;
        [SerializeField] private Image statusIndicator;
        [SerializeField] private TMP_Text networkMessage;
        [SerializeField] private TMP_Text emptyMemberText;
        [Header("Networking")]
        [SerializeField] private NetworkBootstrap bootstrap;
        [SerializeField] private LobbyManager lobbyManager;
        [SerializeField, Range(2, 4)] private int maxPlayers = 4;

        private static readonly Color Offline = new Color32(213, 29, 39, 255);
        private static readonly Color Online = new Color32(105, 168, 134, 255);
        private static readonly Color Connecting = new Color32(201, 156, 84, 255);
        private bool _blinkSignal = true;
        private static readonly Color Neutral = new Color32(216, 216, 216, 255);
        private NetworkBootstrap _subscribedBootstrap;
        private LobbyManager _subscribedLobby;
        private bool _busy;
        private bool _configured;
        private float _nextResolve;
        private RoomInfoViewModel _room = new RoomInfoViewModel();
        private bool Connected => bootstrap != null && bootstrap.HasRunningRunner;
        private bool Busy => _busy || (bootstrap != null && bootstrap.IsBusy);

        private void OnEnable()
        {
            _configured = playerNameInput != null && sessionNameInput != null && hostButton != null
                && joinButton != null && statusText != null && memberCountText != null && memberListText != null;
            if (!_configured) Debug.LogError("[NetworkLobbyUI] Assign all Input, Host/Join and text references.", this);
            if (playerNameInput != null) playerNameInput.characterLimit = 32;
            if (sessionNameInput != null) sessionNameInput.characterLimit = 32;
            if (statusText != null) statusText.richText = false;
            if (memberListText != null) memberListText.richText = false;
            if (hostButton != null) hostButton.onClick.AddListener(OnHostClicked);
            if (joinButton != null) joinButton.onClick.AddListener(OnJoinClicked);
            if (readyButton != null) readyButton.onClick.AddListener(OnReadyClicked);
            if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
            if (leaveButton != null) leaveButton.onClick.AddListener(OnLeaveClicked);
            ResolveServices();
            RefreshMemberList();
            RefreshControls();
            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            if (statusIndicator != null)
            {
                var color = statusIndicator.color;
                color.a = _blinkSignal ? 0.65f + 0.35f * Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f / 0.8f) : 1f;
                statusIndicator.color = color;
            }
            UpdateInputBorder(playerNameInput);
            UpdateInputBorder(sessionNameInput);
        }

        private static void UpdateInputBorder(TMP_InputField input)
        {
            if (input == null || !input.TryGetComponent<Outline>(out var border)) return;
            border.effectColor = input.isFocused ? new Color32(157, 165, 170, 255) : new Color32(89, 97, 102, 255);
        }

        private void OnDisable()
        {
            if (hostButton != null) hostButton.onClick.RemoveListener(OnHostClicked);
            if (joinButton != null) joinButton.onClick.RemoveListener(OnJoinClicked);
            if (readyButton != null) readyButton.onClick.RemoveListener(OnReadyClicked);
            if (startButton != null) startButton.onClick.RemoveListener(OnStartClicked);
            if (leaveButton != null) leaveButton.onClick.RemoveListener(OnLeaveClicked);
            Unsubscribe();
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextResolve)
            {
                _nextResolve = Time.unscaledTime + 0.5f;
                ResolveServices();
            }
            // IsBusy may clear after the final session event, so derive controls every frame.
            RefreshControls();
            RefreshVisuals();
        }

        private void ResolveServices()
        {
            if (bootstrap == null) bootstrap = FindAnyObjectByType<NetworkBootstrap>();
            if (lobbyManager == null) lobbyManager = FindAnyObjectByType<LobbyManager>();
            if (_subscribedBootstrap == bootstrap && _subscribedLobby == lobbyManager) return;
            Unsubscribe();
            _subscribedBootstrap = bootstrap;
            _subscribedLobby = lobbyManager;
            if (_subscribedBootstrap != null)
            {
                _subscribedBootstrap.SessionStateChanged += OnSessionStateChanged;
                OnSessionStateChanged(bootstrap.State, bootstrap.LastError);
            }
            if (_subscribedLobby != null)
            {
                _subscribedLobby.OnRoomUpdated += OnRoomUpdated;
                _subscribedLobby.OnLobbyError += ReportError;
                if (playerNameInput != null && !string.IsNullOrEmpty(lobbyManager.LocalOperatorName))
                    playerNameInput.SetTextWithoutNotify(lobbyManager.LocalOperatorName);
            }
            if (Connected && sessionNameInput != null)
                sessionNameInput.SetTextWithoutNotify(bootstrap.CurrentSessionName);
            RefreshMemberList();
        }

        private void Unsubscribe()
        {
            if (_subscribedBootstrap != null) _subscribedBootstrap.SessionStateChanged -= OnSessionStateChanged;
            if (_subscribedLobby != null)
            {
                _subscribedLobby.OnRoomUpdated -= OnRoomUpdated;
                _subscribedLobby.OnLobbyError -= ReportError;
            }
            _subscribedBootstrap = null;
            _subscribedLobby = null;
        }

        public void OnHostClicked() => Connect(true);
        public void OnJoinClicked() => Connect(false);

        private async void Connect(bool host)
        {
            if (!isActiveAndEnabled || Busy || Connected) return;
            ResolveServices();
            if (!_configured || bootstrap == null || lobbyManager == null)
            {
                ReportError("NETWORK SERVICES UNAVAILABLE. Start the game from Bootstrap and check UI references.");
                return;
            }
            var session = sessionNameInput.text.Trim();
            if (session.Length == 0)
            {
                ReportError("ROOM CODE REQUIRED.");
                return;
            }
            if (!lobbyManager.SetLocalOperatorName(playerNameInput.text))
            {
                ReportError("YOUR NAME REQUIRED.");
                return;
            }
            playerNameInput.SetTextWithoutNotify(lobbyManager.LocalOperatorName);
            sessionNameInput.SetTextWithoutNotify(session);
            _busy = true;
            SetStatus(host ? "> CREATING ROOM..." : "> JOINING ROOM...");
            RefreshControls();
            var service = bootstrap;
            try
            {
                var success = host
                    ? await service.CreateRoomAsync(session, maxPlayers)
                    : await service.JoinRoomAsync(session);
                if (this == null || !isActiveAndEnabled) return;
                if (success) OnSessionStateChanged(service.State, string.Empty);
                else ReportError(service != null ? service.LastError : "NETWORK SERVICE LOST.");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[NetworkLobbyUI] {(host ? "Host" : "Join")} failed: {exception}");
                if (this != null && isActiveAndEnabled) ReportError(exception.Message);
            }
            finally
            {
                if (this != null)
                {
                    _busy = false;
                    if (isActiveAndEnabled) { RefreshMemberList(); RefreshControls(); }
                }
            }
        }

        public void SetStatus(string message)
        {
            message ??= string.Empty;
            var split = networkMessage != null ? message.IndexOf('\n') : -1;
            if (statusText != null) { statusText.text = split >= 0 ? message.Substring(0, split) : message; statusText.color = Neutral; }
            if (networkMessage != null) networkMessage.text = split >= 0 ? message.Substring(split + 1) : string.Empty;
        }

        public void SetMemberCount(int current, int max)
        {
            if (memberCountText != null) memberCountText.text = $"PLAYERS IN ROOM: {current} / {max}";
        }

        public void RefreshMemberList()
        {
            _room = lobbyManager != null ? lobbyManager.CurrentState : new RoomInfoViewModel();
            _room ??= new RoomInfoViewModel();
            SetMemberCount(_room.CurrentPlayers, _room.MaxPlayers > 0 ? _room.MaxPlayers : maxPlayers);
            if (memberListText == null) return;
            var list = new StringBuilder();
            if (_room.Members != null)
                foreach (var member in _room.Members)
                {
                    if (member == null) continue;
                    if (list.Length > 0) list.AppendLine().AppendLine();
                    list.Append("> ").Append(member.DisplayName);
                    if (member.IsLocal) list.Append(_room.IsHost ? " [YOU / HOST]" : " [YOU]");
                    list.AppendLine().Append("  ").Append(member.IsReady ? "READY" : "NOT READY")
                        .Append("  |  TOOL ").Append(member.ToolId);
                }
            if (emptyMemberText != null) emptyMemberText.gameObject.SetActive(list.Length == 0);
            memberListText.text = list.Length == 0 ? (emptyMemberText == null ? "NO PLAYERS YET" : string.Empty) : list.ToString();
        }

        private void OnRoomUpdated(RoomInfoViewModel state) => RefreshMemberList();

        private void OnSessionStateChanged(NetworkSessionState state, string message)
        {
            _blinkSignal = state != NetworkSessionState.InLobby && state != NetworkSessionState.InMatch;
            if (statusIndicator != null) statusIndicator.color =
                state == NetworkSessionState.InLobby || state == NetworkSessionState.InMatch ? Online :
                state == NetworkSessionState.Connecting || state == NetworkSessionState.ShuttingDown ? Connecting : Offline;
            switch (state)
            {
                case NetworkSessionState.Connecting: SetStatus("CONNECTION STATUS: CONNECTING\n> Joining the network...\n" + message); break;
                case NetworkSessionState.InLobby:
                    SetStatus("CONNECTION STATUS: ROOM OPEN\n> Waiting for players to ready up..."); break;
                case NetworkSessionState.InMatch: SetStatus("CONNECTION STATUS: IN MISSION\n> Loading the mission..."); break;
                case NetworkSessionState.ShuttingDown: SetStatus("> LEAVING ROOM..."); break;
                case NetworkSessionState.Failed: ReportError(message); break;
                default: SetStatus("CONNECTION STATUS: OFFLINE\nEnter your name and room code.\nThen create or join a room."); break;
            }
            RefreshMemberList();
        }

        private void ReportError(string message)
        {
            var detail = string.IsNullOrWhiteSpace(message) ? "Check Console for connection details." : message;
            SetStatus("> CONNECTION FAILURE\n" + detail);
            if (statusText != null) statusText.color = Offline;
            if (statusIndicator != null) statusIndicator.color = Offline;
            _blinkSignal = true;
            Debug.LogError($"[NetworkLobbyUI] {detail}", this);
        }

        private void RefreshControls()
        {
            var canConnect = _configured && !Busy && !Connected;
            if (hostButton != null) hostButton.interactable = canConnect;
            if (joinButton != null) joinButton.interactable = canConnect;
            if (playerNameInput != null) playerNameInput.interactable = canConnect;
            if (sessionNameInput != null) sessionNameInput.interactable = canConnect;
            var inLobby = Connected && !Busy && lobbyManager != null && bootstrap.State == NetworkSessionState.InLobby;
            if (readyButton != null)
            {
                readyButton.interactable = inLobby;
                var label = readyButton.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = _room.IsReady ? "NOT READY" : "READY";
            }
            if (startButton != null) startButton.interactable = inLobby && _room.IsHost && _room.CanStartMatch;
            if (leaveButton != null) leaveButton.interactable = Connected && !Busy;
            FadeDisabled(hostButton); FadeDisabled(joinButton); FadeDisabled(readyButton);
            FadeDisabled(startButton); FadeDisabled(leaveButton);
        }

        private static void FadeDisabled(Button button)
        {
            if (button != null && button.TryGetComponent<CanvasGroup>(out var group))
                group.alpha = button.interactable ? 1f : 0.35f;
        }

        private void OnReadyClicked()
        {
            if (!Busy && lobbyManager != null && Connected) lobbyManager.SetReady(!_room.IsReady);
        }

        private void OnStartClicked()
        {
            if (!Busy && lobbyManager != null && _room.CanStartMatch) lobbyManager.TryStartMatch();
        }

        private async void OnLeaveClicked()
        {
            if (Busy || !Connected) return;
            _busy = true;
            RefreshControls();
            try { await bootstrap.Shutdown(); }
            catch (Exception exception)
            {
                Debug.LogError($"[NetworkLobbyUI] Leave failed: {exception}");
                if (this != null && isActiveAndEnabled) ReportError(exception.Message);
            }
            finally { if (this != null) { _busy = false; if (isActiveAndEnabled) RefreshControls(); } }
        }
    }
}
