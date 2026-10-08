using EchoProtocol.Auth;
using EchoProtocol.Gameplay;
using EchoProtocol.Networking;
using EchoProtocol.Profile;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.MainMenu
{
    [DisallowMultipleComponent]
    public sealed class MainMenuMatchmakingController : MonoBehaviour
    {
        [Header("Entry")]
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;

        [Header("Dialog")]
        [SerializeField] private GameObject dialog;
        [SerializeField] private Text titleText;
        [SerializeField] private Text statusText;
        [SerializeField] private InputField roomCodeInput;
        [SerializeField] private GameObject difficultyRow;
        [SerializeField] private Button difficultyButton;
        [SerializeField] private Text difficultyValueText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button backButton;

        [Header("Networking")]
        [SerializeField] private NetworkBootstrap bootstrap;
        [SerializeField] private LobbyManager lobbyManager;
        [SerializeField, Range(2, 4)] private int maxPlayers = 4;

        private MatchDifficulty _difficulty =
            MatchDifficulty.Normal;

        private bool _hostMode;
        private bool _busy;

        private void Awake()
        {
            ResolveServices();
            RefreshDifficultyText();

            if (dialog != null)
            {
                dialog.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (hostButton != null)
                hostButton.onClick.AddListener(OpenHost);

            if (joinButton != null)
                joinButton.onClick.AddListener(OpenJoin);

            if (difficultyButton != null)
                difficultyButton.onClick.AddListener(CycleDifficulty);

            if (confirmButton != null)
                confirmButton.onClick.AddListener(Submit);

            if (backButton != null)
                backButton.onClick.AddListener(CloseDialog);

            ResolveServices();

            if (bootstrap != null)
                bootstrap.SessionStateChanged += HandleSessionStateChanged;
        }

        private void OnDisable()
        {
            if (hostButton != null)
                hostButton.onClick.RemoveListener(OpenHost);

            if (joinButton != null)
                joinButton.onClick.RemoveListener(OpenJoin);

            if (difficultyButton != null)
                difficultyButton.onClick.RemoveListener(CycleDifficulty);

            if (confirmButton != null)
                confirmButton.onClick.RemoveListener(Submit);

            if (backButton != null)
                backButton.onClick.RemoveListener(CloseDialog);

            if (bootstrap != null)
                bootstrap.SessionStateChanged -= HandleSessionStateChanged;
        }

        private void ResolveServices()
        {
            if (bootstrap == null)
                bootstrap = NetworkBootstrap.Instance
                    ?? FindAnyObjectByType<NetworkBootstrap>();

            if (lobbyManager == null)
                lobbyManager =
                    FindAnyObjectByType<LobbyManager>();
        }

        private void OpenHost()
        {
            if (_busy)
                return;

            _hostMode = true;

            if (titleText != null)
                titleText.text = "HOST GAME";

            if (difficultyRow != null)
                difficultyRow.SetActive(true);

            SetConfirmLabel("CREATE ROOM");
            SetStatus(string.Empty);
            OpenDialog();
        }

        private void OpenJoin()
        {
            if (_busy)
                return;

            _hostMode = false;

            if (titleText != null)
                titleText.text = "JOIN GAME";

            if (difficultyRow != null)
                difficultyRow.SetActive(false);

            SetConfirmLabel("JOIN ROOM");
            SetStatus(string.Empty);
            OpenDialog();
        }

        private void OpenDialog()
        {
            if (dialog != null)
                dialog.SetActive(true);

            if (roomCodeInput != null)
            {
                roomCodeInput.interactable = true;
                roomCodeInput.Select();
                roomCodeInput.ActivateInputField();
            }
        }

        private void CloseDialog()
        {
            if (_busy)
                return;

            if (dialog != null)
                dialog.SetActive(false);
        }

        private void CycleDifficulty()
        {
            if (_busy || !_hostMode)
                return;

            _difficulty = _difficulty switch
            {
                MatchDifficulty.Easy =>
                    MatchDifficulty.Normal,

                MatchDifficulty.Normal =>
                    MatchDifficulty.Hard,

                _ =>
                    MatchDifficulty.Easy
            };

            RefreshDifficultyText();
        }

        private void RefreshDifficultyText()
        {
            if (difficultyValueText != null)
            {
                difficultyValueText.text =
                    $"<  {_difficulty.ToString().ToUpperInvariant()}  >";
            }
        }

        private async void Submit()
        {
            if (_busy)
                return;

            ResolveServices();

            if (bootstrap == null || lobbyManager == null)
            {
                SetStatus("NETWORK SERVICES UNAVAILABLE");
                return;
            }

            if (bootstrap.HasRunningRunner || bootstrap.IsBusy)
            {
                SetStatus("NETWORK SESSION IS BUSY");
                return;
            }

            string roomCode =
                roomCodeInput != null
                    ? roomCodeInput.text.Trim()
                    : string.Empty;

            if (roomCode.Length == 0)
            {
                SetStatus("ROOM CODE REQUIRED");
                return;
            }

            string operatorName =
                ResolveOperatorName();

            if (!lobbyManager.SetLocalOperatorName(operatorName))
            {
                SetStatus("PLAYER NAME UNAVAILABLE");
                return;
            }

            _busy = true;
            RefreshInteractableState();

            SetStatus(
                _hostMode
                    ? "CREATING ROOM..."
                    : "JOINING ROOM...");

            bool success;

            try
            {
                success =
                    _hostMode
                        ? await bootstrap.CreateRoomAsync(
                            roomCode,
                            maxPlayers,
                            _difficulty)
                        : await bootstrap.JoinRoomAsync(
                            roomCode);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                success = false;

                if (this != null)
                    SetStatus(exception.Message);
            }

            if (this == null)
                return;

            _busy = false;
            RefreshInteractableState();

            if (!success)
            {
                SetStatus(
                    string.IsNullOrWhiteSpace(bootstrap.LastError)
                        ? "CONNECTION FAILED"
                        : bootstrap.LastError);

                return;
            }

            SetStatus("CONNECTED. ENTERING LOBBY...");
        }

        private static string ResolveOperatorName()
        {
            if (PlayerProfileSession.HasProfile
                && !string.IsNullOrWhiteSpace(
                    PlayerProfileSession.DisplayName))
            {
                return PlayerProfileSession.DisplayName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(
                    AuthSession.DisplayName))
            {
                return AuthSession.DisplayName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(
                    AuthSession.Username))
            {
                return AuthSession.Username.Trim();
            }

            return "Operator";
        }

        private void HandleSessionStateChanged(
            NetworkSessionState state,
            string message)
        {
            if (state == NetworkSessionState.Connecting)
            {
                SetStatus(
                    _hostMode
                        ? "CREATING ROOM..."
                        : "JOINING ROOM...");
            }
            else if (state == NetworkSessionState.Failed)
            {
                SetStatus(
                    string.IsNullOrWhiteSpace(message)
                        ? "CONNECTION FAILED"
                        : message);
            }
        }

        private void SetConfirmLabel(string value)
        {
            if (confirmButton == null)
                return;

            var label =
                confirmButton.GetComponentInChildren<Text>(
                    true);

            if (label != null)
                label.text = value;
        }

        private void SetStatus(string value)
        {
            if (statusText != null)
                statusText.text = value ?? string.Empty;
        }

        private void RefreshInteractableState()
        {
            if (hostButton != null)
                hostButton.interactable = !_busy;

            if (joinButton != null)
                joinButton.interactable = !_busy;

            if (roomCodeInput != null)
                roomCodeInput.interactable = !_busy;

            if (difficultyButton != null)
                difficultyButton.interactable =
                    !_busy && _hostMode;

            if (confirmButton != null)
                confirmButton.interactable = !_busy;

            if (backButton != null)
                backButton.interactable = !_busy;
        }
    }
}
