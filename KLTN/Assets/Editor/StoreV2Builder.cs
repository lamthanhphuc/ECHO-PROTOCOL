using EchoProtocol.UI;
using EchoProtocol.UI.MainMenu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class StoreV2Builder
{
    private const string MainMenuScene =
        "Assets/Scenes/MainMenu.unity";

    private const string LobbyScene =
        "Assets/Scenes/Lobby.unity";

    [MenuItem(
        "ECHO PROTOCOL/Shop/Build Store V2")]
    public static void BuildCurrentScene()
    {
        Build(
            SceneManager.GetActiveScene());

        EditorSceneManager.MarkSceneDirty(
            SceneManager.GetActiveScene());
    }

    public static void ApplyAllBatch()
    {
        BuildScene(
            MainMenuScene,
            true);

        BuildScene(
            LobbyScene,
            false);

        Debug.Log(
            "[ECHO] Store V2 scenes completed.");
    }

    private static void BuildScene(
        string path,
        bool mainMenu)
    {
        var scene =
            EditorSceneManager.OpenScene(
                path,
                OpenSceneMode.Single);

        Build(
            scene);

        if (mainMenu)
            BindMainMenu();

        EditorSceneManager.MarkSceneDirty(
            scene);

        if (!EditorSceneManager.SaveScene(scene))
        {
            throw new System.InvalidOperationException(
                "Could not save " + path);
        }
    }

    private static void Build(
        Scene scene)
    {
        Canvas canvas =
            ResolveStoreCanvas(scene);

        if (canvas == null)
        {
            throw new System.InvalidOperationException(
                "Canvas missing in " +
                scene.name);
        }

        RemoveExistingStorePopups(scene);

        var popup =
            new GameObject(
                "StoreV2Popup",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(TeamToolShopPanel));

        popup.transform.SetParent(
            canvas.transform,
            false);

        var popupRect =
            popup.transform
                as RectTransform;

        Stretch(
            popupRect);

        var overlay =
            popup.GetComponent<Image>();

        overlay.color =
            new Color(
                0f,
                0f,
                0f,
                0.76f);

        var window =
            new GameObject(
                "Window",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline));

        window.transform.SetParent(
            popup.transform,
            false);

        var windowRect =
            window.transform
                as RectTransform;

        Center(
            windowRect,
            1180f,
            680f);

        var background =
            window.GetComponent<Image>();

        background.color =
            new Color32(
                8,
                11,
                12,
                252);

        var outline =
            window.GetComponent<Outline>();

        outline.effectColor =
            new Color32(
                62,
                68,
                68,
                240);

        outline.effectDistance =
            new Vector2(
                1f,
                -1f);

        popup.transform.SetAsLastSibling();

        popup.SetActive(false);
    }

    private static Canvas ResolveStoreCanvas(
        Scene scene)
    {
        if (scene.name == "Lobby")
        {
            foreach (var root
                     in scene.GetRootGameObjects())
            {
                var lobby =
                    root.GetComponentInChildren<
                        NetworkLobbyUI>(true);

                if (lobby == null)
                    continue;

                var lobbyCanvas =
                    lobby.GetComponentInParent<Canvas>();

                if (lobbyCanvas != null)
                    return lobbyCanvas;
            }
        }

        foreach (var root
                 in scene.GetRootGameObjects())
        {
            var canvases =
                root.GetComponentsInChildren<
                    Canvas>(true);

            if (canvases.Length > 0)
                return canvases[0];
        }

        return null;
    }

    private static void RemoveExistingStorePopups(
        Scene scene)
    {
        foreach (var root
                 in scene.GetRootGameObjects())
        {
            var transforms =
                root.GetComponentsInChildren<
                    Transform>(true);

            foreach (var candidate in transforms)
            {
                // DestroyImmediate on a parent also destroys its children.
                // Cached Transform references can therefore already be destroyed.
                if (candidate == null)
                    continue;
                if (candidate.name !=
                    "StoreV2Popup")
                {
                    continue;
                }

                Object.DestroyImmediate(
                    candidate.gameObject);
            }
        }
    }

    private static void BindMainMenu()
    {
        var controller =
            Object.FindAnyObjectByType<
                MainMenuProfileController>(
                    FindObjectsInactive.Include);

        GameObject popup =
            null;

        var activeScene =
            SceneManager.GetActiveScene();

        foreach (var root
                 in activeScene.GetRootGameObjects())
        {
            var transforms =
                root.GetComponentsInChildren<
                    Transform>(true);

            for (int i = 0;
                 i < transforms.Length;
                 i++)
            {
                if (transforms[i].name !=
                    "StoreV2Popup")
                {
                    continue;
                }

                popup =
                    transforms[i].gameObject;

                break;
            }

            if (popup != null)
                break;
        }

        if (controller == null
            || popup == null)
        {
            throw new System.InvalidOperationException(
                "MainMenu Store binding failed.");
        }

        var serialized =
            new SerializedObject(
                controller);

        var property =
            serialized.FindProperty(
                "shopPopup");

        if (property == null)
        {
            throw new System.InvalidOperationException(
                "shopPopup property missing.");
        }

        property.objectReferenceValue =
            popup;

        serialized.ApplyModifiedPropertiesWithoutUndo();
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

    private static void Center(
        RectTransform rect,
        float width,
        float height)
    {
        rect.anchorMin =
            new Vector2(0.5f,0.5f);

        rect.anchorMax =
            new Vector2(0.5f,0.5f);

        rect.pivot =
            new Vector2(0.5f,0.5f);

        rect.anchoredPosition =
            Vector2.zero;

        rect.sizeDelta =
            new Vector2(
                width,
                height);
    }
}
