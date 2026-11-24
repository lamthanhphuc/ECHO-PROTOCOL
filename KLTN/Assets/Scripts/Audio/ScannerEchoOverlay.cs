using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.Audio
{
    // A narrow, translucent device scanline, never edits contacts/text/results.
    public sealed class ScannerEchoOverlay : MonoBehaviour
    {
        private Image _line;
        public static void Attach(Transform screen)
        {
            if (screen == null || screen.GetComponent<ScannerEchoOverlay>() != null) return;
            screen.gameObject.AddComponent<ScannerEchoOverlay>();
        }
        private void Awake()
        {
            var child = new GameObject("Cosmetic ECHO scanline", typeof(RectTransform), typeof(Image));
            child.transform.SetParent(transform, false);
            _line = child.GetComponent<Image>();
            _line.raycastTarget = false;
            _line.color = Color.clear;
            var rect = _line.rectTransform;
            rect.anchorMin = new Vector2(.05f, .5f); rect.anchorMax = new Vector2(.95f, .5f);
            rect.sizeDelta = new Vector2(0, 3);
        }
        private void Update()
        {
            if (_line == null) return;
            float weight = GameAudioRuntime.ScannerInterference;
            _line.color = new Color(.5f, .85f, .8f, weight * .12f);
            if (weight > 0f)
            {
                var rect = _line.rectTransform;
                var size = ((RectTransform)transform).rect;
                rect.anchoredPosition = new Vector2(0, Mathf.Sin(Time.unscaledTime * 18f) * size.height * .3f);
            }
        }
        private void OnDisable() { if (_line != null) _line.color = Color.clear; }
    }
}
