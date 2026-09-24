using EchoProtocol.Networking;
using EchoProtocol.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static partial class NetworkLobbyUIBuilder
{
    [MenuItem("ECHO PROTOCOL/Lobby/Restyle Existing Network Terminal")]
    public static void RestyleExisting()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        var scene = SceneManager.GetActiveScene();
        if (scene.name != NetworkBootstrap.LobbySceneName) { Debug.LogError("Open Lobby in Edit mode first."); return; }
        foreach (var root in scene.GetRootGameObjects())
        {
            var ui = root.GetComponentInChildren<NetworkLobbyUI>(true);
            if (ui == null) continue;
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.RegisterFullObjectHierarchyUndo(ui.gameObject, "Restyle Network Terminal");
            ApplyIndustrialStyle(ui);
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = ui.gameObject;
            Debug.Log("Terminal restyled. Save Lobby with Ctrl+S.");
            return;
        }
        Debug.LogError("No NetworkLobbyUI found. Use Build Network Terminal first.");
    }

    private static void ApplyIndustrialStyle(NetworkLobbyUI ui)
    {
        var root = (RectTransform)ui.transform;
        // Top-left keeps the design stable with CanvasScaler at 1920x1080.
        Place(root, 45, 35, 460, 1010);
        var panel = Ensure<Image>(root.gameObject); panel.color = Hex("080A0DEB"); panel.raycastTarget = false;
        Ensure<CanvasGroup>(root.gameObject).alpha = 1;
        var background = root.Find("Background");
        if (background != null) background.gameObject.SetActive(false); // Avoid stacking two opaque fills.
        var border = GetRect(root, "Border", 0, 0, 460, 1010);
        Stroke(border, "Top", 0, 0, 460, 1, "596166");
        Stroke(border, "Bottom", 0, 1009, 460, 1, "596166");
        Stroke(border, "Left", 0, 0, 1, 1010, "596166");
        Stroke(border, "Right", 459, 0, 1, 1010, "596166");
        for (var i = 0; i < 4; i++)
        {
            var x = i % 2 == 0 ? 0 : 438; var y = i < 2 ? 0 : 1007;
            Stroke(border, "Corner" + i, x, y, 22, 3, i == 0 ? "8F1D1D" : "71807B");
            var rivet = Stroke(border, "Rivet" + i, i % 2 == 0 ? 9 : 447, i < 2 ? 10 : 996, 4, 4, "596166");
            rivet.transform.localRotation = Quaternion.Euler(0, 0, 45);
        }
        var font = root.GetComponentInChildren<TMP_Text>(true)?.font ?? TMP_Settings.defaultFontAsset;
        var header = GetRect(root, "Header", 24, 26, 412, 112);
        TextAt(header, "Title", 0, 0, 412, 42, "ECHO PROTOCOL", 32, "D8D8D8", font);
        TextAt(header, "Subtitle", 0, 46, 412, 20, "M U L T I P L A Y E R   L O B B Y", 11, "8C9499", font);
        var info = TextAt(header, "FacilityInfo", 0, 77, 412, 18, "CREATE A ROOM OR JOIN A FRIEND", 10, "8C9499", font);
        info.alignment = TextAlignmentOptions.Right;
        if (header.Find("Divider") != null) header.Find("Divider").gameObject.SetActive(false);
        Stroke(header, "RedLine", 0, 108, 412, 2, "8F1D1D");
        StyleInput(root, "OperatorSection", 162, "1. YOUR NAME", "PlayerNameInput", font);
        StyleInput(root, "SessionSection", 266, "2. ROOM CODE", "SessionInput", font);
        GetRect(root, "ActionButtons", 24, 376, 412, 116);
        StyleButton(root.Find("ActionButtons/HostButton"), 0, 0, 412, 52);
        StyleButton(root.Find("ActionButtons/JoinButton"), 0, 64, 412, 52);
        var host = root.Find("ActionButtons/HostButton");
        var join = root.Find("ActionButtons/JoinButton");
        for (var i = 0; i < 3; i++) Stroke(host, "SignalBar" + i, 18 + i * 6, 32 - i * 5, 3, 8 + i * 5, "8C9499");
        Stroke(join, "LinkLeft", 18, 22, 12, 2, "8C9499");
        Stroke(join, "LinkMiddle", 24, 27, 12, 2, "8C9499");
        Stroke(join, "LinkRight", 30, 32, 12, 2, "8C9499");
        var status = GetRect(root, "StatusSection", 24, 520, 412, 136);
        var dot = GetRect(status, "StatusIndicator", 0, 6, 9, 9).GetComponent<Image>();
        dot.color = Hex("D51D27");
        dot.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        TextAt(status, "StatusText", 20, 0, 392, 26, "CONNECTION STATUS: OFFLINE", 17, "D8D8D8", font);
        var statusText = status.Find("StatusText").GetComponent<TMP_Text>();
        statusText.enableAutoSizing = false;
        var message = TextAt(status, "NetworkMessage", 20, 36, 392, 94, "Enter your name and room code.\nThen create or join a room.", 14, "8C9499", font);
        message.enableAutoSizing = true; message.fontSizeMin = 11; message.fontSizeMax = 14;
        // Existing serialized count reference stays intact.
        GetRect(status, "MemberCount", 0, 142, 412, 26);
        Stroke(root, "MemberDivider", 24, 694, 412, 1, "4D5356");
        var members = GetRect(root, "MemberList", 24, 713, 412, 166);
        var fill = Ensure<Image>(members.gameObject); fill.color = Hex("050708B3"); fill.raycastTarget = true;
        var outline = Ensure<Outline>(members.gameObject); outline.effectColor = Hex("58606555"); outline.effectDistance = new Vector2(1, -1);
        GetRect(members, "Viewport", 12, 10, 388, 146);
        GetRect(members.Find("Viewport"), "Content", 0, 0, 376, 146);
        for (var i = 0; i < 4; i++)
        {
            Stroke(members, "BracketH" + i, i % 2 == 0 ? 0 : 398, i < 2 ? 0 : 165, 14, 1, "586065");
            Stroke(members, "BracketV" + i, i % 2 == 0 ? 0 : 411, i < 2 ? 0 : 152, 1, 14, "586065");
        }
        var content = members.Find("Viewport/Content").GetComponent<TMP_Text>(); content.text = string.Empty;
        var empty = TextAt(members, "EmptyState", 12, 10, 388, 146, "No players yet.\nCreate or join a room to begin.", 15, "8C9499", font);
        empty.alignment = TextAlignmentOptions.Center;
        GetRect(root, "LobbyControls", 24, 907, 412, 80);
        StyleButton(root.Find("LobbyControls/ReadyButton"), 0, 0, 200, 36);
        StyleButton(root.Find("LobbyControls/StartButton"), 212, 0, 200, 36);
        StyleButton(root.Find("LobbyControls/LeaveButton"), 0, 46, 412, 36);
        var serialized = new SerializedObject(ui);
        Assign(serialized, "networkMessage", message); Assign(serialized, "emptyMemberText", empty);
        serialized.ApplyModifiedProperties();
    }

    private static void StyleInput(Transform root, string sectionName, float y, string title, string inputName, TMP_FontAsset font)
    {
        var section = GetRect(root, sectionName, 24, y, 412, 80);
        TextAt(section, "Label", 0, 0, 412, 24, title, 15, "8C9499", font);
        var rect = GetRect(section, inputName, 0, 32, 412, 48);
        var field = rect.GetComponent<TMP_InputField>();
        field.transition = Selectable.Transition.None;
        rect.GetComponent<Image>().color = Hex("0D1113");
        var outline = Ensure<Outline>(rect.gameObject); outline.effectColor = Hex("596166"); outline.effectDistance = new Vector2(1, -1);
        GetRect(rect, "TextArea", 14, 4, 352, 40);
        GetRect(rect.Find("TextArea"), "Text", 0, 0, 352, 40);
        GetRect(rect.Find("TextArea"), "Placeholder", 0, 0, 352, 40);
        field.textComponent.fontSize = 19; field.placeholder.color = Hex("666D72");
        TextAt(rect, "TerminalIcon", 380, 10, 20, 28, ">_", 14, "666D72", font);
    }

    private static void StyleButton(Transform transform, float x, float y, float w, float h)
    {
        if (transform == null) return;
        Place((RectTransform)transform, x, y, w, h);
        var button = transform.GetComponent<Button>();
        var colors = button.colors;
        colors.normalColor = Hex("111719"); colors.highlightedColor = colors.selectedColor = Hex("253033");
        colors.pressedColor = Hex("481216"); colors.disabledColor = Hex("111719");
        button.colors = colors;
        var outline = Ensure<Outline>(transform.gameObject); outline.effectColor = Hex("71807B"); outline.effectDistance = new Vector2(1, -1);
        Ensure<CanvasGroup>(transform.gameObject).alpha = button.interactable ? 1 : 0.35f;
        GetRect(transform, "Label", w > 300 && h > 40 ? 48 : 8, 0, w > 300 && h > 40 ? w - 60 : w - 16, h);
    }

    private static T Ensure<T>(GameObject go) where T : Component => go.GetComponent<T>() ?? Undo.AddComponent<T>(go);
    private static void Place(RectTransform rect, float x, float y, float w, float h)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
    }
    private static RectTransform GetRect(Transform parent, string name, float x, float y, float w, float h)
    {
        var child = parent.Find(name) as RectTransform;
        if (child == null) { child = Rect(name, parent, x, y, w, h); Undo.RegisterCreatedObjectUndo(child.gameObject, "Add terminal detail"); }
        else Place(child, x, y, w, h);
        return child;
    }
    private static Image Stroke(Transform parent, string name, float x, float y, float w, float h, string color)
    {
        var image = Ensure<Image>(GetRect(parent, name, x, y, w, h).gameObject);
        image.color = Hex(color); image.raycastTarget = false; return image;
    }
    private static TMP_Text TextAt(Transform parent, string name, float x, float y, float w, float h, string text, int size, string color, TMP_FontAsset font)
    {
        var label = Ensure<TextMeshProUGUI>(GetRect(parent, name, x, y, w, h).gameObject);
        label.font = font; label.text = text; label.fontSize = size; label.color = Hex(color);
        label.richText = false; label.raycastTarget = false; return label;
    }
}
