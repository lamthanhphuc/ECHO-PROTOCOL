using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.MainMenu
{
    /// <summary>
    /// Presents the six existing Main Menu labels with TMP without replacing their
    /// RectTransforms or the Button objects that own gameplay actions.
    /// Authored TMP labels take precedence after the optional Editor conversion.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuTypographyController : MonoBehaviour
    {
        private const string ResourceRoot = "UI/MainMenuTypography/";
        private static readonly Color TitleColor = new Color32(248, 247, 244, 255);
        private static readonly Color SubtitleColor = new Color32(205, 26, 38, 255);
        private static readonly Color ButtonColor = new Color32(225, 221, 215, 255);

        private static TMP_FontAsset _runtimeOswald;
        private static TMP_FontAsset _runtimeMono;
        private static Material _runtimeTitleMaterial;
        private static Material _runtimeSubtitleMaterial;
        private static Material _runtimeButtonMaterial;

        private TMP_Text _subtitle;
        private Color _subtitleBaseColor;
        private float _nextSignalDip;
        private float _signalDipEnds;
        private bool _signalDipped;

        private void Awake()
        {
            if (!Application.isPlaying) return;

            Transform titleObject = transform.Find("HeaderText");
            Transform subtitleObject = transform.Find("SystemLabel");
            Transform hostObject = transform.Find("HostGameButton/Text");
            Transform joinObject = transform.Find("JoinGameButton/Text");
            Transform optionsObject = transform.Find("OptionsButton/Text");
            Transform logoutObject = transform.Find("LogoutButton/Text");
            if (titleObject == null || subtitleObject == null || hostObject == null ||
                joinObject == null || optionsObject == null || logoutObject == null)
            {
                Debug.LogWarning("[MainMenuTypography] One or more original menu labels are missing.", this);
                return;
            }

            TMP_FontAsset oswald = Resources.Load<TMP_FontAsset>(ResourceRoot + "OswaldTMP");
            TMP_FontAsset mono = Resources.Load<TMP_FontAsset>(ResourceRoot + "JetBrainsMonoTMP");
            TMP_FontAsset display = Resources.Load<TMP_FontAsset>(ResourceRoot + "IndustrialTitleTMP")
                ?? Resources.Load<TMP_FontAsset>(ResourceRoot + "RobotoBlackTMP");
            if (oswald == null)
                oswald = GetOrCreateFont(ref _runtimeOswald, titleObject.GetComponent<Text>());
            if (mono == null)
                mono = GetOrCreateFont(ref _runtimeMono, subtitleObject.GetComponent<Text>());
            if (display == null) display = oswald;
            if (oswald == null || mono == null)
            {
                Debug.LogWarning("[MainMenuTypography] TMP font could not be loaded; original menu labels remain visible.", this);
                return;
            }

            // Prewarm the glyphs that the existing localization service can show.
            // The authored assets also remain dynamic for later language changes.
            const string oswaldCharacters =
                "ECHO PROTOCOL HOST GAME JOIN GAME OPTIONS LOGOUT TẠO PHÒNG VÀO PHÒNG CÀI ĐẶT ĐĂNG XUẤT";
            const string monoCharacters = "THE SIGNAL IS STILL ALIVE";
            // This TMP version returns false when every requested glyph is
            // already present. Check actual coverage after the add attempt.
            oswald.TryAddCharacters(oswaldCharacters);
            mono.TryAddCharacters(monoCharacters);
            if (display.atlasPopulationMode != AtlasPopulationMode.Static)
                display.TryAddCharacters("ECHO PROTOCOL");
            bool oswaldReady = oswald.HasCharacters(oswaldCharacters);
            bool monoReady = mono.HasCharacters(monoCharacters);
            if (!oswaldReady || !monoReady)
            {
                Debug.LogWarning("[MainMenuTypography] TMP glyph atlas is incomplete; " +
                    "check Oswald and JetBrains Mono font assets.", this);
                return;
            }

            Material titleMaterial = ResolveMaterial("TitleMetal", display, ref _runtimeTitleMaterial, true);
            Material subtitleMaterial = ResolveMaterial("SubtitleSignal", mono, ref _runtimeSubtitleMaterial, false);
            Material buttonMaterial = ResolveMaterial("ButtonIndustrial", oswald, ref _runtimeButtonMaterial, false);

            Configure(titleObject, display, titleMaterial, TitleColor, 124f, 0f,
                TextAlignmentOptions.TopLeft, -15f, true);
            _subtitle = Configure(subtitleObject, mono, subtitleMaterial, SubtitleColor, 24f, 8f,
                TextAlignmentOptions.TopLeft, 0f, false);
            if (_subtitle != null)
            {
                _subtitleBaseColor = _subtitle.color;
                _nextSignalDip = Time.unscaledTime + 6.7f;
            }

            ConfigureButton(hostObject, oswald, buttonMaterial);
            ConfigureButton(joinObject, oswald, buttonMaterial);
            ConfigureButton(optionsObject, oswald, buttonMaterial);
            ConfigureButton(logoutObject, oswald, buttonMaterial);
        }

        private static TMP_FontAsset GetOrCreateFont(ref TMP_FontAsset cache, Text source)
        {
            if (cache != null) return cache;
            if (source == null || source.font == null) return null;
            cache = TMP_FontAsset.CreateFontAsset(source.font);
            if (cache != null) cache.name = "Main Menu / " + source.font.name;
            return cache;
        }

        private static Material ResolveMaterial(string name, TMP_FontAsset font,
            ref Material runtimeCache, bool title)
        {
            Material authored = Resources.Load<Material>(ResourceRoot + name);
            if (authored != null) return authored;
            if (runtimeCache != null) return runtimeCache;

            runtimeCache = new Material(font.material) { name = "Main Menu / " + name };
            if (runtimeCache.HasProperty(ShaderUtilities.ID_OutlineColor))
                runtimeCache.SetColor(ShaderUtilities.ID_OutlineColor,
                    title ? new Color(0.28f, 0.025f, 0.02f, 0.24f)
                          : new Color(0.025f, 0.015f, 0.015f, 0.48f));
            if (runtimeCache.HasProperty(ShaderUtilities.ID_OutlineWidth))
            {
                float outline = title ? 0.025f : name == "ButtonIndustrial" ? 0.018f : 0f;
                if (outline > 0f) runtimeCache.EnableKeyword("OUTLINE_ON");
                runtimeCache.SetFloat(ShaderUtilities.ID_OutlineWidth, outline);
            }
            if (runtimeCache.HasProperty("_FaceDilate"))
                runtimeCache.SetFloat("_FaceDilate", title ? 0.035f
                    : name == "ButtonIndustrial" ? 0.015f : 0f);
            if (runtimeCache.HasProperty(ShaderUtilities.ID_UnderlayColor))
            {
                runtimeCache.EnableKeyword("UNDERLAY_ON");
                runtimeCache.SetColor(ShaderUtilities.ID_UnderlayColor,
                    title ? new Color(0f, 0f, 0f, 0.38f)
                          : new Color(0f, 0f, 0f, 0.24f));
                runtimeCache.SetFloat("_UnderlayOffsetX", 0.045f);
                runtimeCache.SetFloat("_UnderlayOffsetY", -0.055f);
                runtimeCache.SetFloat("_UnderlaySoftness", title ? 0.15f : 0.10f);
            }
            return runtimeCache;
        }

        private static TMP_Text Configure(Transform target, TMP_FontAsset font, Material material,
            Color color, float size, float spacing, TextAlignmentOptions alignment,
            float lineSpacing, bool agedTitle)
        {
            TMP_Text label = target.GetComponent<TMP_Text>();
            if (label != null) return label; // Already authored by the Editor conversion.

            Text original = target.GetComponent<Text>();
            if (original == null) return null;
            Transform existing = target.Find("CinematicTMP");
            if (existing != null) label = existing.GetComponent<TMP_Text>();
            if (label == null)
            {
                var child = new GameObject("CinematicTMP", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                child.layer = target.gameObject.layer;
                child.transform.SetParent(target, false);
                RectTransform rect = (RectTransform)child.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                label = child.GetComponent<TextMeshProUGUI>();
            }

            label.font = font;
            label.fontSharedMaterial = material;
            label.text = original.text;
            label.fontSize = size;
            label.fontStyle = target.parent.name.EndsWith("Button")
                ? FontStyles.Bold : FontStyles.Normal;
            label.characterSpacing = spacing;
            label.lineSpacing = lineSpacing;
            label.alignment = alignment;
            // TMP multiplies vertex-gradient colors by its main color.
            label.color = agedTitle ? Color.white : color;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.enableAutoSizing = false;
            label.richText = original.supportRichText;
            if (agedTitle)
            {
                // The authored face texture spans both title lines, rather than
                // repeating the blood and wear pattern on every glyph.
                label.horizontalMapping = TextureMappingOptions.Paragraph;
                label.verticalMapping = TextureMappingOptions.Paragraph;
                label.enableVertexGradient = true;
                label.colorGradient = new VertexGradient(
                    TitleColor, TitleColor,
                    new Color32(226, 225, 220, 255), new Color32(226, 225, 220, 255));
            }

            // Keep the original component and its localization source intact. It is
            // an immediate fallback if TMP asset creation fails on another machine.
            original.enabled = false;
            return label;
        }

        private static void ConfigureButton(Transform labelObject, TMP_FontAsset font, Material material)
        {
            TMP_Text label = Configure(labelObject, font, material, ButtonColor, 44f, 1.6f,
                TextAlignmentOptions.MidlineLeft, 0f, false);
            if (label == null) return;
            Button button = labelObject.GetComponentInParent<Button>();
            if (button == null) return;
            MainMenuButtonTextFeedback feedback = button.GetComponent<MainMenuButtonTextFeedback>();
            if (feedback == null) feedback = button.gameObject.AddComponent<MainMenuButtonTextFeedback>();
            feedback.Initialize(button, label);
        }

        private void Update()
        {
            if (_subtitle == null) return;
            float now = Time.unscaledTime;
            if (!_signalDipped && now >= _nextSignalDip)
            {
                _signalDipped = true;
                _signalDipEnds = now + 0.055f;
                _subtitle.color = new Color(_subtitleBaseColor.r, _subtitleBaseColor.g,
                    _subtitleBaseColor.b, _subtitleBaseColor.a * 0.84f);
            }
            else if (_signalDipped && now >= _signalDipEnds)
            {
                _signalDipped = false;
                _subtitle.color = _subtitleBaseColor;
                _nextSignalDip = now + 7.4f;
            }
        }
    }
}
