using EchoProtocol.RelayA;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class RelayASetupBuilder
{
    private const string PrefabPath = "Assets/Prefabs/Gameplay/Imported/RelayA.prefab";
    private const string ConfigPath = "Assets/ScriptableObjects/RelayA/RelayA_Hard_Config.asset";

    [MenuItem("Tools/ECHO Protocol/Update Relay A Three-Stage UI")]
    [MenuItem("Tools/ECHO Protocol/Update Both Relay A UIs")]
    public static void UpdateBothUIs()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var ui = root.GetComponentInChildren<RelayAUIController>(true);
            if (ui == null) throw new System.InvalidOperationException("Existing Relay A UI is missing.");
            ui.RebuildRoutingLayout();
            ui.RebuildStabilizationLayout();
            ui.RebuildBreakerLayout();
            ui.BuildLayout();
            EditorUtility.SetDirty(ui);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("[RelayA] Routing, breaker matrix and stabilization UI refreshed; other objects preserved.");
    }

    [MenuItem("Tools/ECHO Protocol/Update Relay A Stabilization UI")]
    public static void UpdateStabilizationUI()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var ui = root.GetComponentInChildren<RelayAUIController>(true);
            if (ui == null) throw new System.InvalidOperationException("Existing Relay A UI is missing; no stages were rebuilt.");
            var controller = root.GetComponent<RelayAController>();
            if (controller != null && controller.Config != null)
            {
                controller.Config.InitializeDefaultCircuitScenariosIfEmpty();
                EditorUtility.SetDirty(controller.Config);
            }
            ui.RebuildStabilizationLayout();
            EditorUtility.SetDirty(ui);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[RelayA] Only stabilization UI refreshed; other stage objects preserved.");
    }

    [MenuItem("Tools/ECHO Protocol/Setup Relay A Power Routing Matrix")]
    public static void SetupRelayA()
    {
        var config = AssetDatabase.LoadAssetAtPath<RelayAConfig>(ConfigPath);
        if (config == null)
        {
            Debug.LogError($"[RelayA] Missing config: {ConfigPath}");
            return;
        }
        config.InitializeDefaultCircuitScenariosIfEmpty();
        EditorUtility.SetDirty(config);

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null) return;
        try
        {
            var controller = Ensure<RelayAController>(root);
            var interaction = Ensure<RelayAInteraction>(root);
            bool newAudio = root.GetComponent<AudioSource>() == null;
            var audio = Ensure<AudioSource>(root);
            if (newAudio)
            {
                audio.playOnAwake = false;
                audio.spatialBlend = 1f;
                audio.rolloffMode = AudioRolloffMode.Linear;
                audio.maxDistance = 16f;
            }

            var existing = root.transform.Find("RelayA_UI");
            var canvasObject = existing != null ? existing.gameObject : new GameObject("RelayA_UI", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(RelayAUIController));
            canvasObject.transform.SetParent(root.transform, false);
            var canvas = Ensure<Canvas>(canvasObject);
            var scaler = Ensure<CanvasScaler>(canvasObject);
            Ensure<GraphicRaycaster>(canvasObject);
            Ensure<CanvasGroup>(canvasObject);
            if (existing == null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 140;
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            var ui = Ensure<RelayAUIController>(canvasObject);
            var uiSerialized = new SerializedObject(ui);
            var panel = uiSerialized.FindProperty("panelRoot").objectReferenceValue as GameObject;
            if (panel == null)
            {
                panel = new GameObject("PowerRoutingPanel", typeof(RectTransform), typeof(Image));
                panel.transform.SetParent(canvasObject.transform, false);
                var rect = panel.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(1160f, 740f);
                rect.anchoredPosition = Vector2.zero;
                panel.GetComponent<Image>().color = new Color(0.015f, 0.05f, 0.065f, 0.98f);
            }
            if (controller.Config == null) Assign(controller, "config", config);
            Assign(controller, "ui", ui);
            Assign(controller, "audioSource", audio);
            Assign(interaction, "controller", controller);
            Assign(ui, "panelRoot", panel);
            Assign(ui, "canvasGroup", canvasObject.GetComponent<CanvasGroup>());
            ui.BuildLayout();
            panel.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[RelayA] Existing routing UI preserved; final stabilization panel added.");
    }

    [MenuItem("Tools/ECHO Protocol/Run Relay A EditMode Tests")]
    public static void RunRelayAEditModeTests()
    {
        var config = AssetDatabase.LoadAssetAtPath<RelayAConfig>(ConfigPath);
        if (config == null) throw new System.InvalidOperationException("Relay A config is missing.");
        config.InitializeDefaultCircuitScenariosIfEmpty();
        for (int i = 0; i < config.CircuitScenarios.Count; i++)
        {
            var board = config.GetCircuitScenario(i);
            if (!RelayACircuitBoard.HasAuthoredSolutions(board))
                throw new System.InvalidOperationException($"Relay A board has no valid reroute: {board.Name}");
        }
        Debug.Log($"[RelayA] {config.CircuitScenarios.Count} boards have initial and post-fault solutions. Run RelayACircuitTests in Test Runner for full coverage.");
    }

    private static T Ensure<T>(GameObject target) where T : Component
    {
        var component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static void Assign(Object target, string name, Object value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(name);
        if (property == null) throw new System.InvalidOperationException($"Missing {name} on {target.name}");
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
