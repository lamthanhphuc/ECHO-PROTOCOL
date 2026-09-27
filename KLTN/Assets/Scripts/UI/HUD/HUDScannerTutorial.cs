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

        private static bool s_shownThisSession;
        private readonly PlayerInteractionControlLock _controlLock = new PlayerInteractionControlLock();

        private PlayerInventory _inventory;
        private GameObject _playerRoot;
        private bool _hadScanner;
        private bool _shownThisMatch;
        private bool _isOpen;
        private bool _releaseWhenInputClears;
        private float _dismissAllowedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSessionState()
        {
            s_shownThisSession = false;
        }

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            ConfigureLayout();
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

            _hadScanner = HasScanner();
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
            _hadScanner = false;
        }

        private void HandleInventoryChanged()
        {
            bool hasScanner = HasScanner();
            if (hasScanner && !_hadScanner && !_shownThisMatch && !s_shownThisSession) Show();
            _hadScanner = hasScanner;
        }

        private bool HasScanner()
        {
            return _inventory != null && PlayerInventory.ResolveToolId(_inventory.TeamToolSlot) == 1;
        }

        private void Show()
        {
            if (_isOpen || _shownThisMatch || s_shownThisSession || _playerRoot == null) return;

            s_shownThisSession = true;
            _shownThisMatch = true;
            _isOpen = true;
            _releaseWhenInputClears = false;
            SetVisual(true);
            _controlLock.Acquire(_playerRoot, Hide);
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

        private void ConfigureLayout()
        {
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);

            var root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;

            var background = Image("Background", transform, new Color(0f, 0f, 0f, 0.68f));
            Stretch(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var panel = Image("Panel", transform, new Color(0.025f, 0.035f, 0.038f, 0.96f));
            panel.gameObject.AddComponent<Outline>().effectColor = new Color(0.25f, 0.55f, 0.55f, 0.55f);
            var panelRect = panel.rectTransform;
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(820f, 480f);

            TextLabel("Title", panelRect, "MÁY QUÉT HIỆN TRƯỜNG", 31, TextAnchor.MiddleCenter, FontStyle.Bold, new Color(0.58f, 0.95f, 0.92f), 28, 48);

            var controls = Row("ControlsRow", panelRect, 88, 112);
            ControlBlock("ScanControl", controls, "[CHUỘT TRÁI]\n<size=27>QUÉT</size>\n<size=20>Quét khu vực trong 10 giây.</size>");
            ControlBlock("ModeControl", controls, "[CHUỘT PHẢI]\n<size=27>ĐỔI CHẾ ĐỘ</size>\n<size=20>Lõi năng lượng ↔ Stalker</size>");

            var modes = Row("ModesRow", panelRect, 220, 98);
            InfoBlock("CoreMode", modes, "◈ LÕI NĂNG LƯỢNG\n<size=21>Tìm lõi gần bạn</size>", new Color(0.35f, 0.78f, 0.76f));
            InfoBlock("StalkerMode", modes, "⚠ STALKER\n<size=21>Phát hiện Stalker đang di chuyển</size>", new Color(0.94f, 0.43f, 0.29f));

            TextLabel("RadarHint", panelRect, "▲ BẠN LUÔN Ở GIỮA RA-ĐA", 23, TextAnchor.MiddleCenter, FontStyle.Bold, new Color(0.78f, 0.84f, 0.84f), 342, 34);
            TextLabel("ContinueText", panelRect, "NHẤN PHÍM BẤT KỲ ĐỂ TIẾP TỤC", 21, TextAnchor.MiddleCenter, FontStyle.Bold, new Color(0.95f, 0.86f, 0.52f), 414, 32);
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
