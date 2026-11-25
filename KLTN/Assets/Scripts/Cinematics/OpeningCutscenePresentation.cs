using System;
using EchoProtocol.Settings;
using EchoProtocol.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.Cinematics
{
    [DisallowMultipleComponent]
    public sealed class OpeningCutscenePresentation : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Image _fade;
        [SerializeField] private TMP_Text _subtitle, _votes, _hint;
        [SerializeField] private Button _skip;
        public event Action SkipRequested;
        public bool IsReady => _canvas != null && _fade != null && _subtitle != null && _votes != null && _hint != null && _skip != null;

        private void Awake()
        {
            if (_skip != null) _skip.onClick.AddListener(RequestSkip);
            Hide();
        }

        private void RequestSkip() => SkipRequested?.Invoke();
        private void OnDestroy() { if (_skip != null) _skip.onClick.RemoveListener(RequestSkip); }

        public void Show(string subtitle, float fade, int votes, int total, bool voted, float hold)
        {
            if (!IsReady) return;
            _canvas.enabled = true;
            _subtitle.text = subtitle;
            _votes.text = $"{votes} / {total}";
            _hint.text = voted ? GameLanguage.Choose("Đã bỏ phiếu", "Vote submitted")
                : GameLanguage.Choose("Giữ SPACE để bỏ qua", "Hold SPACE to skip")
                    + (hold > 0 ? $"  {Mathf.RoundToInt(hold * 100)}%" : string.Empty);
            _skip.interactable = !voted;
            _fade.color = new Color(0, 0, 0, Mathf.Clamp01(fade));
        }

        public void FadeToGameplay(float alpha)
        {
            if (!IsReady) return;
            _canvas.enabled = true;
            _subtitle.text = _votes.text = _hint.text = string.Empty;
            _skip.gameObject.SetActive(false);
            _fade.color = new Color(0, 0, 0, Mathf.Clamp01(alpha));
        }

        public void Hide()
        {
            if (_canvas != null) _canvas.enabled = false;
            if (_skip != null) _skip.gameObject.SetActive(true);
        }

        // Also used by the Editor builder, so the saved scene owns editable UI references.
        public void Build(Transform parent)
        {
            var root = new GameObject("CinematicCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(parent, false);
            _canvas = root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            Panel("LetterboxTop", root.transform, new Vector2(0, 0.9f), Vector2.one, Color.black);
            Panel("LetterboxBottom", root.transform, Vector2.zero, new Vector2(1, 0.1f), Color.black);
            _subtitle = Label("SubtitleText", root.transform, new Vector2(0.18f, 0.13f), new Vector2(0.82f, 0.24f), 28);
            _subtitle.alignment = TextAlignmentOptions.Center;
            _hint = Label("SkipHint", root.transform, new Vector2(0.65f, 0.032f), new Vector2(0.9f, 0.075f), 19);
            _votes = Label("SkipVoteText", root.transform, new Vector2(0.91f, 0.032f), new Vector2(0.98f, 0.075f), 19);
            var skipImage = Panel("SkipButton", root.transform, new Vector2(0.88f, 0.12f), new Vector2(0.98f, 0.17f), new Color(0.08f, 0.1f, 0.11f, 0.9f));
            _skip = skipImage.gameObject.AddComponent<Button>();
            _skip.targetGraphic = skipImage;
            skipImage.raycastTarget = true;
            var skipText = Label("Label", skipImage.transform, Vector2.zero, Vector2.one, 20);
            skipText.text = "SKIP";
            skipText.alignment = TextAlignmentOptions.Center;
            _fade = Panel("FadePanel", root.transform, Vector2.zero, Vector2.one, Color.black);
            _fade.transform.SetAsLastSibling();
            _fade.raycastTarget = false;
            _canvas.enabled = false;
        }

        private static Image Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = obj.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
            return image;
        }

        private static TMP_Text Label(string name, Transform parent, Vector2 min, Vector2 max, float size)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = obj.GetComponent<TMP_Text>();
            UiTypographyTheme.Apply(text);
            text.fontSize = size; text.color = new Color(0.88f, 0.91f, 0.9f);
            text.raycastTarget = false;
            return text;
        }
    }
}
