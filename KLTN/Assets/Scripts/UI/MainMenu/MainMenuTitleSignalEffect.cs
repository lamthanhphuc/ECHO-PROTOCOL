using TMPro;
using UnityEngine;

namespace EchoProtocol.UI.MainMenu
{
    /// <summary>
    /// Draws a restrained red registration echo behind the authored Main Menu title.
    /// The source title and its RectTransform never move. All timing is unscaled so
    /// the introduction still plays while the menu has paused game time.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class MainMenuTitleSignalEffect : MonoBehaviour
    {
        [SerializeField] private Color echoColor = new Color(0.72f, 0.055f, 0.04f, 0.16f);
        [SerializeField, Range(0f, 0.5f)] private float openingDuration = 0.36f;
        [SerializeField, Range(5f, 20f)] private float minimumInterferenceInterval = 8f;
        [SerializeField, Range(5f, 20f)] private float maximumInterferenceInterval = 13f;

        private TextMeshProUGUI _title;
        private TextMeshProUGUI _echo;
        private RectTransform _echoRect;
        private Vector2 _restPosition;
        private float _openingEndsAt;
        private float _interferenceEndsAt;
        private float _nextInterferenceAt;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;

            _title = GetComponent<TextMeshProUGUI>();
            if (_title == null || transform.parent == null) return;

            var echoObject = new GameObject("TitleRedEcho", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            echoObject.layer = gameObject.layer;
            echoObject.transform.SetParent(transform.parent, false);
            echoObject.transform.SetSiblingIndex(transform.GetSiblingIndex());

            RectTransform sourceRect = _title.rectTransform;
            _echoRect = (RectTransform)echoObject.transform;
            _echoRect.anchorMin = sourceRect.anchorMin;
            _echoRect.anchorMax = sourceRect.anchorMax;
            _echoRect.pivot = sourceRect.pivot;
            _echoRect.sizeDelta = sourceRect.sizeDelta;
            _echoRect.localRotation = sourceRect.localRotation;
            _echoRect.localScale = sourceRect.localScale;
            _restPosition = sourceRect.anchoredPosition + new Vector2(0.8f, -0.35f);
            _echoRect.anchoredPosition = _restPosition;

            _echo = echoObject.GetComponent<TextMeshProUGUI>();
            _echo.font = _title.font;
            _echo.fontSharedMaterial = _title.fontSharedMaterial;
            _echo.text = _title.text;
            _echo.fontSize = _title.fontSize;
            _echo.fontStyle = _title.fontStyle;
            _echo.fontWeight = _title.fontWeight;
            _echo.characterSpacing = _title.characterSpacing;
            _echo.wordSpacing = _title.wordSpacing;
            _echo.lineSpacing = _title.lineSpacing;
            _echo.alignment = _title.alignment;
            _echo.margin = _title.margin;
            _echo.richText = _title.richText;
            _echo.textWrappingMode = _title.textWrappingMode;
            _echo.overflowMode = _title.overflowMode;
            _echo.horizontalMapping = _title.horizontalMapping;
            _echo.verticalMapping = _title.verticalMapping;
            _echo.enableAutoSizing = false;
            _echo.enableVertexGradient = false;
            _echo.raycastTarget = false;
            _echo.color = echoColor;

            float now = Time.unscaledTime;
            _openingEndsAt = now + openingDuration;
            _interferenceEndsAt = now;
            _nextInterferenceAt = now + NextInterval();
        }

        private void Update()
        {
            if (_echo == null || _title == null) return;

            if (_echo.text != _title.text) _echo.text = _title.text;
            if (_echo.fontSharedMaterial != _title.fontSharedMaterial)
                _echo.fontSharedMaterial = _title.fontSharedMaterial;

            float now = Time.unscaledTime;
            bool opening = now < _openingEndsAt;
            if (!opening && now >= _nextInterferenceAt)
            {
                _interferenceEndsAt = now + 0.085f;
                _nextInterferenceAt = now + NextInterval();
            }

            Vector2 position = _restPosition;
            float alpha = echoColor.a;
            if (opening)
            {
                int frame = Mathf.FloorToInt((_openingEndsAt - now) / 0.045f);
                if ((frame & 1) == 0)
                {
                    position.x += (frame & 2) == 0 ? 2.1f : -1.5f;
                    alpha *= 1.55f;
                }
                else alpha *= 0.6f;
            }
            else if (now < _interferenceEndsAt)
            {
                position.x += 1.4f;
                alpha *= 1.25f;
            }

            if (_echoRect.anchoredPosition != position)
                _echoRect.anchoredPosition = position;
            Color tint = echoColor;
            tint.a = Mathf.Clamp01(alpha);
            if (_echo.color != tint) _echo.color = tint;
        }

        private float NextInterval()
        {
            float min = Mathf.Min(minimumInterferenceInterval, maximumInterferenceInterval);
            float max = Mathf.Max(minimumInterferenceInterval, maximumInterferenceInterval);
            return Random.Range(min, max);
        }

        private void OnDisable()
        {
            if (_echo != null)
            {
                Destroy(_echo.gameObject);
                _echo = null;
                _echoRect = null;
            }
        }
    }
}
