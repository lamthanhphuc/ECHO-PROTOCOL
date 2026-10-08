using EchoProtocol.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class LobbyRoleLayoutStyle
{
    private const string LobbyScenePath =
        "Assets/Scenes/Lobby.unity";

    private static readonly Color Muted =
        Hex("858B89", 1f);

    [MenuItem(
        "ECHO PROTOCOL/Lobby/Add Host Client Role Layout")]
    public static void BuildFromMenu()
    {
        var scene =
            SceneManager.GetActiveScene();

        if (scene.name != "Lobby")
        {
            Debug.LogError(
                "Open Lobby first.");

            return;
        }

        Build(scene);

        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log(
            "[LobbyRoleLayoutStyle] Applied.");
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
                "Could not save Lobby.");
        }

        Debug.Log(
            "[ECHO] Lobby role layout completed.");
    }

    private static void Build(Scene scene)
    {
        var lobby =
            Object.FindAnyObjectByType<NetworkLobbyUI>(
                FindObjectsInactive.Include);

        if (lobby == null)
        {
            throw new System.InvalidOperationException(
                "NetworkLobbyUI missing.");
        }

        var root =
            lobby.transform as RectTransform;

        var content =
            root?.Find("PreMatchContent")
            as RectTransform;

        if (root == null || content == null)
        {
            throw new System.InvalidOperationException(
                "PreMatchContent missing.");
        }

        Undo.RegisterFullObjectHierarchyUndo(
            lobby.gameObject,
            "Add Lobby Role Layout");

        var serializedLobby =
            new SerializedObject(lobby);

        var difficulty =
            Get<TMP_Dropdown>(
                serializedLobby,
                "difficultyDropdown");

        var ready =
            Get<Button>(
                serializedLobby,
                "readyButton");

        var start =
            Get<Button>(
                serializedLobby,
                "startButton");

        if (difficulty == null
            || ready == null
            || start == null)
        {
            throw new System.InvalidOperationException(
                "Lobby controls are not assigned.");
        }

        var font =
            content.GetComponentInChildren<TMP_Text>(true)
                ?.font
            ?? TMP_Settings.defaultFontAsset;

        // ---------------------------------------------
        // HOST / CLIENT badge
        // ---------------------------------------------

        var roleBadge =
            EnsureText(
                content,
                "RoleBadge",
                font);

        PlaceTopLeft(
            roleBadge.rectTransform,
            330f,
            30f,
            92f,
            30f);

        roleBadge.text =
            "OFFLINE";

        roleBadge.fontSize = 10f;

        roleBadge.fontStyle =
            FontStyles.Bold;

        roleBadge.characterSpacing =
            2f;

        roleBadge.color =
            Muted;

        roleBadge.alignment =
            TextAlignmentOptions.Right;

        // ---------------------------------------------
        // Difficulty ownership indicator
        // ---------------------------------------------

        var authority =
            EnsureText(
                content,
                "DifficultyAuthority",
                font);

        PlaceTopLeft(
            authority.rectTransform,
            238f,
            202f,
            184f,
            20f);

        authority.text =
            string.Empty;

        authority.fontSize = 8f;

        authority.fontStyle =
            FontStyles.Bold;

        authority.characterSpacing =
            1.5f;

        authority.color =
            Muted;

        authority.alignment =
            TextAlignmentOptions.Right;

        // Visual readonly feedback for Client.
        var difficultyGroup =
            difficulty.GetComponent<CanvasGroup>();

        if (difficultyGroup == null)
        {
            difficultyGroup =
                Undo.AddComponent<CanvasGroup>(
                    difficulty.gameObject);
        }

        difficultyGroup.alpha = 1f;

        // ---------------------------------------------
        // Runtime role controller
        // ---------------------------------------------

        var controller =
            root.GetComponent<LobbyRoleLayoutController>();

        if (controller == null)
        {
            controller =
                Undo.AddComponent<LobbyRoleLayoutController>(
                    root.gameObject);
        }

        var serialized =
            new SerializedObject(controller);

        Assign(
            serialized,
            "roleBadgeText",
            roleBadge);

        Assign(
            serialized,
            "difficultyAuthorityText",
            authority);

        Assign(
            serialized,
            "difficultyDropdown",
            difficulty);

        Assign(
            serialized,
            "readyButton",
            ready);

        Assign(
            serialized,
            "startButton",
            start);

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static TMP_Text EnsureText(
        Transform parent,
        string name,
        TMP_FontAsset font)
    {
        var existing =
            parent.Find(name);

        GameObject go;

        if (existing != null)
        {
            go =
                existing.gameObject;
        }
        else
        {
            go =
                new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(TextMeshProUGUI));

            go.transform.SetParent(
                parent,
                false);

            Undo.RegisterCreatedObjectUndo(
                go,
                "Create Lobby Role UI");
        }

        var text =
            go.GetComponent<TextMeshProUGUI>()
            ?? Undo.AddComponent<TextMeshProUGUI>(go);

        text.font = font;
        text.raycastTarget = false;
        text.textWrappingMode =
            TextWrappingModes.NoWrap;

        return text;
    }

    private static T Get<T>(
        SerializedObject serialized,
        string name)
        where T : Object
    {
        return serialized
            .FindProperty(name)
            ?.objectReferenceValue as T;
    }

    private static void Assign(
        SerializedObject serialized,
        string name,
        Object value)
    {
        var property =
            serialized.FindProperty(name);

        if (property != null)
        {
            property.objectReferenceValue =
                value;
        }
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
