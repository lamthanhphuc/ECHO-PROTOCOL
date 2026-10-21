using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EchoProtocol.UI.MainMenu
{
    /// <summary>
    /// Adds restrained text feedback to an existing Main Menu Button without changing
    /// its target graphic, navigation, layout, or onClick listeners.
    /// </summary>
    public sealed class MainMenuButtonTextFeedback : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
        ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        private static readonly int UnderlayColorId = Shader.PropertyToID("_UnderlayColor");
        private static readonly int UnderlayOffsetXId = Shader.PropertyToID("_UnderlayOffsetX");
        private static readonly int UnderlayOffsetYId = Shader.PropertyToID("_UnderlayOffsetY");
        private static readonly int UnderlayDilateId = Shader.PropertyToID("_UnderlayDilate");
        private static readonly int UnderlaySoftnessId = Shader.PropertyToID("_UnderlaySoftness");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

        [SerializeField] private Button button;
        [SerializeField] private Graphic label;
        [SerializeField, Range(0.15f, 0.25f)] private float hoverDuration = 0.2f;
        [SerializeField, Range(0.05f, 0.2f)] private float flashDuration = 0.12f;
        [SerializeField] private Color hoverTextColor = new Color(0.94f, 0.91f, 0.87f, 1f);
        [SerializeField] private Color flashTextColor = new Color(0.82f, 0.30f, 0.27f, 1f);
        [SerializeField] private Color hoverGlowColor = new Color(0.40f, 0.035f, 0.03f, 0.48f);
        [SerializeField] private Color indicatorColor = new Color(0.83f, 0.025f, 0.045f, 0.88f);

        private Shadow _legacyGlow;
        private TMP_Text _tmpLabel;
        private Material _originalTmpMaterial;
        private Material _tmpMaterial;
        private Color _baseTmpGlowColor;
        private Color _appliedTmpGlowColor;
        private float _baseOutlineWidth;
        private float _appliedOutlineWidth;
        private bool _useUnderlay;
        private bool _useOutline;
        private bool _visualsReady;
        private Color _normalTextColor;
        private float _hoverAmount;
        private float _flashStartedAt = float.NegativeInfinity;
        private bool _pointerOver;
        private bool _selected;
        private Image _indicator;
        private RectTransform _indicatorRect;
        private Vector2 _indicatorRestPosition;
        private float _hoverStartedAt = float.NegativeInfinity;

        /// <summary>Bind a TMP label created after this component was enabled.</summary>
        public void Initialize(Button ownerButton, TMP_Text tmpLabel)
        {
            if (ownerButton == null || tmpLabel == null) return;
            bool changed = button != ownerButton || label != tmpLabel;
            if (changed) ReleaseVisuals();
            button = ownerButton;
            label = tmpLabel;
            if (Application.isPlaying && isActiveAndEnabled &&
                (changed || _tmpMaterial == null)) SetupVisuals();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (button == null) button = GetComponent<Button>();
            if (label == null)
            {
                var namedLabel = transform.Find("Text");
                if (namedLabel != null)
                    label = namedLabel.GetComponentInChildren<TMP_Text>(true) as Graphic
                        ?? namedLabel.GetComponent<Text>();
                if (label == null)
                    label = GetComponentInChildren<TMP_Text>(true) as Graphic
                        ?? GetComponentInChildren<Text>(true);
            }

            if (button == null || label == null) return;

            SetupVisuals();
        }

        private void SetupVisuals()
        {
            if (button == null || label == null) return;

            _normalTextColor = label.color;
            _tmpLabel = label as TMP_Text;
            if (_tmpLabel != null)
            {
                if (_tmpMaterial == null) CreateTmpGlowMaterial();
            }
            else if (_legacyGlow == null)
            {
                // UI.Shadow is only used by the legacy Unity UI Text fallback.
                _legacyGlow = label.gameObject.AddComponent<Shadow>();
                _legacyGlow.effectDistance = new Vector2(1.5f, -1.5f);
                _legacyGlow.useGraphicAlpha = true;
            }
            ApplyGlow(0f);
            EnsureIndicator();
            ApplyIndicator(0f);
            _hoverAmount = 0f;
            _flashStartedAt = float.NegativeInfinity;
            _pointerOver = false;
            _selected = false;
            _visualsReady = true;
        }

        private void OnDisable()
        {
            if (!_visualsReady) return;
            if (label != null) label.color = _normalTextColor;
            ApplyGlow(0f);
            ApplyIndicator(0f);
            _pointerOver = false;
            _selected = false;
            _hoverAmount = 0f;
        }

        private void OnDestroy()
        {
            ReleaseVisuals();
        }

        private void CreateTmpGlowMaterial()
        {
            if (_tmpLabel == null) return;
            Material source = _tmpLabel.fontSharedMaterial;
            if (source == null) return;

            _originalTmpMaterial = source;
            _tmpMaterial = new Material(source)
            {
                name = source.name + " (Main Menu Text Glow)",
                hideFlags = HideFlags.DontSave
            };

            // The bundled TMP SDF shaders support an underlay. Other TMP shaders
            // can use a thin outline; neither path edits the shared font material.
            _useUnderlay = _tmpMaterial.HasProperty(UnderlayColorId);
            _useOutline = !_useUnderlay && _tmpMaterial.HasProperty(OutlineColorId)
                && _tmpMaterial.HasProperty(OutlineWidthId);
            if (_useUnderlay)
            {
                bool hadUnderlay = source.IsKeywordEnabled("UNDERLAY_ON");
                _baseTmpGlowColor = hadUnderlay
                    ? source.GetColor(UnderlayColorId) : Color.clear;
                _tmpMaterial.DisableKeyword("UNDERLAY_INNER");
                _tmpMaterial.EnableKeyword("UNDERLAY_ON");
                if (!hadUnderlay)
                {
                    if (_tmpMaterial.HasProperty(UnderlayOffsetXId))
                        _tmpMaterial.SetFloat(UnderlayOffsetXId, 0f);
                    if (_tmpMaterial.HasProperty(UnderlayOffsetYId))
                        _tmpMaterial.SetFloat(UnderlayOffsetYId, 0f);
                    if (_tmpMaterial.HasProperty(UnderlayDilateId))
                        _tmpMaterial.SetFloat(UnderlayDilateId, 0.14f);
                    if (_tmpMaterial.HasProperty(UnderlaySoftnessId))
                        _tmpMaterial.SetFloat(UnderlaySoftnessId, 0.25f);
                }
                _tmpMaterial.SetColor(UnderlayColorId, _baseTmpGlowColor);
            }
            else if (_useOutline)
            {
                _baseTmpGlowColor = source.GetColor(OutlineColorId);
                _baseOutlineWidth = source.GetFloat(OutlineWidthId);
                _tmpMaterial.EnableKeyword("OUTLINE_ON");
            }
            _appliedTmpGlowColor = _baseTmpGlowColor;
            _appliedOutlineWidth = _baseOutlineWidth;
            _tmpLabel.fontSharedMaterial = _tmpMaterial;
        }

        private void ReleaseVisuals()
        {
            if (_visualsReady && label != null) label.color = _normalTextColor;
            if (_legacyGlow != null)
            {
                if (Application.isPlaying) Destroy(_legacyGlow);
                else DestroyImmediate(_legacyGlow);
                _legacyGlow = null;
            }
            if (_tmpMaterial != null)
            {
                if (_tmpLabel != null && _tmpLabel.fontSharedMaterial == _tmpMaterial)
                    _tmpLabel.fontSharedMaterial = _originalTmpMaterial;
                if (Application.isPlaying) Destroy(_tmpMaterial);
                else DestroyImmediate(_tmpMaterial);
                _tmpMaterial = null;
            }
            _tmpLabel = null;
            _originalTmpMaterial = null;
            _useUnderlay = false;
            _useOutline = false;
            _visualsReady = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerOver = true;
            _hoverStartedAt = Time.unscaledTime;
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerOver = false;
            // A mouse click selects a Button in EventSystem; leaving it should still
            // return the label to its authored color.
            _selected = false;
        }
        public void OnSelect(BaseEventData eventData) => _selected = true;
        public void OnDeselect(BaseEventData eventData) => _selected = false;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) Flash();
        }

        public void OnSubmit(BaseEventData eventData) => Flash();

        private void Flash()
        {
            if (button == null || !button.IsActive() || !button.IsInteractable()) return;
            _flashStartedAt = Time.unscaledTime;
            ApplyColors(1f);
        }

        private void Update()
        {
            if (button == null || label == null) return;
            float target = button.IsActive() && button.IsInteractable()
                && (_pointerOver || _selected) ? 1f : 0f;
            _hoverAmount = Mathf.MoveTowards(_hoverAmount, target,
                Time.unscaledDeltaTime / Mathf.Max(0.01f, hoverDuration));
            float flash = Mathf.Clamp01(1f -
                (Time.unscaledTime - _flashStartedAt) / Mathf.Max(0.01f, flashDuration));
            ApplyColors(flash);
        }

        private void ApplyColors(float flash)
        {
            Color textColor = Color.Lerp(_normalTextColor, hoverTextColor, _hoverAmount);
            textColor = Color.Lerp(textColor, flashTextColor, flash);
            textColor.a = _normalTextColor.a;
            if (label.color != textColor) label.color = textColor;

            ApplyGlow(Mathf.Max(_hoverAmount, flash));
            ApplyIndicator(Mathf.Max(_hoverAmount, flash));
        }

        private void EnsureIndicator()
        {
            Transform existing = transform.Find("HoverSignalIndicator");
            bool created = existing == null;
            if (existing == null)
            {
                var marker = new GameObject("HoverSignalIndicator", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                marker.layer = gameObject.layer;
                marker.transform.SetParent(transform, false);
                existing = marker.transform;
            }

            _indicatorRect = existing as RectTransform;
            _indicator = existing.GetComponent<Image>();
            if (_indicator == null || _indicatorRect == null) return;
            _indicator.raycastTarget = false;
            if (created)
            {
                _indicatorRect.anchorMin = new Vector2(0f, 0.5f);
                _indicatorRect.anchorMax = new Vector2(0f, 0.5f);
                _indicatorRect.pivot = new Vector2(0f, 0.5f);
                _indicatorRect.anchoredPosition = new Vector2(38f, 0f);
                _indicatorRect.sizeDelta = new Vector2(3f, 42f);
            }
            _indicatorRestPosition = _indicatorRect.anchoredPosition;
        }

        private void ApplyIndicator(float amount)
        {
            if (_indicator == null || _indicatorRect == null) return;
            Color color = indicatorColor;
            color.a *= Mathf.Lerp(0.72f, 1f, amount);
            if (_indicator.color != color) _indicator.color = color;

            // A single registration skip on hover; the Button and its label stay put.
            float offset = _pointerOver && Time.unscaledTime - _hoverStartedAt < 0.09f
                ? 1f : 0f;
            Vector2 position = _indicatorRestPosition + new Vector2(offset, 0f);
            if (_indicatorRect.anchoredPosition != position)
                _indicatorRect.anchoredPosition = position;
        }

        private void ApplyGlow(float amount)
        {
            Color glowColor = hoverGlowColor;
            glowColor.a *= amount;
            if (_legacyGlow != null)
            {
                if (_legacyGlow.effectColor != glowColor)
                    _legacyGlow.effectColor = glowColor;
                return;
            }
            if (_tmpMaterial == null) return;

            Color tmpGlow = Color.Lerp(_baseTmpGlowColor, hoverGlowColor, amount);
            if (_useUnderlay && _appliedTmpGlowColor != tmpGlow)
            {
                _tmpMaterial.SetColor(UnderlayColorId, tmpGlow);
                _appliedTmpGlowColor = tmpGlow;
            }
            else if (_useOutline)
            {
                if (_appliedTmpGlowColor != tmpGlow)
                {
                    _tmpMaterial.SetColor(OutlineColorId, tmpGlow);
                    _appliedTmpGlowColor = tmpGlow;
                }
                float width = Mathf.Lerp(_baseOutlineWidth,
                    Mathf.Max(_baseOutlineWidth, 0.12f), amount);
                if (!Mathf.Approximately(_appliedOutlineWidth, width))
                {
                    _tmpMaterial.SetFloat(OutlineWidthId, width);
                    _appliedOutlineWidth = width;
                }
            }
        }
    }
}
