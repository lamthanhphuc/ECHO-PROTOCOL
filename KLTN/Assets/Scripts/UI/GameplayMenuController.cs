using EchoProtocol.Networking;
using EchoProtocol.Networking.Authority;
using EchoProtocol.Voice;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using EchoProtocol.Settings;

namespace EchoProtocol.UI
{
    [DefaultExecutionOrder(1000)]
    public sealed class GameplayMenuController : MonoBehaviour
    {
        private GameObject _overlay;
        private Button _lobby;
        private Button _mainMenu;
        private Button _resume, _options;
        private RectTransform _panel;
        private RectTransform _footer;
        private bool _transitioning;
        private readonly PlayerInteractionControlLock _lock = new PlayerInteractionControlLock();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterSceneHandler()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            SceneManager.activeSceneChanged += OnSceneChanged;
            OnSceneChanged(default, SceneManager.GetActiveScene());
        }

        private static void OnSceneChanged(Scene previous, Scene current)
        {
            if (!LobbyMapCatalog.IsGameplayScene(current.name)) return;
            if (FindAnyObjectByType<GameplayMenuController>() != null) return;
            var owner = new GameObject("GameplayMenus");
            owner.AddComponent<GameplayMenuController>();
        }

        private void Awake()
        {
            EnsureEventSystem();
        }
        private void OnEnable() => GameLanguage.Changed += RefreshLanguage;
        private void RefreshLanguage()
        {
            if (_panel == null) return;
            _panel.Find("MenuSubtitle").GetComponent<Text>().text =
                GameLanguage.Choose("Trận đấu vẫn tiếp diễn", "The match continues");
            if (_resume != null) _resume.GetComponentInChildren<Text>(true).text = GameLanguage.Choose("Tiếp tục", "Resume");
            if (_options != null) _options.GetComponentInChildren<Text>(true).text = GameLanguage.Choose("Cài đặt", "Options");
            _lobby.GetComponentInChildren<Text>(true).text = GameLanguage.Choose("Đưa cả đội về phòng chờ", "Return team to lobby");
            _mainMenu.GetComponentInChildren<Text>(true).text = GameLanguage.Choose("Về màn hình chính", "Exit to main menu");
            _footer.GetComponent<Text>().text = GameLanguage.Choose("ESC · Tiếp tục", "ESC · Resume");
        }

        private void Update()
        {
            if (!LobbyMapCatalog.IsGameplayScene(SceneManager.GetActiveScene().name))
            {
                if (_lock.IsLocked) Close();
                return;
            }

            UpdateMenuLayout();
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (_lock.IsLocked)
            {
                if (_lock.ShouldAutoRelease()) Close();
                else if (!_lock.IsTopmost) return;
                else if (_lock.ConsumeEscape()) Close();
                return;
            }

            if (PlayerInteractionControlLock.HasModal || PlayerInteractionControlLock.EscapeConsumedThisFrame
                || PlayerInteractionControlLock.IsGameplayInputBlocked()) return;
            if (keyboard.escapeKey.wasPressedThisFrame) Open();
        }

        private void Open()
        {
            var camera = FindAnyObjectByType<PlayerCamera>();
            if (camera == null || camera.Target == null) return;
            var player = camera.Target.gameObject;
            var networkObject = player.GetComponentInParent<Fusion.NetworkObject>();
            if (networkObject != null && networkObject.IsValid && !networkObject.HasInputAuthority) return;
            _lock.Acquire(player, Close, allowWhileDowned: true);
            if (!_lock.IsLocked) return;
            if (_overlay == null) BuildMenu();
            RefreshLanguage();
            UpdateMenuLayout();
            Canvas.ForceUpdateCanvases();
            _overlay.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(null);
        }

        private void Close()
        {
            var voicePanel = VoiceManager.Instance != null
                ? VoiceManager.Instance.GetComponent<VoiceSettingsPanel>() : null;
            voicePanel?.CloseFromGameplayMenu(_lock);
            if (_overlay != null) _overlay.SetActive(false);
            _lock.Release();
        }

        private bool IsHost => NetworkBootstrap.Instance?.Runner != null
            && NetworkBootstrap.Instance.Runner.IsRunning && NetworkBootstrap.Instance.Runner.IsServer
            && NetworkBootstrap.Instance.Runner.IsSceneAuthority;

        private void OpenSettings()
        {
            VoiceManager.EnsureExists();
            var settings = VoiceManager.Instance.GetComponent<VoiceSettingsPanel>();
            if (settings.OpenFromGameplayMenu(_lock, () =>
                { if (this != null && _lock.IsLocked && _overlay != null) _overlay.SetActive(true); }))
                _overlay.SetActive(false);
        }

        private void ConfirmExit(bool lobby)
        {
            if (_transitioning || lobby && !IsHost) return;
            GameUIFeedback.Instance.Confirm(
                GameLanguage.Choose(lobby ? "Đưa cả đội về phòng chờ?" : "Rời phòng?", lobby ? "Return team to lobby?" : "Leave room?"),
                () => lobby ? GameLanguage.Choose("Trận hiện tại sẽ kết thúc, không tính chiến thắng. Cả đội trở về cùng phòng chờ.",
                    "The current match ends without a win. Everyone returns to the same lobby.")
                    : IsHost ? GameLanguage.Choose("Bạn rời phòng. Quyền chủ phòng chuyển cho người còn lại; nếu không còn ai, phòng sẽ đóng.",
                    "You leave. Host authority transfers to a remaining player; an empty room closes.")
                    : GameLanguage.Choose("Bạn rời trận. Đồng đội vẫn tiếp tục chơi.", "You leave the match. Teammates continue playing."),
                () => Exit(lobby));
        }

        private async void Exit(bool lobby)
        {
            if (_transitioning || lobby && !IsHost) return;
            var bootstrap = NetworkBootstrap.Instance;
            var runner = bootstrap?.Runner;
            _transitioning = true;
            try
            {
                if (lobby)
                {
                    var authority = MatchAuthorityRuntime.Instance;
                    if (authority != null && !await authority.EndAsync("HOST_RETURN_TO_LOBBY"))
                        throw new System.InvalidOperationException("Could not end the current match. Try again.");
                    if (runner == null || runner != bootstrap.Runner || !runner.IsRunning || !runner.IsServer)
                        throw new System.InvalidOperationException("Host authority changed. Try again.");
                    Close();
                    await GameUIFeedback.LoadNetworkSceneAsync(runner, NetworkBootstrap.LobbySceneName);
                }
                else if (bootstrap != null)
                {
                    Close();
                    await bootstrap.LeaveToMainMenuAsync();
                }
            }
            catch (System.Exception error)
            {
                Debug.LogException(error);
                if (this != null) GameUIFeedback.Instance.Confirm(GameLanguage.Choose("Không thể rời trận", "Could not leave match"),
                    () => error.Message, () => { });
            }
            finally { if (this != null) _transitioning = false; }
        }

        private void BuildMenu()
        {
            if (_overlay != null) Destroy(_overlay);
            _overlay = new GameObject("PauseMenu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
            _overlay.SetActive(false);
            _overlay.transform.SetParent(transform, false);
            var canvas = _overlay.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 400;
            var scaler = _overlay.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            _overlay.GetComponent<Image>().color = new Color(0,0,0,.72f);
            var panel = new GameObject("Menu", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(_overlay.transform,false);
            var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);
            _panel = rect;
            rect.sizeDelta = new Vector2(480,400);
            panel.GetComponent<Image>().color = new Color(.02f,.035f,.038f,.98f);
            IndustrialUiStyle.Frame(panel);
            var outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(.34f,.13f,.13f,.8f);
            outline.effectDistance = new Vector2(1,-1);
            Label(panel.transform, "MENU", 28, 22, 36).gameObject.name = "MenuHeading";
            Label(panel.transform, GameLanguage.Choose("Trận đấu vẫn tiếp diễn", "The match continues"), 14, 62, 24).gameObject.name = "MenuSubtitle";
            _resume = MenuButton(panel.transform, GameLanguage.Choose("Tiếp tục", "Resume"), 108, Close);
            _options = MenuButton(panel.transform, GameLanguage.Choose("Cài đặt", "Options"), 170, OpenSettings);
            _lobby = MenuButton(panel.transform, GameLanguage.Choose("Đưa cả đội về phòng chờ", "Return team to lobby"), 244, () => ConfirmExit(true));
            _mainMenu = MenuButton(panel.transform, GameLanguage.Choose("Về màn hình chính", "Exit to main menu"), IsHost ? 306 : 244, () => ConfirmExit(false));
            _footer = Label(panel.transform, "ESC · " + GameLanguage.Choose("Tiếp tục", "Resume"), 12, 376, 22).rectTransform;
            _footer.gameObject.name = "MenuFooter";
            UpdateMenuLayout();
        }

        private void UpdateMenuLayout()
        {
            if (_panel == null || _lobby == null || _mainMenu == null || _footer == null) return;
            bool host = IsHost;
            _lobby.gameObject.SetActive(host);
            ((RectTransform)_mainMenu.transform).anchoredPosition = new Vector2(0, host ? -306 : -244);
            _footer.anchoredPosition = new Vector2(0, host ? -376 : -314);
            _panel.sizeDelta = new Vector2(480, host ? 414 : 352);
            var bounds = ((RectTransform)_overlay.transform).rect;
            _panel.localScale = Vector3.one * Mathf.Min(1f,
                Mathf.Max(.1f, (bounds.width - 32) / 480),
                Mathf.Max(.1f, (bounds.height - 32) / _panel.sizeDelta.y));
        }

        private static Text Label(Transform parent, string value, int size, float y, float height)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent,false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,1);
            rect.anchoredPosition = new Vector2(0,-y); rect.sizeDelta = new Vector2(408,height);
            var text = go.GetComponent<Text>();
            UiTypographyTheme.Apply(text, parent.GetComponent<Button>() != null);
            text.fontSize = size; text.text = value; text.alignment = TextAnchor.MiddleCenter;
            text.color = IndustrialUiStyle.Text; text.raycastTarget = false; return text;
        }

        private static Button MenuButton(Transform parent, string title, float y, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline)); go.transform.SetParent(parent,false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,1);
            rect.anchoredPosition = new Vector2(0,-y); rect.sizeDelta = new Vector2(408,50);
            go.GetComponent<Image>().color = Color.white;
            var button = go.GetComponent<Button>(); var colors = button.colors;
            colors.normalColor = new Color(.035f,.055f,.058f); colors.highlightedColor = colors.selectedColor = new Color(.23f,.065f,.065f);
            colors.pressedColor = new Color(.45f,.10f,.10f); button.colors = colors;
            go.GetComponent<Outline>().effectColor = new Color(.36f,.12f,.12f);
            Label(go.transform,title,20,0,50);
            IndustrialUiStyle.Button(button);
            button.onClick.AddListener(action); return button;
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var owner = new GameObject("GameplayEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            owner.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private void OnDisable() { GameLanguage.Changed -= RefreshLanguage; Close(); }
        private void OnDestroy() => Close();
    }
}
