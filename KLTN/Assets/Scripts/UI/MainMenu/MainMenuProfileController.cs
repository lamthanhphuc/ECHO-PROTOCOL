using System;
using System.Collections;
using EchoProtocol.Api;
using EchoProtocol.Auth;
using EchoProtocol.Core;
using EchoProtocol.Profile;
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
      ShowPackageStatus("Đang tải gói...");
      RefreshPlayerProfile();
      RefreshPaymentCatalog();
    }

    private void OnEnable()
    {
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
      if (profileUsernameText != null) profileUsernameText.text = string.IsNullOrWhiteSpace(display) ? "--" : display;
      if (profileIdentityUsernameText != null) profileIdentityUsernameText.text = string.IsNullOrWhiteSpace(display) ? "--" : display;
      if (profileRoleText != null)
        profileRoleText.text = string.IsNullOrWhiteSpace(AuthSession.Role)
          ? "OPERATOR"
          : AuthSession.Role.ToUpperInvariant();
      if (profileLevelText != null) profileLevelText.text = PlayerProfileSession.HasProfile ? $"CẤP {PlayerProfileSession.Level:N0}" : "CẤP --";
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
      if (profileStatusText != null && PlayerProfileSession.HasProfile) profileStatusText.text = "PERSONNEL RECORD ONLINE";
    }

    public void OpenProfile()
    {
      if (profileOverlay == null) return;
      StopProfileOpeningAnimation();
      ResolveProfileAnimationComponents();
      profileOverlay.transform.SetAsLastSibling();
      profileOverlay.SetActive(true);
      RefreshProfile();
      if (!PlayerProfileSession.HasProfile && profileStatusText != null)
        profileStatusText.text = "LOADING PERSONNEL RECORD...";
      if (!PlayerProfileSession.HasProfile || Time.unscaledTime >= _nextProfileRefreshAt)
        RefreshPlayerProfile();
      if (profileCanvasGroup != null || profilePanel != null)
        _profileOpenRoutine = StartCoroutine(AnimateProfileOpening());
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
        dimImage.raycastTarget = true;
      if (profileOpeningGlitch != null)
      {
        profileOpeningGlitch.raycastTarget = false;
        SetProfileOpeningGlitchAlpha(0f);
      }
    }

    private IEnumerator AnimateProfileOpening()
    {
      if (profileCanvasGroup != null) profileCanvasGroup.alpha = 0f;
      if (profilePanel != null) profilePanel.localScale = Vector3.one * 0.96f;

      float elapsed = 0f;
      while (elapsed < ProfileOpenDurationSeconds)
      {
        elapsed += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(elapsed / ProfileOpenDurationSeconds);
        float easeOut = 1f - Mathf.Pow(1f - progress, 3f);
        if (profileCanvasGroup != null)
          profileCanvasGroup.alpha = progress * progress * (3f - 2f * progress);
        if (profilePanel != null)
          profilePanel.localScale = Vector3.one * Mathf.LerpUnclamped(0.96f, 1f, easeOut);
        float glitch = Mathf.Clamp01(elapsed / 0.045f)
          * Mathf.Clamp01((0.12f - elapsed) / 0.05f);
        SetProfileOpeningGlitchAlpha(0.16f * glitch);
        yield return null;
      }

      if (profileCanvasGroup != null) profileCanvasGroup.alpha = 1f;
      if (profilePanel != null) profilePanel.localScale = Vector3.one;
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
      if (profilePanel != null) profilePanel.localScale = Vector3.one;
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
      if (profileOverlay != null && profileOverlay.activeSelf
          && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        CloseProfile();
      if (_started && AuthSession.IsAuthenticated && Time.unscaledTime >= _nextProfileRefreshAt)
        RefreshPlayerProfile();
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
              ? "UPDATE UNAVAILABLE - SHOWING LAST RECORD"
              : "PERSONNEL RECORD UNAVAILABLE";
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
