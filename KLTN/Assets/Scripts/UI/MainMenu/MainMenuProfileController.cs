using System;
using EchoProtocol.Api;
using EchoProtocol.Auth;
using EchoProtocol.Core;
using EchoProtocol.Profile;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EchoProtocol.UI.MainMenu
{
  public class MainMenuProfileController : MonoBehaviour
  {
    private const float ProfileRefreshIntervalSeconds = 20f;

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

    private void Start()
    {
      AuthRuntime.EnsureExists();

      if (!AuthSession.IsAuthenticated)
      {
        SceneManager.LoadScene(loginSceneName);
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
    }

    private void OnDisable()
    {
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
    }

    private void RefreshProfile()
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
    }

    public void OnClickPlay()
    {
      if (!AuthSession.IsAuthenticated)
      {
        SceneManager.LoadScene(loginSceneName);
        return;
      }

      SceneManager.LoadScene(lobbySceneName);
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

        SceneManager.LoadScene(loginSceneName);
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
            SceneManager.LoadScene(loginSceneName);
            return;
          }

          Debug.LogWarning(
            $"[MainMenu] Player profile refresh failed: {result.Message}");
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
