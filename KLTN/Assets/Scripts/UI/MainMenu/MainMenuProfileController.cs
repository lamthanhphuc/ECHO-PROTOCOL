using System;
using System.Collections;
using EchoProtocol.Api;
using EchoProtocol.Auth;
using EchoProtocol.Core;
using EchoProtocol.Profile;
using EchoProtocol.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EchoProtocol.UI.MainMenu
{
  public class MainMenuProfileController : MonoBehaviour
  {
    private const float ProfileRefreshIntervalSeconds = 20f;
    private const float ProfileOpenDurationSeconds = 0.27f;
    // Mirrors the currently registered backend PROGRESSION_V1 policy.
    private const long ExperiencePerLevel = 500L;

    [SerializeField] private Text welcomeText;
    [SerializeField] private Text roleText;
    [SerializeField] private Text walletText;
    [SerializeField] private Text coinAmountText;
    [SerializeField] private Text topUpBalanceText;
    [SerializeField] private Text playerNameText;
    [SerializeField] private Text levelText;
    [SerializeField] private Text experienceText;
    [SerializeField] private Text totalMatchesText;
    [SerializeField] private Text totalWinsText;
    [SerializeField] private Button playButton;
    [SerializeField] private Button logoutButton;
    [SerializeField] private Button shopButton;
    [SerializeField] private Button topUpButton;
    [SerializeField] private Button webButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button shopCloseButton;
    [SerializeField] private Button topUpCloseButton;
    [SerializeField] private Button package500Button;
    [SerializeField] private Button package1200Button;
    [SerializeField] private Button package2500Button;
    [SerializeField] private Button package5500Button;
    [SerializeField] private GameObject shopPopup;
    [SerializeField] private GameObject topUpPopup;
    [SerializeField] private Button profileButton;
    [SerializeField] private Button profileCloseButton;
    [SerializeField] private GameObject profileOverlay;
    [SerializeField] private RectTransform profilePanel;
    [SerializeField] private CanvasGroup profileCanvasGroup;
    [SerializeField] private Image profileExperienceFill;
    [SerializeField] private Image profileOpeningGlitch;
    [SerializeField] private Text profileUsernameText;
    [SerializeField] private Text profileIdentityUsernameText;
    [SerializeField] private Text profileRoleText;
    [SerializeField] private Text profileLevelText;
    [SerializeField] private Text profileExperienceText;
    [SerializeField] private Text profileMatchesText;
    [SerializeField] private Text profileWinsText;
    [SerializeField] private Text profileWinRateText;
    [SerializeField] private Text profileCreditsText;
    [SerializeField] private Text profileStatusText;
    [SerializeField] private string topUpWebUrl = "https://web.3.1.238.6.sslip.io/wallet";
    [SerializeField] private string webUrl = "https://web.3.1.238.6.sslip.io/";
    [SerializeField] private string lobbySceneName = GameConstants.SceneLobby;
    [SerializeField] private string loginSceneName = GameConstants.SceneLogin;
    private readonly Button[] _packageButtons = new Button[4];
    private PaymentCatalogItemDto[] _packages = Array.Empty<PaymentCatalogItemDto>();
    private bool _profileRefreshInProgress;
    private bool _catalogRequestInProgress;
    private bool _started;
    private float _nextProfileRefreshAt;
    private Coroutine _profileOpenRoutine;
    private float _profileFitScale = 1f;
    private Vector2Int _profileScreenSize;
    private Rect _profileSafeArea;

    private void Start()
    {
      AuthRuntime.EnsureExists();

      if (!AuthSession.IsAuthenticated)
      {
        GameUIFeedback.LoadScene(loginSceneName);
        return;
      }

      _packageButtons[0] = package500Button;
      _packageButtons[1] = package1200Button;
      _packageButtons[2] = package2500Button;
      _packageButtons[3] = package5500Button;
      _started = true;
      RefreshProfile();
      CloseShop();
      CloseTopUp();
      CloseProfile();
      UtilityBarStyle.MainMenu(GetComponentInParent<Canvas>()?.transform.Find("StorePanel") as RectTransform);
      MainMenuLeaderboardController.Install(GetComponentInParent<Canvas>());
      ShowPackageStatus("Đang tải gói...");
      RefreshPlayerProfile();
      RefreshPaymentCatalog();
    }

    private void OnEnable()
    {
      GameLanguage.Changed += RefreshProfile;
      if (optionsButton == null)
        foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include))
          if (button.name == "OptionsButton" && button.gameObject.scene == gameObject.scene) { optionsButton = button; break; }
      if (playButton != null)
      {
        playButton.onClick.AddListener(OnClickPlay);
      }

      if (logoutButton != null)
      {
        logoutButton.onClick.AddListener(OnClickLogout);
      }

      if (shopButton != null)
      {
        shopButton.onClick.AddListener(OnClickShop);
      }

      if (topUpButton != null)
      {
        topUpButton.onClick.AddListener(OnClickTopUp);
      }

      if (webButton != null)
      {
        webButton.onClick.AddListener(OpenWebsite);
      }

      if (optionsButton != null)
      {
        optionsButton.onClick.AddListener(OnClickOptions);
      }

      if (shopCloseButton != null) shopCloseButton.onClick.AddListener(CloseShop);
      if (topUpCloseButton != null) topUpCloseButton.onClick.AddListener(CloseTopUp);
      if (package500Button != null) package500Button.onClick.AddListener(OpenPackage0);
      if (package1200Button != null) package1200Button.onClick.AddListener(OpenPackage1);
      if (package2500Button != null) package2500Button.onClick.AddListener(OpenPackage2);
      if (package5500Button != null) package5500Button.onClick.AddListener(OpenPackage3);
      if (profileButton != null) profileButton.onClick.AddListener(OpenProfile);
      if (profileCloseButton != null) profileCloseButton.onClick.AddListener(CloseProfile);
    }

    private void OnDisable()
    {
      GameLanguage.Changed -= RefreshProfile;
      StopProfileOpeningAnimation();
      if (playButton != null)
      {
        playButton.onClick.RemoveListener(OnClickPlay);
      }

      if (logoutButton != null)
      {
        logoutButton.onClick.RemoveListener(OnClickLogout);
      }

      if (shopButton != null)
      {
        shopButton.onClick.RemoveListener(OnClickShop);
      }

      if (topUpButton != null)
      {
        topUpButton.onClick.RemoveListener(OnClickTopUp);
      }

      if (webButton != null)
      {
        webButton.onClick.RemoveListener(OpenWebsite);
      }

      if (optionsButton != null)
      {
        optionsButton.onClick.RemoveListener(OnClickOptions);
      }

      if (shopCloseButton != null) shopCloseButton.onClick.RemoveListener(CloseShop);
      if (topUpCloseButton != null) topUpCloseButton.onClick.RemoveListener(CloseTopUp);
      if (package500Button != null) package500Button.onClick.RemoveListener(OpenPackage0);
      if (package1200Button != null) package1200Button.onClick.RemoveListener(OpenPackage1);
      if (package2500Button != null) package2500Button.onClick.RemoveListener(OpenPackage2);
      if (package5500Button != null) package5500Button.onClick.RemoveListener(OpenPackage3);
      if (profileButton != null) profileButton.onClick.RemoveListener(OpenProfile);
      if (profileCloseButton != null) profileCloseButton.onClick.RemoveListener(CloseProfile);
    }

    public void RefreshProfile()
    {
      string display;

      if (PlayerProfileSession.HasProfile
          && !string.IsNullOrWhiteSpace(PlayerProfileSession.DisplayName))
      {
        display = PlayerProfileSession.DisplayName;
      }
      else
      {
        display = string.IsNullOrWhiteSpace(AuthSession.DisplayName)
          ? AuthSession.Username
          : AuthSession.DisplayName;
      }

      if (welcomeText != null)
      {
        welcomeText.text = "WELCOME";
      }

      if (playerNameText != null)
      {
        playerNameText.text =
          string.IsNullOrWhiteSpace(display)
            ? "PLAYER_01"
            : display;
      }

      if (roleText != null)
      {
        roleText.text =
          string.IsNullOrWhiteSpace(AuthSession.Role)
            ? "OPERATOR"
            : AuthSession.Role.ToUpperInvariant();
      }

      if (levelText != null)
      {
        levelText.text = PlayerProfileSession.HasProfile
          ? $"LEVEL {PlayerProfileSession.Level}"
          : "LEVEL --";
      }

      if (experienceText != null)
      {
        experienceText.text = PlayerProfileSession.HasProfile
          ? $"{PlayerProfileSession.ExperiencePoints:N0} XP"
          : "-- XP";
      }

      if (totalMatchesText != null)
      {
        totalMatchesText.text = PlayerProfileSession.HasProfile
          ? $"MATCHES {PlayerProfileSession.TotalMatches:N0}"
          : "MATCHES --";
      }

      if (totalWinsText != null)
      {
        totalWinsText.text = PlayerProfileSession.HasProfile
          ? $"WINS {PlayerProfileSession.TotalWins:N0}"
          : "WINS --";
      }

      UpdateCreditsUI();
      UtilityBarStyle.RefreshCreditsLabel(GetComponentInParent<Canvas>()?.transform.Find("StorePanel/CoinContainer") as RectTransform);
      if (profileUsernameText != null) profileUsernameText.text = string.IsNullOrWhiteSpace(display) ? "--" : display;
      if (profileIdentityUsernameText != null) profileIdentityUsernameText.text = string.IsNullOrWhiteSpace(display) ? "--" : display;
      if (profileRoleText != null)
        profileRoleText.text = string.IsNullOrWhiteSpace(AuthSession.Role)
          ? "OPERATOR"
          : AuthSession.Role.ToUpperInvariant();
      if (profileLevelText != null) profileLevelText.text = PlayerProfileSession.HasProfile
        ? GameLanguage.Choose($"CẤP {PlayerProfileSession.Level:N0}", $"LEVEL {PlayerProfileSession.Level:N0}")
        : GameLanguage.Choose("CẤP --", "LEVEL --");
      if (profileExperienceText != null)
      {
        profileExperienceText.text = !PlayerProfileSession.HasProfile
          ? "-- XP"
          : IsCurrentProgressionPolicy()
            ? $"{PlayerProfileSession.ExperiencePoints % ExperiencePerLevel:N0} / {ExperiencePerLevel:N0} XP"
            : $"{PlayerProfileSession.ExperiencePoints:N0} XP";
      }
      UpdateProfileExperienceFill();
      if (profileMatchesText != null) profileMatchesText.text = PlayerProfileSession.HasProfile ? $"{PlayerProfileSession.TotalMatches:N0}" : "--";
      if (profileWinsText != null) profileWinsText.text = PlayerProfileSession.HasProfile ? $"{PlayerProfileSession.TotalWins:N0}" : "--";
      if (profileWinRateText != null)
      {
        profileWinRateText.text = PlayerProfileSession.HasProfile
          ? $"{(PlayerProfileSession.TotalMatches > 0 ? (double)PlayerProfileSession.TotalWins / PlayerProfileSession.TotalMatches * 100d : 0d):0.#}%"
          : "--";
      }
      if (profileCreditsText != null) profileCreditsText.text = PlayerProfileSession.HasProfile ? $"{PlayerProfileSession.WalletBalance:N0}" : "--";
      if (profileStatusText != null && PlayerProfileSession.HasProfile)
        profileStatusText.text = GameLanguage.Choose("HỒ SƠ ĐÃ KẾT NỐI", "PERSONNEL RECORD ONLINE");
      ApplyProfileTypography();
      RefreshActivity();
    }

    private void ApplyProfileTypography()
    {
      if (profilePanel == null) return;
      foreach (var label in profilePanel.GetComponentsInChildren<Text>(true))
      {
        if (label.name == "ClassifiedStamp")
        {
          label.fontSize = 15;
          label.horizontalOverflow = HorizontalWrapMode.Overflow;
          label.verticalOverflow = VerticalWrapMode.Truncate;
          label.text = GameLanguage.Choose("MẬT", "CLASSIFIED");
          label.alignment = TextAnchor.MiddleCenter;
        }
        if (label.name.StartsWith("RecordLabel_"))
        {
          label.fontSize = 12;
          label.rectTransform.sizeDelta = new Vector2(260, 28);
        }
        if (label.name.StartsWith("RecordEmpty_"))
        {
          label.fontSize = 12;
          label.alignment = TextAnchor.MiddleRight;
          label.rectTransform.sizeDelta = new Vector2(420, 28);
        }
      }
      if (profileStatusText != null)
      {
        profileStatusText.fontSize = 11;
        profileStatusText.rectTransform.sizeDelta = new Vector2(260, 26);
        var position = profileStatusText.rectTransform.anchoredPosition;
        position.x = 105;
        profileStatusText.rectTransform.anchoredPosition = position;
      }
    }

    private void RefreshActivity()
    {
      if (profilePanel == null && profileOverlay != null)
        profilePanel = profileOverlay.transform.Find("ProfilePanel") as RectTransform;
      if (profilePanel == null) return;
      string[] names = { "RecordEmpty_0", "RecordEmpty_1", "RecordEmpty_2" };
      PlayerActivityDto[] activities = {
        PlayerProfileSession.RecentMatch,
        PlayerProfileSession.LatestWin,
        PlayerProfileSession.LatestNonSurvival
      };
      for (int i = 0; i < names.Length; i++)
      {
        var label = profilePanel.Find(names[i])?.GetComponent<Text>();
        if (label == null) continue;
        var activity = activities[i];
        if (activity == null)
        {
          label.text = GameLanguage.Choose("CHƯA CÓ DỮ LIỆU", "NO RECORD");
          continue;
        }
        string map = activity.mapId == "Scene_A"
          ? GameLanguage.Choose("Vùng ngập", "Flooded Zone")
          : activity.mapId == "Scene_B" ? GameLanguage.Choose("Cơ sở nghiên cứu", "Research Base")
          : activity.mapId == "SciFi" ? "SciFi" : activity.mapId;
        string result = activity.outcome == "WIN"
          ? GameLanguage.Choose("THẮNG", "WIN") : GameLanguage.Choose("THUA", "LOSS");
        string date = DateTime.TryParse(activity.endedAtUtc, null,
          System.Globalization.DateTimeStyles.AdjustToUniversal, out var ended)
          ? ended.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : "--";
        label.text = $"{date}  |  {map}  |  {result}";
      }
      string[] captions = {
        GameLanguage.Choose("TRẬN GẦN NHẤT", "LATEST MATCH"),
        GameLanguage.Choose("LẦN THẮNG GẦN NHẤT", "LATEST WIN"),
        GameLanguage.Choose("LẦN KHÔNG SỐNG SÓT GẦN NHẤT", "LATEST NON-SURVIVAL")
      };
      for (int i = 0; i < captions.Length; i++)
      {
        var label = profilePanel.Find("RecordLabel_" + i)?.GetComponent<Text>();
        if (label != null) label.text = captions[i];
      }
    }

    public void OpenProfile()
    {
      CloseProfile();
      UtilityProfileOverlay.Open(GetComponentInParent<Canvas>());
    }

    public void CloseProfile()
    {
      StopProfileOpeningAnimation();
      if (profileOverlay != null) profileOverlay.SetActive(false);
    }

    private void UpdateProfileExperienceFill()
    {
      if (profileExperienceFill == null) return;

      long experience = PlayerProfileSession.ExperiencePoints;
      // If the server switches progression policies, do not show a false level fraction.
      bool levelMatchesCurrentPolicy = IsCurrentProgressionPolicy();
      profileExperienceFill.enabled = levelMatchesCurrentPolicy;
      if (!levelMatchesCurrentPolicy) return;

      profileExperienceFill.type = Image.Type.Filled;
      profileExperienceFill.fillMethod = Image.FillMethod.Horizontal;
      profileExperienceFill.fillOrigin = 0;
      profileExperienceFill.fillAmount = (experience % ExperiencePerLevel) / (float)ExperiencePerLevel;
    }

    private static bool IsCurrentProgressionPolicy()
    {
      long experience = PlayerProfileSession.ExperiencePoints;
      return PlayerProfileSession.HasProfile
        && experience >= 0
        && PlayerProfileSession.Level == (experience / ExperiencePerLevel) + 1L;
    }

    private void ResolveProfileAnimationComponents()
    {
      if (profilePanel == null)
        profilePanel = profileOverlay.transform.Find("ProfilePanel") as RectTransform;
      if (profileCanvasGroup == null)
      {
        profileCanvasGroup = profileOverlay.GetComponent<CanvasGroup>();
        if (profileCanvasGroup == null)
          profileCanvasGroup = profileOverlay.AddComponent<CanvasGroup>();
      }
      profileCanvasGroup.blocksRaycasts = true;
      profileCanvasGroup.interactable = true;

      // The full-screen dim is the raycast surface while the dossier fades in.
      var dim = profileOverlay.transform.Find("DimBackground");
      if (dim != null && dim.TryGetComponent<Image>(out var dimImage))
      {
        dimImage.color = new Color(0.015f, 0.018f, 0.02f, 1f);
        dimImage.raycastTarget = true;
      }
      if (profileOpeningGlitch != null)
      {
        profileOpeningGlitch.raycastTarget = false;
        SetProfileOpeningGlitchAlpha(0f);
      }
    }

    private IEnumerator AnimateProfileOpening()
    {
      if (profileCanvasGroup != null) profileCanvasGroup.alpha = 0f;
      if (profilePanel != null) profilePanel.localScale = Vector3.one * (_profileFitScale * 0.96f);

      float elapsed = 0f;
      while (elapsed < ProfileOpenDurationSeconds)
      {
        elapsed += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(elapsed / ProfileOpenDurationSeconds);
        float easeOut = 1f - Mathf.Pow(1f - progress, 3f);
        if (profileCanvasGroup != null)
          profileCanvasGroup.alpha = progress * progress * (3f - 2f * progress);
        if (profilePanel != null)
          profilePanel.localScale = Vector3.one * (_profileFitScale * Mathf.LerpUnclamped(0.96f, 1f, easeOut));
        float glitch = Mathf.Clamp01(elapsed / 0.045f)
          * Mathf.Clamp01((0.12f - elapsed) / 0.05f);
        SetProfileOpeningGlitchAlpha(0.16f * glitch);
        yield return null;
      }

      if (profileCanvasGroup != null) profileCanvasGroup.alpha = 1f;
      if (profilePanel != null) profilePanel.localScale = Vector3.one * _profileFitScale;
      SetProfileOpeningGlitchAlpha(0f);
      _profileOpenRoutine = null;
    }

    private void StopProfileOpeningAnimation()
    {
      if (_profileOpenRoutine != null)
      {
        StopCoroutine(_profileOpenRoutine);
        _profileOpenRoutine = null;
      }
      if (profileCanvasGroup != null) profileCanvasGroup.alpha = 1f;
      if (profilePanel != null) profilePanel.localScale = Vector3.one * _profileFitScale;
      SetProfileOpeningGlitchAlpha(0f);
    }

    private void SetProfileOpeningGlitchAlpha(float alpha)
    {
      if (profileOpeningGlitch == null) return;
      profileOpeningGlitch.raycastTarget = false;
      var color = profileOpeningGlitch.color;
      color.a = alpha;
      profileOpeningGlitch.color = color;
    }

    public void OnClickPlay()
    {
      if (!AuthSession.IsAuthenticated)
      {
        GameUIFeedback.LoadScene(loginSceneName);
        return;
      }

      GameUIFeedback.LoadScene(lobbySceneName);
    }

    public void OnClickOptions()
    {
      EchoProtocol.Voice.VoiceManager.EnsureExists();
      EchoProtocol.Voice.VoiceManager.Instance.GetComponent<EchoProtocol.Voice.VoiceSettingsPanel>().OpenFromMainMenu();
    }
    public void OnClickLogout()
    {
      var runtime = AuthRuntime.EnsureExists();

      runtime.AuthService.Logout(_ =>
      {
        runtime.SetRestoreState(
          SessionRestoreState.None);

        GameUIFeedback.LoadScene(loginSceneName);
      });
    }

    public void OnClickShop()
    {
      if (shopPopup != null)
      {
        shopPopup.SetActive(true);
        var teamTools = shopPopup.GetComponent<TeamToolShopPanel>();
        if (teamTools == null) teamTools = shopPopup.AddComponent<TeamToolShopPanel>();
        teamTools.Open(this);
      }
      if (topUpPopup != null) topUpPopup.SetActive(false);
      Debug.Log("[MainMenu] Shop opened.");
    }

    public void OnClickTopUp()
    {
      OpenTopUpWebsite();
    }

    public void OpenTopUpWebsite()
    {
      if (string.IsNullOrWhiteSpace(topUpWebUrl))
      {
        Debug.LogWarning("[MainMenu] Top Up web URL is empty.");
        return;
      }

      Application.OpenURL(topUpWebUrl);
      Debug.Log($"[MainMenu] Top Up web opened: {topUpWebUrl}");
    }

    public void OpenWebsite()
    {
      if (string.IsNullOrWhiteSpace(webUrl))
      {
        Debug.LogWarning("[MainMenu] Web URL is empty.");
        return;
      }

      Application.OpenURL(webUrl);
      Debug.Log($"[MainMenu] Website opened: {webUrl}");
    }
    public void CloseShop()
    {
      if (shopPopup != null) shopPopup.SetActive(false);
    }

    public void CloseTopUp()
    {
      if (topUpPopup != null) topUpPopup.SetActive(false);
    }

    public void UpdateCreditsUI()
    {
      string amount;

      if (PlayerProfileSession.HasProfile)
      {
        amount = $"{PlayerProfileSession.WalletBalance:N0}";
      }
      else if (AuthSession.IsAuthenticated)
      {
        // Temporary compatibility fallback until /api/player/me
        // completes for the first time.
        amount = $"{AuthSession.WalletBalance:N0}";
      }
      else
      {
        amount = "--";
      }

      if (walletText != null)
      {
        walletText.text = $"{amount}\nEcho Credits";
      }

      if (coinAmountText != null)
      {
        coinAmountText.text = amount;
      }

      if (topUpBalanceText != null)
      {
        topUpBalanceText.text =
          EchoProtocol.Settings.GameLanguage.Choose($"Số dư: {amount} Echo Credits", $"Balance: {amount} Echo Credits");
      }
    }

    private void Update()
    {
      if (profileOverlay != null && profileOverlay.activeSelf &&
          (_profileScreenSize.x != Screen.width || _profileScreenSize.y != Screen.height
           || !_profileSafeArea.Equals(Screen.safeArea))) UpdateProfileLayout();
      if (profileOverlay != null && profileOverlay.activeSelf
          && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        CloseProfile();
      if (_started && AuthSession.IsAuthenticated && Time.unscaledTime >= _nextProfileRefreshAt)
        RefreshPlayerProfile();
    }

    private void UpdateProfileLayout()
    {
      if (profilePanel == null) return;
      var canvas = profileOverlay.GetComponentInParent<Canvas>();
      var canvasRect = canvas != null ? canvas.transform as RectTransform : null;
      if (canvasRect == null || Screen.width <= 0 || Screen.height <= 0) return;
      var safe = Screen.safeArea;
      profilePanel.anchorMin = profilePanel.anchorMax = new Vector2(
        safe.center.x / Screen.width, safe.center.y / Screen.height);
      profilePanel.anchoredPosition = Vector2.zero;
      float width = canvasRect.rect.width * safe.width / Screen.width;
      float height = canvasRect.rect.height * safe.height / Screen.height;
      _profileFitScale = Mathf.Clamp(Mathf.Min((width - 32f) / 1390f,
        (height - 32f) / 820f), 0.05f, 1.25f);
      profilePanel.localScale = Vector3.one * _profileFitScale;
      _profileScreenSize = new Vector2Int(Screen.width, Screen.height);
      _profileSafeArea = safe;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
      if (hasFocus && _started && AuthSession.IsAuthenticated) RefreshPlayerProfile();
    }

    private void RefreshPlayerProfile()
    {
      if (_profileRefreshInProgress
          || !AuthSession.IsAuthenticated)
      {
        return;
      }

      _profileRefreshInProgress = true;
      _nextProfileRefreshAt =
        Time.unscaledTime + ProfileRefreshIntervalSeconds;

      AuthRuntime.EnsureExists()
        .PlayerProfileService
        .GetCurrentProfile(result =>
        {
          if (this == null)
          {
            return;
          }

          _profileRefreshInProgress = false;

          if (result.IsSuccess
              && result.Data != null
              && result.Data.success
              && PlayerProfileSession.HasProfile)
          {
            RefreshProfile();
            return;
          }

          if (!AuthSession.IsAuthenticated)
          {
            GameUIFeedback.LoadScene(loginSceneName);
            return;
          }

          Debug.LogWarning(
            $"[MainMenu] Player profile refresh failed: {result.Message}");
          if (profileOverlay != null && profileOverlay.activeSelf && profileStatusText != null)
            profileStatusText.text = PlayerProfileSession.HasProfile
              ? GameLanguage.Choose("KHÔNG THỂ CẬP NHẬT - HIỂN THỊ DỮ LIỆU CŨ", "UPDATE UNAVAILABLE - SHOWING LAST RECORD")
              : GameLanguage.Choose("KHÔNG THỂ TẢI HỒ SƠ", "PERSONNEL RECORD UNAVAILABLE");
        });
    }

    private void RefreshPaymentCatalog()
    {
      if (_catalogRequestInProgress || !AuthSession.IsAuthenticated) return;
      _catalogRequestInProgress = true;
      ShowPackageStatus("Đang tải gói...");
      AuthRuntime.EnsureExists().Client.GetJson<PaymentCatalogApiResponse>(
        ApiEndpoints.PaymentsCatalog, attachBearer: true, result =>
        {
          if (this == null) return;
          _catalogRequestInProgress = false;
          if (!result.IsSuccess || result.Data == null || !result.Data.success || result.Data.data?.items == null)
          {
            _packages = Array.Empty<PaymentCatalogItemDto>();
            ShowPackageStatus("Không tải được gói");
            Debug.LogWarning($"[MainMenu] Payment catalog unavailable: {result.Message}");
            return;
          }

          _packages = result.Data.data.items;
          for (int i = 0; i < _packageButtons.Length; i++)
          {
            var button = _packageButtons[i];
            if (button == null) continue;
            bool available = i < _packages.Length && _packages[i] != null;
            button.interactable = available;
            var label = button.GetComponentInChildren<Text>();
            if (label == null) continue;
            var item = available ? _packages[i] : null;
            label.text = available
              ? $"{item.walletCredit:N0} Echo Credits\n{item.amount:N0} {item.currency}"
              : "Không có gói";
          }
        });
    }

    private void ShowPackageStatus(string message)
    {
      foreach (var button in _packageButtons)
      {
        if (button == null) continue;
        button.interactable = false;
        var label = button.GetComponentInChildren<Text>();
        if (label != null) label.text = message;
      }
    }

    private void OpenPackage0() => OpenPackage(0);
    private void OpenPackage1() => OpenPackage(1);
    private void OpenPackage2() => OpenPackage(2);
    private void OpenPackage3() => OpenPackage(3);

    private void OpenPackage(int index)
    {
      if (index >= _packages.Length || _packages[index] == null) return;
      OpenTopUpWebsite();
    }
  }

  [Serializable]
  public class PaymentCatalogApiResponse
  {
    public bool success;
    public PaymentCatalogDto data;
  }

  [Serializable]
  public class PaymentCatalogDto
  {
    public PaymentCatalogItemDto[] items;
  }

  [Serializable]
  public class PaymentCatalogItemDto
  {
    public string productReference;
    public string displayName;
    public double amount;
    public string currency;
    public int walletCredit;
    public string provider;
  }
}

namespace EchoProtocol.UI.MainMenu
{
  // Shared profile presentation for Main Menu, Lobby and Store.
  [DisallowMultipleComponent]
  public sealed class UtilityProfileOverlay : MonoBehaviour
  {
    private RectTransform _root;
    private RectTransform _panel;
    private Text _title;
    private Text _name;
    private Text _progress;
    private Text[] _statLabels;
    private Text[] _statValues;
    private Text[] _activityTitles;
    private Text _identity;
    private Text _section;
    private Image _xpFill;
    private bool _loading;
    private bool _failed;
    private int _requestVersion;
    private GameObject _previousSelection;
    private readonly System.Collections.Generic.List<(CanvasGroup group, bool interactable, bool blocks)> _locked = new();
    private Text[] _activity;
    private Text _status;
    private Vector2Int _screenSize;
    private Rect _safeArea;

    public static void Open(Canvas canvas)
    {
      if (canvas == null) return;
      var profile = canvas.gameObject.GetComponent<UtilityProfileOverlay>()
        ?? canvas.gameObject.AddComponent<UtilityProfileOverlay>();
      profile.Show();
    }

    private void OnEnable() => GameLanguage.Changed += Render;
    private void OnDisable()
    {
      GameLanguage.Changed -= Render;
      Close();
    }

    private void Show()
    {
      if (_root == null) Build();
      if (_locked.Count == 0)
      {
        foreach (Transform child in transform)
        {
          if (child == _root) continue;
          var group = child.GetComponent<CanvasGroup>() ?? child.gameObject.AddComponent<CanvasGroup>();
          _locked.Add((group, group.interactable, group.blocksRaycasts));
          group.interactable = group.blocksRaycasts = false;
        }
        _previousSelection = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
      }
      _root.SetAsLastSibling();
      _root.gameObject.SetActive(true);
      UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(_panel.Find("CloseButton").gameObject);
      int version = ++_requestVersion;
      UpdateLayout();
      _loading = true;
      _failed = false;
      Render();
      AuthRuntime.EnsureExists().PlayerProfileService.GetCurrentProfile(result =>
      {
        if (this == null || _root == null || !_root.gameObject.activeSelf || version != _requestVersion) return;
        _loading = false;
        _failed = !result.IsSuccess;
        Render();
        if (!result.IsSuccess && _status != null)
          _status.text = GameLanguage.Choose(
            "KHÔNG THỂ CẬP NHẬT · BẤM TẢI LẠI",
            "UPDATE UNAVAILABLE · SELECT RETRY");
      });
    }

    private void Update()
    {
      if (_root == null || !_root.gameObject.activeSelf) return;
      if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        Close();
      if (_screenSize.x != Screen.width || _screenSize.y != Screen.height
          || !_safeArea.Equals(Screen.safeArea)) UpdateLayout();
    }

    private void Close()
    {
      if (_root == null) return;
      _requestVersion++;
      _root.gameObject.SetActive(false);
      foreach (var state in _locked)
      {
        if (state.group == null) continue;
        state.group.interactable = state.interactable;
        state.group.blocksRaycasts = state.blocks;
      }
      _locked.Clear();
      if (_previousSelection != null && _previousSelection.activeInHierarchy)
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(_previousSelection);
    }

    private void Build()
    {
      var canvas = GetComponent<Canvas>();
      Font bodyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      _root = Rect("UtilityProfileOverlay", canvas.transform);
      _root.anchorMin = Vector2.zero;
      _root.anchorMax = Vector2.one;
      _root.offsetMin = _root.offsetMax = Vector2.zero;
      var backdrop = _root.gameObject.AddComponent<Image>();
      backdrop.color = new Color(0.015f, 0.018f, 0.02f, 1f);
      backdrop.raycastTarget = true;
      _panel = Rect("ProfileDossier", _root);
      _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
      _panel.pivot = new Vector2(0.5f, 0.5f);
      _panel.sizeDelta = new Vector2(1280f, 760f);
      var panelImage = _panel.gameObject.AddComponent<Image>();
      var visuals = Resources.Load<LeaderboardArchiveVisuals>("LeaderboardArchive/Visuals");
      panelImage.sprite = visuals != null ? visuals.dossierMetal : null;
      panelImage.color = panelImage.sprite != null ? Color.white
        : new Color(0.08f, 0.09f, 0.09f, 1f);
      panelImage.raycastTarget = true;
      var border = _panel.gameObject.AddComponent<Outline>();
      border.effectColor = new Color(0.52f, 0.23f, 0.21f, 1f);
      border.effectDistance = new Vector2(2f, -2f);

      Font headingFont = visuals != null && visuals.spectralFont != null ? visuals.spectralFont : bodyFont;
      _title = Label("Title", 34, 20, 950, 44, 28, headingFont);
      Label("DossierSubtitle", 34, 65, 950, 24, 13, headingFont);
      Box("HeaderRule", 34, 93, 1196, 1, new Color(.42f,.23f,.20f,.8f));
      Box("ColumnRule", 329, 110, 1, 558, new Color(.4f,.36f,.3f,.5f));
      Label("ArchiveBadge", 1035, 30, 160, 32, 13, headingFont);
      Label("ProfileHeading", 350, 105, 880, 32, 24, headingFont);
      var portrait = Rect("OperatorPortrait", _panel);
      portrait.anchorMin = portrait.anchorMax = new Vector2(0, 1);
      portrait.pivot = new Vector2(0, 1);
      portrait.anchoredPosition = new Vector2(34, -100);
      portrait.sizeDelta = new Vector2(278, 360);
      var image = portrait.gameObject.AddComponent<Image>();
      image.sprite = visuals != null ? visuals.operatorPortrait : null;
      image.preserveAspect = true;
      image.color = image.sprite != null ? Color.white : new Color(.08f, .09f, .09f);
      image.raycastTarget = false;
      _identity = Label("OperatorNameIdentity", 34, 480, 278, 80, 22, bodyFont);
      _identity.supportRichText = false;
      _name = Label("OperatorName", 350, 143, 880, 42, 28, headingFont);
      _name.supportRichText = false;
      _progress = Label("Progress", 350, 180, 880, 36, 19, bodyFont);
      var track = Box("ExperienceTrack", 350, 229, 880, 12, new Color(.16f,.17f,.17f));
      var fill = Rect("Fill", track);
      fill.anchorMin = Vector2.zero;
      fill.anchorMax = Vector2.one;
      fill.offsetMin = fill.offsetMax = Vector2.zero;
      _xpFill = fill.gameObject.AddComponent<Image>();
      _xpFill.color = new Color(.58f,.13f,.16f);
      _section = Label("StatisticsTitle", 350, 267, 880, 38, 23, headingFont);
      _statLabels = new Text[4];
      _statValues = new Text[4];
      for (int i = 0; i < 4; i++)
      {
        float x = 350 + i * 224;
        var card = Box("StatCard" + i, x, 315, 208, 126, new Color(.035f,.035f,.035f,.93f));
        var edge = card.gameObject.AddComponent<Outline>();
        edge.effectColor = i == 1 ? new Color(.5f,.12f,.13f) : new Color(.32f,.28f,.24f);
        edge.effectDistance = new Vector2(1,-1);
        var iconRect = Rect("StatIcon", card);
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(.5f,1);
        iconRect.pivot = new Vector2(.5f,1);
        iconRect.anchoredPosition = new Vector2(0,-12);
        iconRect.sizeDelta = new Vector2(26,26);
        var icons = new[] { SettingsIcon.Inventory, SettingsIcon.Trophy, SettingsIcon.Team, SettingsIcon.Shop };
        iconRect.gameObject.AddComponent<SettingsIconGraphic>().Configure(icons[i],
          i == 1 ? new Color(.75f,.19f,.22f) : new Color(.72f,.65f,.45f));
        _statValues[i] = Label("StatValue" + i, x + 12, 354, 184, 38, 27, headingFont);
        _statValues[i].alignment = TextAnchor.MiddleCenter;
        _statLabels[i] = Label("StatCaption" + i, x + 8, 392, 192, 35, 14, bodyFont);
        _statLabels[i].alignment = TextAnchor.MiddleCenter;
      }
      Label("ActivityHeading", 350, 451, 660, 28, 20, headingFont);
      _activity = new Text[3];
      _activityTitles = new Text[3];
      for (int i = 0; i < 3; i++)
      {
        float y = 489 + i * 60;
        Box("ActivityRow" + i, 350, y, 640, 54, new Color(.025f,.035f,.035f,.95f));
        _activityTitles[i] = Label("ActivityTitle" + i, 365, y + 3, 610, 21, 13, bodyFont);
        _activity[i] = Label("ActivityValue" + i, 365, y + 24, 610, 27, 14, bodyFont);
      }
      Decoration("PortraitTapeTop", visuals != null ? visuals.agedTape : null, 20, 104, 128, 28, -14);
      Decoration("PortraitTapeCorner", visuals != null ? visuals.agedTape : null, 213, 100, 115, 26, 17);
      Decoration("PortraitTapeBottom", visuals != null ? visuals.agedTape : null, 24, 441, 120, 25, 12);
      Decoration("PersonalNote", visuals != null ? visuals.tornNote : null, 34, 573, 275, 95, -3);
      Label("NoteCopy", 54, 594, 235, 55, 15, headingFont).color = new Color(.17f,.12f,.08f);
      Decoration("Fingerprint", visuals != null ? visuals.fingerprint : null, 1008, 507, 105, 160, 12);
      Decoration("SurveillancePhoto", visuals != null ? visuals.surveillanceStill : null, 1093, 524, 135, 115, 9);
      Decoration("PhotoTape", visuals != null ? visuals.agedTape : null, 1090, 514, 138, 23, 16);
      Label("PhotoCaption", 1010, 649, 220, 24, 11, bodyFont);
      Label("IdentityCaption", 34, 473, 278, 24, 12, headingFont).color = new Color(.68f,.18f,.20f);
      _identity.rectTransform.anchoredPosition = new Vector2(34,-499);
      _identity.rectTransform.sizeDelta = new Vector2(278,36);
      Label("TotalExperience", 34, 541, 278, 26, 15, bodyFont);
      Box("StatisticsAccent", 340, 275, 3, 20, new Color(.6f,.13f,.16f));
      Box("ActivityAccent", 340, 455, 3, 20, new Color(.6f,.13f,.16f));
      _status = Label("Status", 34, 695, 960, 30, 12, bodyFont);
      var retry = Label("Retry", 1050, 695, 180, 32, 16, bodyFont);
      retry.text = "↻";
      retry.alignment = TextAnchor.MiddleCenter;
      retry.raycastTarget = true;
      retry.gameObject.AddComponent<Button>().onClick.AddListener(() => { if (!_loading) Show(); });
      var closeRect = Rect("CloseButton", _panel);
      closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
      closeRect.pivot = new Vector2(1f, 1f);
      closeRect.anchoredPosition = new Vector2(-25f, -25f);
      closeRect.sizeDelta = new Vector2(48f, 48f);
      var closeImage = closeRect.gameObject.AddComponent<Image>();
      var closeButton = closeRect.gameObject.AddComponent<Button>();
      closeButton.targetGraphic = closeImage;
      closeImage.color = new Color(.15f,.055f,.06f);
      var closeText = Label("CloseLabel", 1207, 25, 48, 48, 28, bodyFont);
      closeText.text = "×";
      closeText.alignment = TextAnchor.MiddleCenter;
      closeButton.onClick.AddListener(Close);
    }

    private void Decoration(string name, Sprite sprite, float x, float y, float width, float height, float rotation)
    {
      if (sprite == null) return;
      var rect = Box(name, x, y, width, height, Color.white);
      rect.GetComponent<Image>().sprite = sprite;
      rect.localRotation = Quaternion.Euler(0, 0, rotation);
    }

    private void Copy(string name, string vi, string en)
    {
      var label = _panel.Find(name)?.GetComponent<Text>();
      if (label != null) label.text = GameLanguage.Choose(vi, en);
    }

    private RectTransform Box(string name, float x, float y, float width, float height, Color color)
    {
      var rect = Rect(name, _panel);
      rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
      rect.pivot = new Vector2(0, 1);
      rect.anchoredPosition = new Vector2(x, -y);
      rect.sizeDelta = new Vector2(width, height);
      var image = rect.gameObject.AddComponent<Image>();
      image.color = color;
      image.raycastTarget = false;
      return rect;
    }

    private Text Label(string name, float x, float y, float width,
      float height, int size, Font font)
    {
      var rect = Rect(name, _panel);
      rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
      rect.pivot = new Vector2(0f, 1f);
      rect.anchoredPosition = new Vector2(x, -y);
      rect.sizeDelta = new Vector2(width, height);
      var text = rect.gameObject.AddComponent<Text>();
      text.font = font;
      text.fontSize = size;
      text.color = new Color(0.86f, 0.84f, 0.75f, 1f);
      text.alignment = TextAnchor.MiddleLeft;
      text.raycastTarget = false;
      text.horizontalOverflow = HorizontalWrapMode.Wrap;
      text.verticalOverflow = VerticalWrapMode.Truncate;
      return text;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
      var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
      rect.SetParent(parent, false);
      return rect;
    }

    private void UpdateLayout()
    {
      var canvasRect = GetComponent<Canvas>().transform as RectTransform;
      if (_panel == null || canvasRect == null || Screen.width <= 0 || Screen.height <= 0) return;
      var safe = Screen.safeArea;
      _panel.anchorMin = _panel.anchorMax = new Vector2(
        safe.center.x / Screen.width, safe.center.y / Screen.height);
      float width = canvasRect.rect.width * safe.width / Screen.width;
      float height = canvasRect.rect.height * safe.height / Screen.height;
      _panel.localScale = Vector3.one * Mathf.Clamp(Mathf.Min(
        (width - 40f) / 1280f, (height - 40f) / 760f), 0.05f, 1.25f);
      _screenSize = new Vector2Int(Screen.width, Screen.height);
      _safeArea = safe;
    }

    private void Render()
    {
      if (_title == null) return;
      _title.text = "ECHO PROTOCOL";
      Copy("DossierSubtitle", "HỒ SƠ NHÂN SỰ  ///  NHẬT KÝ NHIỆM VỤ", "PERSONNEL RECORD  ///  MISSION ARCHIVE");
      Copy("ArchiveBadge", "NHẬT KÝ NHIỆM VỤ", "MISSION ARCHIVE");
      Copy("ProfileHeading", "HỒ SƠ ĐIỀU TRA VIÊN", "OPERATOR PROFILE");
      Copy("ActivityHeading", "HOẠT ĐỘNG GẦN ĐÂY", "RECENT ACTIVITY");
      Copy("IdentityCaption", "DANH TÍNH ĐIỀU TRA VIÊN", "OPERATOR IDENTITY");
      Copy("TotalExperience", $"TỔNG KINH NGHIỆM  ·  {PlayerProfileSession.ExperiencePoints:N0} XP",
        $"TOTAL EXPERIENCE  ·  {PlayerProfileSession.ExperiencePoints:N0} XP");
      Copy("NoteCopy", "MỖI NHIỆM VỤ\nĐỀU ĐỂ LẠI DẤU VẾT.", "EVERY MISSION\nLEAVES A TRACE.");
      Copy("PhotoCaption", "ECHO  //  TƯ LIỆU HIỆN TRƯỜNG", "ECHO  //  FIELD ARCHIVE");
      _name.text = string.IsNullOrWhiteSpace(PlayerProfileSession.DisplayName)
        ? (string.IsNullOrWhiteSpace(AuthSession.DisplayName)
          ? AuthSession.Username : AuthSession.DisplayName)
        : PlayerProfileSession.DisplayName;
      _identity.text = _name.text;
      long xp = PlayerProfileSession.ExperiencePoints;
      bool knownPolicy = PlayerProfileSession.HasProfile && xp >= 0
        && PlayerProfileSession.Level == xp / 500 + 1;
      _progress.text = !PlayerProfileSession.HasProfile ? GameLanguage.Choose("CHƯA CÓ DỮ LIỆU", "NO PROFILE DATA")
        : GameLanguage.Choose($"CẤP {PlayerProfileSession.Level}", $"LEVEL {PlayerProfileSession.Level}")
          + (knownPolicy ? $"    ·    {xp % 500:N0} / 500 XP" : $"    ·    {xp:N0} XP");
      _xpFill.gameObject.SetActive(knownPolicy);
      _xpFill.rectTransform.anchorMax = new Vector2(knownPolicy ? (xp % 500) / 500f : 0, 1);
      _section.text = GameLanguage.Choose("THỐNG KÊ NHIỆM VỤ", "MISSION STATISTICS");
      var total = PlayerProfileSession.TotalMatches;
      var wins = PlayerProfileSession.TotalWins;
      string[] values = { total.ToString("N0"), wins.ToString("N0"),
        $"{(total > 0 ? 100f * wins / total : 0f):0.#}%", PlayerProfileSession.WalletBalance.ToString("N0") };
      string[] captions = { GameLanguage.Choose("TỔNG SỐ TRẬN", "TOTAL MATCHES"),
        GameLanguage.Choose("TRẬN THẮNG", "WINS"), GameLanguage.Choose("TỈ LỆ THẮNG", "WIN RATE"),
        GameLanguage.Choose("TÍN DỤNG", "CREDITS") };
      for (int i = 0; i < 4; i++)
      {
        _statValues[i].text = PlayerProfileSession.HasProfile ? values[i] : "—";
        _statLabels[i].text = captions[i];
      }
      var entries = new[] { PlayerProfileSession.RecentMatch,
        PlayerProfileSession.LatestWin, PlayerProfileSession.LatestNonSurvival };
      var titles = new[] {
        GameLanguage.Choose("TRẬN GẦN NHẤT", "LATEST MATCH"),
        GameLanguage.Choose("LẦN THẮNG GẦN NHẤT", "LATEST WIN"),
        GameLanguage.Choose("LẦN KHÔNG SỐNG SÓT GẦN NHẤT", "LATEST NON-SURVIVAL") };
      for (int i = 0; i < _activity.Length; i++)
      {
        var entry = entries[i];
        string detail = GameLanguage.Choose("CHƯA CÓ DỮ LIỆU", "NO RECORD");
        if (entry != null && !string.IsNullOrWhiteSpace(entry.matchId))
        {
          string date = DateTime.TryParse(entry.endedAtUtc, out var value)
            ? value.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : "--";
          string map = entry.mapId == "SciFi" ? GameLanguage.Choose("Cơ sở nghiên cứu", "Research Facility")
            : entry.mapId == "Scene_A" ? GameLanguage.Choose("Vùng ngập", "Flooded District")
            : entry.mapId == "Scene_B" ? GameLanguage.Choose("Khu thanh trừng", "Purge Grounds")
            : GameLanguage.Choose("Bản đồ chưa xác định", "Unknown map");
          string outcome = entry.outcome == "WIN" ? GameLanguage.Choose("THẮNG", "WIN")
            : entry.outcome == "LOSE" || entry.outcome == "LOSS" ? GameLanguage.Choose("THUA", "LOSS")
            : GameLanguage.Choose("CHƯA XÁC ĐỊNH", "UNAVAILABLE");
          detail = $"{date}  ·  {map}  ·  {outcome}";
        }
        _activityTitles[i].text = titles[i];
        _activity[i].text = detail;
      }
      _status.text = _loading ? GameLanguage.Choose("ĐANG CẬP NHẬT HỒ SƠ...", "UPDATING PROFILE...")
        : _failed ? GameLanguage.Choose("KHÔNG THỂ CẬP NHẬT · BẤM TẢI LẠI", "UPDATE UNAVAILABLE · SELECT RETRY")
        : GameLanguage.Choose("HỒ SƠ ĐÃ CẬP NHẬT", "PROFILE UPDATED");
      _panel.Find("Retry").GetComponent<Text>().text = GameLanguage.Choose("TẢI LẠI", "REFRESH");
    }
  }
}
