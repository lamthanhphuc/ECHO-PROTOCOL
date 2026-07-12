using EchoProtocol.Api;
using UnityEngine;

namespace EchoProtocol.Auth
{
  public enum SessionRestoreState
  {
    None,
    Succeeded,
    FailedUnauthorized,
    FailedNetwork
  }

  public class AuthRuntime : MonoBehaviour
  {
    private const string RuntimeObjectName = "AuthRuntime";
    private const string ConfigurationResourcePath = "ApiConfiguration";

    private static AuthRuntime _instance;

    [SerializeField] private ApiConfiguration configuration;

    private ApiClient _apiClient;
    private AuthApiService _authService;
    private bool _initialized;

    public static AuthRuntime Instance => _instance;
    public SessionRestoreState RestoreState { get; private set; } = SessionRestoreState.None;
    public ApiConfiguration Configuration => configuration;
    public ApiClient Client => _apiClient;
    public AuthApiService AuthService => _authService;
    public bool IsInitialized => _initialized;

    public static AuthRuntime EnsureExists()
    {
      if (_instance != null && _instance._initialized)
      {
        return _instance;
      }

      var existing = FindAnyObjectByType<AuthRuntime>();
      if (existing != null)
      {
        if (_instance != null && _instance != existing)
        {
          Destroy(existing.gameObject);
        }
        else
        {
          _instance = existing;
        }
      }

      if (_instance == null)
      {
        var go = new GameObject(RuntimeObjectName);
        _instance = go.AddComponent<AuthRuntime>();
      }

      _instance.InitializeInternal();
      return _instance;
    }

    private void Awake()
    {
      if (_instance != null && _instance != this)
      {
        Destroy(gameObject);
        return;
      }

      _instance = this;
      DontDestroyOnLoad(gameObject);
    }

    private void InitializeInternal()
    {
      if (_initialized)
      {
        return;
      }

      if (configuration == null)
      {
        configuration = Resources.Load<ApiConfiguration>(ConfigurationResourcePath);
      }

      if (configuration == null)
      {
        configuration = ScriptableObject.CreateInstance<ApiConfiguration>();
      }

      _apiClient = GetComponent<ApiClient>();
      if (_apiClient == null)
      {
        _apiClient = gameObject.AddComponent<ApiClient>();
      }

      _authService = GetComponent<AuthApiService>();
      if (_authService == null)
      {
        _authService = gameObject.AddComponent<AuthApiService>();
      }

      _apiClient.Initialize(configuration);
      _authService.Initialize(_apiClient);
      _initialized = true;
    }

    public void SetRestoreState(SessionRestoreState state)
    {
      RestoreState = state;
    }
  }
}
