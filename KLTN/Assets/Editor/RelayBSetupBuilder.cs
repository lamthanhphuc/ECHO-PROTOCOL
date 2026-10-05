using System.IO;
using EchoProtocol.Networking;
using EchoProtocol.RelayB;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static partial class RelayBSetupBuilder
{
    private const string RelayBPrefabPath = "Assets/Prefabs/Gameplay/Imported/RelayB.prefab";
    private const string ConfigPath = "Assets/ScriptableObjects/RelayB/RelayB_Hard_Config.asset";

    [MenuItem("Tools/ECHO Protocol/Setup Relay B Signal Synchronization")]
    public static void SetupRelayB()
    {
        EnsureFolder("Assets/ScriptableObjects/RelayB");
        RelayBConfig config = AssetDatabase.LoadAssetAtPath<RelayBConfig>(ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<RelayBConfig>();
            config.InitializeDefaultPresetsIfEmpty();
            AssetDatabase.CreateAsset(config, ConfigPath);
        }
        else
        {
            config.InitializeDefaultPresetsIfEmpty();
            EditorUtility.SetDirty(config);
        }

        if (!File.Exists(RelayBPrefabPath))
        {
            Debug.LogError($"[RelayBSetupBuilder] RelayB prefab not found at: {RelayBPrefabPath}");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(RelayBPrefabPath);
        try
        {
            RelayBController controller = EnsureComponent<RelayBController>(root);
            RelayBInteraction interaction = EnsureComponent<RelayBInteraction>(root);
            RelayBNetworkState networkState = EnsureComponent<RelayBNetworkState>(root);

            AudioSource audio = EnsureComponent<AudioSource>(root);
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.rolloffMode = AudioRolloffMode.Linear;
            audio.maxDistance = 16f;

            RelayBUIController ui = BuildUi(root.transform);

            SerializedObject controllerSo = new SerializedObject(controller);
            SetObject(controllerSo, "config", config);
            SetObject(controllerSo, "ui", ui);
            SetObject(controllerSo, "audioSource", audio);
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject interactionSo = new SerializedObject(interaction);
            SetObject(interactionSo, "controller", controller);
            interactionSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject networkSo = new SerializedObject(networkState);
            SetObject(networkSo, "controller", controller);
            networkSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, RelayBPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[RelayBSetupBuilder] Relay B Redesigned 3-Tab UI, Controller, and references are ready.");
    }

    [MenuItem("Tools/ECHO Protocol/Run Relay B EditMode Tests")]
    public static void RunRelayBEditModeTests()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        var api = ScriptableObject.CreateInstance<UnityEditor.TestTools.TestRunner.Api.TestRunnerApi>();
        try
        {
            api.Execute(new UnityEditor.TestTools.TestRunner.Api.ExecutionSettings(
                new UnityEditor.TestTools.TestRunner.Api.Filter
                {
                    testMode = UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode,
                    testNames = new[] { "EchoProtocol.RelayB.Tests.RelayBSimulationTests",
                        "EchoProtocol.RelayB.Tests.RelayBDecoderTests", "EchoProtocol.RelayB.Tests.RelayBDecoderUiTests",
                        "EchoProtocol.RelayB.Tests.RelayBDecoderNetworkContractTests" }
                }) { runSynchronously = true });
        }
        finally { Object.DestroyImmediate(api); }
    }


    private static Button CreateButton(Transform parent, string name, string text, Sprite sprite, Vector2 position, Vector2 size, float fontSize = 16f)
    {
        GameObject root = CreatePanel(parent, name, sprite, new Color(0.1f, 0.22f, 0.26f, 0.98f));
        SetAnchored(root, position, size, new Vector2(0f, 1f));
        Button button = root.AddComponent<Button>();
        button.targetGraphic = root.GetComponent<Image>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.75f, 1f, 0.9f, 1f);
        colors.pressedColor = new Color(0.5f, 0.8f, 0.7f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        button.colors = colors;
        CreateText(root.transform, "Label", text, fontSize, Vector2.zero, size, TextAlignmentOptions.Center);
        return button;
    }

    private static Slider CreateSlider(Transform parent, string name, Vector2 position, Vector2 size, float minVal, float maxVal, float defaultVal)
    {
        GameObject root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        SetAnchored(root, position, size, new Vector2(0f, 1f));

        // Background
        GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bg.transform.SetParent(root.transform, false);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one; bgRt.sizeDelta = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0.06f, 0.18f, 0.22f, 0.95f);

        // Fill area
        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(root.transform, false);
        var fillAreaRt = fillArea.GetComponent<RectTransform>();
        fillAreaRt.anchorMin = new Vector2(0f, 0.25f); fillAreaRt.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRt.offsetMin = new Vector2(5f, 0f); fillAreaRt.offsetMax = new Vector2(-15f, 0f);

        GameObject fillObj = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObj.transform.SetParent(fillArea.transform, false);
        var fillRt = fillObj.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one; fillRt.sizeDelta = new Vector2(10f, 0f);
        fillObj.GetComponent<Image>().color = new Color(0.35f, 0.9f, 0.65f, 1f);

        // Handle slide area
        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(root.transform, false);
        var handleAreaRt = handleArea.GetComponent<RectTransform>();
        handleAreaRt.anchorMin = Vector2.zero; handleAreaRt.anchorMax = Vector2.one;
        handleAreaRt.sizeDelta = new Vector2(-20f, 0f); handleAreaRt.anchoredPosition = Vector2.zero;

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        var handleRt = handle.GetComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(20f, 0f);
        handle.GetComponent<Image>().color = new Color(0.55f, 1f, 0.8f, 1f);

        Slider slider = root.AddComponent<Slider>();
        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = minVal; slider.maxValue = maxVal; slider.value = defaultVal;
        return slider;
    }

    private static GameObject CreatePanel(Transform parent, string name, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        return go;
    }

    private static Image CreateBar(Transform parent, string name, Vector2 position, Vector2 size, Color fillColor)
    {
        GameObject bg = CreatePanel(parent, $"{name}Background", null, new Color(0.04f, 0.12f, 0.14f, 0.95f));
        SetAnchored(bg, position, size, new Vector2(0f, 1f));

        GameObject fillObj = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObj.transform.SetParent(bg.transform, false);
        Stretch(fillObj.GetComponent<RectTransform>());

        Image fill = fillObj.GetComponent<Image>();
        fill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 0f;
        fill.color = fillColor;

        return fill;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        string content,
        float fontSize,
        Vector2 position,
        Vector2 size,
        TextAlignmentOptions alignment,
        Vector2? anchorMin = null,
        Vector2? anchorMax = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        Vector2 min = anchorMin ?? new Vector2(0f, 1f);
        Vector2 max = anchorMax ?? new Vector2(0f, 1f);
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = min;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = new Color(0.9f, 0.97f, 1f, 1f);
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    private static Sprite LoadSprite(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetAnchored(GameObject go, Vector2 position, Vector2 size, Vector2 anchor)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        SetAnchored(rt, position, size, anchor);
    }

    private static void SetAnchored(RectTransform rt, Vector2 position, Vector2 size, Vector2 anchor)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    private static T EnsureComponent<T>(GameObject root) where T : Component
    {
        T comp = root.GetComponent<T>();
        if (comp == null)
        {
            comp = root.AddComponent<T>();
        }
        return comp;
    }

    private static void SetObject(SerializedObject so, string propertyName, Object value)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null)
        {
            prop.objectReferenceValue = value;
        }
    }

    private static void SetArray<T>(SerializedObject so, string propertyName, T[] items) where T : Object
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null)
        {
            prop.arraySize = items != null ? items.Length : 0;
            for (int i = 0; i < prop.arraySize; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
        }
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            string leaf = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
