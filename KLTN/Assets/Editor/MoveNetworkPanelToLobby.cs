using System;
using EchoProtocol.Networking;
using EchoProtocol.UI.Debugging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MoveNetworkPanelToLobby
{
    private const string BootstrapPath = "Assets/Scenes/Bootstrap.unity";
    private const string LobbyPath = "Assets/Scenes/Lobby.unity";

    [MenuItem("ECHO PROTOCOL/Move Network Panel To Lobby")]
    public static void MovePanel()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var bootstrapScene = OpenScene(BootstrapPath);
        var lobbyScene = OpenScene(LobbyPath);
        var bootstrap = FindInScene<NetworkBootstrap>(bootstrapScene);
        var source = FindInScene<NetworkTestPanel>(bootstrapScene);
        var panel = FindInScene<NetworkTestPanel>(lobbyScene);

        if (bootstrap == null || (source == null && panel == null))
        {
            Debug.LogError("Cannot migrate: Bootstrap must contain NetworkBootstrap and a network panel must exist.");
            return;
        }

        if (source != null)
        {
            var services = source.GetComponent<LobbyManager>();
            var spawner = source.GetComponent<PlayerSpawner>();
            if (services == null || spawner == null || source.gameObject == bootstrap.gameObject)
            {
                Debug.LogError("Unexpected Bootstrap layout. Expected the panel, LobbyManager and PlayerSpawner on a separate object.");
                return;
            }

            if (panel == null)
            {
                var ui = new GameObject("LobbyUI");
                SceneManager.MoveGameObjectToScene(ui, lobbyScene);
                Undo.RegisterCreatedObjectUndo(ui, "Create Lobby UI");
                panel = Undo.AddComponent<NetworkTestPanel>(ui);
                EditorUtility.CopySerialized(source, panel);
            }

            // These services previously survived because the panel called
            // DontDestroyOnLoad on their shared object. Let Bootstrap own them now.
            Undo.SetTransformParent(services.transform, bootstrap.transform, "Preserve network services");
            Undo.DestroyObjectImmediate(source);
        }

        // Scene assets cannot serialize references into another scene.
        // The panel resolves persistent network services at runtime.
        var serializedPanel = new SerializedObject(panel);
        serializedPanel.FindProperty("_bootstrap").objectReferenceValue = null;
        serializedPanel.FindProperty("_lobbyManager").objectReferenceValue = null;
        serializedPanel.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(bootstrapScene);
        EditorSceneManager.MarkSceneDirty(lobbyScene);
        if (!EditorSceneManager.SaveScene(bootstrapScene) || !EditorSceneManager.SaveScene(lobbyScene))
        {
            throw new InvalidOperationException("Could not save the migrated scenes. Save the open scenes before continuing.");
        }

        SceneManager.SetActiveScene(lobbyScene);
        Selection.activeGameObject = panel.gameObject;
        EditorGUIUtility.PingObject(panel.gameObject);
        Debug.Log("Network panel moved to Lobby/LobbyUI. Test the normal flow starting from Bootstrap. The panel uses OnGUI and appears in Play mode.");
    }

    private static Scene OpenScene(string path)
    {
        var scene = SceneManager.GetSceneByPath(path);
        return scene.IsValid() && scene.isLoaded
            ? scene
            : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var component = root.GetComponentInChildren<T>(true);
            if (component != null) return component;
        }

        return null;
    }
}
