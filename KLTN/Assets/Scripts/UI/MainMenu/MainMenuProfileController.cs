using EchoProtocol.Auth;
using EchoProtocol.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EchoProtocol.UI.MainMenu
{
  public class MainMenuProfileController : MonoBehaviour
  {
    private const string CreditsKey = "ECHO_CREDITS";
    private const int DefaultCredits = 1250;

    [SerializeField] private Text welcomeText;
    [SerializeField] private Text roleText;
    [SerializeField] private Text walletText;
    [SerializeField] private Text playerNameText;
    [SerializeField] private Button playButton;
    [SerializeField] private Button logoutButton;
    [SerializeField] private Button shopButton;
    [SerializeField] private Button topUpButton;
    [SerializeField] private Button shopCloseButton;
    [SerializeField] private Button topUpCloseButton;
    [SerializeField] private Button package500Button;
    [SerializeField] private Button package1200Button;
    [SerializeField] private Button package2500Button;
    [SerializeField] private Button package5500Button;
    [SerializeField] private GameObject shopPopup;
    [SerializeField] private GameObject topUpPopup;
    [SerializeField] private string lobbySceneName = GameConstants.SceneLobby;
    [SerializeField] private string loginSceneName = GameConstants.SceneLogin;
    private int _credits;

    private void Start()
    {
      AuthRuntime.EnsureExists();

      if (!AuthSession.IsAuthenticated)
      {
        SceneManager.LoadScene(loginSceneName);
        return;
      }

      LoadCredits();
      RefreshProfile();
    }

    private void OnEnable()
    {
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

      if (shopCloseButton != null) shopCloseButton.onClick.AddListener(CloseShop);
      if (topUpCloseButton != null) topUpCloseButton.onClick.AddListener(CloseTopUp);
      if (package500Button != null) package500Button.onClick.AddListener(TestAdd500Credits);
      if (package1200Button != null) package1200Button.onClick.AddListener(TestAdd1200Credits);
      if (package2500Button != null) package2500Button.onClick.AddListener(TestAdd2500Credits);
      if (package5500Button != null) package5500Button.onClick.AddListener(TestAdd5500Credits);
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

      if (shopCloseButton != null) shopCloseButton.onClick.RemoveListener(CloseShop);
      if (topUpCloseButton != null) topUpCloseButton.onClick.RemoveListener(CloseTopUp);
      if (package500Button != null) package500Button.onClick.RemoveListener(TestAdd500Credits);
      if (package1200Button != null) package1200Button.onClick.RemoveListener(TestAdd1200Credits);
      if (package2500Button != null) package2500Button.onClick.RemoveListener(TestAdd2500Credits);
      if (package5500Button != null) package5500Button.onClick.RemoveListener(TestAdd5500Credits);
    }

    private void RefreshProfile()
    {
      var display = string.IsNullOrWhiteSpace(AuthSession.DisplayName)
        ? AuthSession.Username
        : AuthSession.DisplayName;

      if (welcomeText != null) welcomeText.text = "WELCOME";
      if (playerNameText != null) playerNameText.text = string.IsNullOrWhiteSpace(display) ? "PLAYER_01" : display;
      if (roleText != null) roleText.text = string.IsNullOrWhiteSpace(AuthSession.Role) ? "OPERATOR" : AuthSession.Role.ToUpperInvariant();
      if (walletText != null) walletText.text = $"{_credits:N0}\nECHO CREDITS";
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

    public void OnClickLogout()
    {
      var runtime = AuthRuntime.EnsureExists();
      runtime.AuthService.LogoutLocal();
      runtime.SetRestoreState(SessionRestoreState.None);
      SceneManager.LoadScene(loginSceneName);
    }

    public void OnClickShop()
    {
      if (shopPopup != null) shopPopup.SetActive(true);
      if (topUpPopup != null) topUpPopup.SetActive(false);
      Debug.Log("[MainMenu] Shop opened.");
    }

    public void OnClickTopUp()
    {
      if (topUpPopup != null) topUpPopup.SetActive(true);
      if (shopPopup != null) shopPopup.SetActive(false);
      Debug.Log("[MainMenu] Top Up opened.");
    }

    public void CloseShop()
    {
      if (shopPopup != null) shopPopup.SetActive(false);
    }

    public void CloseTopUp()
    {
      if (topUpPopup != null) topUpPopup.SetActive(false);
    }

    public void LoadCredits()
    {
      var savedCredits = PlayerPrefs.GetInt(CreditsKey, DefaultCredits);
      _credits = AuthSession.WalletBalance > 0 ? AuthSession.WalletBalance : savedCredits;
      SaveCredits();
    }

    public void SaveCredits()
    {
      PlayerPrefs.SetInt(CreditsKey, Mathf.Max(0, _credits));
      PlayerPrefs.Save();
      UpdateCreditsUI();
    }

    public void SetCoinAmount(int amount)
    {
      _credits = Mathf.Max(0, amount);
      SaveCredits();
    }

    public void AddCoins(int amount)
    {
      if (amount <= 0) return;
      _credits += amount;
      SaveCredits();
      Debug.Log($"[ECHO Credits] Added {amount} credits.");
    }

    public void TestAdd500Credits() => AddCoins(500);
    public void TestAdd1200Credits() => AddCoins(1200);
    public void TestAdd2500Credits() => AddCoins(2500);
    public void TestAdd5500Credits() => AddCoins(5500);

    private void UpdateCreditsUI()
    {
      if (walletText != null) walletText.text = $"{_credits:N0}\nECHO CREDITS";
    }
  }
}
