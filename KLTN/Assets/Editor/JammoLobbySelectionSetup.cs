using System;
using EchoProtocol.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class JammoLobbySelectionSetup
{
    [MenuItem("ECHO PROTOCOL/Player/Install Character Choice in Lobby")]
    public static void Install()
    {
        var scene = SceneManager.GetSceneByName("Lobby");
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity", OpenSceneMode.Additive);
        try
        {
            NetworkLobbyUI ui = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                ui = root.GetComponentInChildren<NetworkLobbyUI>(true);
                if (ui != null) break;
            }
            if (ui == null) throw new InvalidOperationException("Lobby terminal not found.");
            var so = new SerializedObject(ui);
            if (so.FindProperty("characterButton").objectReferenceValue == null)
            {
                var source = (Button)so.FindProperty("readyButton").objectReferenceValue;
                var canvas = source.GetComponentInParent<Canvas>();
                var button = UnityEngine.Object.Instantiate(source, canvas.transform);
                button.name = "CharacterChoiceButton";
                button.onClick = new Button.ButtonClickedEvent();
                var rect = button.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(516f, -70f);
                rect.sizeDelta = new Vector2(280f, 42f);
                rect.localScale = Vector3.one;
                var label = button.GetComponentInChildren<TMP_Text>();
                label.text = "CHARACTER: ASTRONAUT";
                label.fontSize = 18f;
                Undo.RegisterCreatedObjectUndo(button.gameObject, "Add character selection");
                so.FindProperty("characterButton").objectReferenceValue = button;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Jammo] Installed lobby character choice. Choose before Ready.");
        }
        finally
        {
            if (opened && SceneManager.GetActiveScene() != scene) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
