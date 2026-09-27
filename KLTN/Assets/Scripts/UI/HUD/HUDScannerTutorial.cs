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

        private static int s_shownToolMask;
        private readonly PlayerInteractionControlLock _controlLock = new PlayerInteractionControlLock();

        private PlayerInventory _inventory;
        private GameObject _playerRoot;
        private int _lastToolId;
        private int _activeToolId;
        private bool _isOpen;
        private bool _releaseWhenInputClears;
        private float _dismissAllowedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSessionState()
        {
            s_shownToolMask = 0;
        }

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
            if (_inventory == inventory && _playerRoot == playerRoot) return;

            Unbind();

            _inventory = inventory;
            _playerRoot = playerRoot;

            if (_inventory == null) return;

            _lastToolId = GetCurrentToolId();
            _inventory.InventoryChanged += HandleInventoryChanged;
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

        private static bool WasShown(int toolId)
        {
            return toolId > 0 && (s_shownToolMask & (1 << toolId)) != 0;
        }

        private static void MarkShown(int toolId)
        {
            if (toolId > 0) s_shownToolMask |= 1 << toolId;
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

            switch (toolId)
            {
                case 1:
                    ConfigureScannerTutorial(panel, panelRect);
                    break;
                case 2:
                    ConfigureNoiseMakerTutorial(panel, panelRect);
                    break;
                case 3:
                    ConfigureFirstAidTutorial(panel, panelRect);
                    break;
                case 4:
                    ConfigurePlankTutorial(panel, panelRect);
                    break;
                case 6:
                    ConfigureCoreStabilizerTutorial(panel, panelRect);
                    break;
            }
        }

        private void ConfigureScannerTutorial(Image panel, RectTransform panelRect)
        {
            Color accent = new Color(0.58f, 0.95f, 0.92f);

            panel.gameObject.AddComponent<Outline>().effectColor = new Color(accent.r, accent.g, accent.b, 0.55f);
            panelRect.sizeDelta = new Vector2(820f, 480f);

            TextLabel("Title", panelRect, "MÁY QUÉT HIỆN TRƯỜNG", 31, TextAnchor.MiddleCenter, FontStyle.Bold, accent, 28, 48);

            var controls = Row("ControlsRow", panelRect, 88, 112);
            ControlBlock("ScanControl", controls, "[CHUỘT TRÁI]\n<size=27>QUÉT</size>\n<size=20>Quét khu vực trong 10 giây.</size>");
            ControlBlock("ModeControl", controls, "[CHUỘT PHẢI]\n<size=27>ĐỔI CHẾ ĐỘ</size>\n<size=20>Lõi năng lượng ↔ Stalker</size>");

            var modes = Row("ModesRow", panelRect, 220, 98);
            InfoBlock("CoreMode", modes, "◈ LÕI NĂNG LƯỢNG\n<size=21>Tìm lõi gần bạn</size>", new Color(0.35f, 0.78f, 0.76f));
            InfoBlock("StalkerMode", modes, "⚠ STALKER\n<size=21>Phát hiện Stalker đang di chuyển</size>", new Color(0.94f, 0.43f, 0.29f));

            TextLabel("RadarHint", panelRect, "▲ BẠN LUÔN Ở GIỮA RA-ĐA", 23, TextAnchor.MiddleCenter, FontStyle.Bold, new Color(0.78f, 0.84f, 0.84f), 342, 34);
            AddContinueText(panelRect, 414f);
        }

        private void ConfigureSimpleTutorial(Image panel, RectTransform panelRect, string title, string controlText, string infoText, string hintText, Color accent)
        {
            panel.gameObject.AddComponent<Outline>().effectColor = new Color(accent.r, accent.g, accent.b, 0.55f);
            panelRect.sizeDelta = new Vector2(820f, 400f);

            TextLabel("Title", panelRect, title, 31, TextAnchor.MiddleCenter, FontStyle.Bold, accent, 30f, 50f);

            var row = Row("TutorialRow", panelRect, 105f, 130f);
            InfoBlock("Control", row, controlText, accent);
            InfoBlock("Info", row, infoText, new Color(0.78f, 0.84f, 0.84f));

            TextLabel("Hint", panelRect, hintText, 21, TextAnchor.MiddleCenter, FontStyle.Bold, new Color(0.78f, 0.84f, 0.84f), 265f, 42f);
            AddContinueText(panelRect, 335f);
        }

        private void AddContinueText(RectTransform panelRect, float top)
        {
            TextLabel("ContinueText", panelRect, "NHẤN PHÍM BẤT KỲ ĐỂ TIẾP TỤC", 21, TextAnchor.MiddleCenter, FontStyle.Bold, new Color(0.95f, 0.86f, 0.52f), top, 32f);
        }

        private void ConfigureNoiseMakerTutorial(Image panel, RectTransform panelRect)
        {
            ConfigureSimpleTutorial(
                panel,
                panelRect,
                "MÁY TẠO TIẾNG ĐỘNG",
                "[CHUỘT TRÁI]\n<size=27>ĐẶT THIẾT BỊ</size>\n<size=20>Thiết bị được đặt phía trước bạn.</size>",
                "ĐÁNH LẠC HƯỚNG\n<size=21>Tạo tiếng động để thu hút Stalker đến vị trí khác.</size>",
                "Thiết bị sẽ bị tiêu hao sau khi sử dụng.",
                new Color(0.95f, 0.67f, 0.30f));
        }

        private void ConfigureFirstAidTutorial(Image panel, RectTransform panelRect)
        {
            ConfigureSimpleTutorial(
                panel,
                panelRect,
                "BỘ SƠ CỨU",
                "[GIỮ E / CHUỘT TRÁI]\n<size=27>CỨU ĐỒNG ĐỘI</size>\n<size=20>Nhắm vào đồng đội đang bị gục.</size>",
                "CỨU NGƯỜI BỊ GỤC\n<size=21>Không dùng để hồi máu cho người vẫn còn đứng.</size>",
                "Giữ nút cho đến khi quá trình cứu hoàn tất.",
                new Color(0.48f, 0.92f, 0.58f));
        }

        private void ConfigurePlankTutorial(Image panel, RectTransform panelRect)
        {
            ConfigureSimpleTutorial(
                panel,
                panelRect,
                "VÁN CHÈN CỬA",
                "[E / CHUỘT TRÁI]\n<size=27>CHÈN CỬA</size>\n<size=20>Chỉ gắn vào cửa đã bị phá.</size>",
                "CHẶN STALKER\n<size=21>Stalker có thể phá ván để đi qua.</size>",
                "Ván chỉ chặn Stalker trong thời gian ngắn, không khóa chết.",
                new Color(0.82f, 0.63f, 0.38f));
        }

        private void ConfigureCoreStabilizerTutorial(Image panel, RectTransform panelRect)
        {
            ConfigureSimpleTutorial(
                panel,
                panelRect,
                "BỘ ỔN ĐỊNH LÕI",
                "Ở GẦN ĐỒNG ĐỘI\n<size=27>ỔN ĐỊNH LÕI</size>\n<size=20>Giữ khoảng cách trong phạm vi 2,5 m.</size>",
                "[CHUỘT TRÁI]\n<size=27>PHÁT XUNG</size>\n<size=20>Kích hoạt phản hồi của thiết bị.</size>",
                "Ưu tiên đi cùng người đang mang Lõi năng lượng.",
                new Color(0.42f, 0.82f, 1f));
        }

        private static RectTransform Row(string name, Transform parent, float top, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            Stretch(rt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(34f, -top - height), new Vector2(-34f, -top));
            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 18f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            return rt;
        }

        private static void ControlBlock(string name, Transform parent, string text)
        {
            InfoBlock(name, parent, text, new Color(0.58f, 0.95f, 0.92f));
        }

        private static void InfoBlock(string name, Transform parent, string text, Color color)
        {
            var image = Image(name, parent, new Color(color.r * 0.08f, color.g * 0.08f, color.b * 0.08f, 0.82f));
            image.gameObject.AddComponent<Outline>().effectColor = new Color(color.r, color.g, color.b, 0.25f);
            TextLabel("Text", image.transform, text, 19, TextAnchor.MiddleCenter, FontStyle.Bold, color, 0, 0);
            Stretch((RectTransform)image.transform.GetChild(0), Vector2.zero, Vector2.one, new Vector2(12f, 8f), new Vector2(-12f, -8f));
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
