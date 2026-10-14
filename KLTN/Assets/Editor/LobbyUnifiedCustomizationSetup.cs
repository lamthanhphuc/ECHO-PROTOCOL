using EchoProtocol.UI;
using EchoProtocol.UI.MainMenu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class LobbyUnifiedCustomizationSetup
{
    [MenuItem("ECHO Protocol/Lobby/Apply Unified Customization")]
    public static void Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.name!="Lobby" || Application.isPlaying)throw new System.InvalidOperationException("Open Lobby outside Play Mode.");
        GameObject canvas=null;
        foreach(var root in scene.GetRootGameObjects())if(root.name=="NetworkLobbyCanvas")canvas=root;
        if(canvas==null)throw new System.InvalidOperationException("Lobby canvas missing.");
        var navigation=canvas.GetComponent<LobbyCustomizationNavigation>();
        var button=canvas.transform.Find("PreMatchContent/OutfitSelectionButton")?.GetComponent<Button>();
        var store=canvas.GetComponentInChildren<TeamToolShopPanel>(true);
        if(navigation==null || button==null || store==null)throw new System.InvalidOperationException("Customization navigation references missing.");
        var serialized=new SerializedObject(navigation);
        serialized.FindProperty("outfitButton").objectReferenceValue=button;
        serialized.FindProperty("store").objectReferenceValue=store;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        foreach(string name in new[]{"OutfitPanel","PetPanel"}) {
            var panel=canvas.transform.Find(name);
            if(panel!=null)Object.DestroyImmediate(panel.gameObject);
        }
        EditorUtility.SetDirty(navigation);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("LOBBY_UNIFIED_CUSTOMIZATION_VALIDATED: shop bound, obsolete panels removed.");
    }
}
