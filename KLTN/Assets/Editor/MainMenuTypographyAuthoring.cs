using System;
using System.IO;
using EchoProtocol.UI.MainMenu;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Converts only the six existing Main Menu labels to authored TMP components.
/// Run explicitly with the MainMenu scene open in Edit mode. The GameObjects,
/// RectTransforms, Buttons, listeners, and other canvas content are untouched.
/// </summary>
public static class MainMenuTypographyAuthoring
{
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";
    private const string ArtFolder = "Assets/Resources/UI/MainMenuTypography";
    private const string OswaldSource = "Assets/Fonts/Oswald-Header.ttf";
    private const string MonoSource =
        "Assets/Photon/Fusion/Runtime/RuntimeAssets/JetBrainsMono-Regular.ttf";

    private const string OswaldAsset = ArtFolder + "/OswaldTMP.asset";
    private const string MonoAsset = ArtFolder + "/JetBrainsMonoTMP.asset";
    private const string TitlePreset = ArtFolder + "/TitleMetal.mat";
    private const string SubtitlePreset = ArtFolder + "/SubtitleSignal.mat";
    private const string ButtonPreset = ArtFolder + "/ButtonIndustrial.mat";

    private static readonly Color TitleColor = new Color32(216, 211, 206, 255);
    private static readonly Color SubtitleColor = new Color32(168, 68, 75, 255);
    private static readonly Color ButtonColor = new Color32(201, 198, 194, 255);

    private static readonly string[] ButtonNames =
    {
        "HostGameButton", "JoinGameButton", "OptionsButton", "LogoutButton"
    };

    private static double _nextRequestCheck;
    private static bool _reportedDirtyScene;
    private static bool _reportedRequestFailure;

    private static string RequestPath => Path.Combine(
        Path.GetDirectoryName(Application.dataPath) ?? string.Empty,
        "Temp", "MainMenuTypography.apply.request");

    [InitializeOnLoadMethod]
    private static void RegisterRequestRunner()
    {
        EditorApplication.update -= TryApplyRequest;
        EditorApplication.update += TryApplyRequest;
    }

    // A request can be placed in Temp by a local workflow. It is consumed only
    // after a successful scene save, and never while Unity is playing/importing.
    private static void TryApplyRequest()
    {
        if (EditorApplication.timeSinceStartup < _nextRequestCheck) return;
        _nextRequestCheck = EditorApplication.timeSinceStartup + 0.75d;

        string request = RequestPath;
        if (!File.Exists(request))
        {
            _reportedDirtyScene = false;
            _reportedRequestFailure = false;
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode
            || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        Scene previousActive = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = false;
        if (scene.IsValid() && scene.isLoaded && scene.isDirty)
        {
            if (!_reportedDirtyScene)
            {
                Debug.LogWarning("[MainMenuTypography] MainMenu has unsaved changes; " +
                    "request remains pending. Save or discard those changes first.");
                _reportedDirtyScene = true;
            }
            return;
        }

        try
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                openedHere = true;
            }
            ApplyScene(scene);
            File.Delete(request);
            _reportedDirtyScene = false;
            _reportedRequestFailure = false;
        }
        catch (Exception exception)
        {
            // Keep the request for a later retry or a manual inspection.
            if (!_reportedRequestFailure)
            {
                Debug.LogException(exception);
                _reportedRequestFailure = true;
            }
        }
        finally
        {
            if (openedHere && scene.IsValid() && scene.isLoaded && !scene.isDirty)
                EditorSceneManager.CloseScene(scene, true);
            if (previousActive.IsValid() && previousActive.isLoaded
                && SceneManager.GetActiveScene() != previousActive)
                SceneManager.SetActiveScene(previousActive);
        }
    }

    [MenuItem("ECHO PROTOCOL/Main Menu/Apply Cinematic Typography")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode
            || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            Debug.LogWarning("[MainMenuTypography] Run in Edit mode after import finishes.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            Debug.LogError("[MainMenuTypography] Open Assets/Scenes/MainMenu.unity first.");
            return;
        }

        ApplyScene(scene);
    }

    private static void ApplyScene(Scene scene)
    {
        GameObject canvas = FindRoot(scene, "MainMenuCanvas");
        Transform panel = canvas != null ? canvas.transform.Find("MainMenuPanel") : null;
        if (panel == null)
            throw new InvalidOperationException("MainMenuCanvas/MainMenuPanel is missing.");

        // Validate every target before touching the scene or creating assets.
        Transform title = RequireLabel(panel, "HeaderText");
        Transform subtitle = RequireLabel(panel, "SystemLabel");
        foreach (string buttonName in ButtonNames)
        {
            Transform buttonTransform = panel.Find(buttonName);
            if (buttonTransform == null || buttonTransform.GetComponent<Button>() == null)
                throw new InvalidOperationException("Main Menu Button missing: " + buttonName);
            RequireLabel(buttonTransform, "Text");
        }

        Font oswaldSource = AssetDatabase.LoadAssetAtPath<Font>(OswaldSource);
        Font monoSource = AssetDatabase.LoadAssetAtPath<Font>(MonoSource);
        if (oswaldSource == null || monoSource == null)
            throw new InvalidOperationException("Main Menu source fonts are missing.");

        EnsureAssetFolder();
        TMP_FontAsset oswald = EnsureFont(oswaldSource, OswaldAsset,
            "Oswald Main Menu", "ECHO PROTOCOL HOST GAME JOIN OPTIONS LOGOUT TẠO PHÒNG VÀO PHÒNG CÀI ĐẶT ĐĂNG XUẤT Đăng xuất");
        TMP_FontAsset mono = EnsureFont(monoSource, MonoAsset,
            "JetBrains Mono Main Menu", "THE SIGNAL IS STILL ALIVE");

        Material titleMaterial = EnsurePreset(oswald, TitlePreset,
            faceDilate: 0.035f, outlineWidth: 0.025f,
            outlineColor: new Color(0.34f, 0.035f, 0.035f, 0.23f),
            underlayColor: new Color(0.005f, 0.004f, 0.004f, 0.56f),
            underlayX: 0.05f, underlayY: -0.06f, underlaySoftness: 0.08f);
        Material subtitleMaterial = EnsurePreset(mono, SubtitlePreset,
            faceDilate: 0f, outlineWidth: 0f,
            outlineColor: Color.clear,
            underlayColor: new Color(0.16f, 0.015f, 0.015f, 0.18f),
            underlayX: 0f, underlayY: -0.1f, underlaySoftness: 0.05f);
        Material buttonMaterial = EnsurePreset(oswald, ButtonPreset,
            faceDilate: 0.015f, outlineWidth: 0.025f,
            outlineColor: new Color(0.035f, 0.025f, 0.025f, 0.48f),
            underlayColor: new Color(0.025f, 0.012f, 0.012f, 0.24f),
            underlayX: -0.08f, underlayY: -0.04f, underlaySoftness: 0.05f);

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Apply Main Menu Cinematic Typography");

        TextMeshProUGUI titleTmp = ConvertLabel(title, oswald, titleMaterial,
            Color.white, 48f, true, 0f, -15f);
        Color wornEdge = new Color32(181, 171, 166, 255);
        titleTmp.enableVertexGradient = true;
        titleTmp.colorGradient = new VertexGradient(
            TitleColor, TitleColor, wornEdge, wornEdge);
        // TMP material handles the title shadow and red registration offset.
        foreach (Shadow shadow in title.GetComponents<Shadow>())
            Undo.DestroyObjectImmediate(shadow);
        TextMeshProUGUI subtitleTmp = ConvertLabel(subtitle, mono, subtitleMaterial,
            SubtitleColor, 13f, false, 2f, 0f);

        var buttonLabels = new TextMeshProUGUI[ButtonNames.Length];
        int buttonIndex = 0;
        foreach (string buttonName in ButtonNames)
        {
            Transform buttonTransform = panel.Find(buttonName);
            buttonLabels[buttonIndex++] = ConvertLabel(RequireLabel(buttonTransform, "Text"), oswald,
                buttonMaterial, ButtonColor, 27f, true, 1f, 0f);
            if (buttonTransform.GetComponent<MainMenuButtonTextFeedback>() == null)
                Undo.AddComponent<MainMenuButtonTextFeedback>(buttonTransform.gameObject);
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Could not save MainMenu scene.");

        CheckTextBounds(titleTmp);
        CheckTextBounds(subtitleTmp);
        foreach (TextMeshProUGUI buttonLabel in buttonLabels) CheckTextBounds(buttonLabel);
        Debug.Log("[MainMenuTypography] Converted six labels to TMP and saved MainMenu.");
    }

    private static void CheckTextBounds(TextMeshProUGUI label)
    {
        label.ForceMeshUpdate();
        Vector2 preferred = label.GetPreferredValues(label.text,
            label.rectTransform.rect.width, Mathf.Infinity);
        Vector2 available = label.rectTransform.rect.size;
        Debug.Log("[MainMenuTypography] " + label.transform.parent.name + "/" +
            label.name + " preferred " + preferred.ToString("F1") +
            " in " + available.ToString("F1"));
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == name) return root;
        return null;
    }

    private static Transform RequireLabel(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child == null ||
            (child.GetComponent<Text>() == null &&
             child.GetComponent<TextMeshProUGUI>() == null))
            throw new InvalidOperationException("Main Menu label missing: " +
                parent.name + "/" + name);
        return child;
    }

    private static void EnsureAssetFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/UI"))
            AssetDatabase.CreateFolder("Assets/Resources", "UI");
        if (!AssetDatabase.IsValidFolder(ArtFolder))
            AssetDatabase.CreateFolder("Assets/Resources/UI", "MainMenuTypography");
    }

    private static TMP_FontAsset EnsureFont(Font source, string assetPath,
        string assetName, string characters)
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        bool isNew = font == null;
        if (isNew)
        {
            font = TMP_FontAsset.CreateFontAsset(source);
            if (font == null)
                throw new InvalidOperationException("Could not build TMP font: " + source.name);
            font.name = assetName;
        }

        if (!font.TryAddCharacters(characters, out string missing))
            throw new InvalidOperationException("TMP font " + assetName +
                " lacks required glyphs: " + missing);

        if (isNew)
            AssetDatabase.CreateAsset(font, assetPath);

        if (font.material != null && !AssetDatabase.Contains(font.material))
            AssetDatabase.AddObjectToAsset(font.material, font);
        foreach (Texture2D atlas in font.atlasTextures)
            if (atlas != null && !AssetDatabase.Contains(atlas))
                AssetDatabase.AddObjectToAsset(atlas, font);
        EditorUtility.SetDirty(font);
        foreach (Texture2D atlas in font.atlasTextures)
            if (atlas != null) EditorUtility.SetDirty(atlas);
        return font;
    }

    private static Material EnsurePreset(TMP_FontAsset font, string path,
        float faceDilate, float outlineWidth, Color outlineColor,
        Color underlayColor, float underlayX, float underlayY,
        float underlaySoftness)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;

        material = new Material(font.material) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
        SetFloat(material, "_FaceDilate", faceDilate);
        SetFloat(material, "_OutlineWidth", outlineWidth);
        SetColor(material, "_OutlineColor", outlineColor);
        if (outlineWidth > 0f && material.HasProperty("_OutlineWidth"))
            material.EnableKeyword("OUTLINE_ON");
        SetColor(material, "_UnderlayColor", underlayColor);
        SetFloat(material, "_UnderlayOffsetX", underlayX);
        SetFloat(material, "_UnderlayOffsetY", underlayY);
        SetFloat(material, "_UnderlayDilate", 0f);
        SetFloat(material, "_UnderlaySoftness", underlaySoftness);
        if (material.HasProperty("_UnderlayColor"))
            material.EnableKeyword("UNDERLAY_ON");
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void SetFloat(Material material, string property, float value)
    {
        if (material.HasProperty(property)) material.SetFloat(property, value);
    }

    private static void SetColor(Material material, string property, Color value)
    {
        if (material.HasProperty(property)) material.SetColor(property, value);
    }

    private static TextMeshProUGUI ConvertLabel(Transform node, TMP_FontAsset font,
        Material preset, Color color, float size, bool bold,
        float characterSpacing, float lineSpacing)
    {
        TextMeshProUGUI tmp = node.GetComponent<TextMeshProUGUI>();
        Text legacy = node.GetComponent<Text>();
        if (tmp == null)
        {
            if (legacy == null)
                throw new InvalidOperationException("No text component on " + node.name);
            string content = legacy.text;
            TextAlignmentOptions alignment = TranslateAlignment(legacy.alignment);
            bool richText = legacy.supportRichText;
            Undo.DestroyObjectImmediate(legacy);
            tmp = Undo.AddComponent<TextMeshProUGUI>(node.gameObject);
            tmp.text = content;
            tmp.alignment = alignment;
            tmp.richText = richText;
        }
        else if (legacy != null)
        {
            Undo.DestroyObjectImmediate(legacy);
        }

        Undo.RecordObject(tmp, "Style Main Menu TMP Label");
        tmp.font = font;
        tmp.fontSharedMaterial = preset;
        tmp.fontSize = size;
        tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.color = color;
        tmp.characterSpacing = characterSpacing;
        tmp.lineSpacing = lineSpacing;
        tmp.enableAutoSizing = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        tmp.margin = Vector4.zero;
        EditorUtility.SetDirty(tmp);
        return tmp;
    }

    private static TextAlignmentOptions TranslateAlignment(TextAnchor alignment)
    {
        switch (alignment)
        {
            case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft: return TextAlignmentOptions.MidlineLeft;
            case TextAnchor.MiddleCenter: return TextAlignmentOptions.Midline;
            case TextAnchor.MiddleRight: return TextAlignmentOptions.MidlineRight;
            case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
            default: return TextAlignmentOptions.MidlineLeft;
        }
    }
}
