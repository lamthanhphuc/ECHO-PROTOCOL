using System;
using EchoProtocol.Audio;
using TMPro;
using UnityEngine;

namespace EchoProtocol.UI.HUD
{
    public enum CabinetCueStatus { Pending, Ready, Unavailable }
    public sealed class HUDCabinetWarning : IDisposable
    {
        private GameObject _root;
        private Canvas _canvas;
        private TextMeshProUGUI _text;
        private AudioSource _audio;
        private AudioClip _clip;
        private bool _audioStarted;
        private bool _audioObserved;
        private int _shownFrame;

        public HUDCabinetWarning(Transform owner, AudioClip clip)
        {
            _clip = clip;
            if (owner == null || clip == null || TMP_Settings.defaultFontAsset == null) return;
            _root = new GameObject("CabinetWarning", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            _root.transform.SetParent(owner, false);
            _canvas = _root.GetComponent<Canvas>(); _canvas.renderMode = RenderMode.ScreenSpaceOverlay; _canvas.sortingOrder = 1000;
            var scaler = _root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var panel = new GameObject("Warning", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            panel.transform.SetParent(_root.transform, false);
            var rectangle = panel.GetComponent<RectTransform>(); rectangle.anchorMin = new Vector2(.2f, .73f);
            rectangle.anchorMax = new Vector2(.8f, .82f); rectangle.pivot = new Vector2(.5f, 1); rectangle.offsetMin = rectangle.offsetMax = Vector2.zero;
            var image = panel.GetComponent<UnityEngine.UI.Image>(); image.color = new Color(.025f, .02f, .02f, .92f); image.raycastTarget = false;
            var label = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI)); label.transform.SetParent(panel.transform, false);
            var textRectangle = label.GetComponent<RectTransform>(); textRectangle.anchorMin = Vector2.zero; textRectangle.anchorMax = Vector2.one;
            textRectangle.offsetMin = new Vector2(16, 8); textRectangle.offsetMax = new Vector2(-16, -8);
            _text = label.GetComponent<TextMeshProUGUI>(); _text.font = TMP_Settings.defaultFontAsset; _text.fontSize = 30;
            _text.color = new Color(1, .85f, .35f); _text.alignment = TextAlignmentOptions.Center; _text.raycastTarget = false;
            _text.text = "STALKER IS APPROACHING\nLeave your hiding place before it arrives.";
            _audio = GameAudioRuntime.CreateSource(_root, false); _shownFrame = Time.frameCount;
        }
        public CabinetCueStatus Present()
        {
            if (_root == null || _canvas == null || !_canvas.isActiveAndEnabled || _text == null || _audio == null || _clip == null ||
                _clip.loadState == AudioDataLoadState.Failed) return CabinetCueStatus.Unavailable;
            if (_clip.loadState != AudioDataLoadState.Loaded) { _clip.LoadAudioData(); return CabinetCueStatus.Pending; }
            if (!_audioStarted) { _audio.PlayOneShot(_clip); _audioStarted = true; }
            _audioObserved |= _audio.isPlaying;
            Canvas.ForceUpdateCanvases(); _text.ForceMeshUpdate();
            if (!_audioObserved || Time.frameCount <= _shownFrame || _text.textInfo.characterCount == 0 || _text.canvasRenderer.cull ||
                _text.rectTransform.rect.width <= 0 || _text.rectTransform.rect.height <= 0) return CabinetCueStatus.Pending;
            return CabinetCueStatus.Ready;
        }
        public void Dispose()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root);
            _root = null; _clip = null;
        }
    }
}
