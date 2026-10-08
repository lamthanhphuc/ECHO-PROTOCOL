using EchoProtocol.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class LobbyFinalPolishStyle
{
    private const string LobbyScenePath =
        "Assets/Scenes/Lobby.unity";

    private static readonly Color TextPrimary =
        Hex("E0E3DF", 1f);

    private static readonly Color TextSecondary =
        Hex("818987", 1f);

    private static readonly Color Border =
        Hex("343B3C", 1f);

    private static readonly Color ReadyDark =
        Hex("101713", 1f);

    private static readonly Color ReadyHover =
        Hex("19271E", 1f);

    private static readonly Color ReadyText =
        Hex("86A98D", 1f);

    private static readonly Color StartDark =
        Hex("301514", 1f);

    private static readonly Color StartHover =
        Hex("522321", 1f);

    private static readonly Color StartPressed =
        Hex("702C28", 1f);

    private static readonly Color StartBorder =
        Hex("71332F", 1f);

    [MenuItem(
        "ECHO PROTOCOL/Lobby/Apply Final Lobby Polish")]
    public static void ApplyFromMenu()
    {
        var scene =
            SceneManager.GetActiveScene();

        if (scene.name != "Lobby")
        {
            Debug.LogError(
                "Open Lobby scene first.");

            return;
        }

        Apply(scene);

        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log(
            "[LobbyFinalPolishStyle] Applied.");
    }

    public static void ApplyLobbyBatch()
    {
        var scene =
            EditorSceneManager.OpenScene(
                LobbyScenePath,
                OpenSceneMode.Single);

        Apply(scene);

        EditorSceneManager.MarkSceneDirty(scene);

        if (!EditorSceneManager.SaveScene(scene))
        {
            throw new System.InvalidOperationException(
                "Could not save Lobby scene.");
        }

        Debug.Log(
            "[ECHO] Lobby final polish completed.");
    }

    private static void Apply(Scene scene)
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

        var content =
            root?.Find("PreMatchContent")
            as RectTransform;

        if (root == null || content == null)
        {
            throw new System.InvalidOperationException(
                "PreMatchContent missing.");
        }

        Undo.RegisterFullObjectHierarchyUndo(
            ui.gameObject,
            "Final Lobby Polish");

        var serialized =
            new SerializedObject(ui);

        var ready =
            Get<Button>(
                serialized,
                "readyButton");

        var start =
            Get<Button>(
                serialized,
                "startButton");

        var status =
            Get<TMP_Text>(
                serialized,
                "statusText");

        var indicator =
            Get<Image>(
                serialized,
                "statusIndicator");

        var count =
            Get<TMP_Text>(
                serialized,
                "memberCountText");

        var difficulty =
            Get<TMP_Dropdown>(
                serialized,
                "difficultyDropdown");

        // =============================================
        // PANEL
        // =============================================

        var rootImage =
            root.GetComponent<Image>();

        if (rootImage != null)
        {
            rootImage.color =
                Hex("080A0B", 0.93f);
        }

        var rootOutline =
            root.GetComponent<Outline>();

        if (rootOutline != null)
        {
            rootOutline.effectColor =
                Hex("384041", 0.9f);

            rootOutline.effectDistance =
                new Vector2(1f, -1f);
        }

        // =============================================
        // HEADER
        // =============================================

        var title =
            content.Find("MissionHeader")
                ?.GetComponent<TMP_Text>();

        if (title != null)
        {
            title.fontSize = 26f;

            title.fontStyle =
                FontStyles.Bold;

            title.color =
                TextPrimary;

            title.characterSpacing =
                1.5f;
        }

        var accent =
            content.Find("HeaderAccent")
                ?.GetComponent<Image>();

        if (accent != null)
        {
            accent.color =
                Hex("8B403B", 1f);

            accent.rectTransform.sizeDelta =
                new Vector2(72f, 2f);
        }

        // =============================================
        // SECTION LABELS
        // =============================================

        StyleSectionLabel(
            content,
            "SiteLabel");

        StyleSectionLabel(
            content,
            "DifficultyLabel");

        StyleSectionLabel(
            content,
            "TeamToolLabel");

        StyleSectionLabel(
            content,
            "RoomLabel");

        // =============================================
        // VALUE TEXT
        // =============================================

        var site =
            content.Find("SiteValue")
                ?.GetComponent<TMP_Text>();

        if (site != null)
        {
            site.fontSize = 16f;
            site.color = TextPrimary;
        }

        var room =
            content.Find("RoomNameText")
                ?.GetComponent<TMP_Text>();

        if (room != null)
        {
            room.fontSize = 18f;

            room.fontStyle =
                FontStyles.Bold;

            room.color =
                TextPrimary;
        }

        if (count != null)
        {
            count.fontSize = 11f;

            count.fontStyle =
                FontStyles.Bold;

            count.color =
                TextSecondary;

            count.alignment =
                TextAlignmentOptions.Right;
        }

        // =============================================
        // DIFFICULTY
        // =============================================

        if (difficulty != null)
        {
            var image =
                difficulty.GetComponent<Image>();

            if (image != null)
            {
                image.color =
                    Hex("0D1112", 1f);
            }

            var outline =
                difficulty.GetComponent<Outline>();

            if (outline != null)
            {
                outline.effectColor =
                    Border;
            }

            if (difficulty.captionText != null)
            {
                difficulty.captionText.fontSize =
                    13f;

                difficulty.captionText.fontStyle =
                    FontStyles.Bold;

                difficulty.captionText.color =
                    TextPrimary;
            }
        }

        // =============================================
        // READY BUTTON
        // =============================================

        if (ready != null)
        {
            StyleReadyButton(
                ready);
        }

        // =============================================
        // START MISSION — PRIMARY CTA
        // =============================================

        if (start != null)
        {
            StyleStartButton(
                start);
        }

        // =============================================
        // CONNECTION STATUS
        // =============================================

        if (status != null)
        {
            status.fontSize = 10f;

            status.fontStyle =
                FontStyles.Bold;

            status.characterSpacing =
                1.2f;

            status.color =
                TextSecondary;

            status.alignment =
                TextAlignmentOptions.Left;
        }

        if (indicator != null)
        {
            indicator.rectTransform.sizeDelta =
                new Vector2(7f, 7f);
        }

        // =============================================
        // TOOL VALUE
        // =============================================

        var tool =
            content.Find(
                    "TeamToolRow/ToolValueText")
                ?.GetComponent<TMP_Text>();

        if (tool != null)
        {
            tool.fontSize = 13f;

            tool.fontStyle =
                FontStyles.Bold;

            tool.characterSpacing =
                0.8f;

            tool.color =
                TextPrimary;
        }
    }

    private static void StyleSectionLabel(
        Transform content,
        string name)
    {
        var label =
            content.Find(name)
                ?.GetComponent<TMP_Text>();

        if (label == null)
            return;

        label.fontSize = 9f;

        label.fontStyle =
            FontStyles.Bold;

        label.characterSpacing =
            2.2f;

        label.color =
            TextSecondary;
    }

    private static void StyleReadyButton(
        Button button)
    {
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
            ReadyDark;

        image.raycastTarget =
            true;

        button.targetGraphic =
            image;

        var colors =
            button.colors;

        colors.normalColor =
            ReadyDark;

        colors.highlightedColor =
            ReadyHover;

        colors.selectedColor =
            ReadyHover;

        colors.pressedColor =
            Hex("23402C", 1f);

        colors.disabledColor =
            Hex("0B0E0C", 0.55f);

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
            Hex("31483A", 1f);

        outline.effectDistance =
            new Vector2(1f, -1f);

        var label =
            button.GetComponentInChildren<TMP_Text>(
                true);

        if (label != null)
        {
            label.fontSize = 12f;

            label.fontStyle =
                FontStyles.Bold;

            label.characterSpacing =
                1f;

            label.color =
                ReadyText;

            label.alignment =
                TextAlignmentOptions.Center;
        }
    }

    private static void StyleStartButton(
        Button button)
    {
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
            StartDark;

        image.raycastTarget =
            true;

        button.targetGraphic =
            image;

        var colors =
            button.colors;

        colors.normalColor =
            StartDark;

        colors.highlightedColor =
            StartHover;

        colors.selectedColor =
            StartHover;

        colors.pressedColor =
            StartPressed;

        colors.disabledColor =
            Hex("0B0C0C", 0.68f);

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
            StartBorder;

        outline.effectDistance =
            new Vector2(1f, -1f);

        var label =
            button.GetComponentInChildren<TMP_Text>(
                true);

        if (label != null)
        {
            label.fontSize = 14f;

            label.fontStyle =
                FontStyles.Bold;

            label.characterSpacing =
                1f;

            label.color =
                TextPrimary;

            label.alignment =
                TextAlignmentOptions.Center;
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
