using EchoProtocol.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class LobbyTeamToolStyle
{
    private const string LobbyScenePath =
        "Assets/Scenes/Lobby.unity";

    private static readonly Color Control =
        Hex("101415", 0.94f);

    private static readonly Color Border =
        Hex("3A4142", 1f);

    private static readonly Color TextPrimary =
        Hex("DADDD9", 1f);

    private static readonly Color TextSecondary =
        Hex("7F8786", 1f);

    private static readonly Color Accent =
        Hex("8B403B", 1f);

    [MenuItem(
        "ECHO PROTOCOL/Lobby/Add Team Tool Selector")]
    public static void BuildFromMenu()
    {
        var scene =
            SceneManager.GetActiveScene();

        if (scene.name != "Lobby")
        {
            Debug.LogError("Open Lobby first.");
            return;
        }

        Build(scene);

        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log(
            "[LobbyTeamToolStyle] Team Tool selector built.");
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
            "[ECHO] Lobby team tool selector build completed.");
    }

    private static void Build(Scene scene)
    {
        var lobbyUI =
            Object.FindAnyObjectByType<NetworkLobbyUI>(
                FindObjectsInactive.Include);

        if (lobbyUI == null)
        {
            throw new System.InvalidOperationException(
                "NetworkLobbyUI not found.");
        }

        var root =
            lobbyUI.transform as RectTransform;

        var content =
            root?.Find("PreMatchContent")
            as RectTransform;

        if (content == null)
        {
            throw new System.InvalidOperationException(
                "PreMatchContent missing. Run Phase C1 first.");
        }

        Undo.RegisterFullObjectHierarchyUndo(
            lobbyUI.gameObject,
            "Build Team Tool Selector");

        var font =
            content.GetComponentInChildren<TMP_Text>(true)
                ?.font
            ?? TMP_Settings.defaultFontAsset;

        // ---------------------------------------------
        // Label
        // ---------------------------------------------

        var label =
            EnsureText(
                content,
                "TeamToolLabel",
                font);

        PlaceTopLeft(
            label.rectTransform,
            32f,
            304f,
            390f,
            20f);

        label.text = "TEAM TOOL";
        label.fontSize = 10f;
        label.fontStyle = FontStyles.Bold;
        label.characterSpacing = 2f;
        label.color = TextSecondary;
        label.alignment =
            TextAlignmentOptions.Left;

        // ---------------------------------------------
        // Selector
        // ---------------------------------------------

        var row =
            EnsureRect(
                content,
                "TeamToolRow");

        PlaceTopLeft(
            row,
            32f,
            332f,
            390f,
            48f);

        var bg =
            EnsureImage(
                row,
                "Background");

        Stretch(bg.rectTransform);

        bg.color = Control;
        bg.raycastTarget = false;

        var outline =
            row.GetComponent<Outline>();

        if (outline == null)
        {
            outline =
                Undo.AddComponent<Outline>(
                    row.gameObject);
        }

        outline.effectColor = Border;
        outline.effectDistance =
            new Vector2(1f, -1f);

        var previous =
            EnsureButton(
                row,
                "PreviousButton",
                "<",
                font);

        PlaceTopLeft(
            previous.transform as RectTransform,
            0f,
            0f,
            48f,
            48f);

        StyleArrow(previous);

        var value =
            EnsureText(
                row,
                "ToolValueText",
                font);

        PlaceTopLeft(
            value.rectTransform,
            52f,
            0f,
            286f,
            48f);

        value.text = "NONE";
        value.fontSize = 14f;
        value.fontStyle = FontStyles.Bold;
        value.color = TextPrimary;
        value.alignment =
            TextAlignmentOptions.Center;

        var next =
            EnsureButton(
                row,
                "NextButton",
                ">",
                font);

        PlaceTopLeft(
            next.transform as RectTransform,
            342f,
            0f,
            48f,
            48f);

        StyleArrow(next);

        var feedback =
            EnsureText(
                content,
                "TeamToolFeedback",
                font);

        PlaceTopLeft(
            feedback.rectTransform,
            32f,
            386f,
            390f,
            18f);

        feedback.text = string.Empty;
        feedback.fontSize = 9f;
        feedback.fontStyle = FontStyles.Bold;
        feedback.color = Accent;
        feedback.alignment =
            TextAlignmentOptions.Left;

        // ---------------------------------------------
        // Move lower sections down
        // ---------------------------------------------

        Move(
            content,
            "RoomLabel",
            32f,
            420f,
            390f,
            20f);

        Move(
            content,
            "RoomNameText",
            32f,
            448f,
            270f,
            32f);

        var serializedLobby =
            new SerializedObject(lobbyUI);

        var count =
            Get<TMP_Text>(
                serializedLobby,
                "memberCountText");

        var ready =
            Get<Button>(
                serializedLobby,
                "readyButton");

        var start =
            Get<Button>(
                serializedLobby,
                "startButton");

        var exit =
            Get<Button>(
                serializedLobby,
                "exitButton");

        var status =
            Get<TMP_Text>(
                serializedLobby,
                "statusText");

        var indicator =
            Get<Image>(
                serializedLobby,
                "statusIndicator");

        if (count != null)
        {
            PlaceTopLeft(
                count.rectTransform,
                310f,
                450f,
                112f,
                28f);
        }

        if (ready != null)
        {
            PlaceTopLeft(
                ready.transform as RectTransform,
                32f,
                510f,
                390f,
                52f);
        }

        if (start != null)
        {
            PlaceTopLeft(
                start.transform as RectTransform,
                32f,
                580f,
                390f,
                64f);
        }

        if (indicator != null)
        {
            PlaceTopLeft(
                indicator.rectTransform,
                34f,
                674f,
                8f,
                8f);
        }

        if (status != null)
        {
            PlaceTopLeft(
                status.rectTransform,
                54f,
                660f,
                368f,
                40f);
        }

        Hide(content, "FooterDivider");
        Hide(content, "BottomDivider");
        Hide(content, "LeaveDivider");
        Hide(content, "Divider");

        if (exit != null)
        {
            PlaceTopLeft(
                exit.transform as RectTransform,
                32f,
                738f,
                390f,
                44f);

            StyleSecondaryFooterButton(
                exit,
                "LEAVE ROOM");
        }

        // ---------------------------------------------
        // Controller stays on active Lobby root
        // ---------------------------------------------

        var selector =
            root.GetComponent<LobbyTeamToolSelector>();

        if (selector == null)
        {
            selector =
                Undo.AddComponent<LobbyTeamToolSelector>(
                    root.gameObject);
        }

        var serializedSelector =
            new SerializedObject(selector);

        Assign(
            serializedSelector,
            "previousButton",
            previous);

        Assign(
            serializedSelector,
            "nextButton",
            next);

        Assign(
            serializedSelector,
            "valueText",
            value);

        Assign(
            serializedSelector,
            "feedbackText",
            feedback);

        serializedSelector.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void StyleSecondaryFooterButton(
        Button button,
        string labelText)
    {
        button.gameObject.SetActive(true);

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
            Hex("121718", 0.96f);

        image.raycastTarget = true;

        button.targetGraphic = image;

        var outline =
            button.GetComponent<Outline>();

        if (outline == null)
        {
            outline =
                Undo.AddComponent<Outline>(
                    button.gameObject);
        }

        outline.effectColor = Border;
        outline.effectDistance =
            new Vector2(1f, -1f);

        var colors =
            button.colors;

        colors.normalColor =
            Hex("121718", 0.96f);

        colors.highlightedColor =
            Hex("23292A", 1f);

        colors.selectedColor =
            colors.highlightedColor;

        colors.pressedColor =
            Hex("3A2422", 1f);

        colors.disabledColor =
            Hex("0B0D0E", 0.5f);

        colors.fadeDuration = 0.08f;

        button.colors = colors;

        var label =
            button.GetComponentInChildren<TMP_Text>(
                true);

        if (label != null)
        {
            label.text = labelText;
            label.fontSize = 11f;
            label.fontStyle = FontStyles.Bold;
            label.color = TextSecondary;
            label.alignment =
                TextAlignmentOptions.Center;
        }
    }

    private static void Hide(
        Transform parent,
        string name)
    {
        var target =
            parent.Find(name);

        if (target != null)
        {
            target.gameObject.SetActive(false);
        }
    }
    private static void StyleArrow(
        Button button)
    {
        var image =
            button.targetGraphic as Image
            ?? button.GetComponent<Image>();

        image.color = Color.clear;

        var colors =
            button.colors;

        colors.normalColor = Color.clear;
        colors.highlightedColor =
            Hex("252A29", 1f);

        colors.selectedColor =
            colors.highlightedColor;

        colors.pressedColor =
            Hex("482725", 1f);

        colors.disabledColor =
            Hex("090B0B", 0.2f);

        colors.fadeDuration = 0.08f;

        button.colors = colors;

        var label =
            button.GetComponentInChildren<TMP_Text>(
                true);

        if (label != null)
        {
            label.fontSize = 20f;
            label.fontStyle = FontStyles.Bold;
            label.color = TextSecondary;
            label.alignment =
                TextAlignmentOptions.Center;
        }
    }

    private static Button EnsureButton(
        Transform parent,
        string name,
        string text,
        TMP_FontAsset font)
    {
        var go =
            EnsureObject(parent, name);

        var image =
            go.GetComponent<Image>()
            ?? Undo.AddComponent<Image>(go);

        image.raycastTarget = true;

        var button =
            go.GetComponent<Button>()
            ?? Undo.AddComponent<Button>(go);

        button.targetGraphic = image;

        var label =
            EnsureText(
                go.transform,
                "Text",
                font);

        Stretch(label.rectTransform);

        label.text = text;
        label.raycastTarget = false;

        return button;
    }

    private static TMP_Text EnsureText(
        Transform parent,
        string name,
        TMP_FontAsset font)
    {
        var go =
            EnsureObject(parent, name);

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
            EnsureObject(parent, name);

        return go.GetComponent<Image>()
            ?? Undo.AddComponent<Image>(go);
    }

    private static RectTransform EnsureRect(
        Transform parent,
        string name)
    {
        return EnsureObject(
            parent,
            name)
            .GetComponent<RectTransform>();
    }

    private static GameObject EnsureObject(
        Transform parent,
        string name)
    {
        var existing =
            parent.Find(name);

        if (existing != null)
            return existing.gameObject;

        var go =
            new GameObject(
                name,
                typeof(RectTransform));

        go.transform.SetParent(
            parent,
            false);

        Undo.RegisterCreatedObjectUndo(
            go,
            "Create Team Tool UI");

        return go;
    }

    private static void Move(
        Transform parent,
        string name,
        float x,
        float y,
        float width,
        float height)
    {
        var rect =
            parent.Find(name)
            as RectTransform;

        if (rect != null)
        {
            PlaceTopLeft(
                rect,
                x,
                y,
                width,
                height);
        }
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
            serialized.FindProperty(propertyName);

        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void PlaceTopLeft(
        RectTransform rect,
        float x,
        float y,
        float width,
        float height)
    {
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

    private static void Stretch(
        RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Color Hex(
        string hex,
        float alpha)
    {
        ColorUtility.TryParseHtmlString(
            "#" + hex,
            out var color);

        color.a = alpha;
        return color;
    }
}
