using System.Collections;
using EchoProtocol.Api;
using EchoProtocol.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using AuthDomain = global::EchoProtocol.Auth;

namespace EchoProtocol.UI.Auth
{
  public class AuthScreenController : MonoBehaviour
  {
    [Header("Panels")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject registerPanel;
    [SerializeField] private GameObject loadingOverlay;

    [Header("Login")]
    [SerializeField] private InputField loginUsernameInput;
    [SerializeField] private InputField loginPasswordInput;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button goToRegisterButton;

    [Header("Register")]
    [SerializeField] private InputField registerEmailInput;
    [SerializeField] private InputField registerUsernameInput;
    [SerializeField] private InputField registerPasswordInput;
    [SerializeField] private InputField registerConfirmPasswordInput;
    [SerializeField] private Button registerButton;
    [SerializeField] private Button backToLoginButton;

    [Header("Status")]
    [SerializeField] private Text statusText;

    [Header("Navigation")]
    [SerializeField] private string mainMenuSceneName = GameConstants.SceneMainMenu;

    private AuthDomain.AuthRuntime _runtime;
    private bool _busy;
    private bool _startupRestoreRunning;

    private void Start()
    {
      _runtime = AuthDomain.AuthRuntime.EnsureExists();
      StartCoroutine(StartupFlow());
    }

    private void OnEnable()
    {
      if (loginButton != null) loginButton.onClick.AddListener(OnLoginClicked);
      if (goToRegisterButton != null) goToRegisterButton.onClick.AddListener(ShowRegisterPanel);
      if (registerButton != null) registerButton.onClick.AddListener(OnRegisterClicked);
      if (backToLoginButton != null) backToLoginButton.onClick.AddListener(ShowLoginPanel);
    }

    private void OnDisable()
    {
      if (loginButton != null) loginButton.onClick.RemoveListener(OnLoginClicked);
      if (goToRegisterButton != null) goToRegisterButton.onClick.RemoveListener(ShowRegisterPanel);
      if (registerButton != null) registerButton.onClick.RemoveListener(OnRegisterClicked);
      if (backToLoginButton != null) backToLoginButton.onClick.RemoveListener(ShowLoginPanel);
    }

    private IEnumerator StartupFlow()
    {
      if (AuthDomain.AuthSession.IsAuthenticated)
      {
        SceneManager.LoadScene(mainMenuSceneName);
        yield break;
      }

      if (_runtime.RestoreState == AuthDomain.SessionRestoreState.Succeeded)
      {
        SceneManager.LoadScene(mainMenuSceneName);
        yield break;
      }

      if (_runtime.RestoreState == AuthDomain.SessionRestoreState.FailedUnauthorized)
      {
        _runtime.AuthService.LogoutLocal();
        SetBusy(false);
        SetStatus("Session expired. Please log in again.");
        ShowLoginPanel();
        yield break;
      }

      if (_runtime.RestoreState == AuthDomain.SessionRestoreState.FailedNetwork)
      {
        SetBusy(false);
        SetStatus("Cannot connect to server. Check backend connection.");
        ShowLoginPanel();
        yield break;
      }

      if (_runtime.RestoreState == AuthDomain.SessionRestoreState.None)
      {
        if (AuthDomain.TokenStorage.HasToken() && AuthDomain.TokenStorage.IsExpired())
        {
          _runtime.AuthService.LogoutLocal();
          _runtime.SetRestoreState(AuthDomain.SessionRestoreState.FailedUnauthorized);
          SetBusy(false);
          SetStatus("Session expired. Please log in again.");
          ShowLoginPanel();
          yield break;
        }

        if (!AuthDomain.TokenStorage.HasToken() && AuthDomain.TokenStorage.HasStoredExpiry())
        {
          AuthDomain.TokenStorage.Clear();
          SetBusy(false);
          SetStatus(string.Empty);
          ShowLoginPanel();
          yield break;
        }

        if (AuthDomain.TokenStorage.HasToken()
            && !AuthDomain.TokenStorage.IsExpired()
            && !_startupRestoreRunning)
        {
          _startupRestoreRunning = true;
          SetBusy(true);
          SetStatus("Checking session...");

          var completed = false;
          ApiResult<AuthDomain.MeApiResponse> meResult = null;

          _runtime.AuthService.GetCurrentUser(result =>
          {
            meResult = result;
            completed = true;
          });

          yield return new WaitUntil(() => completed);
          _startupRestoreRunning = false;

          if (meResult != null && meResult.IsSuccess && meResult.Data != null && meResult.Data.success)
          {
            _runtime.SetRestoreState(AuthDomain.SessionRestoreState.Succeeded);
            SceneManager.LoadScene(mainMenuSceneName);
            yield break;
          }

          if (meResult != null && AuthDomain.AuthApiService.ShouldClearToken(meResult))
          {
            _runtime.AuthService.LogoutLocal();
            _runtime.SetRestoreState(AuthDomain.SessionRestoreState.FailedUnauthorized);
            SetBusy(false);
            SetStatus("Session expired. Please log in again.");
            ShowLoginPanel();
            yield break;
          }

          _runtime.SetRestoreState(AuthDomain.SessionRestoreState.FailedNetwork);
          SetBusy(false);
          SetStatus("Cannot connect to server. Check backend connection.");
          ShowLoginPanel();
          yield break;
        }
      }

      SetBusy(false);
      SetStatus(string.Empty);
      ShowLoginPanel();
    }

    private void OnLoginClicked()
    {
      if (_busy) return;

      var username = loginUsernameInput != null ? loginUsernameInput.text.Trim() : string.Empty;
      var password = loginPasswordInput != null ? loginPasswordInput.text : string.Empty;

      if (!ValidateLoginInput(username, password, out var validationMessage))
      {
        SetStatus(validationMessage);
        return;
      }

      StartCoroutine(LoginFlow(username, password));
    }

    private IEnumerator LoginFlow(string username, string password)
    {
      SetBusy(true);
      SetStatus("Logging in...");

      var loginCompleted = false;
      ApiResult<AuthDomain.LoginApiResponse> loginResult = null;

      _runtime.AuthService.Login(username, password, result =>
      {
        loginResult = result;
        loginCompleted = true;
      });

      yield return new WaitUntil(() => loginCompleted);

      if (loginResult == null || !loginResult.IsSuccess || loginResult.Data == null || !loginResult.Data.success)
      {
        SetStatus(AuthDomain.AuthErrorMapper.Map(loginResult));
        SetBusy(false);
        yield break;
      }

      SetStatus("Confirming session...");

      var meCompleted = false;
      ApiResult<AuthDomain.MeApiResponse> meResult = null;

      _runtime.AuthService.GetCurrentUser(result =>
      {
        meResult = result;
        meCompleted = true;
      });

      yield return new WaitUntil(() => meCompleted);

      if (meResult != null && meResult.IsSuccess && meResult.Data != null && meResult.Data.success)
      {
        _runtime.SetRestoreState(AuthDomain.SessionRestoreState.Succeeded);
        SceneManager.LoadScene(mainMenuSceneName);
        yield break;
      }

      if (meResult != null && AuthDomain.AuthApiService.ShouldClearToken(meResult))
      {
        _runtime.AuthService.LogoutLocal();
        SetStatus(AuthDomain.AuthErrorMapper.Map(meResult));
        SetBusy(false);
        yield break;
      }

      SetStatus(AuthDomain.AuthErrorMapper.Map(meResult));
      SetBusy(false);
    }

    private void OnRegisterClicked()
    {
      if (_busy) return;

      var email = registerEmailInput != null ? registerEmailInput.text.Trim() : string.Empty;
      var username = registerUsernameInput != null ? registerUsernameInput.text.Trim() : string.Empty;
      var password = registerPasswordInput != null ? registerPasswordInput.text : string.Empty;
      var confirm = registerConfirmPasswordInput != null ? registerConfirmPasswordInput.text : string.Empty;

      if (!ValidateRegisterInput(email, username, password, confirm, out var validationMessage))
      {
        SetStatus(validationMessage);
        return;
      }

      StartCoroutine(RegisterFlow(email, username, password, confirm));
    }

    private IEnumerator RegisterFlow(string email, string username, string password, string confirmPassword)
    {
      SetBusy(true);
      SetStatus("Registering...");

      var completed = false;
      ApiResult<AuthDomain.RegisterApiResponse> registerResult = null;

      _runtime.AuthService.Register(email, username, password, confirmPassword, result =>
      {
        registerResult = result;
        completed = true;
      });

      yield return new WaitUntil(() => completed);

      SetBusy(false);

      if (registerResult != null && registerResult.IsSuccess && registerResult.Data != null && registerResult.Data.success)
      {
        ClearPasswordFields();
        if (loginUsernameInput != null) loginUsernameInput.text = username;
        SetStatus("Registration successful. Please log in.");
        ShowLoginPanel();
        yield break;
      }

      SetStatus(AuthDomain.AuthErrorMapper.Map(registerResult));
    }

    private void ShowLoginPanel()
    {
      if (loginPanel != null) loginPanel.SetActive(true);
      if (registerPanel != null) registerPanel.SetActive(false);
      ClearPasswordFields();
    }

    private void ShowRegisterPanel()
    {
      if (loginPanel != null) loginPanel.SetActive(false);
      if (registerPanel != null) registerPanel.SetActive(true);
      ClearPasswordFields();
      SetStatus(string.Empty);
    }

    private void ClearPasswordFields()
    {
      if (loginPasswordInput != null) loginPasswordInput.text = string.Empty;
      if (registerPasswordInput != null) registerPasswordInput.text = string.Empty;
      if (registerConfirmPasswordInput != null) registerConfirmPasswordInput.text = string.Empty;
    }

    private void SetBusy(bool busy)
    {
      _busy = busy;
      if (loadingOverlay != null) loadingOverlay.SetActive(busy);
      if (loginButton != null) loginButton.interactable = !busy;
      if (registerButton != null) registerButton.interactable = !busy;
      if (goToRegisterButton != null) goToRegisterButton.interactable = !busy;
      if (backToLoginButton != null) backToLoginButton.interactable = !busy;
    }

    private void SetStatus(string message)
    {
      if (statusText != null) statusText.text = message ?? string.Empty;
    }

    private static bool ValidateLoginInput(string username, string password, out string message)
    {
      if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
      {
        message = "Username and password are required.";
        return false;
      }

      if (AuthDomain.PasswordPolicy.ExceedsMaxUtf8ByteLength(password))
      {
        message = "Password must not exceed 72 UTF-8 bytes";
        return false;
      }

      message = string.Empty;
      return true;
    }

    private static bool ValidateRegisterInput(string email, string username, string password, string confirmPassword, out string message)
    {
      if (string.IsNullOrWhiteSpace(email) || email.Length > 255 || !email.Contains("@"))
      {
        message = "A valid email is required.";
        return false;
      }

      if (string.IsNullOrWhiteSpace(username))
      {
        message = "Username is required.";
        return false;
      }

      if (AuthDomain.PasswordPolicy.IsUsernameTooLong(username))
      {
        message = "Username must not exceed 100 characters.";
        return false;
      }

      if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmPassword))
      {
        message = "Password and confirmation are required.";
        return false;
      }

      if (AuthDomain.PasswordPolicy.IsTooShort(password))
      {
        message = "Password must be at least 6 characters.";
        return false;
      }

      if (AuthDomain.PasswordPolicy.ExceedsMaxUtf8ByteLength(password))
      {
        message = "Password must not exceed 72 UTF-8 bytes";
        return false;
      }

      if (password != confirmPassword)
      {
        message = "Password confirmation does not match";
        return false;
      }

      message = string.Empty;
      return true;
    }
  }
}



