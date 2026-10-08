using EchoProtocol.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EchoProtocol.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LobbyPlayerState))]
    public sealed class LobbyPlayerNameplate : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float heightAbovePlayer = 1.35f;

        private static readonly Color NameColor =
            new Color32(216, 220, 218, 255);

        private static readonly Color ReadyColor =
            new Color32(117, 143, 121, 255);

        private static readonly Color NotReadyColor =
            new Color32(120, 126, 128, 225);

        private LobbyPlayerState _playerState;
        private Canvas _canvas;
        private RectTransform _canvasRect;
        private TextMeshProUGUI _nameText;
        private TextMeshProUGUI _stateText;

        private string _displayedName;
        private bool _displayedReady;

        private void Awake()
        {
            _playerState = GetComponent<LobbyPlayerState>();
            CreateNameplate();
        }

        private void LateUpdate()
        {
            bool show =
                SceneManager.GetActiveScene().name == "Lobby"
                && _playerState != null
                && _playerState.Object != null
                && _playerState.Object.IsValid
                && !_playerState.IsGameplayPlayer;

            if (_canvas.enabled != show)
            {
                _canvas.enabled = show;
            }

            if (!show)
            {
                return;
            }

            string name = _playerState.OperatorName.ToString();

            if (string.IsNullOrWhiteSpace(name))
            {
                name =
                    $"Player {_playerState.Object.InputAuthority.PlayerId}";
            }

            bool localPlayer =
                _playerState.Object.HasInputAuthority;

            string displayName =
                localPlayer
                    ? $"{name}   YOU"
                    : name;

            if (_displayedName != displayName)
            {
                _displayedName = displayName;
                _nameText.text = displayName;
            }

            bool ready = _playerState.IsReady;

            if (_displayedReady != ready
                || _stateText.text.Length == 0)
            {
                _displayedReady = ready;

                _stateText.text =
                    ready
                        ? "READY"
                        : "NOT READY";

                _stateText.color =
                    ready
                        ? ReadyColor
                        : NotReadyColor;
            }

            Camera camera = Camera.main;

            if (camera == null)
            {
                return;
            }

            Vector3 awayFromCamera =
                _canvasRect.position
                - camera.transform.position;

            if (awayFromCamera.sqrMagnitude > 0.001f)
            {
                _canvasRect.rotation =
                    Quaternion.LookRotation(
                        awayFromCamera,
                        camera.transform.up);
            }
        }

        private void CreateNameplate()
        {
            var canvasObject =
                new GameObject(
                    "LobbyNameplate",
                    typeof(RectTransform),
                    typeof(Canvas));

            canvasObject.transform.SetParent(
                transform,
                false);

            _canvasRect =
                canvasObject.GetComponent<RectTransform>();

            _canvasRect.localPosition =
                Vector3.up * heightAbovePlayer;

            _canvasRect.localScale =
                Vector3.one * 0.01f;

            _canvasRect.sizeDelta =
                new Vector2(160f, 48f);

            _canvas =
                canvasObject.GetComponent<Canvas>();

            _canvas.renderMode =
                RenderMode.WorldSpace;

            _canvas.overrideSorting = true;
            _canvas.sortingOrder = 100;
            _canvas.enabled = false;

            CreateNameText(canvasObject.transform);
            CreateStateText(canvasObject.transform);
        }

        private void CreateNameText(Transform parent)
        {
            var textObject =
                new GameObject(
                    "PlayerName",
                    typeof(RectTransform),
                    typeof(TextMeshProUGUI));

            textObject.transform.SetParent(
                parent,
                false);

            var rect =
                textObject.GetComponent<RectTransform>();

            rect.anchorMin =
                new Vector2(0f, 0.38f);

            rect.anchorMax =
                new Vector2(1f, 1f);

            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _nameText =
                textObject.GetComponent<TextMeshProUGUI>();

            _nameText.alignment =
                TextAlignmentOptions.Center;

            _nameText.color =
                NameColor;

            _nameText.fontSize = 19f;
            _nameText.fontSizeMin = 11f;
            _nameText.fontSizeMax = 19f;
            _nameText.enableAutoSizing = true;

            _nameText.textWrappingMode =
                TextWrappingModes.NoWrap;

            _nameText.overflowMode =
                TextOverflowModes.Truncate;

            _nameText.richText = false;
            _nameText.raycastTarget = false;

            AddShadow(textObject);
        }

        private void CreateStateText(Transform parent)
        {
            var textObject =
                new GameObject(
                    "PlayerState",
                    typeof(RectTransform),
                    typeof(TextMeshProUGUI));

            textObject.transform.SetParent(
                parent,
                false);

            var rect =
                textObject.GetComponent<RectTransform>();

            rect.anchorMin =
                new Vector2(0f, 0f);

            rect.anchorMax =
                new Vector2(1f, 0.38f);

            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _stateText =
                textObject.GetComponent<TextMeshProUGUI>();

            _stateText.alignment =
                TextAlignmentOptions.Top;

            _stateText.text =
                "NOT READY";

            _stateText.color =
                NotReadyColor;

            _stateText.fontSize = 10.5f;
            _stateText.fontStyle =
                FontStyles.Bold;

            _stateText.characterSpacing = 2f;

            _stateText.textWrappingMode =
                TextWrappingModes.NoWrap;

            _stateText.richText = false;
            _stateText.raycastTarget = false;

            AddShadow(textObject);
        }

        private static void AddShadow(
            GameObject target)
        {
            var shadow =
                target.AddComponent<Shadow>();

            shadow.effectColor =
                new Color(
                    0f,
                    0f,
                    0f,
                    0.9f);

            shadow.effectDistance =
                new Vector2(1f, -1f);

            shadow.useGraphicAlpha = true;
        }
    }
}
