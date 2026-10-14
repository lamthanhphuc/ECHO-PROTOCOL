using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.MainMenu
{
    /// <summary>
    /// Store V2 runtime binding layer.
    ///
    /// Visual authority lives in StoreV2Popup.prefab. This component only:
    /// - shows/hides the prefab-authored Pet variant panel,
    /// - instantiates variant cards from the prefab template,
    /// - binds thumbnail data and click listeners,
    /// - resynchronizes the Team Tool preview after category changes.
    ///
    /// It must not author colors, outlines, card geometry or grid layout.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StoreV2ThumbnailOnlyPresenter : MonoBehaviour
    {
        private PetShopController _petController;

        private RectTransform _petVariantPanel;
        private RectTransform _petVariantGrid;

        private int _builtPetId = -999;
        private float _nextRefresh;

        private bool _teamToolWasActive;
        private Coroutine _teamPreviewRoutine;

        private void OnEnable()
        {
            _builtPetId = -999;
            _nextRefresh = 0f;

            ResolvePetController();
            ResolveVariantUi();
        }

        private void OnDisable()
        {
            if (_teamPreviewRoutine != null)
            {
                StopCoroutine(
                    _teamPreviewRoutine);

                _teamPreviewRoutine = null;
            }
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh)
                return;

            _nextRefresh =
                Time.unscaledTime + 0.15f;

            ApplyNow();
        }

        public void ApplyNow()
        {
            RefreshPetVariants();
            RefreshTeamToolPreview();
        }

        private void ResolvePetController()
        {
            _petController =
                GetComponent<PetShopController>();
        }

        private void ResolveVariantUi()
        {
            _petVariantPanel =
                transform.Find(
                    "Window/PetVariantPanel")
                as RectTransform;

            _petVariantGrid =
                _petVariantPanel != null
                    ? _petVariantPanel.Find("Grid")
                        as RectTransform
                    : null;
        }

        private void RefreshPetVariants()
        {
            if (_petController == null)
                ResolvePetController();

            if (_petVariantPanel == null
                || _petVariantGrid == null)
            {
                ResolveVariantUi();
            }

            if (_petController == null
                || _petVariantPanel == null
                || _petVariantGrid == null)
            {
                return;
            }

            RectTransform petRoot =
                transform.Find(
                    "Window/Content/PetContent")
                as RectTransform;

            bool petVisible =
                petRoot != null
                && petRoot.gameObject.activeInHierarchy;

            _petVariantPanel.gameObject.SetActive(
                petVisible);

            if (!petVisible)
                return;

            int petId =
                _petController.SelectedPetId;

            if (petId < 1 || petId > 4)
            {
                ClearChildren(
                    _petVariantGrid);

                _builtPetId =
                    petId;

                return;
            }

            if (petId == _builtPetId)
                return;

            BuildPetVariantCards(
                petId);

            _builtPetId =
                petId;
        }

        private void BuildPetVariantCards(
            int petId)
        {
            ClearChildren(
                _petVariantGrid);

            int selectedSkin =
                _petController != null
                    ? _petController.SelectedSkinId
                    : 0;

            for (int skinId = 1;
                 skinId < 4;
                 skinId++)
            {
                string resource =
                    "Shop/LobbyVariants/Pets/"
                    + "pet-"
                    + petId
                    + "-skin-"
                    + skinId;

                Texture2D texture =
                    Resources.Load<Texture2D>(
                        resource);

                if (texture == null)
                {
                    Debug.LogWarning(
                        "[StoreV2] Missing Pet outfit thumbnail: "
                        + resource,
                        this);

                    continue;
                }

                int capturedSkinId =
                    skinId;

                GameObject card =
                    StoreV2PrefabCardFactory
                        .CreateVariantCard(
                            transform,
                            _petVariantGrid,
                            "PetVariant_"
                            + skinId,
                            texture,
                            this);

                if (card == null)
                    continue;

                Button button =
                    card.GetComponent<Button>();

                button.onClick.AddListener(
                    () =>
                        InvokePetSkin(
                            capturedSkinId,
                            button));

                if (skinId == selectedSkin)
                {
                    button.Select();
                }
            }
        }

        private void InvokePetSkin(
            int skinId,
            Button sourceButton)
        {
            if (_petController == null)
                ResolvePetController();

            if (_petController == null)
                return;

            if (!_petController.TrySelectSkinFromVariant(
                    skinId))
            {
                return;
            }

            if (sourceButton != null)
                sourceButton.Select();

            Debug.Log(
                "[StoreV2] PET OUTFIT => "
                + _petController.SelectedPetId
                + ":"
                + skinId,
                this);
        }

        private void RefreshTeamToolPreview()
        {
            RectTransform tools =
                transform.Find(
                    "Window/Content/TeamTools")
                as RectTransform;

            bool active =
                tools != null
                && tools.gameObject.activeInHierarchy;

            if (active
                && !_teamToolWasActive)
            {
                if (_teamPreviewRoutine != null)
                {
                    StopCoroutine(
                        _teamPreviewRoutine);
                }

                _teamPreviewRoutine =
                    StartCoroutine(
                        ReselectFirstTeamTool());
            }

            _teamToolWasActive =
                active;
        }

        private IEnumerator ReselectFirstTeamTool()
        {
            yield return null;
            yield return null;
            if(StoreReferencePresentation.IsAwaitingSelection(transform)){_teamPreviewRoutine=null;yield break;}

            RectTransform grid =
                transform.Find(
                    "Window/Content/TeamTools/ItemGrid")
                as RectTransform;

            if (grid == null)
            {
                _teamPreviewRoutine = null;
                yield break;
            }

            if (!grid.gameObject.activeInHierarchy) { _teamPreviewRoutine=null; yield break; }
            foreach (var button in grid.GetComponentsInChildren<Button>(false)) {
                if(!button.interactable)continue;
                button.onClick.Invoke();
                break;
            }

            _teamPreviewRoutine = null;
        }

        private static void ClearChildren(
            Transform parent)
        {
            if (parent == null)
                return;

            for (int i = parent.childCount - 1;
                 i >= 0;
                 i--)
            {
                GameObject child =
                    parent.GetChild(i)
                        .gameObject;

                child.SetActive(false);
                Destroy(child);
            }
        }
    }
}
