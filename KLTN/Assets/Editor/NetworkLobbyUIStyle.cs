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
    private const string LobbyScenePath =
        "Assets/Scenes/Lobby.unity";

    [MenuItem(
        "ECHO PROTOCOL/Lobby/Restyle Existing Network Terminal")]
    public static void RestyleExisting()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode
            || EditorApplication.isCompiling)
        {
            return;
        }

        var scene = SceneManager.GetActiveScene();

        if (scene.name != NetworkBootstrap.LobbySceneName)
        {
            Debug.LogError(
                "Open Lobby in Edit mode first.");

            return;
        }

        if (!TryRestyleScene(scene))
        {
            Debug.LogError(
                "No NetworkLobbyUI found. " +
                "Use Build Network Terminal first.");

            return;
        }

        Selection.activeGameObject =
            Object.FindAnyObjectByType<NetworkLobbyUI>()
                ?.gameObject;

        Debug.Log(
            "Lobby terminal restyled. " +
            "Save Lobby with Ctrl+S.");
    }

    /// <summary>
    /// Allows the same restyle to be applied from
    /// Unity batch mode / terminal.
    /// </summary>
    public static void RestyleLobbyBatch()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode
            || EditorApplication.isCompiling)
        {
            return;
        }

        var scene =
            EditorSceneManager.OpenScene(
                LobbyScenePath,
                OpenSceneMode.Single);

        if (!TryRestyleScene(scene))
        {
            throw new System.InvalidOperationException(
                "NetworkLobbyUI was not found in Lobby scene.");
        }

        EditorSceneManager.SaveScene(scene);

        Debug.Log(
            "[ECHO] Lobby restyle batch completed.");
    }

    private static bool TryRestyleScene(
        Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var ui =
                root.GetComponentInChildren<
                    NetworkLobbyUI>(true);

            if (ui == null)
            {
                continue;
            }

            Undo.IncrementCurrentGroup();

            int group =
                Undo.GetCurrentGroup();

            Undo.RegisterFullObjectHierarchyUndo(
                ui.gameObject,
                "Restyle Network Terminal");

            ApplyIndustrialStyle(ui);

            EditorSceneManager.MarkSceneDirty(scene);

            Undo.CollapseUndoOperations(group);

            return true;
        }

        return false;
    }

    private static void ApplyIndustrialStyle(
        NetworkLobbyUI ui)
    {
        var root =
            (RectTransform)ui.transform;

        // Preserve the current left-side composition
        // but remove the oversized member-list region.
        Place(
            root,
            45,
            35,
            460,
            760);

        var panel =
            Ensure<Image>(root.gameObject);

        panel.color =
            Hex("080A0DD9");

        panel.raycastTarget = false;

        var background =
            root.Find("Background");

        if (background != null)
        {
            background.gameObject.SetActive(false);
        }

        StyleOuterFrame(root);

        var font =
            root.GetComponentInChildren<TMP_Text>(true)
                ?.font
            ?? TMP_Settings.defaultFontAsset;

        StyleHeader(root, font);

        // Keep existing top-section geometry because
        // current Lobby scene may contain manually
        // placed Difficulty UI.
        StyleInput(
            root,
            "OperatorSection",
            162,
            "YOUR NAME",
            "PlayerNameInput",
            font);

        StyleInput(
            root,
            "SessionSection",
            266,
            "ROOM CODE",
            "SessionInput",
            font);

        StyleActions(root);
        StyleStatus(root, font);
        HideRedundantMemberList(root);
        StyleLobbyControls(root, font);

        var serialized =
            new SerializedObject(ui);

        var status =
            root.Find("StatusSection");

        var message =
            status != null
                ? status.Find("NetworkMessage")
                    ?.GetComponent<TMP_Text>()
                : null;

        var members =
            root.Find("MemberList");

        var empty =
            members != null
                ? members.Find("EmptyState")
                    ?.GetComponent<TMP_Text>()
                : null;

        var controls =
            root.Find("LobbyControls");

        var exitButton =
            controls != null
                ? controls.Find("ExitButton")
                    ?.GetComponent<Button>()
                : null;

        if (exitButton != null)
        {
            Assign(
                serialized,
                "exitButton",
                exitButton);
        }

        if (message != null)
        {
            Assign(
                serialized,
                "networkMessage",
                message);
        }

        if (empty != null)
        {
            Assign(
                serialized,
                "emptyMemberText",
                empty);
        }

        serialized.ApplyModifiedProperties();
    }

    private static void StyleOuterFrame(
        RectTransform root)
    {
        var border =
            GetRect(
                root,
                "Border",
                0,
                0,
                460,
                760);

        Stroke(
            border,
            "Top",
            0,
            0,
            460,
            1,
            "3B4143");

        Stroke(
            border,
            "Bottom",
            0,
            759,
            460,
            1,
            "3B4143");

        Stroke(
            border,
            "Left",
            0,
            0,
            1,
            760,
            "3B4143");

        Stroke(
            border,
            "Right",
            459,
            0,
            1,
            760,
            "3B4143");

        for (int i = 0; i < 4; i++)
        {
            SetChildActive(
                border,
                "Corner" + i,
                false);

            SetChildActive(
                border,
                "Rivet" + i,
                false);
        }
    }

    private static void StyleHeader(
        RectTransform root,
        TMP_FontAsset font)
    {
        var header =
            GetRect(
                root,
                "Header",
                24,
                26,
                412,
                112);

        var title =
            TextAt(
                header,
                "Title",
                0,
                0,
                412,
                38,
                "ECHO PROTOCOL",
                28,
                "D7D9D8",
                font);

        title.fontStyle =
            FontStyles.Normal;

        var subtitle =
            TextAt(
                header,
                "Subtitle",
                0,
                43,
                412,
                20,
                "MULTIPLAYER",
                12,
                "777F80",
                font);

        subtitle.characterSpacing = 3f;

        SetChildActive(
            header,
            "FacilityInfo",
            false);

        SetChildActive(
            header,
            "Divider",
            false);

        Stroke(
            header,
            "RedLine",
            0,
            88,
            52,
            2,
            "793235");
    }

    private static void StyleActions(
        RectTransform root)
    {
        var actions =
            GetRect(
                root,
                "ActionButtons",
                24,
                376,
                412,
                116);

        var host =
            actions.Find("HostButton");

        var join =
            actions.Find("JoinButton");

        StyleButton(
            host,
            0,
            0,
            412,
            52,
            false);

        StyleButton(
            join,
            0,
            64,
            412,
            52,
            false);

        for (int i = 0; i < 3; i++)
        {
            SetChildActive(
                host,
                "SignalBar" + i,
                false);
        }

        SetChildActive(
            join,
            "LinkLeft",
            false);

        SetChildActive(
            join,
            "LinkMiddle",
            false);

        SetChildActive(
            join,
            "LinkRight",
            false);
    }

    private static void StyleStatus(
        RectTransform root,
        TMP_FontAsset font)
    {
        var status =
            GetRect(
                root,
                "StatusSection",
                24,
                520,
                412,
                112);

        var dot =
            GetRect(
                status,
                "StatusIndicator",
                0,
                6,
                8,
                8)
            .GetComponent<Image>();

        dot.color =
            Hex("9C3D38");

        dot.sprite =
            AssetDatabase
                .GetBuiltinExtraResource<Sprite>(
                    "UI/Skin/Knob.psd");

        var statusText =
            TextAt(
                status,
                "StatusText",
                20,
                0,
                270,
                26,
                "CONNECTION STATUS: OFFLINE",
                15,
                "D7D9D8",
                font);

        statusText.enableAutoSizing = true;
        statusText.fontSizeMin = 11;
        statusText.fontSizeMax = 15;

        var count =
            GetRect(
                status,
                "MemberCount",
                294,
                0,
                118,
                26);

        var countText =
            count.GetComponent<TMP_Text>();

        if (countText != null)
        {
            countText.fontSize = 12;
            countText.color =
                Hex("777F80");

            countText.alignment =
                TextAlignmentOptions.Right;
        }

        var message =
            TextAt(
                status,
                "NetworkMessage",
                20,
                34,
                392,
                62,
                "Enter your name and room code.",
                13,
                "777F80",
                font);

        message.enableAutoSizing = true;
        message.fontSizeMin = 10;
        message.fontSizeMax = 13;
    }

    private static void HideRedundantMemberList(
        RectTransform root)
    {
        SetChildActive(
            root,
            "MemberDivider",
            false);

        var members =
            root.Find("MemberList");

        if (members != null)
        {
            members.gameObject.SetActive(false);
        }
    }

    private static void StyleLobbyControls(
        RectTransform root,
        TMP_FontAsset font)
    {
        var controls =
            GetRect(
                root,
                "LobbyControls",
                24,
                650,
                412,
                82);

        StyleButton(
            controls.Find("ReadyButton"),
            0,
            0,
            200,
            36,
            false);

        StyleButton(
            controls.Find("StartButton"),
            212,
            0,
            200,
            36,
            true);

        StyleButton(
            controls.Find("LeaveButton"),
            0,
            46,
            200,
            36,
            false);

        var exitButton =
            controls.Find("ExitButton")
                ?.GetComponent<Button>();

        if (exitButton == null)
        {
            exitButton =
                Button(
                    "ExitButton",
                    controls,
                    212,
                    46,
                    200,
                    36,
                    "EXIT",
                    font);

            Undo.RegisterCreatedObjectUndo(
                exitButton.gameObject,
                "Add Lobby Exit Button");
        }

        StyleButton(
            exitButton.transform,
            212,
            46,
            200,
            36,
            false);
    }

    private static void StyleInput(
        Transform root,
        string sectionName,
        float y,
        string title,
        string inputName,
        TMP_FontAsset font)
    {
        var section =
            GetRect(
                root,
                sectionName,
                24,
                y,
                412,
                80);

        TextAt(
            section,
            "Label",
            0,
            0,
            412,
            22,
            title,
            12,
            "777F80",
            font);

        var rect =
            GetRect(
                section,
                inputName,
                0,
                32,
                412,
                48);

        var field =
            rect.GetComponent<TMP_InputField>();

        if (field == null)
        {
            return;
        }

        field.transition =
            Selectable.Transition.None;

        var inputImage =
            rect.GetComponent<Image>();

        if (inputImage != null)
        {
            inputImage.color =
                Hex("0C0F10");
        }

        var outline =
            Ensure<Outline>(
                rect.gameObject);

        outline.effectColor =
            Hex("353B3D");

        outline.effectDistance =
            new Vector2(1f, -1f);

        GetRect(
            rect,
            "TextArea",
            14,
            4,
            382,
            40);

        var textArea =
            rect.Find("TextArea");

        if (textArea != null)
        {
            GetRect(
                textArea,
                "Text",
                0,
                0,
                382,
                40);

            GetRect(
                textArea,
                "Placeholder",
                0,
                0,
                382,
                40);
        }

        field.textComponent.fontSize = 18;

        if (field.placeholder is TMP_Text placeholder)
        {
            placeholder.color =
                Hex("5D6365");
        }

        SetChildActive(
            rect,
            "TerminalIcon",
            false);
    }

    private static void StyleButton(
        Transform transform,
        float x,
        float y,
        float width,
        float height,
        bool primary)
    {
        if (transform == null)
        {
            return;
        }

        Place(
            (RectTransform)transform,
            x,
            y,
            width,
            height);

        var button =
            transform.GetComponent<Button>();

        if (button == null)
        {
            return;
        }

        var colors =
            button.colors;

        colors.normalColor =
            Hex("111416");

        colors.highlightedColor =
            Hex("22282A");

        colors.selectedColor =
            Hex("22282A");

        colors.pressedColor =
            primary
                ? Hex("4A4035")
                : Hex("372326");

        colors.disabledColor =
            Hex("0D1011");

        colors.fadeDuration = 0.08f;

        button.colors = colors;

        var outline =
            Ensure<Outline>(
                transform.gameObject);

        outline.effectColor =
            primary
                ? Hex("8C8170")
                : Hex("3E4547");

        outline.effectDistance =
            new Vector2(1f, -1f);

        var labelRect =
            GetRect(
                transform,
                "Label",
                8,
                0,
                width - 16,
                height);

        var label =
            labelRect.GetComponent<TMP_Text>();

        if (label != null)
        {
            label.color =
                primary
                    ? Hex("E2DDD3")
                    : Hex("D7D9D8");

            label.fontSize =
                height >= 48f
                    ? 14f
                    : 12f;

            label.alignment =
                TextAlignmentOptions.Center;
        }
    }

    private static void SetChildActive(
        Transform parent,
        string childName,
        bool active)
    {
        if (parent == null)
        {
            return;
        }

        var child =
            parent.Find(childName);

        if (child != null)
        {
            child.gameObject.SetActive(active);
        }
    }

    private static T Ensure<T>(
        GameObject go)
        where T : Component
    {
        return go.GetComponent<T>()
            ?? Undo.AddComponent<T>(go);
    }

    private static void Place(
        RectTransform rect,
        float x,
        float y,
        float width,
        float height)
    {
        rect.anchorMin =
            rect.anchorMax =
            rect.pivot =
                new Vector2(0f, 1f);

        rect.anchoredPosition =
            new Vector2(x, -y);

        rect.sizeDelta =
            new Vector2(width, height);
    }

    private static RectTransform GetRect(
        Transform parent,
        string name,
        float x,
        float y,
        float width,
        float height)
    {
        var child =
            parent.Find(name)
            as RectTransform;

        if (child == null)
        {
            child =
                Rect(
                    name,
                    parent,
                    x,
                    y,
                    width,
                    height);

            Undo.RegisterCreatedObjectUndo(
                child.gameObject,
                "Add lobby UI detail");
        }
        else
        {
            Place(
                child,
                x,
                y,
                width,
                height);
        }

        return child;
    }

    private static Image Stroke(
        Transform parent,
        string name,
        float x,
        float y,
        float width,
        float height,
        string color)
    {
        var image =
            Ensure<Image>(
                GetRect(
                    parent,
                    name,
                    x,
                    y,
                    width,
                    height)
                .gameObject);

        image.color =
            Hex(color);

        image.raycastTarget = false;

        return image;
    }

    private static TMP_Text TextAt(
        Transform parent,
        string name,
        float x,
        float y,
        float width,
        float height,
        string text,
        int size,
        string color,
        TMP_FontAsset font)
    {
        var label =
            Ensure<TextMeshProUGUI>(
                GetRect(
                    parent,
                    name,
                    x,
                    y,
                    width,
                    height)
                .gameObject);

        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = Hex(color);
        label.richText = false;
        label.raycastTarget = false;

        return label;
    }
}
