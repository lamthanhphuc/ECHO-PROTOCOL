using System.Linq;
using EchoProtocol.Networking;
using EchoProtocol.UI.MainMenu;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    public void MainMenuOffersModeOnlyWhenCreatingRoom() => ValidateMenu();
    public static void ValidateMenu()
    {
        var active = SceneManager.GetActiveScene();
        Assert.That(active.path,Is.Not.EqualTo("Assets/Scenes/MainMenu.unity"),"Run from Scene_B to preserve an open authored MainMenu scene.");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity",OpenSceneMode.Additive);
        MainMenuMatchmakingController controller = null;
        try
        {
            controller = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MainMenuMatchmakingController>(true)).Single();
            controller.SendMessage("Awake");
            var so = new SerializedObject(controller);
            var original = (GameObject)so.FindProperty("difficultyRow").objectReferenceValue;
            var mode = original.transform.parent.Find("GameModeRow");
            Assert.That(mode,Is.Not.Null);
            controller.SendMessage("OpenHost");
            Assert.That(mode.gameObject.activeSelf,Is.True);
            var button = mode.GetComponentInChildren<Button>(true);
            button.onClick.Invoke();
            Assert.That(button.GetComponentInChildren<Text>().text,Is.EqualTo(EchoProtocol.Settings.GameLanguage.Choose("<  THANH TRỪNG  >", "<  PURGE  >")));
            var modeRect = (RectTransform)mode;
            var confirm = (Button)so.FindProperty("confirmButton").objectReferenceValue;
            Assert.That(((RectTransform)confirm.transform).anchoredPosition.y,Is.LessThan(modeRect.anchoredPosition.y-modeRect.sizeDelta.y));
            controller.SendMessage("OpenJoin");
            Assert.That(mode.gameObject.activeSelf,Is.False);
        }
        finally
        {
            if(controller)controller.SendMessage("OnDisable");
            EditorSceneManager.CloseScene(scene,true);
            SceneManager.SetActiveScene(active);
        }
    }
}
