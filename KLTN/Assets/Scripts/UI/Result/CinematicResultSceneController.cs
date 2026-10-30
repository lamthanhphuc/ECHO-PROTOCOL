using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using EchoProtocol.Audio;
using EchoProtocol.Auth;
using EchoProtocol.Core;
using EchoProtocol.Networking;
using EchoProtocol.Networking.Authority;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EchoProtocol.UI.Result
{
    /// <summary>Presentation of the replicated match result. It never computes or submits rewards.</summary>
    [DisallowMultipleComponent]
    public sealed class CinematicResultSceneController : MonoBehaviour
    {
        private const string ReduceMotionKey = "EchoProtocol.Result.ReduceMotion";
        private static readonly Color VictoryAccent = new(0.43f, 0.65f, 0.57f, 1f);
        private static readonly Color DefeatAccent = new(0.76f, 0.25f, 0.27f, 1f);
        private static readonly Color Muted = new(0.62f, 0.65f, 0.62f, 1f);
        private static readonly Color PrimaryButtonFill = new(0.20f, 0.035f, 0.045f, 0.96f);
        private static readonly Color PrimaryButtonBorder = new(0.77f, 0.13f, 0.16f, 1f);
        private static readonly Color PrimaryButtonText = new(0.94f, 0.82f, 0.78f, 1f);
        private static readonly Color SecondaryButtonFill = new(0.07f, 0.085f, 0.09f, 0.94f);
        private static readonly Color SecondaryButtonBorder = new(0.48f, 0.49f, 0.47f, 0.9f);
        private static readonly Color SecondaryButtonText = new(0.84f, 0.82f, 0.77f, 1f);

        [SerializeField] private bool previewDefeatWithoutMatch;

        private ResultSnapshot _snapshot;
        private CanvasGroup _titleGroup;
        private CanvasGroup _statsGroup;
        private CanvasGroup _mediaGroup;
        private CanvasGroup _teamGroup;
        private CanvasGroup _rewardGroup;
        private CanvasGroup _actionsGroup;
        private CanvasGroup _introBlack;
        private CanvasGroup[] _statRows;
        private Button _returnButton;
        private Button _retryButton;
        private Button _motionButton;
        private Button _skipButton;
        private Image _recordingDot;
        private Image _glitchBand;
        private Image _defeatFlash;
        private Image _backgroundShade;
        private RectTransform _canvasRect;
        private RectTransform _designRoot;
        private RectTransform _controlsRoot;
        private RectTransform _titleRect;
        private RectTransform _subtitleRect;
        private RectTransform _titleRuleRect;
        private Vector2 _lastCanvasSize;
        private AudioSource _ambience;
        private CancellationTokenSource _rewardCancellation;
        private Coroutine _intro;
        private bool _reducedMotion;
        private bool _introComplete;
        private bool _navigating;
        private bool _victory;
        private float _introStartedAt;
        private Vector2 _titleBasePosition;

        private void Awake()
        {
            _snapshot = ResultSession.Current;
            _titleGroup = Find<CanvasGroup>("TitleGroup");
            _statsGroup = Find<CanvasGroup>("StatsGroup");
            _mediaGroup = Find<CanvasGroup>("MediaGroup");
            _teamGroup = Find<CanvasGroup>("TeamGroup");
            _rewardGroup = Find<CanvasGroup>("RewardGroup");
            _actionsGroup = Find<CanvasGroup>("ActionsGroup");
            _introBlack = Find<CanvasGroup>("IntroBlack");
            _statRows = new CanvasGroup[5];
            for (int i = 0; i < _statRows.Length; i++)
                _statRows[i] = Find<CanvasGroup>($"StatRow{i}");

            _returnButton = Find<Button>("ReturnButton");
            _retryButton = Find<Button>("RetryButton");
            _motionButton = Find<Button>("MotionButton");
            _skipButton = Find<Button>("SkipButton");
            _recordingDot = Find<Image>("RecordingDot");
            _glitchBand = Find<Image>("GlitchBand");
            _defeatFlash = Find<Image>("DefeatFlash");
            _backgroundShade = Find<Image>("BackgroundShade");
            _canvasRect = GetComponent<RectTransform>();
            _designRoot = Find<RectTransform>("DesignRoot");
            _controlsRoot = Find<RectTransform>("ControlsRoot");
            _titleRect = Find<RectTransform>("TitleMain");
            _subtitleRect = Find<RectTransform>("TitleSub");
            _titleRuleRect = Find<RectTransform>("AccentTitleRule");
            if (_titleRect != null) _titleBasePosition = _titleRect.anchoredPosition;

            if (_returnButton != null) _returnButton.onClick.AddListener(ReturnToLobby);
            if (_retryButton != null) _retryButton.onClick.AddListener(RetryMission);
            if (_motionButton != null) _motionButton.onClick.AddListener(ToggleReducedMotion);
            if (_skipButton != null) _skipButton.onClick.AddListener(FinishIntro);
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            bool victory = _snapshot != null
                ? _snapshot.Outcome == NetworkMatchResult.Win
                : !previewDefeatWithoutMatch;
            _victory = victory;
            SetActive("BackgroundVictory", victory);
            SetActive("BackgroundDefeat", !victory);
            BindReport(victory);
            SetTitleLayout(victory);
            ApplyPalette(victory);
            UpdateResponsiveLayout(force: true);

            _reducedMotion = PlayerPrefs.GetInt(ReduceMotionKey, 0) != 0;
            RefreshMotionLabel();
            GameAudioRuntime.EnsureInitialized();
            _ambience = GameAudioRuntime.CreateSource(gameObject, false);
            GameAudioRuntime.Loop(_ambience, victory
                ? "map_ambience/fluorescent_buzz_loop"
                : "horror_ambience/tension_drone_loop", 0.18f);

            _rewardCancellation = new CancellationTokenSource();
            if (_snapshot != null && Guid.TryParse(_snapshot.MatchId, out var matchId))
                _ = PollRewardAsync(matchId, _rewardCancellation.Token);

            if (_reducedMotion) FinishIntro();
            else
            {
                _introStartedAt = Time.unscaledTime;
                _intro = StartCoroutine(Reveal());
            }
        }

        private void Update()
        {
            UpdateResponsiveLayout();
            if (!_navigating && _snapshot != null && MatchAuthorityRuntime.Instance != null)
            {
                var saveStatus = MatchAuthorityRuntime.Instance.GetResultSaveStatus(_snapshot.MatchId);
                if (!string.IsNullOrEmpty(saveStatus)) SetText("ActionStatus", saveStatus);
                if (MatchAuthorityRuntime.Instance.IsResultPermanentlyRejected(_snapshot.MatchId))
                {
                    SetText("ActionStatus", saveStatus + EchoProtocol.Settings.GameLanguage.Choose(
                        "\nF7: thử gửi lại • F8: lưu hồ sơ đối soát", "\nF7: retry submission • F8: export recovery record"));
                    if (Keyboard.current != null && Keyboard.current.f7Key.wasPressedThisFrame)
                        MatchAuthorityRuntime.Instance.RetryRejectedResult(_snapshot.MatchId);
                    if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
                        MatchAuthorityRuntime.Instance.ExportRejectedResult(_snapshot.MatchId);
                }
            }
            if (_recordingDot != null)
                _recordingDot.color = new Color(0.78f, 0.15f, 0.18f,
                    _reducedMotion ? 1f : 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 2.8f));
            if (_backgroundShade != null && !_reducedMotion)
                _backgroundShade.color = new Color(0.025f, 0.028f, 0.028f,
                    0.55f + Mathf.PerlinNoise(Time.unscaledTime * 0.65f, 0.37f) * 0.025f);

            if (_introComplete || _reducedMotion) return;
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame
                || Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                FinishIntro();
        }

        private void OnDestroy()
        {
            if (_returnButton != null) _returnButton.onClick.RemoveListener(ReturnToLobby);
            if (_retryButton != null) _retryButton.onClick.RemoveListener(RetryMission);
            if (_motionButton != null) _motionButton.onClick.RemoveListener(ToggleReducedMotion);
            if (_skipButton != null) _skipButton.onClick.RemoveListener(FinishIntro);
            _rewardCancellation?.Cancel();
            _rewardCancellation?.Dispose();
            if (_ambience != null) _ambience.Stop();
        }

        private T Find<T>(string objectName) where T : Component
        {
            foreach (var component in GetComponentsInChildren<T>(true))
                if (component.gameObject.name == objectName) return component;
            return null;
        }

        private void SetActive(string objectName, bool active)
        {
            foreach (var child in GetComponentsInChildren<Transform>(true))
                if (child.name == objectName) { child.gameObject.SetActive(active); return; }
        }

        private void SetText(string objectName, string value)
        {
            var label = Find<Text>(objectName);
            if (label != null) label.text = value;
        }

        private void BindReport(bool victory)
        {
            var now = DateTime.Now;
            SetText("TimeStamp", now.ToString("HH:mm:ss"));
            SetText("DateStamp", now.ToString("dd MMM yyyy").ToUpperInvariant());
            string caseId = _snapshot != null && Guid.TryParse(_snapshot.MatchId, out var id)
                ? id.ToString("N")[..8].ToUpperInvariant() : "UNAVAILABLE";
            SetText("CaseFile", "CASE FILE // " + caseId);
            SetText("TitleMain", victory
                ? "YOU <color=#A53A3E>SURVIVED...</color>"
                : "THE PROTOCOL\nHAS <color=#B93E43>FAILED.</color>");
            SetText("TitleSub", victory
                ? "BUT IT'S NOT OVER."
                : "YOUR SCREAMS ARE NOW PART OF THE EXPERIMENT.");
            SetText("PhotoCaption", victory ? "EXTRACTION COMPLETE" : "ALL SIGNALS LOST");

            bool hasResult = _snapshot != null;
            SetText("StatValue0", hasResult && _snapshot.DurationSeconds >= 0f
                ? FormatDuration(_snapshot.DurationSeconds) : "--:--");
            SetText("StatValue1", hasResult && _snapshot.ObjectiveCompletion >= 0f
                ? $"{Mathf.RoundToInt(_snapshot.ObjectiveCompletion * 100f)}%" : "NO DATA");
            SetText("StatValue2", hasResult && _snapshot.Survivors >= 0 && _snapshot.TeamSize > 0
                ? $"{_snapshot.Survivors} / {_snapshot.TeamSize}" : "NO DATA");
            SetText("StatValue3", "NO DATA");
            SetText("StatValue4", "NO DATA");

            int count = hasResult ? Mathf.Min(4, _snapshot.Players.Count) : 0;
            SetActive("TeamEmpty", count == 0);
            for (int i = 0; i < 4; i++)
            {
                SetActive($"TeamRow{i}", i < count);
                if (i >= count) continue;
                var player = _snapshot.Players[i];
                var name = Find<Text>($"TeamName{i}");
                if (name != null)
                {
                    name.supportRichText = false;
                    name.text = string.IsNullOrWhiteSpace(player.Name)
                        ? "UNKNOWN OPERATOR" : player.Name;
                    name.horizontalOverflow = HorizontalWrapMode.Wrap;
                    name.verticalOverflow = VerticalWrapMode.Truncate;
                    name.resizeTextForBestFit = true;
                    name.resizeTextMinSize = 12;
                    name.resizeTextMaxSize = 20;
                }
                SetText($"TeamStatus{i}", player.Status);
                SetText($"TeamMeta{i}", player.DownedCount >= 0 && player.ReviveCount >= 0
                    ? $"LAST ZONE: DOWN {player.DownedCount} / REVIVE {player.ReviveCount}"
                    : "PERSONNEL STATUS RECORDED");
                var status = Find<Text>($"TeamStatus{i}");
                if (status != null) status.color = player.Status == "SURVIVED"
                    ? new Color(0.47f, 0.82f, 0.69f)
                    : player.Status == "DECEASED" ? DefeatAccent : Muted;
            }

            SetText("RewardXp", "-- XP");
            SetText("RewardCredits", "-- CREDITS");
            SetText("RewardStatus", hasResult ? "AWAITING SERVER CONFIRMATION" : "NO MATCH RECORD");
            var runner = NetworkBootstrap.Instance?.Runner;
            bool inRoom = runner != null && runner.IsRunning;
            bool host = inRoom && runner.IsSceneAuthority && runner.IsServer;
            SetText("RetryLabel", host ? "RETRY MISSION" : inRoom ? "HOST ONLY" : "PLAY AGAIN");
            SetText("ReturnLabel", "RETURN TO LOBBY");
            SetText("RetryHint", host ? "RESTARTS WHEN THE TEAM IS READY"
                : inRoom ? "ONLY THE HOST MAY RESTART" : "OPEN LOBBY TO START A NEW MISSION");
        }

        private void SetTitleLayout(bool victory)
        {
            var title = _titleRect != null ? _titleRect.GetComponent<Text>() : null;
            var subtitle = _subtitleRect != null ? _subtitleRect.GetComponent<Text>() : null;
            if (title != null) title.fontSize = victory ? 92 : 58;
            if (subtitle != null) subtitle.fontSize = victory ? 34 : 26;

            if (_titleRect != null)
            {
                _titleRect.anchoredPosition = new Vector2(_titleRect.anchoredPosition.x,
                    victory ? -140f : -102f);
                _titleRect.sizeDelta = new Vector2(_titleRect.sizeDelta.x,
                    victory ? 125f : 178f);
                _titleBasePosition = _titleRect.anchoredPosition;
            }
            if (_subtitleRect != null)
            {
                _subtitleRect.anchoredPosition = new Vector2(_subtitleRect.anchoredPosition.x,
                    victory ? -260f : -282f);
                _subtitleRect.sizeDelta = new Vector2(_subtitleRect.sizeDelta.x,
                    victory ? 50f : 38f);
            }
            if (_titleRuleRect != null)
                _titleRuleRect.anchoredPosition = new Vector2(_titleRuleRect.anchoredPosition.x,
                    victory ? -309f : -324f);
        }

        private void UpdateResponsiveLayout(bool force = false)
        {
            if (_canvasRect == null) return;
            var canvasSize = _canvasRect.rect.size;
            if (!force && canvasSize == _lastCanvasSize) return;
            _lastCanvasSize = canvasSize;
            float scale = Mathf.Min(canvasSize.x / 1920f, canvasSize.y / 1080f);
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f) return;
            if (_designRoot != null) _designRoot.localScale = Vector3.one * scale;
            if (_controlsRoot != null) _controlsRoot.localScale = Vector3.one * scale;
        }

        private static string FormatDuration(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return total >= 3600
                ? $"{total / 3600:00}:{total / 60 % 60:00}:{total % 60:00}"
                : $"{total / 60:00}:{total % 60:00}";
        }

        private void ApplyPalette(bool victory)
        {
            Color accent = victory ? VictoryAccent : DefeatAccent;
            foreach (var image in GetComponentsInChildren<Image>(true))
                if (image.name.StartsWith("Accent", StringComparison.Ordinal)) image.color = accent;
            var runner = NetworkBootstrap.Instance?.Runner;
            bool canRetry = runner == null || !runner.IsRunning
                || runner.IsServer && runner.IsSceneAuthority;
            bool retryIsPrimary = !victory && canRetry;
            StyleActionButton("RetryButton", "RetryLabel", retryIsPrimary, canRetry);
            StyleActionButton("ReturnButton", "ReturnLabel", !retryIsPrimary, true);
            var photo = Find<Image>("SurveillancePhoto");
            if (photo != null) photo.color = victory
                ? new Color(0.70f, 0.84f, 0.81f, 1f)
                : new Color(0.90f, 0.50f, 0.50f, 1f);
        }

        private void StyleActionButton(string buttonName, string labelName, bool primary, bool enabled)
        {
            var fill = Find<Image>(buttonName);
            if (fill != null) fill.color = primary ? PrimaryButtonFill : SecondaryButtonFill;

            Color borderColor = !enabled ? new Color(0.28f, 0.29f, 0.28f, 0.7f)
                : primary ? PrimaryButtonBorder : SecondaryButtonBorder;
            foreach (string side in new[] { "Top", "Bottom", "Left", "Right" })
            {
                var border = Find<Image>($"{buttonName}Border{side}");
                if (border != null) border.color = borderColor;
            }

            var label = Find<Text>(labelName);
            if (label != null) label.color = !enabled ? Muted
                : primary ? PrimaryButtonText : SecondaryButtonText;

            var button = Find<Button>(buttonName);
            if (button == null) return;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = primary
                ? new Color(1.35f, 1.18f, 1.18f, 1f)
                : new Color(1.25f, 1.28f, 1.28f, 1f);
            colors.pressedColor = primary
                ? new Color(0.72f, 0.56f, 0.56f, 1f)
                : new Color(0.70f, 0.74f, 0.74f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.42f, 0.42f, 0.42f, 0.75f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.12f;
            button.colors = colors;
        }

        private IEnumerator Reveal()
        {
            SetAlpha(_titleGroup, 0f);
            SetAlpha(_statsGroup, 0f);
            SetAlpha(_mediaGroup, 0f);
            SetAlpha(_teamGroup, 0f);
            SetAlpha(_rewardGroup, 0f);
            SetAlpha(_actionsGroup, 0f);
            SetAlpha(_introBlack, 1f);
            foreach (var row in _statRows) SetAlpha(row, 0f);
            SetActionsEnabled(false);

            while (!_introComplete)
            {
                float t = Time.unscaledTime - _introStartedAt;
                SetAlpha(_introBlack, 1f - Ease(t, 0.15f, 1.0f));
                if (_defeatFlash != null)
                {
                    float flash = _victory ? 0f : Mathf.Min(
                        Ease(t, 0.32f, 0.45f), 1f - Ease(t, 0.68f, 1.08f)) * 0.22f;
                    _defeatFlash.color = new Color(0.69f, 0.07f, 0.09f, flash);
                }
                SetAlpha(_titleGroup, Ease(t, 2.5f, 4.0f));
                SetAlpha(_mediaGroup, Ease(t, 2.9f, 4.1f));
                SetAlpha(_statsGroup, Ease(t, 4.0f, 4.45f));
                for (int i = 0; i < _statRows.Length; i++)
                    SetAlpha(_statRows[i], Ease(t, 4.1f + i * 0.25f, 4.55f + i * 0.25f));
                SetAlpha(_teamGroup, Ease(t, 5.5f, 6.4f));
                SetAlpha(_rewardGroup, Ease(t, 5.7f, 6.5f));
                SetAlpha(_actionsGroup, Ease(t, 6.7f, 7.0f));
                if (_glitchBand != null)
                    _glitchBand.color = new Color(0.55f, 0.66f, 0.64f,
                        t >= 1.0f && t < 4.1f
                            ? 0.035f + 0.05f * Mathf.Abs(Mathf.Sin(t * 17f)) : 0f);
                if (_titleRect != null)
                    _titleRect.anchoredPosition = _titleBasePosition
                        + new Vector2(t > 2.5f && t < 4.0f ? Mathf.Sin(t * 31f) * 1.7f : 0f, 0f);
                if (t >= 7.0f) { FinishIntro(); yield break; }
                yield return null;
            }
        }

        private static float Ease(float t, float start, float end)
        {
            float x = Mathf.InverseLerp(start, end, t);
            return x * x * (3f - 2f * x);
        }

        private static void SetAlpha(CanvasGroup group, float alpha)
        {
            if (group != null) group.alpha = alpha;
        }

        public void FinishIntro()
        {
            if (_introComplete) return;
            _introComplete = true;
            if (_intro != null) StopCoroutine(_intro);
            SetAlpha(_introBlack, 0f);
            if (_defeatFlash != null) _defeatFlash.color = Color.clear;
            SetAlpha(_titleGroup, 1f);
            SetAlpha(_statsGroup, 1f);
            SetAlpha(_mediaGroup, 1f);
            SetAlpha(_teamGroup, 1f);
            SetAlpha(_rewardGroup, 1f);
            SetAlpha(_actionsGroup, 1f);
            foreach (var row in _statRows) SetAlpha(row, 1f);
            if (_glitchBand != null) _glitchBand.color = Color.clear;
            if (_titleRect != null) _titleRect.anchoredPosition = _titleBasePosition;
            SetActionsEnabled(true);
            SetActive("SkipButton", false);
        }

        private void SetActionsEnabled(bool enabled)
        {
            if (_returnButton != null) _returnButton.interactable = enabled;
            if (_retryButton != null)
            {
                var runner = NetworkBootstrap.Instance?.Runner;
                _retryButton.interactable = enabled && (runner == null || !runner.IsRunning
                    || runner.IsServer && runner.IsSceneAuthority);
            }
        }

        private void ToggleReducedMotion()
        {
            _reducedMotion = !_reducedMotion;
            PlayerPrefs.SetInt(ReduceMotionKey, _reducedMotion ? 1 : 0);
            PlayerPrefs.Save();
            RefreshMotionLabel();
            if (_reducedMotion) FinishIntro();
        }

        private void RefreshMotionLabel() => SetText("MotionLabel",
            _reducedMotion ? "MOTION: REDUCED" : "MOTION: CINEMATIC");

        private async Task PollRewardAsync(Guid matchId, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var authority = MatchAuthorityRuntime.Instance;
                if (authority != null && authority.IsResultPermanentlyRejected(matchId.ToString("D")))
                {
                    SetText("RewardStatus", authority.GetResultSaveStatus(matchId.ToString("D")));
                    return;
                }
                var reward = await ResultRewardBridge.LoadAsync(matchId, token);
                if (token.IsCancellationRequested) return;
                if (reward.HasConfirmedReward)
                {
                    SetText("RewardXp", $"+{reward.ExperienceAwarded.Value:N0} XP");
                    SetText("RewardCredits", $"+{reward.CreditsAwarded.Value:N0} CREDITS");
                    SetText("RewardStatus", "SERVER REWARD CONFIRMED");
                    return;
                }
                SetText("RewardStatus", reward.Status switch
                {
                    ResultRewardStatus.Pending or ResultRewardStatus.NotFound => "AWAITING SERVER CONFIRMATION",
                    ResultRewardStatus.Unauthenticated => "SIGN IN TO VIEW REWARD",
                    ResultRewardStatus.Failed or ResultRewardStatus.Unavailable => "REWARD RECORD TEMPORARILY UNAVAILABLE",
                    _ => "REWARD RECORD UNAVAILABLE"
                });
                if (reward.Status == ResultRewardStatus.Unauthenticated
                    || reward.Status == ResultRewardStatus.InvalidMatchId) return;
                try { await Task.Delay(TimeSpan.FromSeconds(3), token); }
                catch (OperationCanceledException) { return; }
            }
        }

        private void ReturnToLobby() => NavigateToLobby(false);

        private void RetryMission() => NavigateToLobby(true);

        private async void NavigateToLobby(bool retry)
        {
            if (_navigating || !_introComplete) return;
            var bootstrap = NetworkBootstrap.Instance;
            NetworkRunner runner = bootstrap?.Runner;
            bool inRoom = runner != null && runner.IsRunning;
            bool host = inRoom && runner.IsServer && runner.IsSceneAuthority;
            if (retry && inRoom && !host) return;
            _navigating = true;
            SetActionsEnabled(false);
            if (retry) SetText("ActionStatus", host
                ? "WAITING FOR TEAM READY IN LOBBY..." : "OPENING LOBBY...");
            else SetText("ActionStatus", host ? "RETURNING TEAM TO LOBBY..."
                : inRoom ? "LEAVING ROOM..." : "OPENING LOBBY...");

            try
            {
                if (host)
                {
                    if (retry) ResultRetryCoordinator.Begin();
                    await GameUIFeedback.LoadNetworkSceneAsync(runner, NetworkBootstrap.LobbySceneName);
                }
                else if (bootstrap != null && inRoom)
                    await bootstrap.ShutdownRunnerAsync();
                else
                {
                    ResultSession.Clear();
                    if (bootstrap != null && AuthSession.IsAuthenticated)
                        GameUIFeedback.LoadScene(GameConstants.SceneLobby, LoadSceneMode.Single);
                    else
                    {
                        // A direct Result preview must initialize persistent session services
                        // and pass the normal authentication check before entering Lobby.
                        BootstrapSceneFlowController.RequestLobbyAfterAuthentication();
                        GameUIFeedback.LoadScene(GameConstants.SceneBootstrap, LoadSceneMode.Single);
                    }
                }
            }
            catch (Exception exception)
            {
                if (retry) ResultRetryCoordinator.Cancel();
                Debug.LogError($"[Result] Lobby navigation failed: {exception.Message}");
                if (this == null) return;
                _navigating = false;
                SetText("ActionStatus", "RETURN FAILED");
                SetActionsEnabled(true);
            }
        }
    }

    /// <summary>Host retry uses the ordinary lobby ready checks and backend match start.</summary>
    internal sealed class ResultRetryCoordinator : MonoBehaviour
    {
        private static ResultRetryCoordinator _instance;
        private float _nextAttemptAt;
        private bool _enteredLobby;

        internal static void Begin()
        {
            if (_instance != null) return;
            var root = new GameObject("Result Retry Coordinator");
            DontDestroyOnLoad(root);
            _instance = root.AddComponent<ResultRetryCoordinator>();
        }

        internal static void Cancel()
        {
            if (_instance != null) Destroy(_instance.gameObject);
        }

        private void Update()
        {
            var runner = NetworkBootstrap.Instance?.Runner;
            if (runner == null || !runner.IsRunning || !runner.IsServer)
            {
                Destroy(gameObject);
                return;
            }
            if (SceneManager.GetActiveScene().name != NetworkBootstrap.LobbySceneName)
            {
                if (_enteredLobby) Destroy(gameObject);
                return;
            }
            _enteredLobby = true;
            if (Time.unscaledTime < _nextAttemptAt) return;
            _nextAttemptAt = Time.unscaledTime + 1f;
            var lobby = FindAnyObjectByType<LobbyManager>();
            if (lobby != null && lobby.CurrentState.CanStartMatch && lobby.TryStartMatch())
                Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
