using System;
using EchoProtocol.Core;
using EchoProtocol.Gameplay;
using EchoProtocol.Networking;
using EchoProtocol.Networking.Authority;
using EchoProtocol.UI.MainMenu;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EchoProtocol.UI
{
    [DisallowMultipleComponent]
    public sealed class LobbyRuntimeBindingsController : MonoBehaviour
    {
        [Header("Services")]
        [SerializeField] private LobbyManager lobbyManager;
        [SerializeField] private NetworkBootstrap bootstrap;

        [Header("Lobby Actions")]
        [SerializeField] private Button exitButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private Button readyButton;
        [SerializeField] private Button startButton;
        [SerializeField] private TMP_Dropdown difficultyDropdown;

        [Header("Character")]
        [SerializeField] private Button characterPreviousButton;
        [SerializeField] private Button characterNextButton;
        [SerializeField] private RawImage characterPortrait;
        [SerializeField] private TMP_Text characterNameText;

        [Header("Character Locked Preview")]
        [SerializeField] private GameObject characterLockOverlay;
        [SerializeField] private TMP_Text characterLockText;

        [SerializeField]
        private Color characterOwnedTint =
            Color.white;

        [SerializeField]
        private Color characterLockedTint =
            new Color(
                0.34f,
                0.34f,
                0.34f,
                0.72f);

        private RoomInfoViewModel _room =
            new RoomInfoViewModel();

        private LobbyManager _subscribedLobby;
        private bool _exitInProgress;

        private int _previewCharacterId = -1;
        private int _lastEquippedCharacterId = -1;


        public int PreviewCharacterId
        {
            get
            {
                if (_previewCharacterId >= 0)
                    return _previewCharacterId;

                return CharacterShopAssets
                    .AstronautCharacterId;
            }
        }


        public bool PreviewCharacterOwned =>
            CharacterShopAssets.IsOwned(
                PreviewCharacterId);


        public void Configure(
            LobbyManager manager,
            NetworkBootstrap networkBootstrap,
            Button exit,
            Button leave,
            Button ready,
            Button start,
            TMP_Dropdown difficulty,
            Button characterPrevious,
            Button characterNext,
            RawImage portrait,
            TMP_Text characterName)
        {
            lobbyManager = manager;
            bootstrap = networkBootstrap;

            exitButton = exit;
            leaveButton = leave;
            readyButton = ready;
            startButton = start;
            difficultyDropdown = difficulty;

            characterPreviousButton =
                characterPrevious;

            characterNextButton =
                characterNext;

            characterPortrait =
                portrait;

            characterNameText =
                characterName;
        }


        public void ConfigureCharacterPreview(
            GameObject lockOverlay,
            TMP_Text lockText)
        {
            characterLockOverlay =
                lockOverlay;

            characterLockText =
                lockText;
        }


        private void OnEnable()
        {
            _previewCharacterId = -1;
            _lastEquippedCharacterId = -1;

            if (exitButton != null)
                exitButton.onClick.AddListener(OnExit);

            if (leaveButton != null)
                leaveButton.onClick.AddListener(OnLeave);

            if (readyButton != null)
                readyButton.onClick.AddListener(OnReady);

            if (startButton != null)
                startButton.onClick.AddListener(OnStart);

            if (difficultyDropdown != null)
            {
                difficultyDropdown
                    .onValueChanged
                    .AddListener(
                        OnDifficultyChanged);
            }

            if (characterPreviousButton != null)
            {
                characterPreviousButton
                    .onClick
                    .AddListener(
                        OnCharacterPrevious);
            }

            if (characterNextButton != null)
            {
                characterNextButton
                    .onClick
                    .AddListener(
                        OnCharacterNext);
            }

            TeamToolOwnershipSession.Changed +=
                HandleOwnershipChanged;

            ResolveServices();
            SyncLobbySubscription();

            if (!TeamToolOwnershipSession.IsLoaded
                && !TeamToolOwnershipSession.IsLoading)
            {
                TeamToolOwnershipSession.Refresh();
            }

            Refresh();
        }


        private void OnDisable()
        {
            if (exitButton != null)
                exitButton.onClick.RemoveListener(OnExit);

            if (leaveButton != null)
                leaveButton.onClick.RemoveListener(OnLeave);

            if (readyButton != null)
                readyButton.onClick.RemoveListener(OnReady);

            if (startButton != null)
                startButton.onClick.RemoveListener(OnStart);

            if (difficultyDropdown != null)
            {
                difficultyDropdown
                    .onValueChanged
                    .RemoveListener(
                        OnDifficultyChanged);
            }

            if (characterPreviousButton != null)
            {
                characterPreviousButton
                    .onClick
                    .RemoveListener(
                        OnCharacterPrevious);
            }

            if (characterNextButton != null)
            {
                characterNextButton
                    .onClick
                    .RemoveListener(
                        OnCharacterNext);
            }

            TeamToolOwnershipSession.Changed -=
                HandleOwnershipChanged;

            if (_subscribedLobby != null)
            {
                _subscribedLobby.OnRoomUpdated -=
                    HandleRoomUpdated;
            }

            _subscribedLobby = null;
        }


        private void ResolveServices()
        {
            if (bootstrap == null)
            {
                bootstrap =
                    NetworkBootstrap.Instance
                    ?? FindAnyObjectByType<
                        NetworkBootstrap>();
            }

            if (lobbyManager == null)
            {
                lobbyManager =
                    FindAnyObjectByType<
                        LobbyManager>();
            }
        }


        private void SyncLobbySubscription()
        {
            if (_subscribedLobby
                == lobbyManager)
            {
                return;
            }

            if (_subscribedLobby != null)
            {
                _subscribedLobby.OnRoomUpdated -=
                    HandleRoomUpdated;
            }

            _subscribedLobby =
                lobbyManager;

            if (_subscribedLobby != null)
            {
                _subscribedLobby.OnRoomUpdated +=
                    HandleRoomUpdated;
            }
        }


        private void HandleRoomUpdated(
            RoomInfoViewModel room)
        {
            _room =
                room ?? new RoomInfoViewModel();

            Refresh();
        }


        private void HandleOwnershipChanged()
        {
            Refresh();
        }


        private void Refresh()
        {
            ResolveServices();
            SyncLobbySubscription();

            if (lobbyManager != null)
            {
                _room =
                    lobbyManager.CurrentState
                    ?? new RoomInfoViewModel();
            }

            bool inRoom =
                lobbyManager != null
                && lobbyManager.IsInRoom;

            if (readyButton != null)
            {
                readyButton.interactable =
                    inRoom;

                var readyText =
                    readyButton
                        .GetComponentInChildren<
                            TMP_Text>(true);

                if (readyText != null)
                {
                    readyText.text =
                        _room.IsReady
                            ? "NOT READY"
                            : "READY";
                }
            }

            if (startButton != null)
            {
                startButton.interactable =
                    inRoom
                    && _room.IsHost
                    && _room.CanStartMatch;
            }

            if (leaveButton != null)
            {
                leaveButton.interactable =
                    inRoom;
            }

            if (exitButton != null)
            {
                exitButton.interactable =
                    !_exitInProgress;
            }

            LobbyPlayerState state = null;

            bool hasPlayer =
                inRoom
                && lobbyManager
                    .TryGetLocalPlayerState(
                        out state,
                        false)
                && state != null;

            bool canBrowseCharacter =
                hasPlayer
                && CharacterShopAssets.Items.Length
                    > 1;

            if (characterPreviousButton != null)
            {
                characterPreviousButton.interactable =
                    canBrowseCharacter;
            }

            if (characterNextButton != null)
            {
                characterNextButton.interactable =
                    canBrowseCharacter;
            }

            if (state != null)
            {
                RefreshCharacter(
                    state);
            }

            RefreshDifficulty(
                inRoom);
        }


        private void RefreshDifficulty(
            bool inRoom)
        {
            if (difficultyDropdown == null)
                return;

            var difficulty =
                MatchAuthorityRuntime.Instance != null
                    ? MatchAuthorityRuntime
                        .Instance
                        .Difficulty
                    : MatchDifficulty.Normal;

            difficultyDropdown
                .SetValueWithoutNotify(
                    (int)difficulty);

            difficultyDropdown
                .RefreshShownValue();

            difficultyDropdown.interactable =
                inRoom
                && _room.IsHost;
        }


        private void OnDifficultyChanged(
            int value)
        {
            ResolveServices();
            SyncLobbySubscription();

            if (bootstrap == null
                || lobbyManager == null
                || bootstrap.State
                    != NetworkSessionState.InLobby
                || !_room.IsHost)
            {
                RefreshDifficulty(
                    lobbyManager != null
                    && lobbyManager.IsInRoom);

                return;
            }

            var difficulty =
                (MatchDifficulty)Mathf.Clamp(
                    value,
                    (int)MatchDifficulty.Easy,
                    (int)MatchDifficulty.Hard);

            MatchAuthorityRuntime
                .EnsureExists(bootstrap)
                .RequestDifficulty(
                    difficulty);
            LobbySelectionMemory.Write("difficulty", (int)difficulty);
        }


        private void OnReady()
        {
            if (lobbyManager == null
                || !lobbyManager.IsInRoom)
            {
                return;
            }

            lobbyManager.SetReady(
                !_room.IsReady);
        }


        private void OnStart()
        {
            if (lobbyManager == null
                || !_room.IsHost
                || !_room.CanStartMatch)
            {
                return;
            }

            lobbyManager.TryStartMatch();
        }


        private void OnLeave()
        {
            if (lobbyManager == null
                || !lobbyManager.IsInRoom)
            {
                return;
            }

            lobbyManager.LeaveRoom();
        }


        private async void OnExit()
        {
            if (_exitInProgress)
                return;

            _exitInProgress = true;

            Refresh();

            try
            {
                ResolveServices();

                if (bootstrap != null)
                {
                    await bootstrap
                        .ShutdownRunnerAsync();
                }

                GameUIFeedback.LoadScene(
                    GameConstants.SceneMainMenu);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[Lobby] Exit failed: "
                    + exception);

                _exitInProgress =
                    false;

                Refresh();
            }
        }


        private void OnCharacterPrevious()
        {
            CycleCharacter(-1);
        }


        private void OnCharacterNext()
        {
            CycleCharacter(1);
        }


        private void CycleCharacter(
            int direction)
        {
            ResolveServices();
            SyncLobbySubscription();

            if (lobbyManager == null
                || !lobbyManager
                    .TryGetLocalPlayerState(
                        out var state,
                        false)
                || state == null)
            {
                return;
            }

            EnsureCharacterPreview(
                state);

            var items =
                CharacterShopAssets.Items;

            if (items == null
                || items.Length == 0)
            {
                return;
            }

            int currentIndex = 0;

            for (int index = 0;
                 index < items.Length;
                 index++)
            {
                if (items[index].CharacterId
                    == _previewCharacterId)
                {
                    currentIndex =
                        index;

                    break;
                }
            }

            int nextIndex =
                (
                    currentIndex
                    + direction
                    + items.Length
                )
                % items.Length;

            _previewCharacterId =
                items[nextIndex]
                    .CharacterId;

            bool owned =
                CharacterShopAssets.IsOwned(
                    _previewCharacterId);

            if (owned
                && !state.IsReady
                && state.CharacterId
                    != _previewCharacterId)
            {
                state.RequestCharacter(
                    _previewCharacterId);
            }

            RefreshCharacter(
                state);
        }


        private void EnsureCharacterPreview(
            LobbyPlayerState state)
        {
            if (state == null)
                return;

            if (_previewCharacterId < 0)
            {
                _previewCharacterId =
                    state.CharacterId;
            }
            else if (
                _lastEquippedCharacterId >= 0
                && state.CharacterId
                    != _lastEquippedCharacterId
                && _previewCharacterId
                    == _lastEquippedCharacterId)
            {
                _previewCharacterId =
                    state.CharacterId;
            }

            _lastEquippedCharacterId =
                state.CharacterId;
        }


        private void RefreshCharacter(
            LobbyPlayerState state)
        {
            EnsureCharacterPreview(
                state);

            int previewId =
                PreviewCharacterId;

            CharacterShopAssets.Item item;

            if (!CharacterShopAssets
                    .TryGetByCharacterId(
                        previewId,
                        out item))
            {
                return;
            }

            bool owned =
                CharacterShopAssets.IsOwned(
                    previewId);

            if (characterNameText != null)
            {
                characterNameText.text =
                    item.Name
                    + (
                        owned
                            ? string.Empty
                            : " // LOCKED"
                    );
            }

            if (characterPortrait != null)
            {
                string resource =
                    previewId
                        == CharacterShopAssets
                            .JammoCharacterId
                        ? "Shop/Characters/jammo"
                        : "Shop/Characters/astronaut";

                var texture =
                    Resources.Load<Texture2D>(
                        resource);

                characterPortrait.texture =
                    texture;

                characterPortrait.color =
                    texture == null
                        ? Color.clear
                        : (
                            owned
                                ? characterOwnedTint
                                : characterLockedTint
                        );
            }

            if (characterLockOverlay != null)
            {
                characterLockOverlay.SetActive(
                    !owned);
            }

            if (characterLockText != null)
            {
                characterLockText.text =
                    "LOCKED";
            }
        }
    }
}
