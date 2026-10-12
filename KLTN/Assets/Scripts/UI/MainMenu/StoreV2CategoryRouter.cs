using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.MainMenu
{
    [DisallowMultipleComponent]
    public sealed class StoreV2CategoryRouter : MonoBehaviour
    {
        private enum StoreCategory
        {
            Character,
            TeamTool,
            Pet
        }

        private Button _characterButton;
        private Button _teamToolButton;
        private Button _petButton;

        private RectTransform _categories;
        private RectTransform _content;
        private RectTransform _teamTools;

        private CharacterShopController _characters;
        private PetShopController _pets;

        private StoreV2PreviewLighting _lighting;

        private Coroutine _bindRoutine;
        private Coroutine _dynamicRoutine;

        private StoreCategory _selected =
            StoreCategory.TeamTool;

        private void OnEnable()
        {
            if (_bindRoutine != null)
            {
                StopCoroutine(
                    _bindRoutine);
            }

            _bindRoutine =
                StartCoroutine(
                    BindWhenReady());
        }

        private void OnDisable()
        {
            if (_bindRoutine != null)
            {
                StopCoroutine(
                    _bindRoutine);

                _bindRoutine = null;
            }

            if (_dynamicRoutine != null)
            {
                StopCoroutine(
                    _dynamicRoutine);

                _dynamicRoutine = null;
            }
        }

        private IEnumerator BindWhenReady()
        {
            // TeamToolShopPanel.Open() executes during the
            // same activation frame.
            yield return null;

            EnsureControllers();

            for (int frame = 0;
                 frame < 40;
                 frame++)
            {
                Resolve();

                if (Ready())
                    break;

                yield return null;
            }

            Resolve();

            if (!Ready())
            {
                Debug.LogError(
                    "[StoreV2] Category router could not resolve scene shell.",
                    this);

                yield break;
            }

            BindButtons();

            FixRaycastOrder();

            ApplyCategory(
                StoreCategory.TeamTool,
                true);

            _bindRoutine = null;

            Debug.Log(
                "[StoreV2] Category router READY.",
                this);
        }

        private void EnsureControllers()
        {
            _characters =
                GetComponent<
                    CharacterShopController>();

            if (_characters == null)
            {
                _characters =
                    gameObject.AddComponent<
                        CharacterShopController>();
            }

            _characters.EnsureBuilt();

            _pets =
                GetComponent<
                    PetShopController>();

            if (_pets == null)
            {
                _pets =
                    gameObject.AddComponent<
                        PetShopController>();
            }

            _pets.EnsureBuilt();
        }

        private void Resolve()
        {
            _categories =
                FindRect(
                    "Categories");

            _content =
                FindRect(
                    "Content");

            _teamTools =
                FindRect(
                    "TeamTools");

            _characterButton =
                FindButton(
                    "CharacterButton");

            _teamToolButton =
                FindButton(
                    "TeamToolButton");

            _petButton =
                FindButton(
                    "PetButton");

            if (_characters == null)
            {
                _characters =
                    GetComponent<
                        CharacterShopController>();
            }

            if (_pets == null)
            {
                _pets =
                    GetComponent<
                        PetShopController>();
            }

            _lighting =
                GetComponent<
                    StoreV2PreviewLighting>();
        }

        private bool Ready()
        {
            return
                _categories != null
                && _content != null
                && _teamTools != null
                && _characterButton != null
                && _teamToolButton != null
                && _petButton != null
                && _characters != null
                && _pets != null;
        }

        private void BindButtons()
        {
            _characterButton
                .onClick
                .RemoveAllListeners();

            _teamToolButton
                .onClick
                .RemoveAllListeners();

            _petButton
                .onClick
                .RemoveAllListeners();

            _characterButton
                .onClick
                .AddListener(
                    ShowCharacter);

            _teamToolButton
                .onClick
                .AddListener(
                    ShowTeamTool);

            _petButton
                .onClick
                .AddListener(
                    ShowPet);

            PrepareButton(
                _characterButton);

            PrepareButton(
                _teamToolButton);

            PrepareButton(
                _petButton);

            Debug.Log(
                "[StoreV2] Category button listeners bound.",
                this);
        }

        private static void PrepareButton(
            Button button)
        {
            if (button == null)
                return;

            button.interactable =
                true;

            button.navigation =
                new Navigation
                {
                    mode =
                        Navigation.Mode.None
                };

            if (button.targetGraphic != null)
            {
                button
                    .targetGraphic
                    .raycastTarget = true;
            }
        }

        private void FixRaycastOrder()
        {
            if (_content != null)
            {
                Image image =
                    _content.GetComponent<Image>();

                if (image != null)
                    image.raycastTarget = false;
            }

            if (_teamTools != null)
            {
                Image image =
                    _teamTools.GetComponent<Image>();

                if (image != null)
                    image.raycastTarget = false;
            }

            DisableContainerRaycast(
                "SelectedPreviewBox");

            DisableContainerRaycast(
                "ItemGrid");

            DisableContainerRaycast(
                "CharacterContent");

            DisableContainerRaycast(
                "PetContent");

            // Entire Content subtree is rendered before
            // Categories. Children dynamically created by
            // Character/Pet cannot cover these buttons.
            if (_content != null)
            {
                _content.SetAsLastSibling();
            }

            if (_categories != null)
            {
                _categories.SetAsLastSibling();
            }

            RectTransform credits =
                FindRect(
                    "CreditsBox");

            if (credits != null)
            {
                credits.SetAsLastSibling();
            }

            RectTransform close =
                FindRect(
                    "CloseButton");

            if (close != null)
            {
                close.SetAsLastSibling();
            }
        }

        private void DisableContainerRaycast(
            string objectName)
        {
            RectTransform rect =
                FindRect(
                    objectName);

            if (rect == null)
                return;

            Button button =
                rect.GetComponent<Button>();

            if (button != null)
                return;

            Image image =
                rect.GetComponent<Image>();

            if (image != null)
            {
                image.raycastTarget =
                    false;
            }
        }

        private void ShowCharacter()
        {
            Debug.Log(
                "[StoreV2] CLICK Character",
                this);

            ApplyCategory(
                StoreCategory.Character,
                false);
        }

        private void ShowTeamTool()
        {
            Debug.Log(
                "[StoreV2] CLICK TeamTool",
                this);

            ApplyCategory(
                StoreCategory.TeamTool,
                false);
        }

        private void ShowPet()
        {
            Debug.Log(
                "[StoreV2] CLICK Pet",
                this);

            ApplyCategory(
                StoreCategory.Pet,
                false);
        }

        private void ApplyCategory(
            StoreCategory category,
            bool force)
        {
            if (!force
                && _selected == category)
            {
                return;
            }

            _selected =
                category;

            bool showCharacter =
                category
                == StoreCategory.Character;

            bool showTools =
                category
                == StoreCategory.TeamTool;

            bool showPet =
                category
                == StoreCategory.Pet;

            // Hide all content first.
            if (_teamTools != null)
            {
                _teamTools
                    .gameObject
                    .SetActive(false);
            }

            _characters.SetVisible(
                false,
                false);

            _pets.SetVisible(
                false,
                false);

            // Show exactly one.
            if (showTools)
            {
                _teamTools
                    .gameObject
                    .SetActive(true);
            }
            else if (showCharacter)
            {
                _characters.SetVisible(
                    true,
                    false);
            }
            else
            {
                _pets.SetVisible(
                    true,
                    false);
            }

            SetCategorySelected(
                _characterButton,
                showCharacter);

            SetCategorySelected(
                _teamToolButton,
                showTools);

            SetCategorySelected(
                _petButton,
                showPet);

            FixRaycastOrder();
            RefreshVisuals();

            if (_dynamicRoutine != null)
            {
                StopCoroutine(
                    _dynamicRoutine);
            }

            _dynamicRoutine =
                StartCoroutine(
                    RefreshDynamicCatalog());

            EnsurePassiveContainerRaycasts();

            Debug.Log(
                "[StoreV2] CATEGORY => "
                + category,
                this);
        }

        private IEnumerator RefreshDynamicCatalog()
        {
            int previousCount = -1;
            int stablePasses = 0;

            for (int pass = 0;
                 pass < 30;
                 pass++)
            {
                yield return
                    new WaitForSecondsRealtime(
                        0.10f);

                int count =
                    GetComponentsInChildren<
                        Transform>(
                        true).Length;

                if (count != previousCount)
                {
                    previousCount =
                        count;

                    stablePasses = 0;

                    FixRaycastOrder();
                    RefreshVisuals();
                    EnsurePassiveContainerRaycasts();
                }
                else
                {
                    stablePasses++;

                    if (stablePasses >= 5)
                        break;
                }
            }

            _dynamicRoutine = null;
        }

        private void EnsurePassiveContainerRaycasts()
        {
            SetGraphicRaycast(
                "Window/Content/TeamTools",
                false);

            SetGraphicRaycast(
                "Window/Content/TeamTools/ItemGrid",
                false);

            SetGraphicRaycast(
                "Window/Content/CharacterContent",
                false);

            SetGraphicRaycast(
                "Window/Content/CharacterContent/CharacterGrid",
                false);

            SetGraphicRaycast(
                "Window/Content/PetContent",
                false);

            SetGraphicRaycast(
                "Window/Content/PetContent/PetGrid",
                false);

            SetGraphicRaycast(
                "Window/Content/PetContent/SkinGrid",
                false);

            SetGraphicRaycast(
                "Window/CharacterVariantPanel",
                false);

            SetGraphicRaycast(
                "Window/PetVariantPanel",
                false);
        }

        private void SetGraphicRaycast(
            string path,
            bool value)
        {
            Transform node =
                transform.Find(path);

            if (node == null)
                return;

            Graphic graphic =
                node.GetComponent<Graphic>();

            if (graphic != null)
            {
                graphic.raycastTarget =
                    value;
            }
        }

        private void RefreshVisuals()
        {
            // Static Store layout and styling are prefab-authored.
            // Runtime refresh is limited to preview lighting behavior.
            if (_lighting != null)
                _lighting.ApplyNow();
        }

        private static void SetCategorySelected(
            Button button,
            bool selected)
        {
            if (button == null)
                return;

            Image image =
                button.targetGraphic
                as Image;

            if (image != null)
            {
                image.color =
                    selected
                        ? new Color(
                            0.16f,
                            0.05f,
                            0.045f,
                            1f)
                        : new Color(
                            0.025f,
                            0.030f,
                            0.032f,
                            0.98f);
            }

            Outline outline =
                button.GetComponent<Outline>();

            if (outline == null)
            {
                outline =
                    button.gameObject
                        .AddComponent<Outline>();
            }

            outline.effectColor =
                selected
                    ? new Color(
                        0.92f,
                        0.24f,
                        0.19f,
                        1f)
                    : new Color(
                        0.20f,
                        0.22f,
                        0.23f,
                        0.85f);

            outline.effectDistance =
                selected
                    ? new Vector2(
                        2f,
                        -2f)
                    : new Vector2(
                        1f,
                        -1f);
        }

        private Button FindButton(
            string objectName)
        {
            RectTransform rect =
                FindRect(
                    objectName);

            return rect != null
                ? rect.GetComponent<Button>()
                : null;
        }

        private RectTransform FindRect(
            string objectName)
        {
            Transform[] all =
                GetComponentsInChildren<
                    Transform>(
                    true);

            for (int i = 0;
                 i < all.Length;
                 i++)
            {
                if (all[i].name
                    == objectName)
                {
                    return all[i]
                        as RectTransform;
                }
            }

            return null;
        }
    }
}