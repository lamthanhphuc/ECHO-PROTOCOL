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

        private LobbyPlayerState _playerState;
        private Canvas _canvas;
        private RectTransform _canvasRect;
        private TextMeshProUGUI _nameText;
        private string _displayedName;

        private void Awake()
        {
            _playerState = GetComponent<LobbyPlayerState>();
            CreateNameplate();
        }

        private void LateUpdate()
        {
            bool show = SceneManager.GetActiveScene().name == "Lobby"
                && _playerState != null
                && _playerState.Object != null
                && _playerState.Object.IsValid;

            if (_canvas.enabled != show) _canvas.enabled = show;
            if (!show) return;

            string name = _playerState.OperatorName.ToString();
            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"Player {_playerState.Object.InputAuthority.PlayerId}";
            }

            if (_displayedName != name)
            {
                _displayedName = name;
                _nameText.text = name;
            }

            Camera camera = Camera.main;
            if (camera == null) return;

            Vector3 awayFromCamera = _canvasRect.position - camera.transform.position;
            if (awayFromCamera.sqrMagnitude > 0.001f)
            {
                _canvasRect.rotation = Quaternion.LookRotation(awayFromCamera, camera.transform.up);
            }
        }

        private void CreateNameplate()
        {
            var canvasObject = new GameObject("LobbyNameplate", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            _canvasRect = canvasObject.GetComponent<RectTransform>();
            _canvasRect.localPosition = Vector3.up * heightAbovePlayer;
            _canvasRect.localScale = Vector3.one * 0.01f;
            _canvasRect.sizeDelta = new Vector2(120f, 32f);

            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = 100;
            _canvas.enabled = false;

            var textObject = new GameObject("PlayerName", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(canvasObject.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            _nameText = textObject.GetComponent<TextMeshProUGUI>();
            _nameText.alignment = TextAlignmentOptions.Center;
            _nameText.color = new Color(0.9f, 0.98f, 1f);
            _nameText.fontSize = 20f;
            _nameText.fontSizeMin = 10f;
            _nameText.fontSizeMax = 20f;
            _nameText.enableAutoSizing = true;
            _nameText.enableWordWrapping = false;
            _nameText.overflowMode = TextOverflowModes.Truncate;
            _nameText.richText = false;
            _nameText.raycastTarget = false;

            var shadow = textObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }
    }
}
