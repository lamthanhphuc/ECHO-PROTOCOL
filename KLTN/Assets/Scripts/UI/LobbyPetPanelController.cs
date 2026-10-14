using EchoProtocol.Networking;
using EchoProtocol.Pets;
using EchoProtocol.UI.MainMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI
{
    [DisallowMultipleComponent]
    public sealed class LobbyPetPanelController : MonoBehaviour
    {
        [Header("Navigation")]
        [SerializeField] private Button petButton;
        [SerializeField] private Button backButton;
        [SerializeField] private GameObject preMatchContent;
        [SerializeField] private GameObject petPanel;

        [Header("Pet Preview")]
        [SerializeField] private Button previousPetButton;
        [SerializeField] private Button nextPetButton;
        [SerializeField] private RawImage petThumbnail;
        [SerializeField] private TMP_Text petNameText;
        [SerializeField] private TMP_Text petStateText;
        [SerializeField] private GameObject petLockOverlay;
        [SerializeField] private TMP_Text petLockText;

        [Header("Pet Skin Cards")]
        [SerializeField] private Button[] skinButtons =
            new Button[PetSkinLibrary.SkinCount];

        [SerializeField] private RawImage[] skinThumbnails =
            new RawImage[PetSkinLibrary.SkinCount];

        [SerializeField] private TMP_Text[] skinNameTexts =
            new TMP_Text[PetSkinLibrary.SkinCount];

        [SerializeField] private TMP_Text[] skinStateTexts =
            new TMP_Text[PetSkinLibrary.SkinCount];

        [SerializeField] private GameObject[] skinLockOverlays =
            new GameObject[PetSkinLibrary.SkinCount];

        [SerializeField] private TMP_Text[] skinLockTexts =
            new TMP_Text[PetSkinLibrary.SkinCount];

        [SerializeField] private GameObject[] skinSelectedBadges =
            new GameObject[PetSkinLibrary.SkinCount];

        [Header("Details")]
        [SerializeField] private TMP_Text selectedPetSkinName;
        [SerializeField] private TMP_Text selectedPetSkinState;
        [SerializeField] private TMP_Text selectedPetSkinDescription;

        [Header("Runtime")]
        [SerializeField] private LobbyManager lobbyManager;

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

        private int _previewPetId = -1;
        private int _previewSkinId;
        private int _lastEquippedPetId = -1;


        public void Configure(
            Button openButton,
            Button back,
            GameObject preMatch,
            GameObject panel,
            Button previousPet,
            Button nextPet,
            RawImage petImage,
            TMP_Text petName,
            TMP_Text petState,
            GameObject petLock,
            TMP_Text petLockLabel,
            Button[] cards,
            RawImage[] thumbnails,
            TMP_Text[] names,
            TMP_Text[] states,
            GameObject[] locks,
            TMP_Text[] lockLabels,
            GameObject[] selectedBadges,
            TMP_Text detailName,
            TMP_Text detailState,
            TMP_Text detailDescription)
        {
            petButton = openButton;
            backButton = back;
            preMatchContent = preMatch;
            petPanel = panel;

            previousPetButton =
                previousPet;

            nextPetButton =
                nextPet;

            petThumbnail =
                petImage;

            petNameText =
                petName;

            petStateText =
                petState;

            petLockOverlay =
                petLock;

            petLockText =
                petLockLabel;

            skinButtons =
                cards;

            skinThumbnails =
                thumbnails;

            skinNameTexts =
                names;

            skinStateTexts =
                states;

            skinLockOverlays =
                locks;

            skinLockTexts =
                lockLabels;

            skinSelectedBadges =
                selectedBadges;

            selectedPetSkinName =
                detailName;

            selectedPetSkinState =
                detailState;

            selectedPetSkinDescription =
                detailDescription;
        }


        private void OnEnable()
        {
            if (petButton != null)
            {
                petButton
                    .onClick
                    .AddListener(Open);
            }

            if (backButton != null)
            {
                backButton
                    .onClick
                    .AddListener(Close);
            }

            if (previousPetButton != null)
            {
                previousPetButton
                    .onClick
                    .AddListener(
                        OnPreviousPet);
            }

            if (nextPetButton != null)
            {
                nextPetButton
                    .onClick
                    .AddListener(
                        OnNextPet);
            }

            BindSkinListeners();

            TeamToolOwnershipSession.Changed +=
                HandleOwnershipChanged;

            ResolveLobbyManager();
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
            if (petButton != null)
            {
                petButton
                    .onClick
                    .RemoveListener(Open);
            }

            if (backButton != null)
            {
                backButton
                    .onClick
                    .RemoveListener(Close);
            }

            if (previousPetButton != null)
            {
                previousPetButton
                    .onClick
                    .RemoveListener(
                        OnPreviousPet);
            }

            if (nextPetButton != null)
            {
                nextPetButton
                    .onClick
                    .RemoveListener(
                        OnNextPet);
            }

            UnbindSkinListeners();

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


        private void BindSkinListeners()
        {
            if (skinButtons == null)
                return;

            if (skinButtons.Length > 0
                && skinButtons[0] != null)
            {
                skinButtons[0]
                    .onClick
                    .AddListener(OnSkin0);
            }

            if (skinButtons.Length > 1
                && skinButtons[1] != null)
            {
                skinButtons[1]
                    .onClick
                    .AddListener(OnSkin1);
            }

            if (skinButtons.Length > 2
                && skinButtons[2] != null)
            {
                skinButtons[2]
                    .onClick
                    .AddListener(OnSkin2);
            }

            if (skinButtons.Length > 3
                && skinButtons[3] != null)
            {
                skinButtons[3]
                    .onClick
                    .AddListener(OnSkin3);
            }
        }


        private void UnbindSkinListeners()
        {
            if (skinButtons == null)
                return;

            if (skinButtons.Length > 0
                && skinButtons[0] != null)
            {
                skinButtons[0]
                    .onClick
                    .RemoveListener(OnSkin0);
            }

            if (skinButtons.Length > 1
                && skinButtons[1] != null)
            {
                skinButtons[1]
                    .onClick
                    .RemoveListener(OnSkin1);
            }

            if (skinButtons.Length > 2
                && skinButtons[2] != null)
            {
                skinButtons[2]
                    .onClick
                    .RemoveListener(OnSkin2);
            }

            if (skinButtons.Length > 3
                && skinButtons[3] != null)
            {
                skinButtons[3]
                    .onClick
                    .RemoveListener(OnSkin3);
            }
        }


        private void OnPreviousPet()
        {
            CyclePet(-1);
        }


        private void OnNextPet()
        {
            CyclePet(1);
        }


        private void OnSkin0()
        {
            SelectSkin(0);
        }


        private void OnSkin1()
        {
            SelectSkin(1);
        }


        private void OnSkin2()
        {
            SelectSkin(2);
        }


        private void OnSkin3()
        {
            SelectSkin(3);
        }


        private void CyclePet(
            int direction)
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

            EnsurePetPreview(
                state);

            var pets =
                PetShopAssets.Pets;

            if (pets == null
                || pets.Length == 0)
            {
                return;
            }

            int currentIndex = 0;

            for (int index = 0;
                 index < pets.Length;
                 index++)
            {
                if (pets[index].PetId
                    == _previewPetId)
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
                    + pets.Length
                )
                % pets.Length;

            _previewPetId =
                pets[nextIndex].PetId;

            // Changing pet always changes the
            // skin grid to that pet's default skin.
            _previewSkinId = 0;

            bool owned =
                PetShopAssets.IsPetOwned(
                    _previewPetId);

            if (owned
                && !state.IsReady
                && state.PetId
                    != _previewPetId)
            {
                state.RequestPet(
                    _previewPetId);
            }

            Refresh();
        }


        private void SelectSkin(
            int skinId)
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

            EnsurePetPreview(
                state);

            _previewSkinId =
                Mathf.Clamp(
                    skinId,
                    0,
                    PetSkinLibrary.SkinCount - 1);

            bool owned =
                PetShopAssets.IsSkinOwned(
                    _previewPetId,
                    _previewSkinId);

            bool canEquip =
                owned
                && !state.IsReady
                && state.PetId
                    == _previewPetId;

            if (canEquip
                && state.PetSkinId
                    != _previewSkinId)
            {
                state.RequestPetSkin(
                    _previewSkinId);
            }

            Refresh();
        }


        private void EnsurePetPreview(
            LobbyPlayerState state)
        {
            if (state == null)
                return;

            int fallbackPetId =
                PetShopAssets.Pets.Length > 0
                    ? PetShopAssets.Pets[0].PetId
                    : 1;

            if (_previewPetId < 1)
            {
                _previewPetId =
                    state.PetId > 0
                        ? state.PetId
                        : fallbackPetId;

                _previewSkinId =
                    state.PetId
                        == _previewPetId
                        ? state.PetSkinId
                        : 0;
            }
            else if (
                _lastEquippedPetId > 0
                && state.PetId
                    != _lastEquippedPetId
                && _previewPetId
                    == _lastEquippedPetId)
            {
                _previewPetId =
                    state.PetId > 0
                        ? state.PetId
                        : fallbackPetId;

                _previewSkinId =
                    state.PetId > 0
                        ? state.PetSkinId
                        : 0;
            }

            _lastEquippedPetId =
                state.PetId;

            _previewSkinId =
                Mathf.Clamp(
                    _previewSkinId,
                    0,
                    PetSkinLibrary.SkinCount - 1);
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

            if (petButton != null)
            {
                petButton.interactable =
                    hasPlayer;
            }

            if (previousPetButton != null)
            {
                previousPetButton.interactable =
                    hasPlayer;
            }

            if (nextPetButton != null)
            {
                nextPetButton.interactable =
                    hasPlayer;
            }

            if (!hasPlayer)
                return;

            EnsurePetPreview(
                state);

            PetShopAssets.PetItem pet;

            if (!PetShopAssets.TryGetPet(
                    _previewPetId,
                    out pet))
            {
                return;
            }

            bool petOwned =
                PetShopAssets.IsPetOwned(
                    _previewPetId);

            bool petEquipped =
                state.PetId
                    == _previewPetId;

            if (petThumbnail != null)
            {
                string resource =
                    LobbyVariantThumbnailAssets
                        .GetPetSkinResource(
                            _previewPetId,
                            _previewSkinId);

                if (string.IsNullOrWhiteSpace(
                        resource))
                {
                    resource =
                        PetShopThumbnailAssets
                            .GetThumbnailResource(
                                _previewPetId);
                }

                var texture =
                    Resources.Load<Texture2D>(
                        resource);

                petThumbnail.texture =
                    texture;

                petThumbnail.color =
                    texture == null
                        ? Color.clear
                        : (
                            petOwned
                                ? ownedTint
                                : lockedTint
                        );
            }

            if (petNameText != null)
            {
                petNameText.text =
                    pet.Name;
            }

            if (petStateText != null)
            {
                if (!petOwned)
                {
                    petStateText.text =
                        "LOCKED // AVAILABLE IN STORE";
                }
                else if (petEquipped)
                {
                    petStateText.text =
                        "EQUIPPED";
                }
                else
                {
                    petStateText.text =
                        "OWNED // PREVIEW";
                }
            }

            if (petLockOverlay != null)
            {
                petLockOverlay.SetActive(
                    !petOwned);
            }

            if (petLockText != null)
            {
                petLockText.text =
                    "LOCKED";
            }

            for (int index = 0;
                 index < PetSkinLibrary.SkinCount;
                 index++)
            {
                PetShopAssets.SkinItem skin;

                bool hasSkin =
                    PetShopAssets.TryGetSkin(
                        _previewPetId,
                        index,
                        out skin);

                bool owned =
                    hasSkin
                    && PetShopAssets.IsSkinOwned(
                        _previewPetId,
                        index);

                bool equipped =
                    state.PetId
                        == _previewPetId
                    && state.PetSkinId
                        == index;

                if (skinButtons != null
                    && index < skinButtons.Length
                    && skinButtons[index] != null)
                {
                    // Locked skins remain browseable.
                    skinButtons[index]
                        .interactable =
                        hasSkin;
                }

                if (skinNameTexts != null
                    && index < skinNameTexts.Length
                    && skinNameTexts[index] != null)
                {
                    skinNameTexts[index].text =
                        hasSkin
                            ? skin.Name
                            : "SKIN";
                }

                if (skinThumbnails != null
                    && index < skinThumbnails.Length
                    && skinThumbnails[index] != null)
                {
                    string resource =
                        LobbyVariantThumbnailAssets
                            .GetPetSkinResource(
                                _previewPetId,
                                index);

                    var texture =
                        Resources.Load<Texture2D>(
                            resource);

                    skinThumbnails[index]
                        .texture =
                        texture;

                    skinThumbnails[index]
                        .color =
                        texture == null
                            ? Color.clear
                            : (
                                owned
                                    ? ownedTint
                                    : lockedTint
                            );
                }

                if (skinStateTexts != null
                    && index < skinStateTexts.Length
                    && skinStateTexts[index] != null)
                {
                    skinStateTexts[index].text =
                        !owned
                            ? "LOCKED"
                            : (
                                equipped
                                    ? "EQUIPPED"
                                    : "OWNED"
                            );
                }

                if (skinLockOverlays != null
                    && index < skinLockOverlays.Length
                    && skinLockOverlays[index] != null)
                {
                    skinLockOverlays[index]
                        .SetActive(
                            !owned);
                }

                if (skinLockTexts != null
                    && index < skinLockTexts.Length
                    && skinLockTexts[index] != null)
                {
                    skinLockTexts[index].text =
                        "LOCKED";
                }

                if (skinSelectedBadges != null
                    && index < skinSelectedBadges.Length
                    && skinSelectedBadges[index] != null)
                {
                    skinSelectedBadges[index]
                        .SetActive(
                            _previewSkinId
                                == index);
                }
            }

            PetShopAssets.SkinItem previewSkin;

            bool hasPreviewSkin =
                PetShopAssets.TryGetSkin(
                    _previewPetId,
                    _previewSkinId,
                    out previewSkin);

            bool previewSkinOwned =
                hasPreviewSkin
                && PetShopAssets.IsSkinOwned(
                    _previewPetId,
                    _previewSkinId);

            bool previewSkinEquipped =
                state.PetId
                    == _previewPetId
                && state.PetSkinId
                    == _previewSkinId;

            if (selectedPetSkinName != null)
            {
                selectedPetSkinName.text =
                    pet.Name
                    + " // "
                    + (
                        hasPreviewSkin
                            ? previewSkin.Name
                            : "SKIN"
                    );
            }

            if (selectedPetSkinState != null)
            {
                if (!petOwned
                    || !previewSkinOwned)
                {
                    selectedPetSkinState.text =
                        "LOCKED // AVAILABLE IN STORE";
                }
                else if (previewSkinEquipped)
                {
                    selectedPetSkinState.text =
                        _previewSkinId == 0
                            ? "DEFAULT PET OUTFIT"
                            : "EQUIPPED PET OUTFIT";
                }
                else
                {
                    selectedPetSkinState.text =
                        "OWNED // SELECT TO EQUIP";
                }
            }

            if (selectedPetSkinDescription != null)
            {
                selectedPetSkinDescription.text =
                    pet.Description;
            }
        }


        public void Open()
        {
            ResolveLobbyManager();

            _previewPetId = -1;
            _previewSkinId = 0;
            _lastEquippedPetId = -1;

            if (lobbyManager != null
                && lobbyManager
                    .TryGetLocalPlayerState(
                        out var state,
                        false)
                && state != null)
            {
                EnsurePetPreview(
                    state);
            }

            if (preMatchContent != null)
            {
                preMatchContent
                    .SetActive(false);
            }

            if (petPanel != null)
            {
                petPanel
                    .SetActive(true);
            }

            Refresh();
        }


        public void Close()
        {
            if (petPanel != null)
            {
                petPanel
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