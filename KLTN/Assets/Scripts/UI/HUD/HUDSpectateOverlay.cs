using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.HUD
{
    public sealed class HUDSpectateOverlay : MonoBehaviour
    {
        [SerializeField] private PlayerSpectateController spectateController;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text titleTmp;
        [SerializeField] private TMP_Text hintTmp;
        [SerializeField] private Text titleText;
        [SerializeField] private Text hintText;
        [SerializeField, Min(1f)] private float fadeSpeed = 14f;

        private float _targetAlpha;

        public void BindSpectateController(PlayerSpectateController controller)
        {
            spectateController = controller;
        }

        private void Awake()
        {
            ResolveComponents();
            SetAlpha(0f);
        }

        private void Update()
        {
            if (spectateController == null || !spectateController.IsSpectating)
            {
                _targetAlpha = 0f;
                SetAlpha(Mathf.MoveTowards(GetAlpha(), _targetAlpha, fadeSpeed * Time.deltaTime));
                return;
            }

            _targetAlpha = 1f;
            string targetLabel = spectateController.SpectateTargetLabel;
            if (string.IsNullOrWhiteSpace(targetLabel)) targetLabel = "Không có tín hiệu";

            SetText(titleTmp, titleText, $"Đang quan sát · {targetLabel}");
            SetText(hintTmp, hintText, "[Chuột trái] Đổi người chơi");
            SetAlpha(Mathf.MoveTowards(GetAlpha(), _targetAlpha, fadeSpeed * Time.deltaTime));
        }

        public static HUDSpectateOverlay CreateDefault(Transform parent)
        {
            var root = new GameObject("SpectateOverlay", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            root.transform.SetParent(parent, false);

            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -32f);
            rect.sizeDelta = new Vector2(360f, 68f);

            var background = root.GetComponent<Image>();
            background.color = new Color(0.02f, 0.04f, 0.07f, 0.78f);

            var overlay = root.AddComponent<HUDSpectateOverlay>();
            overlay.canvasGroup = root.GetComponent<CanvasGroup>();
            overlay.titleTmp = CreateText(root.transform, "Title", 0f, -8f, 16, FontStyles.Normal);
            overlay.hintTmp = CreateText(root.transform, "Hint", 0f, -34f, 12, FontStyles.Normal);
            overlay.SetAlpha(0f);
            return overlay;
        }

        private static TMP_Text CreateText(Transform parent, string name, float x, float y, int size, FontStyles style)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(-40f, 30f);

            var text = textObject.GetComponent<TMP_Text>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = name == "Title" ? HUDPresentationStyle.Ink : HUDPresentationStyle.Muted;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.text = string.Empty;
            return text;
        }

        private void ResolveComponents()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (titleTmp == null && titleText != null) titleTmp = titleText.GetComponent<TMP_Text>();
            if (hintTmp == null && hintText != null) hintTmp = hintText.GetComponent<TMP_Text>();
        }

        private float GetAlpha()
        {
            return canvasGroup != null ? canvasGroup.alpha : 0f;
        }

        private void SetAlpha(float alpha)
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = alpha;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private static void SetText(TMP_Text tmp, Text legacy, string content)
        {
            if (tmp != null) tmp.text = content;
            if (legacy != null) legacy.text = content;
        }
    }
}
