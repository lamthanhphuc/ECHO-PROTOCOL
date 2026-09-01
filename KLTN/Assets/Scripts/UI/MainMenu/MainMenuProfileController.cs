using EchoProtocol.Auth;
using EchoProtocol.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EchoProtocol.UI.MainMenu
{
  public class MainMenuProfileController : MonoBehaviour
  {
    [SerializeField] private Text welcomeText;
    [SerializeField] private Text roleText;
    [SerializeField] private Text walletText;
    [SerializeField] private Button playButton;
    [SerializeField] private Button logoutButton;
    [SerializeField] private string lobbySceneName = GameConstants.SceneLobby;
    [SerializeField] private string loginSceneName = GameConstants.SceneLogin;

    private void Start()
    {
      AuthRuntime.EnsureExists();

      if (!AuthSession.IsAuthenticated)
      {
        SceneManager.LoadScene(loginSceneName);
        return;
      }

      RefreshProfile();
    }

    private void OnEnable()
    {
      if (playButton != null)
      {
        playButton.onClick.AddListener(OnPlayClicked);
      }

      if (logoutButton != null)
      {
        logoutButton.onClick.AddListener(OnLogoutClicked);
      }
    }

    private void OnDisable()
    {
      if (playButton != null)
      {
        playButton.onClick.RemoveListener(OnPlayClicked);
      }

      if (logoutButton != null)
      {
        logoutButton.onClick.RemoveListener(OnLogoutClicked);
      }
    }

    private void RefreshProfile()
    {
      var display = string.IsNullOrWhiteSpace(AuthSession.DisplayName)
        ? AuthSession.Username
        : AuthSession.DisplayName;

      if (welcomeText != null) welcomeText.text = $"Welcome, {display}";
      if (roleText != null) roleText.text = $"Role: {AuthSession.Role}";
      if (walletText != null) walletText.text = $"Wallet: {AuthSession.WalletBalance}";
    }

    private void OnPlayClicked()
    {
      if (!AuthSession.IsAuthenticated)
      {
        SceneManager.LoadScene(loginSceneName);
        return;
      }

      SceneManager.LoadScene(lobbySceneName);
    }

    private void OnLogoutClicked()
    {
      var runtime = AuthRuntime.EnsureExists();
      runtime.AuthService.LogoutLocal();
      runtime.SetRestoreState(SessionRestoreState.None);
      SceneManager.LoadScene(loginSceneName);
    }
  }
}
