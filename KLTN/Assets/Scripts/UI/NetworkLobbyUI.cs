using EchoProtocol.Pets;
using System;
using System.Collections.Generic;
using System.Text;
using EchoProtocol.Core;
using EchoProtocol.Gameplay;
using EchoProtocol.Auth;
using EchoProtocol.Networking;
using EchoProtocol.Networking.Authority;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EchoProtocol.UI
{
    /// <summary>Canvas presentation only; session ownership remains in NetworkBootstrap.</summary>
    public sealed class NetworkLobbyUI : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private TMP_InputField playerNameInput;
        [SerializeField] private TMP_InputField sessionNameInput;
        [Header("Difficulty")]
        [SerializeField] private TMP_Dropdown difficultyDropdown;
        [Header("Buttons")]
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button readyButton;
        [SerializeField] private Button characterButton;
        [SerializeField] private Button petButton;
        [SerializeField] private Button startButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private Button exitButton;
        [Header("Status")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text memberCountText;
        [SerializeField] private TMP_Text roomNameText;
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
        private bool _inputsInitialized;
        private float _nextResolve;
        private RoomInfoViewModel _room = new RoomInfoViewModel();
        private bool Connected => bootstrap != null && bootstrap.HasRunningRunner;
        private bool Busy => _busy || (bootstrap != null && bootstrap.IsBusy);

        private void OnEnable()
        {
            EchoProtocol.Settings.GameLanguage.Changed += RefreshMemberList;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _configured = playerNameInput != null && sessionNameInput != null && hostButton != null
                && joinButton != null && statusText != null && memberCountText != null && memberListText != null
                && difficultyDropdown != null;
            if (!_configured) Debug.LogError("[NetworkLobbyUI] Assign all Input, Host/Join, difficulty and text references.", this);
            ConfigureDifficultyDropdown();
            if (playerNameInput != null) playerNameInput.characterLimit = 32;
            if (sessionNameInput != null) sessionNameInput.characterLimit = 32;
            if (!_inputsInitialized)
            {
                var loginName = string.IsNullOrWhiteSpace(AuthSession.DisplayName)
                    ? AuthSession.Username : AuthSession.DisplayName;
                if (playerNameInput != null && !string.IsNullOrWhiteSpace(loginName))
                    playerNameInput.SetTextWithoutNotify(loginName.Trim());
                if (sessionNameInput != null)
                    sessionNameInput.SetTextWithoutNotify(string.Empty);
                _inputsInitialized = true;
            }
            if (statusText != null) statusText.richText = false;
            if (networkMessage != null)
            {
                networkMessage.richText = false;
                networkMessage.enableAutoSizing = false;
                networkMessage.fontSize = 13;
                networkMessage.textWrappingMode = TextWrappingModes.Normal;
            }
            if (memberListText != null) memberListText.richText = false;
            if (hostButton != null) hostButton.onClick.AddListener(OnHostClicked);
            if (joinButton != null) joinButton.onClick.AddListener(OnJoinClicked);
            if (readyButton != null) readyButton.onClick.AddListener(OnReadyClicked);
            EnsurePetButton();
            if (petButton != null) petButton.onClick.AddListener(OnPetClicked);
            if (characterButton != null) characterButton.onClick.AddListener(OnCharacterClicked);
            if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
            if (leaveButton != null) leaveButton.onClick.AddListener(OnLeaveClicked);
            if (exitButton != null) exitButton.onClick.AddListener(OnExitClicked);
            if (difficultyDropdown != null)
                difficultyDropdown.onValueChanged.AddListener(OnDifficultyChanged);
            ResolveServices();
            RefreshMemberList();
            RefreshControls();
            RefreshVisuals();
        }

        private void ConfigureDifficultyDropdown()
        {
            if (difficultyDropdown == null) return;

            difficultyDropdown.gameObject.SetActive(true);
            difficultyDropdown.ClearOptions();
            difficultyDropdown.AddOptions(new List<string> { "EASY", "NORMAL", "HARD" });
            var difficulty = MatchAuthorityRuntime.Instance != null
                ? MatchAuthorityRuntime.Instance.Difficulty
                : MatchDifficulty.Normal;
            difficultyDropdown.SetValueWithoutNotify((int)difficulty);
            difficultyDropdown.RefreshShownValue();
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
            EchoProtocol.Settings.GameLanguage.Changed -= RefreshMemberList;
            if (hostButton != null) hostButton.onClick.RemoveListener(OnHostClicked);
            if (joinButton != null) joinButton.onClick.RemoveListener(OnJoinClicked);
            if (readyButton != null) readyButton.onClick.RemoveListener(OnReadyClicked);
            if (petButton != null) petButton.onClick.RemoveListener(OnPetClicked);
            if (characterButton != null) characterButton.onClick.RemoveListener(OnCharacterClicked);
            if (startButton != null) startButton.onClick.RemoveListener(OnStartClicked);
            if (leaveButton != null) leaveButton.onClick.RemoveListener(OnLeaveClicked);
            if (exitButton != null) exitButton.onClick.RemoveListener(OnExitClicked);
            if (difficultyDropdown != null)
                difficultyDropdown.onValueChanged.RemoveListener(OnDifficultyChanged);
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
                if (playerNameInput != null && string.IsNullOrWhiteSpace(playerNameInput.text)
                    && !string.IsNullOrEmpty(lobbyManager.LocalOperatorName))
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
            var difficulty = (MatchDifficulty)Mathf.Clamp(difficultyDropdown.value, 0, 2);
            _busy = true;
            SetStatus(host ? "> CREATING ROOM..." : "> JOINING ROOM...");
            RefreshControls();
            var service = bootstrap;
            try
            {
                var success = host
                    ? await service.CreateRoomAsync(session, maxPlayers, difficulty)
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
            if (memberCountText != null) memberCountText.text = $"{current} / {max}";
        }

        public void RefreshMemberList()
        {            if (roomNameText != null)
            {
                roomNameText.text =
                    bootstrap != null
                    && !string.IsNullOrWhiteSpace(bootstrap.CurrentSessionName)
                        ? bootstrap.CurrentSessionName.ToUpperInvariant()
                        : "----";
            }

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
                    if (member.IsLocal) list.Append(EchoProtocol.Settings.GameLanguage.Choose(_room.IsHost ? " [BẠN / HOST]" : " [BẠN]", _room.IsHost ? " [YOU / HOST]" : " [YOU]"));
                    list.AppendLine().Append("  ").Append(EchoProtocol.Settings.GameLanguage.Choose(member.IsReady ? "SẴN SÀNG" : "CHƯA SẴN SÀNG", member.IsReady ? "READY" : "NOT READY"))
                        .Append(EchoProtocol.Settings.GameLanguage.Choose("  |  CÔNG CỤ ", "  |  TOOL ")).Append(member.ToolId)
                        .Append("  |  ").Append(member.CharacterId == 1 ? "JAMMO" : "ASTRONAUT")
                        .AppendLine().Append("  PET: ").Append(PetCatalog.Name(member.PetId));
                }
            if (emptyMemberText != null) emptyMemberText.gameObject.SetActive(list.Length == 0);
            memberListText.text = list.Length == 0 ? (emptyMemberText == null ? "NO PLAYERS YET" : string.Empty) : list.ToString();
        }

        private void OnRoomUpdated(RoomInfoViewModel state) => RefreshMemberList();

        private void OnSessionStateChanged(NetworkSessionState state, string message)
        {
            if ((state == NetworkSessionState.InLobby || state == NetworkSessionState.InMatch)
                && difficultyDropdown != null && MatchAuthorityRuntime.Instance != null)
            {
                difficultyDropdown.SetValueWithoutNotify((int)MatchAuthorityRuntime.Instance.Difficulty);
                difficultyDropdown.RefreshShownValue();
            }

            _blinkSignal = state != NetworkSessionState.InLobby && state != NetworkSessionState.InMatch;
            if (statusIndicator != null) statusIndicator.color =
                state == NetworkSessionState.InLobby || state == NetworkSessionState.InMatch ? Online :
                state == NetworkSessionState.Connecting || state == NetworkSessionState.ShuttingDown ? Connecting : Offline;
            switch (state)
            {
                case NetworkSessionState.Connecting: SetStatus("CONNECTING..."); break;
                case NetworkSessionState.InLobby:
                    SetStatus("ROOM OPEN"); break;
                case NetworkSessionState.InMatch: SetStatus("MISSION STARTING..."); break;
                case NetworkSessionState.ShuttingDown: SetStatus("LEAVING ROOM..."); break;
                case NetworkSessionState.Failed: ReportError(message); break;
                default: SetStatus("OFFLINE"); break;
            }
            RefreshMemberList();
        }

        private void ReportError(string message)
        {
            var detail = message ?? string.Empty;
            var feedback = ConnectionFeedback(detail);
            SetStatus(feedback);
            if (networkMessage != null) networkMessage.color = Neutral;
            if (statusText != null) statusText.color = Offline;
            if (statusIndicator != null) statusIndicator.color = Offline;
            _blinkSignal = true;
            Debug.LogError($"[NetworkLobbyUI] {detail}", this);
        }

        // Only safe, player-facing descriptions reach the UI; raw service errors stay in Console.
        internal static string ConnectionFeedback(string detail) => EchoProtocol.Settings.GameLanguage.Translate(ConnectionFeedbackSource(detail));

        private static string ConnectionFeedbackSource(string detail)
        {
            string error = (detail ?? string.Empty).ToLowerInvariant();
            if (error.Contains("authenticated user"))
                return "PHIÊN ĐĂNG NHẬP KHÔNG HỢP LỆ\nVề menu chính và đăng nhập lại, sau đó thử kết nối.";
            if (error.Contains("session is busy") || error.Contains("operation is already in progress"))
                return "ĐANG CÓ THAO TÁC KẾT NỐI\nChờ thao tác hiện tại hoàn tất rồi thử lại.";
            if (error.Contains("room code required") || error.Contains("room name is required"))
                return "THIẾU MÃ PHÒNG\nNhập mã phòng, rồi chọn Tạo phòng hoặc Vào phòng.";
            if (error.Contains("your name required") || error.Contains("player name unavailable"))
                return "THIẾU TÊN NGƯỜI CHƠI\nNhập tên của bạn trước khi kết nối.";
            if (error.Contains("gamenotfound"))
                return "KHÔNG TÌM THẤY PHÒNG\nKiểm tra mã phòng và hỏi host đã mở phòng chưa. Sau đó chọn Vào phòng để thử lại.";
            if (error.Contains("gameisfull"))
                return "PHÒNG ĐÃ ĐẦY\nNhờ host kiểm tra chỗ trống hoặc dùng mã phòng khác.";
            if (error.Contains("gameclosed"))
                return "PHÒNG ĐÃ ĐÓNG\nTrận có thể đã bắt đầu. Nhờ host mở phòng mới rồi nhập lại mã.";
            if (error.Contains("host disconnected"))
                return "HOST ĐÃ NGẮT KẾT NỐI\nNhờ host tạo lại phòng rồi chọn Vào phòng với mã mới.";
            if (error.Contains("timeout") || error.Contains("timedout"))
                return "KẾT NỐI QUÁ THỜI GIAN\nKiểm tra mạng và mã phòng. Chọn Tạo phòng hoặc Vào phòng để thử lại.";
            if (error.Contains("authentication") || error.Contains("invalidregion") || error.Contains("maxccu"))
                return "DỊCH VỤ CHƯA CHO PHÉP KẾT NỐI\nThử lại sau ít phút. Nếu vẫn lỗi, liên hệ người quản lý game.";
            if (error.Contains("services unavailable") || error.Contains("service lost"))
                return "DỊCH VỤ KẾT NỐI CHƯA SẴN SÀNG\nVề menu chính và mở lại lobby. Nếu vẫn lỗi, khởi động lại game.";
            if (error.Contains("every player must be ready"))
                return "CHƯA THỂ BẮT ĐẦU\nTất cả người chơi cần bật Sẵn sàng trước khi host bắt đầu.";
            if (error.Contains("only the authoritative host"))
                return "CHỈ HOST CÓ THỂ BẮT ĐẦU\nNhờ chủ phòng bắt đầu trận.";
            if (Application.internetReachability == NetworkReachability.NotReachable)
                return "KHÔNG CÓ KẾT NỐI MẠNG\nBật Wi-Fi hoặc cắm mạng rồi chọn Tạo phòng / Vào phòng để thử lại.";
            if (error.Contains("connection failed") || error.Contains("disconnect") || error.Contains("connectionrefused"))
                return "KẾT NỐI BỊ GIÁN ĐOẠN\nKiểm tra mạng và hỏi host phòng còn mở không. Sau đó thử kết nối lại.";
            if (error.Contains("start") || error.Contains("ready") || error.Contains("scene load"))
                return "CHƯA THỂ THỰC HIỆN\nKiểm tra trạng thái Sẵn sàng và kết nối của phòng rồi thử lại. Nếu vẫn lỗi, mở lại phòng.";
            return "KHÔNG THỂ KẾT NỐI\nChưa xác định được nguyên nhân. Kiểm tra mạng, mã phòng rồi thử lại; nếu vẫn lỗi, khởi động lại game.";
        }

        private void EnsurePetButton()
        {
            if (petButton != null || characterButton == null) return;
            petButton = Instantiate(characterButton, characterButton.transform.parent);
            petButton.name = "PetSelectionButton";
            petButton.onClick = new Button.ButtonClickedEvent();
            var rect = (RectTransform)petButton.transform;
            rect.anchoredPosition += Vector2.down * (rect.rect.height + 8f);
        }
        private void OnPetClicked()
        {
            if (lobbyManager != null && lobbyManager.TryGetLocalPlayerState(out var state, false))
                state.RequestPet((state.PetId + 1) % 5);
        }

        private void OnCharacterClicked()
        {
            if (lobbyManager != null && lobbyManager.TryGetLocalPlayerState(out var state, false))
                state.RequestCharacter(state.CharacterId == 0 ? 1 : 0);
        }

        private void RefreshControls()
        {
            var canConnect =
                _configured
                && !Busy
                && !Connected;

            var inLobby =
                Connected
                && !Busy
                && lobbyManager != null
                && bootstrap != null
                && bootstrap.State == NetworkSessionState.InLobby;

            if (hostButton != null)
                hostButton.interactable = canConnect;

            if (joinButton != null)
                joinButton.interactable = canConnect;

            if (playerNameInput != null)
                playerNameInput.interactable = canConnect;

            if (sessionNameInput != null)
                sessionNameInput.interactable = canConnect;

            if (difficultyDropdown != null)
            {
                difficultyDropdown.interactable =
                    canConnect
                    || (inLobby && _room.IsHost);
            }

            if (characterButton != null)
            {
                characterButton.interactable = false;
                string character = "ASTRONAUT";
                if (lobbyManager != null && lobbyManager.TryGetLocalPlayerState(out var local, false))
                {
                    character = local.CharacterId == 1 ? "JAMMO" : "ASTRONAUT";
                    characterButton.interactable = inLobby && !local.IsReady;
                }
                var characterLabel = characterButton.GetComponentInChildren<TMP_Text>();
                if (characterLabel != null) characterLabel.text = "CHARACTER: " + character;
            }
            if (petButton != null)
            {
                petButton.interactable = false;
                int petId = 0;
                if (lobbyManager != null && lobbyManager.TryGetLocalPlayerState(out var petOwner, false))
                {
                    petId = petOwner.PetId;
                    petButton.interactable = inLobby && !petOwner.IsReady;
                }
                var label = petButton.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = "PET: " + PetCatalog.Name(petId);
            }
            if (readyButton != null)
            {
                readyButton.interactable = inLobby;
                var label = readyButton.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = _room.IsReady ? "NOT READY" : "READY";
            }
            if (startButton != null) startButton.interactable = inLobby && _room.IsHost && _room.CanStartMatch;
            if (leaveButton != null) leaveButton.interactable = Connected && !Busy;
            if (exitButton != null) exitButton.interactable = !Busy;
            FadeDisabled(hostButton); FadeDisabled(joinButton); FadeDisabled(readyButton);
            FadeDisabled(startButton); FadeDisabled(leaveButton); FadeDisabled(exitButton);
        }

        private void OnDifficultyChanged(int value)
        {
            if (difficultyDropdown == null || Busy)
            {
                return;
            }

            var difficulty =
                (MatchDifficulty)Mathf.Clamp(
                    value,
                    (int)MatchDifficulty.Easy,
                    (int)MatchDifficulty.Hard);

            // Before room creation Connect() already reads the dropdown value.
            if (!Connected)
            {
                return;
            }

            // Only the Host can change difficulty while waiting in Lobby.
            if (bootstrap == null
                || bootstrap.State != NetworkSessionState.InLobby
                || !_room.IsHost)
            {
                var current =
                    MatchAuthorityRuntime.Instance != null
                        ? MatchAuthorityRuntime.Instance.Difficulty
                        : MatchDifficulty.Normal;

                difficultyDropdown.SetValueWithoutNotify(
                    (int)current);

                difficultyDropdown.RefreshShownValue();
                return;
            }

            MatchAuthorityRuntime
                .EnsureExists(bootstrap)
                .RequestDifficulty(difficulty);
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

        private void OnLeaveClicked()
        {
            if (Busy || !Connected) return;
            GameUIFeedback.Instance.Confirm(EchoProtocol.Settings.GameLanguage.Choose("Rời phòng?", "Leave room?"),
                GameUIFeedback.LeaveWarning, LeaveConfirmed,
                EchoProtocol.Settings.GameLanguage.Choose("Rời phòng", "Leave room"));
        }

        private async void LeaveConfirmed()
        {
            if (Busy || !Connected) return;
            _busy = true;
            RefreshControls();
            try { await bootstrap.ShutdownRunnerAsync(); }
            catch (Exception exception)
            {
                Debug.LogError($"[NetworkLobbyUI] Leave failed: {exception}");
                if (this != null && isActiveAndEnabled) ReportError(exception.Message);
            }
            finally { if (this != null) { _busy = false; if (isActiveAndEnabled) RefreshControls(); } }
        }

        private void OnExitClicked()
        {
            if (Busy) return;
            GameUIFeedback.Instance.Confirm(EchoProtocol.Settings.GameLanguage.Choose("Về menu chính?", "Return to main menu?"),
                () => Connected ? GameUIFeedback.LeaveWarning() : EchoProtocol.Settings.GameLanguage.Choose("Bạn muốn về menu chính?", "Return to the main menu?"),
                ExitConfirmed, EchoProtocol.Settings.GameLanguage.Choose("Về menu", "Return to menu"));
        }

        private async void ExitConfirmed()
        {
            if (Busy) return;

            _busy = true;
            RefreshControls();

            try
            {
                if (bootstrap != null)
                    await bootstrap.LeaveToMainMenuAsync();
                else SceneManager.LoadScene(GameConstants.SceneMainMenu);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[NetworkLobbyUI] Exit failed: {exception}");
                if (this != null && isActiveAndEnabled)
                    ReportError(exception.Message);
            }
            finally
            {
                if (this != null)
                {
                    _busy = false;
                    if (isActiveAndEnabled)
                        RefreshControls();
                }
            }
        }
    }
}
