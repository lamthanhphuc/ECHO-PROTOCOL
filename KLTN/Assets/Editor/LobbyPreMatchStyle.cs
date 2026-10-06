using EchoProtocol.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class LobbyPreMatchStyle
{
    private const string LobbyScenePath =
        "Assets/Scenes/Lobby.unity";

    private static readonly Color Panel =
        Hex("080A0C", 0.91f);

    private static readonly Color Secondary =
        Hex("101415", 0.94f);

    private static readonly Color Border =
        Hex("3A4142", 1f);

    private static readonly Color TextPrimary =
        Hex("DADDD9", 1f);

    private static readonly Color TextSecondary =
        Hex("7F8786", 1f);

    private static readonly Color Accent =
        Hex("8B403B", 1f);

    private static readonly Color Ready =
        Hex("758F79", 1f);

    [MenuItem(
        "ECHO PROTOCOL/Lobby/Build Pre-Match Layout")]
    public static void BuildFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode
            || EditorApplication.isCompiling)
        {
            return;
        }

        var scene =
            SceneManager.GetActiveScene();

        if (scene.name != "Lobby")
        {
            Debug.LogError(
                "[LobbyPreMatchStyle] Open Lobby first.");

            return;
        }

        Build(scene);

        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log(
            "[LobbyPreMatchStyle] Layout built. Save Lobby.");
    }

    public static void BuildLobbyBatch()
    {
        var scene =
            EditorSceneManager.OpenScene(
                LobbyScenePath,
                OpenSceneMode.Single);

        Build(scene);

        EditorSceneManager.MarkSceneDirty(scene);

        if (!EditorSceneManager.SaveScene(scene))
        {
            throw new System.InvalidOperationException(
                "Could not save Lobby scene.");
        }

        Debug.Log(
            "[ECHO] Lobby pre-match layout build completed.");
    }

    private static void Build(Scene scene)
    {
        var ui =
            Object.FindAnyObjectByType<NetworkLobbyUI>(
                FindObjectsInactive.Include);

        if (ui == null)
        {
            throw new System.InvalidOperationException(
                "NetworkLobbyUI not found.");
        }

        var root =
            ui.transform as RectTransform;

        if (root == null)
        {
            throw new System.InvalidOperationException(
                "NetworkLobbyUI RectTransform missing.");
        }

        Undo.RegisterFullObjectHierarchyUndo(
            ui.gameObject,
            "Build Pre-Match Lobby");

        var serialized =
            new SerializedObject(ui);

        var difficulty =
            Get<TMP_Dropdown>(
                serialized,
                "difficultyDropdown");

        var ready =
            Get<Button>(
                serialized,
                "readyButton");

        var start =
            Get<Button>(
                serialized,
                "startButton");

        var leave =
            Get<Button>(
                serialized,
                "leaveButton");

        var exit =
            Get<Button>(
                serialized,
                "exitButton");

        var status =
            Get<TMP_Text>(
                serialized,
                "statusText");

        var count =
            Get<TMP_Text>(
                serialized,
                "memberCountText");

        var statusIndicator =
            Get<Image>(
                serialized,
                "statusIndicator");

        if (difficulty == null
            || ready == null
            || start == null
            || exit == null
            || count == null)
        {
            throw new System.InvalidOperationException(
                "Lobby controls are not fully assigned.");
        }

        TMP_FontAsset font =
            root.GetComponentInChildren<TMP_Text>(true)
                ?.font
            ?? TMP_Settings.defaultFontAsset;

        // =================================================
        // ROOT RIGHT-SIDE PANEL
        // =================================================

        PlaceTopRight(
            root,
            44f,
            54f,
            455f,
            810f);

        var rootImage =
            root.GetComponent<Image>();

        if (rootImage == null)
        {
            rootImage =
                Undo.AddComponent<Image>(
                    root.gameObject);
        }

        rootImage.color = Panel;
        rootImage.raycastTarget = false;

        var rootOutline =
            root.GetComponent<Outline>();

        if (rootOutline == null)
        {
            rootOutline =
                Undo.AddComponent<Outline>(
                    root.gameObject);
        }

        rootOutline.effectColor = Border;
        rootOutline.effectDistance =
            new Vector2(1f, -1f);

        // =================================================
        // HIDE OLD SESSION-ENTRY UI
        // =================================================

        Hide(root, "Header");
        Hide(root, "OperatorSection");
        Hide(root, "SessionSection");
        Hide(root, "ActionButtons");
        Hide(root, "MemberDivider");
        Hide(root, "MemberList");

        // Old connection area is replaced by compact status.
        Hide(root, "StatusSection");

        // Controls are moved before hiding the old container.
        var oldControls =
            root.Find("LobbyControls");

        // =================================================
        // NEW PRE-MATCH CONTENT
        // =================================================

        var content =
            EnsureRect(
                root,
                "PreMatchContent");

        Stretch(content);

        // Header
        var title =
            EnsureText(
                content,
                "MissionHeader",
                font);

        PlaceTopLeft(
            title.rectTransform,
            32f,
            28f,
            390f,
            38f);

        title.text =
            "MISSION SETUP";

        title.fontSize = 25f;
        title.fontStyle =
            FontStyles.Bold;

        title.color =
            TextPrimary;

        title.alignment =
            TextAlignmentOptions.Left;

        var line =
            EnsureImage(
                content,
                "HeaderAccent");

        PlaceTopLeft(
            line.rectTransform,
            32f,
            76f,
            66f,
            2f);

        line.color =
            Accent;

        line.raycastTarget =
            false;

        // Mission site
        CreateLabel(
            content,
            "SiteLabel",
            "MISSION SITE",
            32f,
            112f,
            390f,
            20f,
            font);

        var site =
            EnsureText(
                content,
                "SiteValue",
                font);

        PlaceTopLeft(
            site.rectTransform,
            32f,
            138f,
            390f,
            32f);

        site.text =
            "SCI-FI FACILITY";

        site.fontSize = 17f;
        site.fontStyle =
            FontStyles.Bold;

        site.color =
            TextPrimary;

        // Difficulty
        CreateLabel(
            content,
            "DifficultyLabel",
            "DIFFICULTY",
            32f,
            202f,
            390f,
            20f,
            font);

        difficulty.transform.SetParent(
            content,
            false);

        PlaceTopLeft(
            difficulty.transform as RectTransform,
            32f,
            230f,
            390f,
            48f);

        StyleDropdown(
            difficulty);

        // Room
        CreateLabel(
            content,
            "RoomLabel",
            "ROOM",
            32f,
            320f,
            390f,
            20f,
            font);

        var roomName =
            EnsureText(
                content,
                "RoomNameText",
                font);

        PlaceTopLeft(
            roomName.rectTransform,
            32f,
            348f,
            270f,
            32f);

        roomName.text =
            "----";

        roomName.fontSize = 17f;
        roomName.fontStyle =
            FontStyles.Bold;

        roomName.color =
            TextPrimary;

        roomName.alignment =
            TextAlignmentOptions.Left;

        count.transform.SetParent(
            content,
            false);

        PlaceTopLeft(
            count.rectTransform,
            310f,
            350f,
            112f,
            28f);

        count.fontSize = 13f;
        count.color =
            TextSecondary;

        count.alignment =
            TextAlignmentOptions.Right;

        // Ready
        ready.transform.SetParent(
            content,
            false);

        StyleButton(
            ready,
            32f,
            424f,
            390f,
            52f,
            false);

        // Start
        start.transform.SetParent(
            content,
            false);

        StyleButton(
            start,
            32f,
            494f,
            390f,
            64f,
            true);

        // Compact connection state
        if (statusIndicator != null)
        {
            statusIndicator.transform.SetParent(
                content,
                false);

            PlaceTopLeft(
                statusIndicator.rectTransform,
                34f,
                608f,
                8f,
                8f);
        }

        if (status != null)
        {
            status.transform.SetParent(
                content,
                false);

            PlaceTopLeft(
                status.rectTransform,
                54f,
                594f,
                368f,
                40f);

            status.fontSize = 11f;
            status.color =
                TextSecondary;

            status.alignment =
                TextAlignmentOptions.Left;
        }

        // LEAVE ROOM uses the existing Exit button because
        // OnExitClicked already returns to MainMenu.
        exit.transform.SetParent(
            content,
            false);

        StyleTextButton(
            exit,
            "LEAVE ROOM",
            32f,
            710f,
            150f,
            40f);

        // Old LeaveRoom returns to Lobby after network shutdown,
        // so it must not be part of the new flow.
        if (leave != null)
        {
            leave.gameObject.SetActive(false);
        }

        if (oldControls != null)
        {
            oldControls.gameObject.SetActive(false);
        }

        // =================================================
        // SERIALIZED NEW ROOM NAME
        // =================================================

        Assign(
            serialized,
            "roomNameText",
            roomName);

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void StyleDropdown(
        TMP_Dropdown dropdown)
    {
        var image =
            dropdown.GetComponent<Image>();

        if (image != null)
        {
            image.color =
                Secondary;
        }

        var outline =
            dropdown.GetComponent<Outline>();

        if (outline == null)
        {
            outline =
                Undo.AddComponent<Outline>(
                    dropdown.gameObject);
        }

        outline.effectColor =
            Border;

        outline.effectDistance =
            new Vector2(1f, -1f);

        if (dropdown.captionText != null)
        {
            dropdown.captionText.fontSize =
                14f;

            dropdown.captionText.color =
                TextPrimary;

            dropdown.captionText.alignment =
                TextAlignmentOptions.Left;
        }
    }

    private static void StyleButton(
        Button button,
        float x,
        float y,
        float width,
        float height,
        bool primary)
    {
        button.gameObject.SetActive(true);

        PlaceTopLeft(
            button.transform as RectTransform,
            x,
            y,
            width,
            height);

        var image =
            button.targetGraphic as Image
            ?? button.GetComponent<Image>();

        if (image == null)
        {
            image =
                Undo.AddComponent<Image>(
                    button.gameObject);
        }

        image.color =
            primary
                ? Hex("351B19", 0.98f)
                : Secondary;

        image.raycastTarget =
            true;

        button.targetGraphic =
            image;

        var colors =
            button.colors;

        colors.normalColor =
            image.color;

        colors.highlightedColor =
            primary
                ? Hex("592725", 1f)
                : Hex("232929", 1f);

        colors.selectedColor =
            colors.highlightedColor;

        colors.pressedColor =
            primary
                ? Hex("77322D", 1f)
                : Hex("382624", 1f);

        colors.disabledColor =
            Hex("0E1111", 0.55f);

        colors.fadeDuration =
            0.08f;

        button.colors =
            colors;

        var outline =
            button.GetComponent<Outline>();

        if (outline == null)
        {
            outline =
                Undo.AddComponent<Outline>(
                    button.gameObject);
        }

        outline.effectColor =
            primary
                ? Accent
                : Border;

        outline.effectDistance =
            new Vector2(1f, -1f);

        var label =
            button.GetComponentInChildren<TMP_Text>(
                true);

        if (label != null)
        {
            label.fontSize =
                primary
                    ? 16f
                    : 14f;

            label.fontStyle =
                FontStyles.Bold;

            label.color =
                primary
                    ? TextPrimary
                    : Ready;

            label.alignment =
                TextAlignmentOptions.Center;
        }
    }

    private static void StyleTextButton(
        Button button,
        string labelText,
        float x,
        float y,
        float width,
        float height)
    {
        button.gameObject.SetActive(true);

        PlaceTopLeft(
            button.transform as RectTransform,
            x,
            y,
            width,
            height);

        var image =
            button.targetGraphic as Image
            ?? button.GetComponent<Image>();

        if (image != null)
        {
            image.color =
                Color.clear;

            image.raycastTarget =
                true;

            button.targetGraphic =
                image;
        }

        var colors =
            button.colors;

        colors.normalColor =
            Color.clear;

        colors.highlightedColor =
            Hex("202424", 0.8f);

        colors.selectedColor =
            colors.highlightedColor;

        colors.pressedColor =
            Hex("3A2422", 0.9f);

        button.colors =
            colors;

        var label =
            button.GetComponentInChildren<TMP_Text>(
                true);

        if (label != null)
        {
            label.text =
                labelText;

            label.fontSize = 11f;
            label.fontStyle =
                FontStyles.Normal;

            label.color =
                TextSecondary;

            label.alignment =
                TextAlignmentOptions.Left;
        }
    }

    private static void CreateLabel(
        Transform parent,
        string name,
        string value,
        float x,
        float y,
        float width,
        float height,
        TMP_FontAsset font)
    {
        var text =
            EnsureText(
                parent,
                name,
                font);

        PlaceTopLeft(
            text.rectTransform,
            x,
            y,
            width,
            height);

        text.text =
            value;

        text.fontSize = 10f;

        text.fontStyle =
            FontStyles.Bold;

        text.characterSpacing = 2f;

        text.color =
            TextSecondary;

        text.alignment =
            TextAlignmentOptions.Left;
    }

    private static TMP_Text EnsureText(
        Transform parent,
        string name,
        TMP_FontAsset font)
    {
        var go =
            EnsureObject(
                parent,
                name);

        var text =
            go.GetComponent<TextMeshProUGUI>()
            ?? Undo.AddComponent<TextMeshProUGUI>(go);

        text.font = font;
        text.raycastTarget = false;
        text.textWrappingMode =
            TextWrappingModes.NoWrap;

        return text;
    }

    private static Image EnsureImage(
        Transform parent,
        string name)
    {
        var go =
            EnsureObject(
                parent,
                name);

        return go.GetComponent<Image>()
            ?? Undo.AddComponent<Image>(go);
    }

    private static RectTransform EnsureRect(
        Transform parent,
        string name)
    {
        return EnsureObject(
            parent,
            name).GetComponent<RectTransform>();
    }

    private static GameObject EnsureObject(
        Transform parent,
        string name)
    {
        var existing =
            parent.Find(name);

        if (existing != null)
        {
            return existing.gameObject;
        }

        var go =
            new GameObject(
                name,
                typeof(RectTransform));

        go.transform.SetParent(
            parent,
            false);

        Undo.RegisterCreatedObjectUndo(
            go,
            "Create Lobby Pre-Match UI");

        return go;
    }

    private static T Get<T>(
        SerializedObject serialized,
        string propertyName)
        where T : Object
    {
        return serialized
            .FindProperty(propertyName)
            ?.objectReferenceValue as T;
    }

    private static void Assign(
        SerializedObject serialized,
        string propertyName,
        Object value)
    {
        var property =
            serialized.FindProperty(
                propertyName);

        if (property != null)
        {
            property.objectReferenceValue =
                value;
        }
    }

    private static void Hide(
        Transform root,
        string name)
    {
        var target =
            root.Find(name);

        if (target != null)
        {
            target.gameObject.SetActive(false);
        }
    }

    private static void PlaceTopLeft(
        RectTransform rect,
        float x,
        float y,
        float width,
        float height)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin =
            new Vector2(0f, 1f);

        rect.anchorMax =
            new Vector2(0f, 1f);

        rect.pivot =
            new Vector2(0f, 1f);

        rect.anchoredPosition =
            new Vector2(x, -y);

        rect.sizeDelta =
            new Vector2(width, height);
    }

    private static void PlaceTopRight(
        RectTransform rect,
        float x,
        float y,
        float width,
        float height)
    {
        rect.anchorMin =
            new Vector2(1f, 1f);

        rect.anchorMax =
            new Vector2(1f, 1f);

        rect.pivot =
            new Vector2(1f, 1f);

        rect.anchoredPosition =
            new Vector2(-x, -y);

        rect.sizeDelta =
            new Vector2(width, height);
    }

    private static void Stretch(
        RectTransform rect)
    {
        rect.anchorMin =
            Vector2.zero;

        rect.anchorMax =
            Vector2.one;

        rect.offsetMin =
            Vector2.zero;

        rect.offsetMax =
            Vector2.zero;
    }

    private static Color Hex(
        string hex,
        float alpha)
    {
        ColorUtility.TryParseHtmlString(
            "#" + hex,
            out var color);

        color.a =
            alpha;

        return color;
    }
}
