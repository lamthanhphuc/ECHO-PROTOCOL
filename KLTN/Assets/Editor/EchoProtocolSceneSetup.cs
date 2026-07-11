using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using EchoProtocol.Core;

public static class EchoProtocolSceneSetup
{
    private static readonly string[] FoundationScenes =
    {
        GameConstants.SceneBootstrap,
        GameConstants.SceneLogin,
        GameConstants.SceneMainMenu,
        GameConstants.SceneLobby,
        GameConstants.SceneGame,
        GameConstants.SceneResult
    };

    [MenuItem("ECHO PROTOCOL/Create Foundation Scenes")]
    public static void CreateFoundationScenes()
    {
        const string scenesPath = "Assets/Scenes";

        foreach (var sceneName in FoundationScenes)
        {
            var path = $"{scenesPath}/{sceneName}.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            {
                Debug.Log($"[ECHO PROTOCOL] Scene exists, skipped: {path}");
                continue;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"[ECHO PROTOCOL] Created scene: {path}");
        }

        AssetDatabase.Refresh();
        Debug.Log("[ECHO PROTOCOL] Foundation scenes setup complete.");
    }
}
