using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.MainMenu
{
    /// <summary>
    /// Store V2 Character outfit binding layer.
    /// Layout, card visuals and Button transition styling are authored in
    /// StoreV2Popup.prefab. Runtime code only binds thumbnails and selection.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StoreV2CharacterOutfitStrip : MonoBehaviour
    {
        private CharacterShopController _controller;

        private RectTransform _panel;
        private RectTransform _grid;

        private int _builtCharacterId = -999;
        private int _builtOutfitCount = -1;
        private float _nextRefresh;

        private void OnEnable()
        {
            Resolve();

            _builtCharacterId = -999;
            _builtOutfitCount = -1;
            _nextRefresh = 0f;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh)
                return;

            _nextRefresh =
                Time.unscaledTime + 0.10f;

            ApplyNow();
        }

        public void ApplyNow()
        {
            if (_controller == null
                || _panel == null
                || _grid == null)
            {
                Resolve();
            }

            if (_controller == null
                || _panel == null
                || _grid == null)
            {
                return;
            }

            _controller.EnsureBuilt();

            RectTransform characterRoot =
                transform.Find(
                    "Window/Content/CharacterContent")
                as RectTransform;

            bool characterVisible =
                characterRoot != null
                && characterRoot.gameObject.activeInHierarchy;

            _panel.gameObject.SetActive(
                characterVisible);

            if (!characterVisible)
                return;

            int characterId =
                _controller.SelectedCharacterId;

            int outfitCount =
                _controller.SelectedOutfitCount;

            if (characterId < 0
                || outfitCount <= 0)
            {
                ClearChildren();

                _builtCharacterId =
                    characterId;

                _builtOutfitCount =
                    outfitCount;

                return;
            }

            if (characterId == _builtCharacterId
                && outfitCount == _builtOutfitCount)
            {
                return;
            }

            BuildCards(
                characterId,
                outfitCount);

            _builtCharacterId =
                characterId;

            _builtOutfitCount =
                outfitCount;
        }

        private void Resolve()
        {
            _controller =
                GetComponent<CharacterShopController>();

            _panel =
                transform.Find(
                    "Window/CharacterVariantPanel")
                as RectTransform;

            _grid =
                _panel != null
                    ? _panel.Find("Grid")
                        as RectTransform
                    : null;
        }

        private void BuildCards(
            int characterId,
            int outfitCount)
        {
            ClearChildren();

            int selectedOutfit =
                _controller != null
                    ? _controller.SelectedOutfitId
                    : 0;

            for (int outfitId = 0;
                 outfitId < outfitCount;
                 outfitId++)
            {
                string resource =
                    "Shop/LobbyVariants/Characters/"
                    + "character-"
                    + characterId
                    + "-outfit-"
                    + outfitId;

                Texture2D texture =
                    Resources.Load<Texture2D>(
                        resource);

                if (texture == null)
                {
                    Debug.LogWarning(
                        "[StoreV2] Missing Character outfit thumbnail: "
                        + resource,
                        this);

                    continue;
                }

                int capturedOutfitId =
                    outfitId;

                GameObject card =
                    StoreV2PrefabCardFactory
                        .CreateVariantCard(
                            transform,
                            _grid,
                            "CharacterOutfit_"
                            + outfitId,
                            texture,
                            this);

                if (card == null)
                    continue;

                Button button =
                    card.GetComponent<Button>();

                button.onClick.AddListener(
                    () =>
                        SelectOutfit(
                            capturedOutfitId,
                            button));

                if (outfitId == selectedOutfit)
                {
                    button.Select();
                }
            }
        }

        private void SelectOutfit(
            int outfitId,
            Button sourceButton)
        {
            if (_controller == null)
                Resolve();

            if (_controller == null)
                return;

            if (!_controller.TrySelectOutfitFromVariant(
                    outfitId))
            {
                Debug.LogWarning(
                    "[StoreV2] Character outfit selection failed: "
                    + _controller.SelectedCharacterId
                    + ":"
                    + outfitId,
                    this);

                return;
            }

            if (sourceButton != null)
                sourceButton.Select();

            Debug.Log(
                "[StoreV2] CHARACTER OUTFIT => "
                + _controller.SelectedCharacterId
                + ":"
                + outfitId,
                this);
        }

        private void ClearChildren()
        {
            if (_grid == null)
                return;

            for (int i = _grid.childCount - 1;
                 i >= 0;
                 i--)
            {
                Transform child =
                    _grid.GetChild(i);

                if (Application.isPlaying)
                {
                    Destroy(
                        child.gameObject);
                }
                else
                {
                    DestroyImmediate(
                        child.gameObject);
                }
            }
        }
    }
}
