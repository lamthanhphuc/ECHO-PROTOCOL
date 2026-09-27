using EchoProtocol.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.HUD
{
    [DisallowMultipleComponent]
    public sealed class HUDNoiseMakerPlacement : MonoBehaviour
    {
        [SerializeField] private Text markerText;

        private NetworkPlayerInteractor _interactor;
        private Camera _camera;

        private void Awake()
        {
            if (markerText == null)
            {
                CreateMarker();
            }

            SetVisible(false);
        }

        public void BindPlayer(NetworkPlayerInteractor interactor)
        {
            _interactor = interactor;
        }

        public void Unbind()
        {
            _interactor = null;
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (_interactor == null
                || !_interactor.TryGetNoiseMakerPlacementPreview(out var worldPosition))
            {
                SetVisible(false);
                return;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_camera == null)
            {
                SetVisible(false);
                return;
            }

            Vector3 screen = _camera.WorldToScreenPoint(worldPosition + Vector3.up * 0.12f);
            if (screen.z <= 0f)
            {
                SetVisible(false);
                return;
            }

            markerText.rectTransform.position = screen;
            SetVisible(true);
        }

        private void CreateMarker()
        {
            var go = new GameObject(
                "NoiseMakerPlacementMarker",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));

            go.transform.SetParent(transform, false);

            markerText = go.GetComponent<Text>();
            markerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            markerText.text = "◎\n<size=16>ĐẶT TẠI ĐÂY</size>";
            markerText.fontSize = 38;
            markerText.fontStyle = FontStyle.Bold;
            markerText.alignment = TextAnchor.MiddleCenter;
            markerText.supportRichText = true;
            markerText.color = new Color(1f, 0.65f, 0.2f, 0.95f);
            markerText.raycastTarget = false;
            markerText.rectTransform.sizeDelta = new Vector2(180f, 80f);
        }

        private void SetVisible(bool visible)
        {
            if (markerText != null && markerText.gameObject.activeSelf != visible)
            {
                markerText.gameObject.SetActive(visible);
            }
        }

        private void OnDisable()
        {
            SetVisible(false);
        }
    }
}
