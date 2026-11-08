using System.Linq;
using System.Reflection;
using EchoProtocol.Networking;
using EchoProtocol.UI.MainMenu;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LobbyGameModeTests
{
    [Test]
    public void RoomModesAllowOnlyTheirOwnMaps() => ValidateMapPolicy();
    public static void ValidateMapPolicy()
    {
        Assert.That(LobbyMapCatalog.IsMapAllowed(0,0),Is.True);
        Assert.That(LobbyMapCatalog.IsMapAllowed(0,1),Is.True);
        Assert.That(LobbyMapCatalog.IsMapAllowed(0,2),Is.False);
        Assert.That(LobbyMapCatalog.IsMapAllowed(1,0),Is.False);
        Assert.That(LobbyMapCatalog.IsMapAllowed(1,1),Is.False);
        Assert.That(LobbyMapCatalog.IsMapAllowed(1,2),Is.True);
        Assert.That(LobbyMapCatalog.SceneName(2),Is.EqualTo("Scene_B"));
    }
    [Test]
    public void MainMenuLeavesModeSelectionToTheLobby() => ValidateMenu();
    public static void ValidateMenu()
    {
        var active = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/MainMenu.unity");
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity",OpenSceneMode.Additive);
        MainMenuMatchmakingController controller = null;
        try
        {
            controller = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MainMenuMatchmakingController>(true)).Single();
            var so = new SerializedObject(controller);
            var original = (GameObject)so.FindProperty("difficultyRow").objectReferenceValue;
            var mode = original.transform.parent.Find("GameModeRow");
            InvokeMenuAction(controller, "OpenHost");
            Assert.That(original.activeSelf, Is.False);
            Assert.That(mode == null || !mode.gameObject.activeSelf, Is.True);
            InvokeMenuAction(controller, "OpenJoin");
            Assert.That(original.activeSelf, Is.False);
            Assert.That(mode == null || !mode.gameObject.activeSelf, Is.True);
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene,true);
            SceneManager.SetActiveScene(active);
        }
    }

    private static void InvokeMenuAction(MainMenuMatchmakingController controller, string name)
    {
        var method = typeof(MainMenuMatchmakingController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, name);
        method.Invoke(controller, null);
    }
}
