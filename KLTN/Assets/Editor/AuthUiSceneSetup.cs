using EchoProtocol.Api;
using EchoProtocol.Auth;
using EchoProtocol.Core;
using EchoProtocol.UI.Auth;
using EchoProtocol.UI.MainMenu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class AuthUiSceneSetup
{
  private const string MenuPath = "ECHO PROTOCOL/Setup Auth UI";
  private const string ResourcesFolder = "Assets/Resources";
  private const string ApiConfigurationAssetPath = ResourcesFolder + "/ApiConfiguration.asset";
  private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
  private const string LoginScenePath = "Assets/Scenes/Login.unity";
  private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

  [MenuItem(MenuPath)]
  public static void SetupAuthUi()
  {
    EnsureResourcesFolder();
    EnsureApiConfigurationAsset();
    SetupBootstrapScene();
    SetupLoginScene();
    SetupMainMenuScene();
    EnsureBuildSettings();
    AssetDatabase.SaveAssets();
    Debug.Log("[ECHO PROTOCOL] Auth UI setup complete.");
  }

  private static void EnsureResourcesFolder()
  {
    if (!AssetDatabase.IsValidFolder("Assets/Resources"))
    {
      AssetDatabase.CreateFolder("Assets", "Resources");
    }
  }

  private static void EnsureApiConfigurationAsset()
  {
    var existing = AssetDatabase.LoadAssetAtPath<ApiConfiguration>(ApiConfigurationAssetPath);
    if (existing != null)
    {
      return;
    }

    var config = ScriptableObject.CreateInstance<ApiConfiguration>();
    AssetDatabase.CreateAsset(config, ApiConfigurationAssetPath);
  }

  private static void SetupBootstrapScene()
  {
    var scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);

    var appFlow = FindOrCreateRoot("AppFlow");
    GetOrAddComponent<BootstrapSceneFlowController>(appFlow);

    var authRuntimeObject = GameObject.Find("AuthRuntime");
    if (authRuntimeObject == null)
    {
      authRuntimeObject = new GameObject("AuthRuntime");
    }

    var authRuntime = GetOrAddComponent<AuthRuntime>(authRuntimeObject);
    var config = AssetDatabase.LoadAssetAtPath<ApiConfiguration>(ApiConfigurationAssetPath);
    if (config != null)
    {
      var so = new SerializedObject(authRuntime);
      so.FindProperty("configuration").objectReferenceValue = config;
      so.ApplyModifiedPropertiesWithoutUndo();
    }

    GetOrAddComponent<ApiClient>(authRuntimeObject);
    GetOrAddComponent<AuthApiService>(authRuntimeObject);

    EditorSceneManager.MarkSceneDirty(scene);
    EditorSceneManager.SaveScene(scene);
  }

  private static void SetupLoginScene()
  {
    var scene = EditorSceneManager.OpenScene(LoginScenePath, OpenSceneMode.Single);
    EnsureEventSystem();

    var canvas = FindOrCreateCanvas("AuthCanvas");
    var authRoot = FindOrCreateChild(canvas.transform, "AuthRoot");
    var controller = GetOrAddComponent<AuthScreenController>(authRoot);

    var loginPanel = FindOrCreateChild(authRoot.transform, "LoginPanel");
    SetupFormPanel(loginPanel);
    var registerPanel = FindOrCreateChild(authRoot.transform, "RegisterPanel");
    SetupFormPanel(registerPanel);
    registerPanel.SetActive(false);

    var loadingOverlay = FindOrCreateChild(authRoot.transform, "LoadingOverlay");
    var loadingText = FindOrCreateText(loadingOverlay.transform, "LoadingText", "Loading...");
    StretchRect(loadingText.rectTransform);
    loadingOverlay.SetActive(false);

    var loginUsername = FindOrCreateInputField(loginPanel.transform, "UsernameInput", "Username");
    var loginPassword = FindOrCreateInputField(loginPanel.transform, "PasswordInput", "Password", password: true);
    var loginButton = FindOrCreateButton(loginPanel.transform, "LoginButton", "Login");
    var goRegisterButton = FindOrCreateButton(loginPanel.transform, "GoToRegisterButton", "Create account");

    var registerEmail = FindOrCreateInputField(registerPanel.transform, "EmailInput", "Email");
    var registerUsername = FindOrCreateInputField(registerPanel.transform, "UsernameInput", "Username");
    var registerPassword = FindOrCreateInputField(registerPanel.transform, "PasswordInput", "Password", password: true);
    var registerConfirm = FindOrCreateInputField(registerPanel.transform, "ConfirmPasswordInput", "Confirm password", password: true);
    var registerButton = FindOrCreateButton(registerPanel.transform, "RegisterButton", "Register");
    var backLoginButton = FindOrCreateButton(registerPanel.transform, "BackToLoginButton", "Back to login");

    var statusText = FindOrCreateText(authRoot.transform, "StatusText", string.Empty);

    WireAuthScreenController(
      controller,
      loginPanel,
      registerPanel,
      loadingOverlay,
      loginUsername,
      loginPassword,
      loginButton,
      goRegisterButton,
      registerEmail,
      registerUsername,
      registerPassword,
      registerConfirm,
      registerButton,
      backLoginButton,
      statusText);

    EditorSceneManager.MarkSceneDirty(scene);
    EditorSceneManager.SaveScene(scene);
  }

  private static void SetupMainMenuScene()
  {
    var scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
    EnsureEventSystem();

    var canvas = FindOrCreateCanvas("MainMenuCanvas");
    var root = FindOrCreateChild(canvas.transform, "MainMenuRoot");
    SetupFormPanel(root);
    var controller = GetOrAddComponent<MainMenuProfileController>(root);

    var welcomeText = FindOrCreateText(root.transform, "WelcomeText", "Welcome");
    var roleText = FindOrCreateText(root.transform, "RoleText", "Role:");
    var walletText = FindOrCreateText(root.transform, "WalletText", "Wallet:");
    var logoutButton = FindOrCreateButton(root.transform, "LogoutButton", "Logout");

    var so = new SerializedObject(controller);
    so.FindProperty("welcomeText").objectReferenceValue = welcomeText;
    so.FindProperty("roleText").objectReferenceValue = roleText;
    so.FindProperty("walletText").objectReferenceValue = walletText;
    so.FindProperty("logoutButton").objectReferenceValue = logoutButton;
    so.FindProperty("loginSceneName").stringValue = GameConstants.SceneLogin;
    so.ApplyModifiedPropertiesWithoutUndo();

    EditorSceneManager.MarkSceneDirty(scene);
    EditorSceneManager.SaveScene(scene);
  }

  private static void EnsureBuildSettings()
  {
    var required = new[]
    {
      BootstrapScenePath,
      LoginScenePath,
      MainMenuScenePath
    };

    var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
    foreach (var path in required)
    {
      if (scenes.Exists(s => s.path == path))
      {
        continue;
      }

      scenes.Add(new EditorBuildSettingsScene(path, true));
    }

    EditorBuildSettings.scenes = scenes.ToArray();
  }

  private static void WireAuthScreenController(
    AuthScreenController controller,
    GameObject loginPanel,
    GameObject registerPanel,
    GameObject loadingOverlay,
    InputField loginUsername,
    InputField loginPassword,
    Button loginButton,
    Button goRegisterButton,
    InputField registerEmail,
    InputField registerUsername,
    InputField registerPassword,
    InputField registerConfirm,
    Button registerButton,
    Button backLoginButton,
    Text statusText)
  {
    var so = new SerializedObject(controller);
    so.FindProperty("loginPanel").objectReferenceValue = loginPanel;
    so.FindProperty("registerPanel").objectReferenceValue = registerPanel;
    so.FindProperty("loadingOverlay").objectReferenceValue = loadingOverlay;
    so.FindProperty("loginUsernameInput").objectReferenceValue = loginUsername;
    so.FindProperty("loginPasswordInput").objectReferenceValue = loginPassword;
    so.FindProperty("loginButton").objectReferenceValue = loginButton;
    so.FindProperty("goToRegisterButton").objectReferenceValue = goRegisterButton;
    so.FindProperty("registerEmailInput").objectReferenceValue = registerEmail;
    so.FindProperty("registerUsernameInput").objectReferenceValue = registerUsername;
    so.FindProperty("registerPasswordInput").objectReferenceValue = registerPassword;
    so.FindProperty("registerConfirmPasswordInput").objectReferenceValue = registerConfirm;
    so.FindProperty("registerButton").objectReferenceValue = registerButton;
    so.FindProperty("backToLoginButton").objectReferenceValue = backLoginButton;
    so.FindProperty("statusText").objectReferenceValue = statusText;
    so.FindProperty("mainMenuSceneName").stringValue = GameConstants.SceneMainMenu;
    so.ApplyModifiedPropertiesWithoutUndo();
  }

  private static void SetupFormPanel(GameObject panel)
  {
    var rect = panel.GetComponent<RectTransform>();
    rect.sizeDelta = new Vector2(420f, 360f);

    var layout = GetOrAddComponent<VerticalLayoutGroup>(panel);
    layout.childAlignment = TextAnchor.UpperCenter;
    layout.spacing = 10f;
    layout.padding = new RectOffset(16, 16, 16, 16);
    layout.childControlHeight = true;
    layout.childControlWidth = true;
    layout.childForceExpandHeight = false;
    layout.childForceExpandWidth = true;

    GetOrAddComponent<ContentSizeFitter>(panel).verticalFit = ContentSizeFitter.FitMode.PreferredSize;
  }

  private static void EnsureEventSystem()
  {
    var eventSystem = Object.FindAnyObjectByType<EventSystem>();
    if (eventSystem == null)
    {
      var go = new GameObject("EventSystem");
      eventSystem = go.AddComponent<EventSystem>();
    }

    var standalone = eventSystem.GetComponent<StandaloneInputModule>();
    if (standalone != null)
    {
      Object.DestroyImmediate(standalone);
    }

    GetOrAddComponent<InputSystemUIInputModule>(eventSystem.gameObject);
  }

  private static GameObject FindOrCreateRoot(string name)
  {
    var existing = GameObject.Find(name);
    return existing != null ? existing : new GameObject(name);
  }

  private static GameObject FindOrCreateCanvas(string name)
  {
    var existing = GameObject.Find(name);
    if (existing != null)
    {
      GetOrAddComponent<Canvas>(existing);
      GetOrAddComponent<CanvasScaler>(existing);
      GetOrAddComponent<GraphicRaycaster>(existing);
      return existing;
    }

    var canvasGo = new GameObject(name);
    var canvas = canvasGo.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    canvasGo.AddComponent<CanvasScaler>();
    canvasGo.AddComponent<GraphicRaycaster>();
    return canvasGo;
  }

  private static GameObject FindOrCreateChild(Transform parent, string name)
  {
    var child = parent.Find(name);
    if (child != null)
    {
      return child.gameObject;
    }

    var go = new GameObject(name, typeof(RectTransform));
    go.transform.SetParent(parent, false);
    var rect = go.GetComponent<RectTransform>();
    rect.anchorMin = new Vector2(0.5f, 0.5f);
    rect.anchorMax = new Vector2(0.5f, 0.5f);
    rect.sizeDelta = new Vector2(400f, 40f);
    rect.anchoredPosition = Vector2.zero;
    return go;
  }

  private static Text FindOrCreateText(Transform parent, string name, string defaultText)
  {
    var child = parent.Find(name);
    GameObject go;
    if (child != null)
    {
      go = child.gameObject;
    }
    else
    {
      go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
    }

    var text = GetOrAddComponent<Text>(go);
    text.text = defaultText;
    text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    text.color = Color.white;
    text.alignment = TextAnchor.MiddleCenter;
    return text;
  }

  private static InputField FindOrCreateInputField(Transform parent, string name, string placeholder, bool password = false)
  {
    var child = parent.Find(name);
    GameObject go;
    if (child != null)
    {
      go = child.gameObject;
    }
    else
    {
      go = new GameObject(name, typeof(RectTransform), typeof(Image));
      go.transform.SetParent(parent, false);
      var image = go.GetComponent<Image>();
      image.color = new Color(1f, 1f, 1f, 0.12f);

      var textGo = new GameObject("Text", typeof(RectTransform));
      textGo.transform.SetParent(go.transform, false);
      var text = textGo.AddComponent<Text>();
      text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      text.color = Color.white;
      text.supportRichText = false;
      StretchRect(text.rectTransform);

      var placeholderGo = new GameObject("Placeholder", typeof(RectTransform));
      placeholderGo.transform.SetParent(go.transform, false);
      var placeholderText = placeholderGo.AddComponent<Text>();
      placeholderText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      placeholderText.text = placeholder;
      placeholderText.color = new Color(1f, 1f, 1f, 0.4f);
      StretchRect(placeholderText.rectTransform);

      var input = go.AddComponent<InputField>();
      input.textComponent = text;
      input.placeholder = placeholderText;
    }

    var field = GetOrAddComponent<InputField>(go);
    if (password)
    {
      field.contentType = InputField.ContentType.Password;
    }

    var layoutElement = GetOrAddComponent<LayoutElement>(go);
    layoutElement.minHeight = 36f;

    return field;
  }

  private static Button FindOrCreateButton(Transform parent, string name, string label)
  {
    var child = parent.Find(name);
    GameObject go;
    if (child != null)
    {
      go = child.gameObject;
    }
    else
    {
      go = new GameObject(name, typeof(RectTransform), typeof(Image));
      go.transform.SetParent(parent, false);
      var image = go.GetComponent<Image>();
      image.color = new Color(0.2f, 0.45f, 0.85f, 1f);

      var textGo = new GameObject("Text", typeof(RectTransform));
      textGo.transform.SetParent(go.transform, false);
      var text = textGo.AddComponent<Text>();
      text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      text.text = label;
      text.color = Color.white;
      text.alignment = TextAnchor.MiddleCenter;
      StretchRect(text.rectTransform);

      go.AddComponent<Button>();
    }

    var button = GetOrAddComponent<Button>(go);
    var labelText = go.GetComponentInChildren<Text>();
    if (labelText != null)
    {
      labelText.text = label;
    }

    var layoutElement = GetOrAddComponent<LayoutElement>(go);
    layoutElement.minHeight = 36f;

    return button;
  }

  private static void StretchRect(RectTransform rect)
  {
    rect.anchorMin = Vector2.zero;
    rect.anchorMax = Vector2.one;
    rect.offsetMin = Vector2.zero;
    rect.offsetMax = Vector2.zero;
  }

  private static T GetOrAddComponent<T>(GameObject go) where T : Component
  {
    var component = go.GetComponent<T>();
    return component != null ? component : go.AddComponent<T>();
  }
}
