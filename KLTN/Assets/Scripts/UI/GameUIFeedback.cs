using System;
using EchoProtocol.Networking;
using EchoProtocol.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EchoProtocol.UI
{
    /// <summary>Persistent confirmations, network transitions and non-modal interaction feedback.</summary>
    public sealed class GameUIFeedback : MonoBehaviour
    {
        private static GameUIFeedback _instance;
        private readonly PlayerInteractionControlLock _lock = new();
        private readonly PlayerInteractionControlLock _progressLock = new();
        private GameObject _modal, _progress, _toast;
        private TMP_Text _title, _body, _acceptText, _cancelText, _progressText, _toastText;
        private Button _accept, _cancel, _progressCancel;
        private Action _accepted, _cancelled;
        private Func<string> _bodyProvider;
        private bool _resolutionDialog, _returning;
        private float _toastUntil, _lastRejection;
        private NetworkSessionState _previousState;
        private NetworkBootstrap _previousBootstrap;
        private string _toastSource;

        public static GameUIFeedback Instance
        {
            get
            {
                if (_instance == null) new GameObject("GameUIFeedback", typeof(RectTransform)).AddComponent<GameUIFeedback>();
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => _instance = null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize() { _ = Instance; }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            _modal = Layer("Confirmation", new Color(0, 0, 0, 0.76f));
            var card = Card(_modal.transform, 560, 260);
            _title = Label(card, 24, 22, 512, 36, 22);
            _body = Label(card, 24, 70, 512, 105, 17);
            _cancel = Button(card, "Cancel", 24, 194, 242, 42, out _cancelText);
            _accept = Button(card, "Accept", 282, 194, 254, 42, out _acceptText);
            _cancel.onClick.AddListener(() => Complete(false));
            _accept.onClick.AddListener(() => Complete(true));
            _cancel.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = _accept, selectOnUp = _accept, selectOnDown = _accept };
            _accept.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = _cancel, selectOnUp = _cancel, selectOnDown = _cancel };

            _progress = Layer("NetworkTransition", new Color(0.018f, 0.022f, 0.024f, 1f));
            var progressCard = Card(_progress.transform, 600, 230);
            _progressText = Label(progressCard, 28, 30, 544, 112, 19);
            _progressCancel = Button(progressCard, "ReturnToMenu", 28, 162, 544, 40, out var progressCancelText);
            progressCancelText.text = GameLanguage.Choose("Hủy và về menu", "Cancel and return to menu");
            _progressCancel.onClick.AddListener(ReturnToMenu);

            _toast = new GameObject("InteractionFeedback", typeof(RectTransform), typeof(Image));
            _toast.transform.SetParent(transform, false);
            var toastRect = _toast.GetComponent<RectTransform>();
            toastRect.anchorMin = toastRect.anchorMax = new Vector2(0.5f, 0);
            toastRect.pivot = new Vector2(0.5f, 0);
            toastRect.anchoredPosition = new Vector2(0, 220);
            toastRect.sizeDelta = new Vector2(560, 58);
            _toast.GetComponent<Image>().color = new Color(0.04f, 0.045f, 0.045f, 0.96f);
            _toast.GetComponent<Image>().raycastTarget = false;
            _toastText = Label(_toast.transform, 16, 8, 528, 42, 16);
            _modal.SetActive(false); _progress.SetActive(false); _toast.SetActive(false);
            NetworkPlayerInteractor.LocalRequestCompleted += InteractionCompleted;
            SceneManager.activeSceneChanged += SceneChanged;
        }

        public void Confirm(string title, Func<string> body, Action accepted, string acceptLabel = null, Action cancelled = null)
        {
            if (_modal.activeSelf) return;
            EnsureEventSystem();
            _accepted = accepted; _cancelled = cancelled; _bodyProvider = body;
            _title.text = title;
            _body.text = body();
            _acceptText.text = acceptLabel ?? GameLanguage.Choose("Xác nhận", "Confirm");
            _cancelText.text = GameLanguage.Choose("Hủy", "Cancel");
            _lock.Acquire(LocalPlayer(), () => Complete(false), allowWhileDowned: true);
            _modal.SetActive(true);
            _cancel.Select();
        }

        public static string LeaveWarning() => NetworkBootstrap.Instance != null && NetworkBootstrap.Instance.Runner != null
            && NetworkBootstrap.Instance.Runner.IsServer
            ? GameLanguage.Choose("Bạn là host. Rời phòng sẽ ngắt kết nối cả đội và kết thúc trận đang chơi.",
                "You are the host. Leaving will disconnect the team and end any active match.")
            : GameLanguage.Choose("Bạn sẽ rời phòng hiện tại và ngắt kết nối với đồng đội.", "You will leave the current room and disconnect from your teammates.");

        private void Complete(bool accept)
        {
            if (!_modal.activeSelf) return;
            var action = accept ? _accepted : _cancelled;
            _modal.SetActive(false);
            _accepted = _cancelled = null; _bodyProvider = null; _resolutionDialog = false;
            _lock.Release();
            action?.Invoke();
        }

        private void Update()
        {
            GameGraphicsSettings.TickResolutionPreview();
            if (GameGraphicsSettings.HasResolutionPreview && !_modal.activeSelf)
            {
                Confirm(GameLanguage.Choose("Giữ thay đổi hiển thị?", "Keep display changes?"),
                    () => GameLanguage.Choose($"Tự khôi phục cấu hình cũ sau {GameGraphicsSettings.ResolutionPreviewSecondsRemaining:0} giây.",
                        $"Previous settings will be restored in {GameGraphicsSettings.ResolutionPreviewSecondsRemaining:0} seconds."),
                    GameGraphicsSettings.ConfirmResolution, GameLanguage.Choose("Giữ thay đổi", "Keep changes"), GameGraphicsSettings.RevertResolution);
                _resolutionDialog = true;
            }
            if (_modal.activeSelf)
            {
                if (_resolutionDialog && !GameGraphicsSettings.HasResolutionPreview) Complete(false);
                else if (_lock.ConsumeEscape()) Complete(false);
                else if (_bodyProvider != null) _body.text = _bodyProvider();
            }
            var network = NetworkBootstrap.Instance;
            if (network != _previousBootstrap) { _previousBootstrap = network; _previousState = network != null ? network.State : NetworkSessionState.Disconnected; }
            bool retry = network != null && network.IsReconnecting;
            bool loading = network != null && network.IsSceneLoading;
            bool joining = network != null && network.State == NetworkSessionState.Connecting;
            bool show = retry || loading || _returning || joining;
            bool opened = show && !_progress.activeSelf;
            if (show && !_progress.activeSelf)
            {
                EnsureEventSystem();
            }
            if (show)
            {
                var player = LocalPlayer();
                if (!_progressLock.IsLocked || _progressLock.Player != player)
                    _progressLock.Acquire(player, allowWhileDowned: true);
            }
            if (!show) _progressLock.Release();
            _progress.SetActive(show);
            _progressCancel.gameObject.SetActive(retry);
            if (opened && retry) _progressCancel.Select();
            if (retry)
            {
                _progressText.text = GameLanguage.Choose(
                    $"Đang kết nối lại…\nLần thử {network.ReconnectAttempt} · Còn {Mathf.CeilToInt(network.ReconnectSecondsRemaining)} giây\nKiểm tra mạng hoặc hủy để về menu.",
                    $"Reconnecting…\nAttempt {network.ReconnectAttempt} · {Mathf.CeilToInt(network.ReconnectSecondsRemaining)} seconds remaining\nCheck your network, or cancel to return to the menu.");
                _progressCancel.GetComponentInChildren<TMP_Text>().text = GameLanguage.Choose("Hủy và về menu", "Cancel and return to menu");
            }
            else _progressText.text = _returning ? GameLanguage.Choose("Đang rời phòng…", "Leaving room…")
                : loading ? GameLanguage.Choose("Đang tải khu vực…\nVui lòng chờ.", "Loading area…\nPlease wait.")
                : GameLanguage.Choose("Đang kết nối…\nVui lòng chờ.", "Connecting…\nPlease wait.");
            if (network != null && network.State != _previousState)
            {
                if (network.State == NetworkSessionState.Failed && !retry
                    && SceneManager.GetActiveScene().name == LobbyManager.GameSceneName)
                    Confirm(GameLanguage.Choose("Mất kết nối", "Disconnected"),
                        () => GameLanguage.Translate(NetworkLobbyUI.ConnectionFeedback(network.LastError)),
                        ReturnToMenu, GameLanguage.Choose("Về menu", "Return to menu"));
                _previousState = network.State;
            }
            _toast.SetActive(Time.unscaledTime < _toastUntil && !show && !_modal.activeSelf);
            if (_toast.activeSelf) _toastText.text = GameLanguage.Translate(_toastSource);
        }

        private async void ReturnToMenu()
        {
            if (_returning) return;
            _returning = true;
            try
            {
                if (NetworkBootstrap.Instance != null) await NetworkBootstrap.Instance.LeaveToMainMenuAsync();
                else SceneManager.LoadScene(EchoProtocol.Core.GameConstants.SceneMainMenu);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Toast(GameLanguage.Choose("Không thể rời phòng. Vui lòng thử lại.", "Could not leave the room. Please retry."));
            }
            finally { _returning = false; }
        }

        public void Toast(string message)
        {
            _toastSource = message;
            _toastUntil = Time.unscaledTime + 3f;
        }

        private void InteractionCompleted(InteractionRequestResult response)
        {
            if (response.Accepted || response.Result == InteractionValidationResult.DuplicateRequest
                || Time.unscaledTime - _lastRejection < 0.75f) return;
            _lastRejection = Time.unscaledTime;
            string message = response.Result switch
            {
                InteractionValidationResult.OutOfRange => GameLanguage.Choose("Đứng gần vật thể hơn để tương tác.", "Move closer to interact."),
                InteractionValidationResult.MissingRequiredTool => GameLanguage.Choose("Bạn chưa có công cụ cần thiết.", "You do not have the required tool."),
                InteractionValidationResult.OnCooldown => GameLanguage.Choose("Thao tác đang hồi. Chờ một chút rồi thử lại.", "This action is on cooldown. Wait a moment and retry."),
                InteractionValidationResult.InvalidTarget => GameLanguage.Choose("Vật thể không còn khả dụng. Chọn lại mục tiêu.", "This object is no longer available. Select another target."),
                InteractionValidationResult.InvalidTargetState => GameLanguage.Choose("Vật thể chưa sẵn sàng cho thao tác này.", "This object is not ready for that action."),
                _ => GameLanguage.Choose("Chưa thể tương tác lúc này. Kiểm tra trạng thái nhân vật rồi thử lại.", "Cannot interact now. Check your character state and retry.")
            };
            Toast(message);
        }

        private void SceneChanged(Scene oldScene, Scene newScene)
        {
            Complete(false);
            _toastUntil = 0;
            _lock.Release();
            _progressLock.Release();
        }
        private void OnDestroy()
        {
            NetworkPlayerInteractor.LocalRequestCompleted -= InteractionCompleted;
            SceneManager.activeSceneChanged -= SceneChanged;
            if (GameGraphicsSettings.HasResolutionPreview) GameGraphicsSettings.RevertResolution();
            _lock.Release();
            _progressLock.Release();
            if (_instance == this) _instance = null;
        }

        private GameObject LocalPlayer()
        {
            var camera = FindAnyObjectByType<PlayerCamera>();
            return camera != null && camera.Target != null ? camera.Target.gameObject : gameObject;
        }
        private static void EnsureEventSystem()
        {
            if (EventSystem.current == null)
                new GameObject("FeedbackEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule))
                    .GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        private GameObject Layer(string name, Color color)
        {
            var owner = new GameObject(name, typeof(RectTransform), typeof(Image));
            owner.transform.SetParent(transform, false);
            SettingsMenuWidgets.Stretch(owner.GetComponent<RectTransform>());
            owner.GetComponent<Image>().color = color;
            return owner;
        }
        private static Transform Card(Transform parent, float width, float height)
        {
            var owner = new GameObject("Card", typeof(RectTransform), typeof(Image));
            owner.transform.SetParent(parent, false);
            var rect = owner.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            owner.GetComponent<Image>().color = new Color32(24, 29, 30, 255);
            return owner.transform;
        }
        private static TMP_Text Label(Transform parent, float x, float y, float w, float h, float size)
        {
            var label = SettingsMenuWidgets.Label("Label", parent, x, y, w, h, string.Empty, size);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            return label;
        }
        private static Button Button(Transform parent, string name, float x, float y, float w, float h, out TMP_Text text)
        {
            var button = SettingsMenuWidgets.Button(name, parent, x, y, w, h, string.Empty, false, 16);
            text = button.GetComponentInChildren<TMP_Text>();
            return button;
        }
    }
}
