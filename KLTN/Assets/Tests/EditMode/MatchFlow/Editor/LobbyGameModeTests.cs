using System.Linq;
using System.Reflection;
using EchoProtocol.Networking;
using EchoProtocol.Networking.Authority;
using EchoProtocol.UI.MainMenu;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LobbyGameModeTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(LobbyMapCatalog.SalvageMode)]
    public void ProductionRoomPropertiesFitFusionLimit(int mode)
    {
        var owner = new GameObject("Room property limit regression");
        try
        {
            // Keep checking the production property budget; do not add per-mode room keys.
            var authority = owner.AddComponent<MatchAuthorityRuntime>();
            var properties = authority.BuildHostSessionProperties();
            var existing = properties.Keys.ToArray();
            properties[LobbyMapCatalog.GameModeKey] = mode;
            properties[LobbyMapCatalog.PropertyKey] = mode == 1 ? 2 : 0;
            Assert.That(properties.Count, Is.LessThanOrEqualTo(10), "Fusion rejects StartGame above ten custom properties.");
            Assert.That(existing.All(properties.ContainsKey), Is.True, "Keep the existing mode/backend properties.");
        }
        finally { Object.DestroyImmediate(owner); }
    }
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
    public void SalvageUsesOnlyItsOwnSciFiCopy()
    {
        Assert.That(LobbyMapCatalog.IsMapAllowed(LobbyMapCatalog.SalvageMode, 0), Is.True);
        Assert.That(LobbyMapCatalog.IsMapAllowed(LobbyMapCatalog.SalvageMode, 1), Is.False);
        Assert.That(LobbyMapCatalog.IsMapAllowed(LobbyMapCatalog.SalvageMode, 2), Is.False);
        Assert.That(LobbyMapCatalog.IsMapAllowed(3, 0), Is.False);
        Assert.That(LobbyMapCatalog.SceneName(0, 0), Is.EqualTo("SciFi"));
        Assert.That(LobbyMapCatalog.SceneName(0, 1), Is.EqualTo("Scene_A"));
        Assert.That(LobbyMapCatalog.SceneName(1, 2), Is.EqualTo("Scene_B"));
        Assert.That(LobbyMapCatalog.SceneName(LobbyMapCatalog.SalvageMode, 0), Is.EqualTo("Salvage_SciFi"));
        Assert.That(LobbyMapCatalog.TelemetryGameMode(LobbyMapCatalog.SalvageScene), Is.EqualTo("SALVAGE"));
        Assert.That(LobbyMapCatalog.TelemetryMapId(LobbyMapCatalog.SalvageScene), Is.EqualTo("SALVAGE-SCIFI"));
        Assert.That(LobbyMapCatalog.IsGameplayScene(LobbyMapCatalog.SalvageScene), Is.True);
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
