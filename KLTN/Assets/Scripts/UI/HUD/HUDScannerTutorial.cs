using System;
using System.Runtime.CompilerServices;
using EchoProtocol.Networking;
using EchoProtocol.Networking.Authority;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace EchoProtocol.UI.HUD
{
    [DisallowMultipleComponent]
    public sealed class HUDScannerTutorial : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Behaviour")]
        [SerializeField, Min(0f)] private float dismissInputDelay = 0.2f;

        private readonly PlayerInteractionControlLock _controlLock = new PlayerInteractionControlLock();

        private PlayerInventory _inventory;
        private GameObject _playerRoot;
        private int _shownToolMask;
        private string _tutorialScopeKey = string.Empty;
        private int _lastToolId;
        private int _activeToolId;
        private bool _isOpen;
        private bool _releaseWhenInputClears;
        private float _dismissAllowedAt;

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            SetVisual(false);
        }

        private void Update()
        {
            if (_releaseWhenInputClears && !IsAnyButtonPressed())
            {
                _releaseWhenInputClears = false;
                _controlLock.Release();
            }

            if (!_isOpen) return;

            if (_controlLock.ShouldAutoRelease())
            {
                Hide(true);
                return;
            }

            if (Time.unscaledTime < _dismissAllowedAt) return;
            if (WasAnyButtonPressedThisFrame()) Hide(false);
        }

        public void BindPlayer(PlayerInventory inventory, GameObject playerRoot)
        {
            RefreshTutorialScope(playerRoot);

            if (_inventory == inventory && _playerRoot == playerRoot) return;

            Unbind();

            _inventory = inventory;
            _playerRoot = playerRoot;

            if (_inventory == null) return;

            _inventory.InventoryChanged += HandleInventoryChanged;

            int currentToolId = GetCurrentToolId();
            _lastToolId = currentToolId;

            if (currentToolId > 0 && IsSupportedTool(currentToolId) && !WasShown(currentToolId)) Show(currentToolId);
        }

        public void Unbind()
        {
            if (_inventory != null)
            {
                _inventory.InventoryChanged -= HandleInventoryChanged;
            }

            Hide();
            _inventory = null;
            _playerRoot = null;
            _lastToolId = 0;
            _activeToolId = 0;
        }

        private void HandleInventoryChanged()
        {
            int toolId = GetCurrentToolId();
            if (toolId > 0 && toolId != _lastToolId && IsSupportedTool(toolId) && !WasShown(toolId)) Show(toolId);
            _lastToolId = toolId;
        }

        private int GetCurrentToolId()
        {
            return _inventory == null ? 0 : PlayerInventory.ResolveToolId(_inventory.TeamToolSlot);
        }

        private static bool IsSupportedTool(int toolId)
        {
            return toolId == 1 || toolId == 2 || toolId == 3 || toolId == 4 || toolId == 6;
        }

        private static string ResolveTutorialScopeKey(GameObject playerRoot)
        {
            if (playerRoot == null) return string.Empty;

            LobbyPlayerState lobbyState = playerRoot.GetComponent<LobbyPlayerState>();
            if (lobbyState != null && lobbyState.Object != null && lobbyState.Object.IsValid)
            {
                int playerId = lobbyState.Object.InputAuthority.PlayerId;
                MatchAuthorityRuntime authority = MatchAuthorityRuntime.Instance;

                if (authority != null && authority.TryGetMatchId(out Guid matchId)) return $"{matchId:D}:{playerId}";
                if (lobbyState.Runner != null && lobbyState.Runner.SessionInfo.IsValid) return $"{lobbyState.Runner.SessionInfo.Name}:{playerId}";

                return $"network:{playerId}";
            }

            return $"offline:{RuntimeHelpers.GetHashCode(playerRoot)}";
        }

        private void RefreshTutorialScope(GameObject playerRoot)
        {
            string nextScope = ResolveTutorialScopeKey(playerRoot);
            if (string.Equals(_tutorialScopeKey, nextScope, StringComparison.Ordinal)) return;

            Hide();
            _tutorialScopeKey = nextScope;
            _shownToolMask = 0;
            _lastToolId = 0;
            _activeToolId = 0;
        }

        private bool WasShown(int toolId)
        {
            return toolId > 0 && (_shownToolMask & (1 << toolId)) != 0;
        }

        private void MarkShown(int toolId)
        {
            if (toolId > 0) _shownToolMask |= 1 << toolId;
        }

        private void Show(int toolId)
        {
            if (_isOpen || _playerRoot == null || !IsSupportedTool(toolId) || WasShown(toolId)) return;

            MarkShown(toolId);
            _activeToolId = toolId;
            _isOpen = true;
            _releaseWhenInputClears = false;
            ConfigureLayout(toolId);
            SetVisual(true);
            _controlLock.Acquire(_playerRoot, Hide, unlockCursor: false);
            _dismissAllowedAt = Time.unscaledTime + dismissInputDelay;
        }

        public void Hide() => Hide(true);

        private void Hide(bool releaseNow)
        {
            if (!_isOpen)
            {
                if (releaseNow)
                {
                    _releaseWhenInputClears = false;
                    _controlLock.Release();
                }
                return;
            }

            _isOpen = false;
            SetVisual(false);
            if (releaseNow || !IsAnyButtonPressed())
            {
                _releaseWhenInputClears = false;
                _controlLock.Release();
            }
            else
            {
                _releaseWhenInputClears = true;
            }
        }

        private void SetVisual(bool visible)
        {
            if (canvasGroup == null) return;

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = visible;
        }

        private void ConfigureLayout(int toolId)
        {
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);

            var root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;

            var background = Image("Background", transform, new Color(0f, 0f, 0f, 0.68f));
            Stretch(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var panel = Image("Panel", transform, new Color(0.025f, 0.035f, 0.038f, 0.96f));
            var panelRect = panel.rectTransform;
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;

            ConfigureToolCard(panelRect, toolId);
        }

        private void ConfigureToolCard(RectTransform panel, int toolId)
        {
            string title, controls, description;
            string interact = EchoProtocol.Settings.GameplayInputSettings.GetKeyLabel(
                EchoProtocol.Settings.GameplayAction.Interact);
            switch (toolId)
            {
                case 1:
                    title = "Máy quét hiện trường";
                    controls = "[Chuột trái]  Quét khu vực trong 10 giây\n[Chuột phải]  Đổi giữa lõi năng lượng và Stalker";
                    description = "Bạn ở giữa radar. Chế độ lõi tìm nguồn năng lượng gần bạn.\nChế độ Stalker chỉ phát hiện Stalker đang di chuyển.";
                    break;
                case 2:
                    title = "Máy tạo tiếng động";
                    controls = "[Chuột trái]  Đặt thiết bị\nVị trí xem trước cho biết nơi thiết bị sẽ được đặt.";
                    description = "Đèn đỏ nhấp nháy báo khu vực đang thu hút Stalker.\nRời khỏi khu vực sau khi đặt thiết bị.";
                    break;
                case 3:
                    title = "Bộ sơ cứu";
                    controls = $"[Giữ {interact} / Chuột trái]  Cứu đồng đội\nNhắm vào người bị gục và giữ đến khi cứu hoàn tất.";
                    description = "Chỉ dùng để cứu người bị gục.\nKhông hồi máu cho người vẫn còn đứng.";
                    break;
                case 4:
                    title = "Ván chèn cửa";
                    controls = $"[{interact} / Chuột trái]  Chèn cửa\nChỉ gắn ván vào cửa đã bị phá.";
                    description = "Ván chặn Stalker trong thời gian ngắn.\nStalker có thể phá ván để đi qua.";
                    break;
                case 6:
                    title = "Bộ ổn định lõi";
                    controls = "[Chuột trái]  Kích hoạt\nVùng ổn định bán kính 5 m, kéo dài 15 giây.";
                    description = "Người mang lõi trong vùng có thể chạy nước rút bình thường.\nHồi chiêu 45 giây; theo dõi trên ô trang bị.";
                    break;
                default: return;
            }
            panel.sizeDelta = new Vector2(620f, 300f);
            TextLabel("Title", panel, title, 23, TextAnchor.MiddleLeft,
                FontStyle.Bold, HUDPresentationStyle.Ink, 20f, 36f);
            TextLabel("Controls", panel, controls, 16, TextAnchor.UpperLeft,
                FontStyle.Normal, HUDPresentationStyle.Ink, 82f, 62f);
            TextLabel("Description", panel, description, 14, TextAnchor.UpperLeft,
                FontStyle.Normal, HUDPresentationStyle.Muted, 164f, 64f);
            TextLabel("ContinueText", panel, "Nhấn phím bất kỳ để tiếp tục", 12,
                TextAnchor.MiddleLeft, FontStyle.Normal, HUDPresentationStyle.Muted, 250f, 24f);
        }

        private static Image Image(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text TextLabel(string name, Transform parent, string text, int size, TextAnchor anchor, FontStyle style, Color color, float top, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = anchor;
            label.supportRichText = true;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.color = color;
            label.raycastTarget = false;
            if (height > 0f) Stretch((RectTransform)go.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -top - height), new Vector2(-24f, -top));
            return label;
        }

        private static void Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        private static bool WasAnyButtonPressedThisFrame()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;

            if (Mouse.current != null
                && (Mouse.current.leftButton.wasPressedThisFrame
                    || Mouse.current.rightButton.wasPressedThisFrame
                    || Mouse.current.middleButton.wasPressedThisFrame
                    || Mouse.current.forwardButton.wasPressedThisFrame
                    || Mouse.current.backButton.wasPressedThisFrame))
            {
                return true;
            }

            if (Gamepad.current != null)
            {
                foreach (var control in Gamepad.current.allControls)
                {
                    if (control is ButtonControl button && button.wasPressedThisFrame) return true;
                }
            }

            return false;
        }

        private static bool IsAnyButtonPressed()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.isPressed) return true;

            if (Mouse.current != null
                && (Mouse.current.leftButton.isPressed
                    || Mouse.current.rightButton.isPressed
                    || Mouse.current.middleButton.isPressed
                    || Mouse.current.forwardButton.isPressed
                    || Mouse.current.backButton.isPressed))
            {
                return true;
            }

            if (Gamepad.current != null)
            {
                foreach (var control in Gamepad.current.allControls)
                {
                    if (control is ButtonControl button && button.isPressed) return true;
                }
            }

            return false;
        }

        private void OnDisable()
        {
            Unbind();
        }
    }
}
