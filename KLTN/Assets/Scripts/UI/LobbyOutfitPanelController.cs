using EchoProtocol.Networking;
using EchoProtocol.UI.MainMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI
{
    [DisallowMultipleComponent]
    public sealed class LobbyOutfitPanelController : MonoBehaviour
    {
        [Header("Navigation")]
        [SerializeField] private Button outfitButton;
        [SerializeField] private Button backButton;
        [SerializeField] private GameObject preMatchContent;
        [SerializeField] private GameObject outfitPanel;

        [Header("Runtime")]
        [SerializeField] private LobbyManager lobbyManager;
        [SerializeField] private LobbyRuntimeBindingsController runtimeBindings;

        [Header("Outfit Cards")]
        [SerializeField] private Button[] outfitButtons =
            new Button[
                CharacterOutfitLibrary.OutfitCount];

        [SerializeField] private RawImage[] outfitThumbnails =
            new RawImage[
                CharacterOutfitLibrary.OutfitCount];

        [SerializeField] private TMP_Text[] outfitNameTexts =
            new TMP_Text[
                CharacterOutfitLibrary.OutfitCount];

        [SerializeField] private GameObject defaultSelectedBadge;

        [SerializeField] private GameObject[] lockOverlays =
            new GameObject[
                CharacterOutfitLibrary.OutfitCount];

        [SerializeField] private TMP_Text[] lockTexts =
            new TMP_Text[
                CharacterOutfitLibrary.OutfitCount];

        [SerializeField] private GameObject[] selectedBadges =
            new GameObject[
                CharacterOutfitLibrary.OutfitCount];

        [Header("Details")]
        [SerializeField] private TMP_Text selectedOutfitName;
        [SerializeField] private TMP_Text selectedOutfitState;

        [Header("Locked Preview")]
        [SerializeField] private Color ownedTint =
            Color.white;

        [SerializeField] private Color lockedTint =
            new Color(
                0.34f,
                0.34f,
                0.34f,
                0.72f);

        private LobbyManager _subscribedLobby;

        private int _previewOutfitId = -1;
        private int _previewCharacterId = -1;


        public void Configure(
            Button outfit,
            Button back,
            GameObject preMatch,
            GameObject panel,
            Button[] cards,
            RawImage[] thumbnails,
            TMP_Text[] names,
            GameObject selectedBadge,
            TMP_Text selectedName,
            TMP_Text selectedState)
        {
            outfitButton = outfit;
            backButton = back;
            preMatchContent = preMatch;
            outfitPanel = panel;

            outfitButtons = cards;
            outfitThumbnails = thumbnails;
            outfitNameTexts = names;

            defaultSelectedBadge =
                selectedBadge;

            selectedOutfitName =
                selectedName;

            selectedOutfitState =
                selectedState;
        }


        public void ConfigureLockedPreview(
            LobbyRuntimeBindingsController runtime,
            GameObject[] locks,
            TMP_Text[] lockLabels,
            GameObject[] selectionBadges)
        {
            runtimeBindings =
                runtime;

            lockOverlays =
                locks;

            lockTexts =
                lockLabels;

            selectedBadges =
                selectionBadges;
        }


        private void OnEnable()
        {
            if (outfitButton != null)
            {
                outfitButton
                    .onClick
                    .AddListener(Open);
            }

            if (backButton != null)
            {
                backButton
                    .onClick
                    .AddListener(Close);
            }

            BindCardListeners();

            TeamToolOwnershipSession.Changed +=
                HandleOwnershipChanged;

            ResolveLobbyManager();
            SyncLobbySubscription();
            Refresh();
        }


        private void OnDisable()
        {
            if (outfitButton != null)
            {
                outfitButton
                    .onClick
                    .RemoveListener(Open);
            }

            if (backButton != null)
            {
                backButton
                    .onClick
                    .RemoveListener(Close);
            }

            UnbindCardListeners();

            TeamToolOwnershipSession.Changed -=
                HandleOwnershipChanged;

            if (_subscribedLobby != null)
            {
                _subscribedLobby.OnRoomUpdated -=
                    HandleRoomUpdated;
            }

            _subscribedLobby =
                null;
        }


        private void ResolveLobbyManager()
        {
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
            Refresh();
        }


        private void HandleOwnershipChanged()
        {
            Refresh();
        }


        private void BindCardListeners()
        {
            if (outfitButtons == null)
                return;

            if (outfitButtons.Length > 0
                && outfitButtons[0] != null)
            {
                outfitButtons[0]
                    .onClick
                    .AddListener(OnOutfit0);
            }

            if (outfitButtons.Length > 1
                && outfitButtons[1] != null)
            {
                outfitButtons[1]
                    .onClick
                    .AddListener(OnOutfit1);
            }

            if (outfitButtons.Length > 2
                && outfitButtons[2] != null)
            {
                outfitButtons[2]
                    .onClick
                    .AddListener(OnOutfit2);
            }

            if (outfitButtons.Length > 3
                && outfitButtons[3] != null)
            {
                outfitButtons[3]
                    .onClick
                    .AddListener(OnOutfit3);
            }
        }


        private void UnbindCardListeners()
        {
            if (outfitButtons == null)
                return;

            if (outfitButtons.Length > 0
                && outfitButtons[0] != null)
            {
                outfitButtons[0]
                    .onClick
                    .RemoveListener(OnOutfit0);
            }

            if (outfitButtons.Length > 1
                && outfitButtons[1] != null)
            {
                outfitButtons[1]
                    .onClick
                    .RemoveListener(OnOutfit1);
            }

            if (outfitButtons.Length > 2
                && outfitButtons[2] != null)
            {
                outfitButtons[2]
                    .onClick
                    .RemoveListener(OnOutfit2);
            }

            if (outfitButtons.Length > 3
                && outfitButtons[3] != null)
            {
                outfitButtons[3]
                    .onClick
                    .RemoveListener(OnOutfit3);
            }
        }


        private void OnOutfit0()
        {
            SelectOutfit(0);
        }


        private void OnOutfit1()
        {
            SelectOutfit(1);
        }


        private void OnOutfit2()
        {
            SelectOutfit(2);
        }


        private void OnOutfit3()
        {
            SelectOutfit(3);
        }


        private void SelectOutfit(
            int outfitId)
        {
            ResolveLobbyManager();
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

            int characterId =
                GetPreviewCharacterId(
                    state);

            _previewOutfitId =
                Mathf.Clamp(
                    outfitId,
                    0,
                    CharacterOutfitLibrary
                        .OutfitCount - 1);

            _previewCharacterId =
                characterId;

            bool owned =
                CharacterOutfitShopAssets.IsOwned(
                    characterId,
                    _previewOutfitId);

            bool canEquip =
                owned
                && !state.IsReady
                && state.CharacterId
                    == characterId;

            if (canEquip
                && state.CharacterOutfitId
                    != _previewOutfitId)
            {
                state.RequestCharacterOutfit(
                    _previewOutfitId);
            }

            Refresh();
        }


        private int GetPreviewCharacterId(
            LobbyPlayerState state)
        {
            if (runtimeBindings != null)
            {
                return runtimeBindings
                    .PreviewCharacterId;
            }

            return state != null
                ? state.CharacterId
                : CharacterShopAssets
                    .AstronautCharacterId;
        }


        private void EnsurePreview(
            LobbyPlayerState state)
        {
            int characterId =
                GetPreviewCharacterId(
                    state);

            if (_previewCharacterId
                != characterId)
            {
                _previewCharacterId =
                    characterId;

                _previewOutfitId =
                    state != null
                    && state.CharacterId
                        == characterId
                        ? state.CharacterOutfitId
                        : 0;
            }

            if (_previewOutfitId < 0)
            {
                _previewOutfitId =
                    state != null
                    && state.CharacterId
                        == characterId
                        ? state.CharacterOutfitId
                        : 0;
            }

            _previewOutfitId =
                Mathf.Clamp(
                    _previewOutfitId,
                    0,
                    CharacterOutfitLibrary
                        .OutfitCount - 1);
        }


        private void Refresh()
        {
            ResolveLobbyManager();
            SyncLobbySubscription();

            LobbyPlayerState state = null;

            bool hasPlayer =
                lobbyManager != null
                && lobbyManager.IsInRoom
                && lobbyManager
                    .TryGetLocalPlayerState(
                        out state,
                        false)
                && state != null;

            if (outfitButton != null)
            {
                outfitButton.interactable =
                    hasPlayer;
            }

            if (!hasPlayer)
                return;

            EnsurePreview(
                state);

            int characterId =
                _previewCharacterId;

            for (int index = 0;
                 index
                    < CharacterOutfitLibrary
                        .OutfitCount;
                 index++)
            {
                bool owned =
                    CharacterOutfitShopAssets.IsOwned(
                        characterId,
                        index);

                if (outfitNameTexts != null
                    && index
                        < outfitNameTexts.Length
                    && outfitNameTexts[index]
                        != null)
                {
                    outfitNameTexts[index].text =
                        CharacterOutfitLibrary
                            .GetName(
                                characterId,
                                index);
                }

                if (outfitThumbnails != null
                    && index
                        < outfitThumbnails.Length
                    && outfitThumbnails[index]
                        != null)
                {
                    string resource =
                        LobbyVariantThumbnailAssets
                            .GetCharacterOutfitResource(
                                characterId,
                                index);

                    var texture =
                        Resources.Load<Texture2D>(
                            resource);

                    outfitThumbnails[index]
                        .texture =
                        texture;

                    outfitThumbnails[index]
                        .color =
                        texture == null
                            ? Color.clear
                            : (
                                owned
                                    ? ownedTint
                                    : lockedTint
                            );
                }

                if (outfitButtons != null
                    && index
                        < outfitButtons.Length
                    && outfitButtons[index]
                        != null)
                {
                    // Preview remains clickable
                    // whether owned or locked.
                    outfitButtons[index]
                        .interactable =
                        true;
                }

                if (lockOverlays != null
                    && index
                        < lockOverlays.Length
                    && lockOverlays[index]
                        != null)
                {
                    lockOverlays[index]
                        .SetActive(
                            !owned);
                }

                if (lockTexts != null
                    && index
                        < lockTexts.Length
                    && lockTexts[index]
                        != null)
                {
                    lockTexts[index].text =
                        "LOCKED";
                }

                if (selectedBadges != null
                    && index
                        < selectedBadges.Length
                    && selectedBadges[index]
                        != null)
                {
                    selectedBadges[index]
                        .SetActive(
                            _previewOutfitId
                                == index);
                }
            }

            if (defaultSelectedBadge != null)
            {
                defaultSelectedBadge.SetActive(
                    false);
            }

            CharacterShopAssets.Item character;

            string characterName =
                CharacterShopAssets
                    .TryGetByCharacterId(
                        characterId,
                        out character)
                    ? character.Name
                    : "CHARACTER";

            string outfitName =
                CharacterOutfitLibrary.GetName(
                    characterId,
                    _previewOutfitId);

            bool previewOwned =
                CharacterOutfitShopAssets.IsOwned(
                    characterId,
                    _previewOutfitId);

            bool equipped =
                state.CharacterId
                    == characterId
                && state.CharacterOutfitId
                    == _previewOutfitId;

            if (selectedOutfitName != null)
            {
                selectedOutfitName.text =
                    characterName
                    + " // "
                    + outfitName;
            }

            if (selectedOutfitState != null)
            {
                if (!previewOwned)
                {
                    selectedOutfitState.text =
                        "LOCKED // AVAILABLE IN STORE";
                }
                else if (equipped)
                {
                    selectedOutfitState.text =
                        _previewOutfitId == 0
                            ? "DEFAULT OUTFIT"
                            : "EQUIPPED OUTFIT";
                }
                else
                {
                    selectedOutfitState.text =
                        "OWNED // SELECT TO EQUIP";
                }
            }
        }


        public void Open()
        {
            ResolveLobbyManager();

            if (lobbyManager != null
                && lobbyManager
                    .TryGetLocalPlayerState(
                        out var state,
                        false)
                && state != null)
            {
                _previewCharacterId =
                    GetPreviewCharacterId(
                        state);

                _previewOutfitId =
                    state.CharacterId
                        == _previewCharacterId
                        ? state.CharacterOutfitId
                        : 0;
            }

            if (preMatchContent != null)
            {
                preMatchContent
                    .SetActive(false);
            }

            if (outfitPanel != null)
            {
                outfitPanel
                    .SetActive(true);
            }

            Refresh();
        }


        public void Close()
        {
            if (outfitPanel != null)
            {
                outfitPanel
                    .SetActive(false);
            }

            if (preMatchContent != null)
            {
                preMatchContent
                    .SetActive(true);
            }
        }
    }
}