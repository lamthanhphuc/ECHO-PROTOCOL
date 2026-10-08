using EchoProtocol.UI.MainMenu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MainMenuMatchmakingStyle
{
    private const string ScenePath =
        "Assets/Scenes/MainMenu.unity";

    private static readonly Color TextPrimary =
        Hex("E4E3DE", 1f);

    private static readonly Color TextSecondary =
        Hex("8A8C88", 1f);

    private static readonly Color Accent =
        Hex("8D3935", 1f);

    private static readonly Color Panel =
        Hex("080A0A", 0.94f);

    private static readonly Color Control =
        Hex("111515", 0.96f);

    private static readonly Color Border =
        Hex("414543", 1f);

    [MenuItem(
        "ECHO PROTOCOL/Main Menu/Build Matchmaking Menu")]
    public static void BuildFromMenu()
    {
        var scene =
            SceneManager.GetActiveScene();

        if (scene.name != "MainMenu")
        {
            Debug.LogError(
                "Open MainMenu first.");
            return;
        }

        Build(scene);

        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log(
            "[MainMenuMatchmakingStyle] Built. Save scene.");
    }

    public static void BuildMainMenuBatch()
    {
        var scene =
            EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);

        Build(scene);

        EditorSceneManager.MarkSceneDirty(scene);

        if (!EditorSceneManager.SaveScene(scene))
        {
            throw new System.InvalidOperationException(
                "Could not save MainMenu.");
        }

        Debug.Log(
            "[ECHO] Main menu matchmaking build completed.");
    }

    private static void Build(Scene scene)
    {
        var canvas =
            GameObject.Find("MainMenuCanvas");

        var profile =
            Object.FindAnyObjectByType<MainMenuProfileController>(
                FindObjectsInactive.Include);

        if (canvas == null || profile == null)
        {
            throw new System.InvalidOperationException(
                "MainMenuCanvas or profile controller missing.");
        }

        var root =
            profile.transform as RectTransform;

        if (root == null)
        {
            throw new System.InvalidOperationException(
                "MainMenuPanel RectTransform missing.");
        }

        Undo.RegisterFullObjectHierarchyUndo(
            canvas,
            "Build Main Menu Matchmaking");

        var font =
            root.GetComponentInChildren<Text>(true)?.font
            ?? Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");

        // -------------------------------------------------
        // Stronger DEVOUR-like title composition
        // -------------------------------------------------

        var header =
            root.Find("HeaderText")
                ?.GetComponent<Text>();

        if (header != null)
        {
            PlaceTopLeft(
                header.rectTransform,
                0f,
                0f,
                430f,
                78f);

            header.fontSize = 54;
            header.fontStyle = FontStyle.Bold;
            header.color = TextPrimary;
            header.alignment =
                TextAnchor.MiddleLeft;
        }

        var subtitle =
            root.Find("SystemLabel")
                ?.GetComponent<Text>();

        if (subtitle != null)
        {
            PlaceTopLeft(
                subtitle.rectTransform,
                3f,
                78f,
                420f,
                24f);

            subtitle.fontSize = 12;
            subtitle.color = TextSecondary;
        }

        var divider =
            root.Find("Divider")
                ?.GetComponent<Image>();

        if (divider != null)
        {
            PlaceTopLeft(
                divider.rectTransform,
                3f,
                118f,
                76f,
                2f);

            divider.color = Accent;
        }

        // Old PLAY is no longer the navigation path.
        var oldPlay =
            root.Find("PlayButton");

        if (oldPlay != null)
        {
            oldPlay.gameObject.SetActive(false);
        }

        var host =
            EnsureTextButton(
                root,
                "HostGameButton",
                "HOST GAME",
                font);

        var join =
            EnsureTextButton(
                root,
                "JoinGameButton",
                "JOIN GAME",
                font);

        var options =
            root.Find("OptionsButton")
                ?.GetComponent<Button>();

        var logout =
            root.Find("LogoutButton")
                ?.GetComponent<Button>();

        StyleMenuButton(
            host,
            "HOST GAME",
            2f,
            465f,
            320f,
            52f);

        StyleMenuButton(
            join,
            "JOIN GAME",
            2f,
            527f,
            320f,
            52f);

        StyleMenuButton(
            options,
            "OPTIONS",
            2f,
            589f,
            320f,
            52f);

        StyleMenuButton(
            logout,
            "LOGOUT",
            2f,
            651f,
            320f,
            52f);

        // -------------------------------------------------
        // Matchmaking popup
        // -------------------------------------------------

        var dialog =
            EnsureObject(
                canvas.transform,
                "MatchmakingDialog");

        var dialogRect =
            dialog.GetComponent<RectTransform>();

        Stretch(dialogRect);

        var overlay =
            EnsureImage(
                dialog.transform,
                "Overlay");

        Stretch(overlay.rectTransform);

        overlay.color =
            new Color(
                0f,
                0f,
                0f,
                0.68f);

        overlay.raycastTarget = true;

        var window =
            EnsurePanel(
                dialog.transform,
                "Window",
                Panel);

        PlaceCenter(
            window,
            0f,
            0f,
            500f,
            350f);

        var title =
            EnsureText(
                window,
                "TitleText",
                font);

        PlaceTopLeft(
            title.rectTransform,
            34f,
            26f,
            430f,
            40f);

        title.text = "HOST GAME";
        title.fontSize = 28;
        title.fontStyle = FontStyle.Bold;
        title.color = TextPrimary;
        title.alignment = TextAnchor.MiddleLeft;

        var titleLine =
            EnsureImage(
                window,
                "TitleLine");

        PlaceTopLeft(
            titleLine.rectTransform,
            34f,
            76f,
            64f,
            2f);

        titleLine.color = Accent;
        titleLine.raycastTarget = false;

        var roomLabel =
            EnsureText(
                window,
                "RoomLabel",
                font);

        PlaceTopLeft(
            roomLabel.rectTransform,
            34f,
            102f,
            430f,
            22f);

        roomLabel.text = "ROOM CODE";
        roomLabel.fontSize = 11;
        roomLabel.fontStyle = FontStyle.Bold;
        roomLabel.color = TextSecondary;

        var roomInput =
            EnsureInputField(
                window,
                "RoomCodeInput",
                font);

        PlaceTopLeft(
            roomInput.transform as RectTransform,
            34f,
            130f,
            432f,
            46f);

        roomInput.characterLimit = 32;
        roomInput.lineType =
            InputField.LineType.SingleLine;

        var difficultyRow =
            EnsurePanel(
                window,
                "DifficultyRow",
                Color.clear);

        PlaceTopLeft(
            difficultyRow,
            34f,
            194f,
            432f,
            42f);

        var difficultyLabel =
            EnsureText(
                difficultyRow,
                "Label",
                font);

        PlaceTopLeft(
            difficultyLabel.rectTransform,
            0f,
            0f,
            160f,
            42f);

        difficultyLabel.text =
            "DIFFICULTY";

        difficultyLabel.fontSize = 12;
        difficultyLabel.color = TextSecondary;
        difficultyLabel.alignment =
            TextAnchor.MiddleLeft;

        var difficultyButton =
            EnsureTextButton(
                difficultyRow,
                "DifficultyButton",
                "<  NORMAL  >",
                font);

        StyleControlButton(
            difficultyButton,
            176f,
            0f,
            256f,
            42f);

        var confirm =
            EnsureTextButton(
                window,
                "ConfirmButton",
                "CREATE ROOM",
                font);

        StylePrimaryButton(
            confirm,
            34f,
            254f,
            278f,
            48f);

        var back =
            EnsureTextButton(
                window,
                "BackButton",
                "BACK",
                font);

        StyleControlButton(
            back,
            324f,
            254f,
            142f,
            48f);

        var status =
            EnsureText(
                window,
                "StatusText",
                font);

        PlaceTopLeft(
            status.rectTransform,
            34f,
            312f,
            432f,
            22f);

        status.text = string.Empty;
        status.fontSize = 11;
        status.color = TextSecondary;
        status.alignment =
            TextAnchor.MiddleLeft;

        var controller =
            root.GetComponent<MainMenuMatchmakingController>();

        if (controller == null)
        {
            controller =
                Undo.AddComponent<MainMenuMatchmakingController>(
                    root.gameObject);
        }

        var staleDialogController =
            dialog.GetComponent<MainMenuMatchmakingController>();

        if (staleDialogController != null)
        {
            Undo.DestroyObjectImmediate(
                staleDialogController);
        }

        var serialized =
            new SerializedObject(controller);

        Assign(serialized, "hostButton", host);
        Assign(serialized, "joinButton", join);
        Assign(serialized, "dialog", dialog);
        Assign(serialized, "titleText", title);
        Assign(serialized, "statusText", status);
        Assign(serialized, "roomCodeInput", roomInput);
        Assign(serialized, "difficultyRow", difficultyRow.gameObject);
        Assign(serialized, "difficultyButton", difficultyButton);

        var difficultyValue =
            difficultyButton.GetComponentInChildren<Text>(
                true);

        Assign(
            serialized,
            "difficultyValueText",
            difficultyValue);

        Assign(serialized, "confirmButton", confirm);
        Assign(serialized, "backButton", back);

        serialized.ApplyModifiedPropertiesWithoutUndo();

        dialog.SetActive(false);
    }

    private static InputField EnsureInputField(
        Transform parent,
        string name,
        Font font)
    {
        var existing =
            parent.Find(name);

        GameObject go;

        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go =
                new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(InputField));

            go.transform.SetParent(
                parent,
                false);

            Undo.RegisterCreatedObjectUndo(
                go,
                "Create Room Code Input");
        }

        var image =
            go.GetComponent<Image>()
            ?? Undo.AddComponent<Image>(go);

        image.color = Control;

        var input =
            go.GetComponent<InputField>()
            ?? Undo.AddComponent<InputField>(go);

        input.targetGraphic = image;

        var text =
            EnsureText(
                go.transform,
                "Text",
                font);

        PlaceStretch(
            text.rectTransform,
            14f,
            10f,
            14f,
            10f);

        text.fontSize = 16;
        text.color = TextPrimary;
        text.alignment =
            TextAnchor.MiddleLeft;

        var placeholder =
            EnsureText(
                go.transform,
                "Placeholder",
                font);

        PlaceStretch(
            placeholder.rectTransform,
            14f,
            10f,
            14f,
            10f);

        placeholder.text =
            "Enter room code";

        placeholder.fontSize = 15;
        placeholder.fontStyle =
            FontStyle.Italic;

        placeholder.color =
            new Color(
                TextSecondary.r,
                TextSecondary.g,
                TextSecondary.b,
                0.6f);

        placeholder.alignment =
            TextAnchor.MiddleLeft;

        input.textComponent = text;
        input.placeholder = placeholder;

        return input;
    }

    private static Button EnsureTextButton(
        Transform parent,
        string name,
        string label,
        Font font)
    {
        var go =
            EnsureObject(
                parent,
                name);

        var image =
            go.GetComponent<Image>()
            ?? Undo.AddComponent<Image>(go);

        var button =
            go.GetComponent<Button>()
            ?? Undo.AddComponent<Button>(go);

        image.raycastTarget = true;
        button.targetGraphic = image;

        var text =
            EnsureText(
                go.transform,
                "Text",
                font);

        Stretch(text.rectTransform);

        text.text = label;
        text.raycastTarget = false;

        return button;
    }

    private static void StyleMenuButton(
        Button button,
        string label,
        float x,
        float y,
        float width,
        float height)
    {
        if (button == null)
            return;

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
            image.color = Color.clear;
            image.raycastTarget = true;
        }

        var colors =
            button.colors;

        colors.normalColor = Color.clear;

        colors.highlightedColor =
            Hex("171A19", 0.78f);

        colors.selectedColor =
            colors.highlightedColor;

        colors.pressedColor =
            Hex("3B2422", 0.92f);

        button.colors = colors;

        var text =
            button.GetComponentInChildren<Text>(true);

        if (text != null)
        {
            text.text = label;
            text.fontSize = 27;
            text.fontStyle = FontStyle.Normal;
            text.color = TextPrimary;
            text.alignment =
                TextAnchor.MiddleLeft;

            text.raycastTarget = false;

            PlaceStretch(
                text.rectTransform,
                8f,
                0f,
                0f,
                0f);
        }
    }

    private static void StyleControlButton(
        Button button,
        float x,
        float y,
        float width,
        float height)
    {
        PlaceTopLeft(
            button.transform as RectTransform,
            x,
            y,
            width,
            height);

        var image =
            button.targetGraphic as Image
            ?? button.GetComponent<Image>();

        image.color = Control;

        var colors =
            button.colors;

        colors.normalColor = Control;
        colors.highlightedColor =
            Hex("222727", 1f);

        colors.selectedColor =
            colors.highlightedColor;

        colors.pressedColor =
            Hex("342321", 1f);

        button.colors = colors;

        var text =
            button.GetComponentInChildren<Text>(true);

        text.fontSize = 13;
        text.fontStyle = FontStyle.Bold;
        text.color = TextPrimary;
        text.alignment =
            TextAnchor.MiddleCenter;
    }

    private static void StylePrimaryButton(
        Button button,
        float x,
        float y,
        float width,
        float height)
    {
        StyleControlButton(
            button,
            x,
            y,
            width,
            height);

        var image =
            button.targetGraphic as Image;

        if (image != null)
            image.color =
                Hex("351B19", 0.96f);

        var colors =
            button.colors;

        colors.normalColor =
            Hex("351B19", 0.96f);

        colors.highlightedColor =
            Hex("592725", 1f);

        colors.selectedColor =
            colors.highlightedColor;

        colors.pressedColor =
            Hex("762F2A", 1f);

        button.colors = colors;
    }

    private static RectTransform EnsurePanel(
        Transform parent,
        string name,
        Color color)
    {
        var go =
            EnsureObject(parent, name);

        var image =
            go.GetComponent<Image>()
            ?? Undo.AddComponent<Image>(go);

        image.color = color;

        return go.GetComponent<RectTransform>();
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
            "Create Main Menu Matchmaking UI");

        return go;
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

    private static Text EnsureText(
        Transform parent,
        string name,
        Font font)
    {
        var go =
            EnsureObject(parent, name);

        var text =
            go.GetComponent<Text>()
            ?? Undo.AddComponent<Text>(go);

        text.font = font;
        text.raycastTarget = false;

        return text;
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

    private static void PlaceCenter(
        RectTransform rect,
        float x,
        float y,
        float width,
        float height)
    {
        rect.anchorMin =
            new Vector2(0.5f, 0.5f);

        rect.anchorMax =
            new Vector2(0.5f, 0.5f);

        rect.pivot =
            new Vector2(0.5f, 0.5f);

        rect.anchoredPosition =
            new Vector2(x, y);

        rect.sizeDelta =
            new Vector2(width, height);
    }

    private static void PlaceStretch(
        RectTransform rect,
        float left,
        float top,
        float right,
        float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;

        rect.offsetMin =
            new Vector2(left, bottom);

        rect.offsetMax =
            new Vector2(-right, -top);
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
