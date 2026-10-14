using EchoProtocol.UI.HUD;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class GameplayHUDPresentationSetup
{
    private const string SessionKey = "Echo.HUD.FieldPresentation.v5";

    static GameplayHUDPresentationSetup()
    {
        EditorApplication.delayCall += ApplyOnce;
    }

    private static void ApplyOnce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;
        if (SessionState.GetBool(SessionKey, false)) return;
        ApplyPrefabs();
        SessionState.SetBool(SessionKey, true);
    }

    [MenuItem("Tools/ECHO Protocol/HUD/Apply Field Presentation")]
    public static void ApplyPrefabs()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        foreach (var path in new[]
        {
            "Assets/Resources/PF_GameplayHUD_Canvas.prefab",
            "Assets/Prefabs/UI/PF_GameplayHUD_Canvas.prefab",
            "Assets/Resources/PF_FieldScanner_HUD.prefab"
        })
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                HUDPresentationStyle.Apply(root.transform);
                foreach (var scanner in root.GetComponentsInChildren<HUDFieldScanner>(true))
                    scanner.ConfigureLayout();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        Debug.Log("[HUD] Field presentation applied to gameplay and standalone scanner prefabs.");
    }
}
