using System.Collections;
using EchoProtocol.Networking;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EchoProtocol.UI
{
    [DefaultExecutionOrder(1000)]
    public sealed class GameplayMenuController : MonoBehaviour
    {
        private readonly PlayerInteractionControlLock _lock = new PlayerInteractionControlLock();
        private GameObject _root;
        private RectTransform _windowRect;
        private Text _title;
        private Text _slotOne;
        private Text _slotTwo;
        private Text _teamTool;
        private GameObject _inventoryContent;
        private Button _resumeButton;
        private Button _inventoryButton;
        private Button _leaveButton;
        private PlayerInventoryDropInput _dropInput;
        private PlayerInventory _inventory;
        private bool _showingInventory;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterSceneHandler()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            SceneManager.activeSceneChanged += OnSceneChanged;
            OnSceneChanged(default, SceneManager.GetActiveScene());
        }

        private static void OnSceneChanged(Scene previous, Scene current)
        {
            if (current.name != LobbyManager.GameSceneName) return;
            if (FindAnyObjectByType<GameplayMenuController>() != null) return;
            var owner = new GameObject("GameplayMenus");
            owner.AddComponent<GameplayMenuController>();
        }

        private void Awake()
        {
            EnsureEventSystem();
            BuildCanvas();
        }

        private void Update()
        {
            if (SceneManager.GetActiveScene().name != LobbyManager.GameSceneName)
            {
                if (_lock.IsLocked) Close();
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (_lock.IsLocked)
            {
                if (_lock.ShouldAutoRelease() || _lock.ConsumeEscape()) Close();
                else if (keyboard.tabKey.wasPressedThisFrame) Open(!_showingInventory);
                else if (_showingInventory) RefreshInventory();
                return;
            }

            if (PlayerInteractionControlLock.HasModal || PlayerInteractionControlLock.EscapeConsumedThisFrame
                || PlayerInteractionControlLock.IsGameplayInputBlocked()) return;
            if (keyboard.escapeKey.wasPressedThisFrame) Open(false);
            else if (keyboard.tabKey.wasPressedThisFrame) Open(true);
        }

        private void Open(bool inventory)
        {
            var camera = FindAnyObjectByType<PlayerCamera>();
            if (camera == null || camera.Target == null) return;
            var player = camera.Target.gameObject;
            var networkObject = player.GetComponentInParent<Fusion.NetworkObject>();
            if (networkObject != null && networkObject.IsValid && !networkObject.HasInputAuthority) return;
            if (!_lock.IsLocked) _lock.Acquire(player, Close);
            if (!_lock.IsLocked) return;
            _inventory = player.GetComponentInParent<PlayerInventory>();
            _dropInput = player.GetComponentInParent<PlayerInventoryDropInput>();
            _showingInventory = inventory;
            _windowRect.sizeDelta = inventory ? new Vector2(490f, 510f) : new Vector2(470f, 315f);
            _title.text = inventory ? "INVENTORY" : "PAUSED";
            _inventoryContent.SetActive(inventory);
            _inventoryButton.gameObject.SetActive(!inventory);
            _leaveButton.gameObject.SetActive(!inventory);
            _resumeButton.GetComponentInChildren<Text>().text = inventory ? "Back" : "Resume";
            _root.SetActive(true);
            RefreshInventory();
        }

        private void RefreshInventory()
        {
            if (!_showingInventory) return;
            _slotOne.text = SlotText(0);
            _slotTwo.text = SlotText(1);
            _teamTool.text = "Team Tool: " + (_inventory != null && _inventory.TeamToolSlot != null
                ? _inventory.TeamToolSlot.DisplayName : "Empty");
        }

        private string SlotText(int slot)
        {
            var item = _inventory != null ? _inventory.GetNormalSlot(slot) : null;
            string selected = _dropInput != null && _dropInput.SelectedNormalSlot == slot ? "> " : "  ";
            return selected + "Slot " + (slot + 1) + ": " + (item != null ? item.DisplayName : "Empty");
        }

        private void Close()
        {
            _root.SetActive(false);
            _lock.Release();
            _inventory = null;
            _dropInput = null;
        }

        private void LeaveRoom()
        {
            Close();
            var bootstrap = NetworkBootstrap.Instance;
            if (bootstrap != null) _ = bootstrap.Shutdown();
        }

        private void SelectSlot(int slot)
        {
            _dropInput?.SelectNormalSlot(slot);
            RefreshInventory();
        }

        private void DropSelected()
        {
            var input = _dropInput;
            Close();
            if (input != null) StartCoroutine(DropNextFrame(input));
        }

        private static IEnumerator DropNextFrame(PlayerInventoryDropInput input)
        {
            yield return null;
            if (input != null) input.DropCurrentItem();
        }

        private void BuildCanvas()
        {
            var canvasObject = new GameObject("GameplayMenuCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _root = Panel("Overlay", canvasObject.transform, new Color(0.02f, 0.04f, 0.06f, 0.8f));
            Stretch(_root.GetComponent<RectTransform>());
            var window = Panel("Menu", _root.transform, new Color(0.08f, 0.12f, 0.15f, 0.98f));
            var rect = window.GetComponent<RectTransform>();
            _windowRect = rect;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(470f, 390f);
            rect.anchoredPosition = Vector2.zero;
            var layout = window.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 24, 24);
            layout.spacing = 12f;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            _title = Label("Title", window.transform, 27, FontStyle.Bold);
            _title.text = "PAUSED";
            _inventoryContent = new GameObject("Slots", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            _inventoryContent.transform.SetParent(window.transform, false);
            _inventoryContent.GetComponent<LayoutElement>().preferredHeight = 284f;
            var slots = _inventoryContent.GetComponent<VerticalLayoutGroup>();
            slots.spacing = 8f;
            slots.childControlHeight = true;
            slots.childForceExpandHeight = false;
            _slotOne = Label("Slot 1", _inventoryContent.transform, 19, FontStyle.Normal);
            _slotTwo = Label("Slot 2", _inventoryContent.transform, 19, FontStyle.Normal);
            _teamTool = Label("Team Tool", _inventoryContent.transform, 19, FontStyle.Normal);
            _resumeButton = ActionButton("Resume", window.transform, Close);
            _inventoryButton = ActionButton("Inventory", window.transform, () => Open(true));
            _leaveButton = ActionButton("Leave Room", window.transform, LeaveRoom);
            ActionButton("Select Slot 1", _inventoryContent.transform, () => SelectSlot(0));
            ActionButton("Select Slot 2", _inventoryContent.transform, () => SelectSlot(1));
            ActionButton("Drop Selected", _inventoryContent.transform, DropSelected);
            _root.SetActive(false);
        }

        private static GameObject Panel(string name, Transform parent, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Text Label(string name, Transform parent, int size, FontStyle style)
        {
            var owner = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            owner.transform.SetParent(parent, false);
            owner.GetComponent<LayoutElement>().preferredHeight = size + 14f;
            var text = owner.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.color = new Color(0.9f, 0.96f, 0.98f);
            text.alignment = TextAnchor.MiddleLeft;
            return text;
        }

        private static Button ActionButton(string label, Transform parent, UnityEngine.Events.UnityAction action)
        {
            var owner = Panel(label, parent, new Color(0.16f, 0.27f, 0.31f));
            owner.AddComponent<LayoutElement>().preferredHeight = 45f;
            var button = owner.AddComponent<Button>();
            button.targetGraphic = owner.GetComponent<Image>();
            button.onClick.AddListener(action);
            var text = Label("Label", owner.transform, 18, FontStyle.Normal);
            Stretch(text.rectTransform);
            text.alignment = TextAnchor.MiddleCenter;
            text.text = label;
            return button;
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var owner = new GameObject("GameplayEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            owner.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private void OnDisable() => Close();
        private void OnDestroy() => Close();
    }
}
