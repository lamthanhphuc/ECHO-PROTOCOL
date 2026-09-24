using EchoProtocol.Networking;
using EchoProtocol.UI;
using EchoProtocol.UI.Debugging;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Builds editable native Canvas objects in the currently open Lobby scene.</summary>
public static partial class NetworkLobbyUIBuilder
{
    private static readonly Color Main = Hex("D8D8D8");
    private static readonly Color Secondary = Hex("7E8589");

    [MenuItem("ECHO PROTOCOL/Lobby/Build Network Terminal")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        var scene = SceneManager.GetActiveScene();
        if (scene.name != NetworkBootstrap.LobbySceneName)
        {
            Debug.LogError("[NetworkLobbyUIBuilder] Open Lobby in Edit mode first.");
            return;
        }
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponentInChildren<NetworkLobbyUI>(true) != null)
            {
                Debug.LogWarning("[NetworkLobbyUIBuilder] Terminal already exists; edit it in Inspector or Undo before rebuilding.");
                return;
            }
        var font = TMP_Settings.defaultFontAsset;
        if (font == null)
        {
            Debug.LogError("[NetworkLobbyUIBuilder] Import TMP Essential Resources and assign TMP Settings default font first.");
            return;
        }
        Undo.IncrementCurrentGroup();
        var group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Build Network Terminal");
        var canvasObject = new GameObject("NetworkLobbyCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasObject, "Create Network Lobby Canvas");
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var terminal = Rect("NetworkTerminal", canvasObject.transform, 32, 32, 460, 1016);
        var background = Rect("Background", terminal, 0, 0, 460, 1016).gameObject.AddComponent<Image>();
        var backgroundColor = Hex("080A0D"); backgroundColor.a = 0.9f;
        background.color = backgroundColor;
        background.raycastTarget = false;
        var border = Rect("Border", terminal, 0, 0, 460, 1016);
        Line(border, "Top", 0, 0, 460, 1);
        Line(border, "Bottom", 0, 1015, 460, 1);
        Line(border, "Left", 0, 0, 1, 1016);
        Line(border, "Right", 459, 0, 1, 1016);
        var header = Rect("Header", terminal, 24, 26, 412, 102);
        Label("Title", header, 0, 0, 412, 42, "ECHO PROTOCOL", 30, Main, font);
        Label("Subtitle", header, 0, 47, 412, 24, "MULTIPLAYER LOBBY", 16, Secondary, font);
        Line(header, "Divider", 0, 94, 412, 1);
        var operatorSection = Rect("OperatorSection", terminal, 24, 148, 412, 90);
        Label("Label", operatorSection, 0, 0, 412, 24, "1. YOUR NAME", 16, Secondary, font);
        var player = Input("PlayerNameInput", operatorSection, 32, "ENTER YOUR NAME", "", font);
        var sessionSection = Rect("SessionSection", terminal, 24, 252, 412, 90);
        Label("Label", sessionSection, 0, 0, 412, 24, "2. ROOM CODE", 16, Secondary, font);
        var session = Input("SessionInput", sessionSection, 32, "ENTER ROOM CODE", "echo-test", font);
        var actions = Rect("ActionButtons", terminal, 24, 368, 412, 116);
        var host = Button("HostButton", actions, 0, 0, 412, 52, "CREATE ROOM", font);
        var join = Button("JoinButton", actions, 0, 64, 412, 52, "JOIN ROOM", font);
        var status = Rect("StatusSection", terminal, 24, 514, 412, 164);
        var indicator = Rect("StatusIndicator", status, 0, 8, 8, 8).gameObject.AddComponent<Image>();
        indicator.color = Hex("8F1D1D"); indicator.raycastTarget = false;
        var statusText = Label("StatusText", status, 20, 0, 392, 114, "CONNECTION STATUS: OFFLINE", 16, Main, font);
        statusText.enableAutoSizing = true; statusText.fontSizeMin = 12; statusText.fontSizeMax = 16;
        var count = Label("MemberCount", status, 0, 132, 412, 28, "PLAYERS IN ROOM: 0 / 4", 16, Secondary, font);
        Line(terminal, "MemberDivider", 24, 690, 412, 1);

        var listRoot = Rect("MemberList", terminal, 24, 710, 412, 170);
        var scroll = listRoot.gameObject.AddComponent<ScrollRect>();
        var viewport = Rect("Viewport", listRoot, 0, 0, 412, 170);
        viewport.gameObject.AddComponent<RectMask2D>();
        var hit = viewport.gameObject.AddComponent<Image>(); hit.color = Color.clear;
        var members = Label("Content", viewport, 0, 0, 396, 170, "NO PLAYERS YET", 17, Main, font);
        var fitter = members.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport; scroll.content = members.rectTransform;
        scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24;

        var controls = Rect("LobbyControls", terminal, 24, 908, 412, 84);
        var ready = Button("ReadyButton", controls, 0, 0, 200, 36, "READY", font);
        var start = Button("StartButton", controls, 212, 0, 200, 36, "START MISSION", font);
        var leave = Button("LeaveButton", controls, 0, 46, 412, 36, "LEAVE ROOM", font);
        ready.interactable = start.interactable = leave.interactable = false;

        var controller = terminal.gameObject.AddComponent<NetworkLobbyUI>();
        var serialized = new SerializedObject(controller);
        Assign(serialized, "playerNameInput", player); Assign(serialized, "sessionNameInput", session);
        Assign(serialized, "hostButton", host); Assign(serialized, "joinButton", join);
        Assign(serialized, "readyButton", ready); Assign(serialized, "startButton", start); Assign(serialized, "leaveButton", leave);
        Assign(serialized, "statusText", statusText); Assign(serialized, "memberCountText", count);
        Assign(serialized, "memberListText", members); Assign(serialized, "statusIndicator", indicator);
        // Persistent Bootstrap services live in another scene and resolve at runtime.
        serialized.ApplyModifiedPropertiesWithoutUndo();
        ApplyIndustrialStyle(controller);

        EventSystem eventSystem = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            eventSystem = root.GetComponentInChildren<EventSystem>(true);
            if (eventSystem != null) break;
        }
        if (eventSystem == null)
        {
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(events, "Create UI EventSystem");
            SceneManager.MoveGameObjectToScene(events, scene);
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        else if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
        {
            var oldModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (oldModule != null) { Undo.RecordObject(oldModule, "Disable legacy UI input"); oldModule.enabled = false; }
            Undo.AddComponent<InputSystemUIInputModule>(eventSystem.gameObject).AssignDefaultActions();
        }
        // Preserve the debug component as a rollback option; do not destroy its GameObject.
        foreach (var root in scene.GetRootGameObjects())
            foreach (var debug in root.GetComponentsInChildren<NetworkTestPanel>(true))
            {
                Undo.RecordObject(debug, "Disable legacy lobby preview");
                debug.enabled = false;
            }
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = terminal.gameObject;
        Debug.Log("[NetworkLobbyUIBuilder] Terminal created and wired. Save Lobby, then test from Bootstrap. Legacy debug component retained, disabled.");
    }

    private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static TMP_Text Label(string name, Transform parent, float x, float y, float w, float h,
        string text, float size, Color color, TMP_FontAsset font)
    {
        var label = Rect(name, parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font; label.text = text; label.fontSize = size; label.color = color;
        label.richText = false; label.raycastTarget = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    private static void Line(Transform parent, string name, float x, float y, float w, float h)
    {
        var line = Rect(name, parent, x, y, w, h).gameObject.AddComponent<Image>();
        line.color = new Color32(73, 81, 86, 255); line.raycastTarget = false;
    }

    private static Button Button(string name, Transform parent, float x, float y, float w, float h, string text, TMP_FontAsset font)
    {
        var rect = Rect(name, parent, x, y, w, h);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = Color.white;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = Hex("171A1D"); colors.highlightedColor = Hex("292D31");
        colors.selectedColor = Hex("292D31"); colors.pressedColor = Hex("650F14");
        colors.disabledColor = Hex("101214"); colors.fadeDuration = 0.1f;
        button.colors = colors;
        var label = Label("Label", rect, 8, 0, w - 16, h, text, 16, Main, font);
        label.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private static TMP_InputField Input(string name, Transform parent, float y, string hint, string value, TMP_FontAsset font)
    {
        var rect = Rect(name, parent, 0, y, 412, 52);
        var image = rect.gameObject.AddComponent<Image>(); image.color = Hex("171A1D");
        var field = rect.gameObject.AddComponent<TMP_InputField>(); field.targetGraphic = image;
        var viewport = Rect("TextArea", rect, 12, 6, 388, 40);
        viewport.gameObject.AddComponent<RectMask2D>();
        var text = Label("Text", viewport, 0, 0, 388, 40, "", 20, Main, font);
        var placeholder = Label("Placeholder", viewport, 0, 0, 388, 40, hint, 17, Secondary, font);
        text.alignment = placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        field.textViewport = viewport; field.textComponent = (TextMeshProUGUI)text; field.placeholder = placeholder;
        field.fontAsset = font; field.characterLimit = 32; field.lineType = TMP_InputField.LineType.SingleLine;
        field.richText = false; field.text = value;
        return field;
    }

    private static void Assign(SerializedObject serialized, string name, Object value) =>
        serialized.FindProperty(name).objectReferenceValue = value;

    private static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString("#" + value, out var color);
        return color;
    }
}
