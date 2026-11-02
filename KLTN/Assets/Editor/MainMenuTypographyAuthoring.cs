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
/// Authors the six Main Menu labels and their reference artwork. The existing
/// Button objects retain their listeners; their hit areas follow the visible rows.
/// </summary>
public static class MainMenuTypographyAuthoring
{
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";
    private const string ArtFolder = "Assets/Resources/UI/MainMenuTypography";
    private const string OswaldSource = "Assets/Fonts/Oswald-Header.ttf";
    private const string DisplaySource = "Assets/Fonts/Roboto-Black.ttf";
    private const string MonoSource =
        "Assets/Photon/Fusion/Runtime/RuntimeAssets/JetBrainsMono-Regular.ttf";

    private const string OswaldAsset = ArtFolder + "/OswaldTMP.asset";
    private const string MonoAsset = ArtFolder + "/JetBrainsMonoTMP.asset";
    private const string TitlePreset = ArtFolder + "/TitleMetal.mat";
    private const string SubtitlePreset = ArtFolder + "/SubtitleSignal.mat";
    private const string ButtonPreset = ArtFolder + "/ButtonIndustrial.mat";
    private const string ScratchTexture = ArtFolder + "/TitleScratches.png";
    private const string BloodTexture = ArtFolder + "/TitleBlood.png";
    private const string TitleFaceTexture = ArtFolder + "/TitleDistressedFace.png";
    private const string ButtonFaceTexture = ArtFolder + "/ButtonWornFace.png";
    private const string FullSdfShader = "Assets/TextMesh Pro/Shaders/TMP_SDF.shader";

    private static readonly Color TitleColor = new Color32(248, 247, 244, 255);
    private static readonly Color SubtitleColor = new Color32(205, 26, 38, 255);
    private static readonly Color ButtonColor = new Color32(225, 221, 215, 255);

    private static readonly string[] ButtonNames =
    {
        "HostGameButton", "JoinGameButton", "OptionsButton", "LogoutButton"
    };
    private static readonly string[] VietnameseButtonLabels =
    {
        "TẠO PHÒNG", "VÀO PHÒNG", "CÀI ĐẶT", "ĐĂNG XUẤT"
    };

    private static double _nextRequestCheck;
    private static bool _reportedDirtyScene;
    private static bool _reportedRequestFailure;

    private static string RequestPath => Path.Combine(
        Path.GetDirectoryName(Application.dataPath) ?? string.Empty,
        "Temp", "MainMenuTypography.industrial.request");

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

        // A pending local authoring request can outlive a source edit while
        // Unity is unfocused. Import that edit before applying stale code.
        DateTime compiledAt = File.GetLastWriteTimeUtc(typeof(MainMenuTypographyAuthoring).Assembly.Location);
        foreach (string source in Directory.GetFiles(Path.Combine(Application.dataPath, "Editor"), "MainMenu*.cs"))
        {
            if (File.GetLastWriteTimeUtc(source) <= compiledAt) continue;
            AssetDatabase.Refresh();
            return;
        }

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
        RectSnapshot[] originalRects = CaptureProtectedRects(canvas.transform, panel);

        Font oswaldSource = AssetDatabase.LoadAssetAtPath<Font>(OswaldSource);
        Font displaySource = AssetDatabase.LoadAssetAtPath<Font>(DisplaySource);
        Font monoSource = AssetDatabase.LoadAssetAtPath<Font>(MonoSource);
        if (oswaldSource == null || displaySource == null || monoSource == null)
            throw new InvalidOperationException("Main Menu source fonts are missing.");

        EnsureAssetFolder();
        FaceTextures art = EnsureArtTextures();
        TMP_FontAsset oswald = EnsureFont(oswaldSource, OswaldAsset,
            "Oswald Main Menu", "ECHO PROTOCOL HOST GAME JOIN OPTIONS LOGOUT TẠO PHÒNG VÀO PHÒNG CÀI ĐẶT ĐĂNG XUẤT Đăng xuất");
        TMP_FontAsset mono = EnsureFont(monoSource, MonoAsset,
            "JetBrains Mono Main Menu", "THE SIGNAL IS STILL ALIVE 01234");
        TMP_FontAsset display = MainMenuIndustrialTitleFont.Ensure(ArtFolder);

        Material titleMaterial = EnsurePreset(display, TitlePreset,
            faceDilate: 0f, outlineWidth: 0.025f,
            outlineColor: new Color(0.34f, 0.035f, 0.035f, 0.23f),
            underlayColor: new Color(0.005f, 0.004f, 0.004f, 0.56f),
            underlayX: 0.05f, underlayY: -0.06f, underlaySoftness: 0.08f,
            faceTexture: art.title, redGlow: true);
        Material subtitleMaterial = EnsurePreset(mono, SubtitlePreset,
            faceDilate: 0f, outlineWidth: 0f,
            outlineColor: Color.clear,
            underlayColor: new Color(0.16f, 0.015f, 0.015f, 0.18f),
            underlayX: 0f, underlayY: -0.1f, underlaySoftness: 0.05f);
        Material buttonMaterial = EnsurePreset(oswald, ButtonPreset,
            faceDilate: 0.015f, outlineWidth: 0.025f,
            outlineColor: new Color(0.035f, 0.025f, 0.025f, 0.48f),
            underlayColor: new Color(0.025f, 0.012f, 0.012f, 0.24f),
            underlayX: -0.08f, underlayY: -0.04f, underlaySoftness: 0.05f,
            faceTexture: art.button);

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Apply Main Menu Cinematic Typography");

        // Restyling used to leave the original UI.Text title beside the
        // authored TMP title. Keep the preferred label and remove stale
        // direct duplicates so the wordmark is rendered exactly once.
        RemoveDuplicateDirectNamedLabels(panel, title, "HeaderText");

        TextMeshProUGUI titleTmp = ConvertLabel(title, display, titleMaterial,
            Color.white, 124f, false, 0f, -15f);
        Color wornEdge = new Color32(226, 225, 220, 255);
        titleTmp.enableVertexGradient = true;
        titleTmp.colorGradient = new VertexGradient(
            TitleColor, TitleColor, wornEdge, wornEdge);
        // One UV span covers the complete title, keeping the blood and gouges
        // in deliberate positions instead of repeating on every character.
        titleTmp.horizontalMapping = TextureMappingOptions.Paragraph;
        titleTmp.verticalMapping = TextureMappingOptions.Paragraph;
        FitReferenceTitle(titleTmp);
        if (title.GetComponent<MainMenuTitleSignalEffect>() == null)
            Undo.AddComponent<MainMenuTitleSignalEffect>(title.gameObject);
        // TMP material handles the title shadow and red registration offset.
        foreach (Shadow shadow in title.GetComponents<Shadow>())
            Undo.DestroyObjectImmediate(shadow);
        TextMeshProUGUI subtitleTmp = ConvertLabel(subtitle, mono, subtitleMaterial,
            SubtitleColor, 24f, false, 12.5f, 0f);
        PlaceTopCenter(subtitleTmp.rectTransform, 0f, 278f, 600f, 38f);
        subtitleTmp.alignment = TextAlignmentOptions.Center;
        AuthorTitleAccents(panel);

        var buttonLabels = new TextMeshProUGUI[ButtonNames.Length];
        int buttonIndex = 0;
        foreach (string buttonName in ButtonNames)
        {
            Transform buttonTransform = panel.Find(buttonName);
            PlaceTopCenter((RectTransform)buttonTransform, 0f,
                390f + buttonIndex * 100f, 420f, 76f);
            Transform labelTransform = RequireLabel(buttonTransform, "Text");
            RemoveDuplicateDirectTextLabels(buttonTransform, labelTransform);
            TextMeshProUGUI label = ConvertLabel(labelTransform, oswald,
                buttonMaterial, ButtonColor, 44f, true, 1.6f, 0f);
            label.text = VietnameseButtonLabels[buttonIndex];
            label.alignment = TextAlignmentOptions.Center;
            PlaceTopCenter(label.rectTransform, 0f, 0f, 294f, 76f);
            label.horizontalMapping = TextureMappingOptions.Paragraph;
            label.verticalMapping = TextureMappingOptions.Paragraph;
            buttonLabels[buttonIndex++] = label;
            if (buttonTransform.GetComponent<MainMenuButtonTextFeedback>() == null)
                Undo.AddComponent<MainMenuButtonTextFeedback>(buttonTransform.gameObject);
            var feedback = new SerializedObject(buttonTransform.GetComponent<MainMenuButtonTextFeedback>());
            feedback.FindProperty("indicatorColor").colorValue = new Color(0.83f, 0.025f, 0.045f, 0.88f);
            feedback.ApplyModifiedProperties();
            AuthorButtonRow(buttonTransform);
        }

        AssertRectsUnchanged(originalRects);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Could not save MainMenu scene.");

        AssertRectsUnchanged(originalRects);
        ValidateAppliedArt(titleTmp, buttonLabels, display, oswald,
            titleMaterial, buttonMaterial, art);

        CheckTextBounds(titleTmp);
        CheckTextBounds(subtitleTmp);
        foreach (TextMeshProUGUI buttonLabel in buttonLabels) CheckTextBounds(buttonLabel);
        Debug.Log("[MainMenuTypography] Verified title blood/scratch face texture, " +
            "full SDF materials, Vietnamese glyphs, six TMP assignments, " +
            "reference-sized title and four aligned button hit areas; saved MainMenu.");
        MainMenuTypographyPreview.Capture(scene, Path.Combine(
            Path.GetDirectoryName(Application.dataPath) ?? string.Empty,
            "Temp", "MainMenuTypography-preview.png"));
    }

    private static void FitReferenceTitle(TextMeshProUGUI title)
    {
        RectTransform rect = title.rectTransform;
        PlaceTopCenter(rect, 0f, 26f, 660f, 270f);
        rect.localScale = Vector3.one;
        title.alignment = TextAlignmentOptions.Center;
        title.ForceMeshUpdate(true, true);
        Bounds firstLine = GlyphBounds(title, 0);
        if (firstLine.size.y <= 0f)
            throw new InvalidOperationException("Title font produced no visible glyphs.");
        title.fontSize *= 88f / firstLine.size.y;
        title.ForceMeshUpdate(true, true);
        if (title.textInfo.lineCount != 2)
            throw new InvalidOperationException("The title must remain exactly two lines.");
        float advance = title.textInfo.lineInfo[0].baseline - title.textInfo.lineInfo[1].baseline;
        float fontScale = title.fontSize * 0.01f;
        title.lineSpacing += (118f - advance) / Mathf.Max(0.01f, fontScale);
        title.ForceMeshUpdate(true, true);
        Bounds all = GlyphBounds(title, -1);
        float horizontalScale = 570f / all.size.x;
        rect.localScale = new Vector3(horizontalScale, 1f, 1f);
        // Keep the visible two-line wordmark centered on the shared menu axis.
        rect.anchoredPosition = new Vector2(0f, -46f - all.max.y);
        Debug.Log("[MainMenuTypography] Reference title glyph bounds " +
            (all.size.x * horizontalScale).ToString("F1") + " x " +
            all.size.y.ToString("F1") + "; font " + title.fontSize.ToString("F1"));
    }

    private static Bounds GlyphBounds(TMP_Text label, int line)
    {
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, 0f);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, 0f);
        bool found = false;
        for (int i = 0; i < label.textInfo.characterCount; i++)
        {
            TMP_CharacterInfo character = label.textInfo.characterInfo[i];
            if (!character.isVisible || (line >= 0 && character.lineNumber != line)) continue;
            var metrics = character.textElement.glyph.metrics;
            float left = character.origin + metrics.horizontalBearingX * character.scale;
            float top = character.baseLine + metrics.horizontalBearingY * character.scale;
            // TMP quads include SDF padding. Fit the actual glyph metrics so
            // changing shader glow does not change the measured title size.
            min = Vector3.Min(min, new Vector3(left, top - metrics.height * character.scale, 0f));
            max = Vector3.Max(max, new Vector3(left + metrics.width * character.scale, top, 0f));
            found = true;
        }
        return found ? new Bounds((min + max) * 0.5f, max - min) : new Bounds();
    }

    private static void PlaceTopLeft(RectTransform rect, float x, float y,
        float width, float height)
    {
        Undo.RecordObject(rect, "Fit Main Menu reference");
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.localScale = Vector3.one;
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        EditorUtility.SetDirty(rect);
    }

    private static void PlaceTopCenter(RectTransform rect, float x, float y,
        float width, float height)
    {
        Undo.RecordObject(rect, "Fit centered Main Menu reference");
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.localScale = Vector3.one;
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        EditorUtility.SetDirty(rect);
    }

    private static void AuthorTitleAccents(Transform panel)
    {
        Transform accents = panel.Find("TitleSignalAccents");
        if (accents == null)
        {
            var go = new GameObject("TitleSignalAccents", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(MainMenuTitleAccentGraphic));
            go.layer = panel.gameObject.layer;
            go.transform.SetParent(panel, false);
            Undo.RegisterCreatedObjectUndo(go, "Create title signal artwork");
            accents = go.transform;
        }
        PlaceTopCenter((RectTransform)accents, 0f, 42f, 642f, 216f);
        accents.GetComponent<MainMenuTitleAccentGraphic>().Configure(
            new Color(0.83f, 0.035f, 0.045f, 1f), 1.25f);
        accents.SetSiblingIndex(panel.Find("HeaderText").GetSiblingIndex() + 1);
    }

    private static Image RowImage(Transform parent, string name, float x,
        float y, float width, float height, Color color)
    {
        Transform node = parent.Find(name);
        if (node == null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Create menu row artwork");
            node = go.transform;
        }
        Image image = node.GetComponent<Image>();
        PlaceTopLeft(image.rectTransform, x, y, width, height);
        image.color = color;
        image.raycastTarget = false;
        EditorUtility.SetDirty(image);
        return image;
    }

    private static void AuthorButtonRow(Transform button)
    {
        Image surface = RowImage(button, "IndustrialRowSurface", 0f, 0f, 420f, 76f,
            new Color(0.006f, 0.008f, 0.01f, 0.62f));
        surface.transform.SetAsFirstSibling();
        RowImage(button, "RowTopRule", 0f, 0f, 420f, 1f,
            new Color(0.54f, 0.50f, 0.46f, 0.20f));
        RowImage(button, "RowBottomRule", 0f, 75f, 420f, 1f,
            new Color(0.54f, 0.50f, 0.46f, 0.26f));
        RowImage(button, "IndicatorBloom", 33f, 15f, 12f, 46f,
            new Color(0.55f, 0.015f, 0.025f, 0.15f));
        RowImage(button, "HoverSignalIndicator", 38f, 17f, 3f, 42f,
            new Color(0.83f, 0.025f, 0.045f, 0.88f));
        RowImage(button, "IndicatorCore", 39f, 20f, 1f, 36f,
            new Color(1f, 0.29f, 0.24f, 0.82f));
        RowImage(button, "RightIndicatorBloom", 375f, 15f, 12f, 46f,
            new Color(0.55f, 0.015f, 0.025f, 0.15f));
        RowImage(button, "RightHoverSignalIndicator", 379f, 17f, 3f, 42f,
            new Color(0.83f, 0.025f, 0.045f, 0.88f));
        RowImage(button, "RightIndicatorCore", 380f, 20f, 1f, 36f,
            new Color(1f, 0.29f, 0.24f, 0.82f));
        Transform number = button.Find("RowNumber");
        if (number != null)
            Undo.DestroyObjectImmediate(number.gameObject);
    }

    private struct RectSnapshot
    {
        private readonly RectTransform _rect;
        private readonly Vector2 _anchorMin;
        private readonly Vector2 _anchorMax;
        private readonly Vector2 _pivot;
        private readonly Vector2 _anchoredPosition;
        private readonly Vector2 _sizeDelta;
        private readonly Vector3 _localScale;
        private readonly Quaternion _localRotation;

        public RectSnapshot(Transform node)
        {
            _rect = node as RectTransform;
            if (_rect == null)
                throw new InvalidOperationException("Main Menu RectTransform missing: " + node.name);
            _anchorMin = _rect.anchorMin;
            _anchorMax = _rect.anchorMax;
            _pivot = _rect.pivot;
            _anchoredPosition = _rect.anchoredPosition;
            _sizeDelta = _rect.sizeDelta;
            _localScale = _rect.localScale;
            _localRotation = _rect.localRotation;
        }

        public void AssertUnchanged()
        {
            if (_rect == null || _rect.anchorMin != _anchorMin ||
                _rect.anchorMax != _anchorMax || _rect.pivot != _pivot ||
                _rect.anchoredPosition != _anchoredPosition ||
                _rect.sizeDelta != _sizeDelta || _rect.localScale != _localScale ||
                _rect.localRotation != _localRotation)
                throw new InvalidOperationException(
                    "Main Menu RectTransform changed during typography authoring: " +
                    (_rect != null ? _rect.name : "destroyed object"));
        }
    }

    private static RectSnapshot[] CaptureProtectedRects(Transform canvas, Transform panel)
    {
        var snapshots = new System.Collections.Generic.List<RectSnapshot>();
        foreach (RectTransform rect in canvas.GetComponentsInChildren<RectTransform>(true))
            if (rect == panel || !rect.IsChildOf(panel)) snapshots.Add(new RectSnapshot(rect));
        return snapshots.ToArray();
    }

    private static void AssertRectsUnchanged(RectSnapshot[] snapshots)
    {
        foreach (RectSnapshot snapshot in snapshots) snapshot.AssertUnchanged();
    }

    private static void ValidateAppliedArt(TextMeshProUGUI title,
        TextMeshProUGUI[] buttons, TMP_FontAsset display, TMP_FontAsset oswald,
        Material titleMaterial, Material buttonMaterial, FaceTextures art)
    {
        if (title.font != display || title.fontSharedMaterial != titleMaterial ||
            titleMaterial.shader != AssetDatabase.LoadAssetAtPath<Shader>(FullSdfShader) ||
            titleMaterial.GetTexture("_FaceTex") != art.title ||
            titleMaterial.GetTexture("_MainTex") != display.material.GetTexture("_MainTex") ||
            title.horizontalMapping != TextureMappingOptions.Paragraph ||
            title.verticalMapping != TextureMappingOptions.Paragraph ||
            title.GetComponent<MainMenuTitleSignalEffect>() == null)
            throw new InvalidOperationException("Authored title font, texture, shader, mapping, or signal component is missing.");

        if (buttonMaterial.shader != titleMaterial.shader ||
            buttonMaterial.GetTexture("_FaceTex") != art.button)
            throw new InvalidOperationException("Button distressed face material is missing.");
        for (int i = 0; i < buttons.Length; i++)
        {
            TextMeshProUGUI button = buttons[i];
            if (button.font != oswald || button.fontSharedMaterial != buttonMaterial ||
                button.text != VietnameseButtonLabels[i] ||
                button.horizontalMapping != TextureMappingOptions.Paragraph ||
                button.verticalMapping != TextureMappingOptions.Paragraph)
                throw new InvalidOperationException("Authored button TMP is missing its font or material: " +
                    button.transform.parent.name);
        }

        foreach (string path in new[] { ScratchTexture, BloodTexture })
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null ||
                !(AssetImporter.GetAtPath(path) is TextureImporter importer) ||
                !importer.alphaIsTransparency)
                throw new InvalidOperationException("Transparent source art is missing: " + path);
        }
        if (!oswald.HasCharacters("TẠO PHÒNG VÀO PHÒNG CÀI ĐẶT ĐĂNG XUẤT",
                out uint[] missingUnicodes))
            throw new InvalidOperationException("Vietnamese menu glyphs are missing: " +
                string.Join(", ", missingUnicodes));
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

    private static void RemoveDuplicateDirectTextLabels(
        Transform button,
        Transform preferred)
    {
        for (int i = button.childCount - 1; i >= 0; i--)
        {
            Transform child = button.GetChild(i);
            if (child == preferred || child.name != "Text")
                continue;

            if (child.GetComponent<Text>() != null ||
                child.GetComponent<TMP_Text>() != null)
                Undo.DestroyObjectImmediate(child.gameObject);
        }
    }

    private static void RemoveDuplicateDirectNamedLabels(
        Transform parent,
        Transform preferred,
        string name)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child == preferred || child.name != name)
                continue;

            if (child.GetComponent<Text>() != null ||
                child.GetComponent<TMP_Text>() != null)
                Undo.DestroyObjectImmediate(child.gameObject);
        }
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

    private struct FaceTextures
    {
        public Texture2D title;
        public Texture2D button;
    }

    // The two transparent layers are retained as editable source art. The
    // mostly opaque composites are sampled by TMP's _FaceTex and clipped to glyphs by
    // its SDF; this needs no image object that could cover the menu background.
    private static FaceTextures EnsureArtTextures()
    {
        const int width = 1024;
        const int height = 256;
        var scratches = new Color32[width * height];
        var blood = new Color32[width * height];
        var random = new System.Random(0xEC4016);

        // Thin broken gouges, short scoring lines, and chipped pockets are
        // sized to remain visible after the texture shrinks into a UI title.
        for (int i = 0; i < 90; i++)
        {
            float x = random.Next(width);
            float y = random.Next(height);
            float length = 6f + random.Next(55);
            float slope = -13f + random.Next(27);
            PaintStroke(scratches, width, height, x, y, x + length, y + slope,
                0.55f + (float)random.NextDouble() * 1.15f,
                new Color32(9, 8, 9, (byte)random.Next(55, 145)));
        }
        PaintStroke(scratches, width, height, 156, 22, 221, 125, 2.1f,
            new Color32(7, 6, 7, 222));
        PaintStroke(scratches, width, height, 489, 201, 548, 115, 2.4f,
            new Color32(7, 6, 7, 212));
        PaintStroke(scratches, width, height, 790, 52, 843, 160, 1.9f,
            new Color32(7, 6, 7, 216));
        for (int i = 0; i < 700; i++)
        {
            float x = random.Next(width);
            float y = random.Next(height);
            float rx = 0.4f + (float)random.NextDouble() * 1.9f;
            float ry = 0.3f + (float)random.NextDouble() * 1.3f;
            PaintOval(scratches, width, height, x, y, rx, ry,
                new Color32(10, 8, 9, (byte)random.Next(45, 160)));
        }
        for (int i = 0; i < 22; i++)
        {
            float x = random.Next(width);
            float y = random.Next(height);
            PaintOval(scratches, width, height, x, y, 5f + random.Next(9),
                1.5f + random.Next(3), new Color32(5, 4, 4, 155));
        }

        PaintBloodCluster(blood, width, height, 320, 180, 42, random);
        PaintBloodCluster(blood, width, height, 710, 65, 28, random);
        PaintBloodCluster(blood, width, height, 480, 180, 30, random);
        PaintBloodCluster(blood, width, height, 940, 75, 23, random);

        var titleFace = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            float v = y / (float)(height - 1);
            float brushedBand = Mathf.Sin(v * 24f) * 2f + Mathf.Sin(v * 77f);
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                float grain = (Hash01(x, y) - 0.5f) * 20f;
                float weathering = Mathf.Clamp01((SmoothNoise(x * 0.055f, y * 0.075f) - 0.46f) * 2.8f);
                float pitting = Hash01(x + 4721, y + 13) > 0.968f ? 40f : 0f;
                float steel = 238f + 12f * v + brushedBand + grain - weathering * 45f - pitting;
                float gouge = scratches[index].a / 255f;
                // Only overlapping deep gouges expose the background through
                // the glyph. Most of the metal face stays fully opaque.
                steel *= 1f - gouge * 0.76f;
                float stain = blood[index].a / 255f * 0.86f;
                byte erodedAlpha = ToByte(255f -
                    Mathf.Pow(Mathf.Clamp01((gouge - 0.38f) / 0.62f), 1.2f) * 105f);
                titleFace[index] = new Color32(
                    ToByte(Mathf.Lerp(steel + 1f, blood[index].r, stain)),
                    ToByte(Mathf.Lerp(steel + 1f, blood[index].g, stain)),
                    ToByte(Mathf.Lerp(steel, blood[index].b, stain)), erodedAlpha);
            }
        }

        const int buttonWidth = 512;
        const int buttonHeight = 128;
        var buttonFace = new Color32[buttonWidth * buttonHeight];
        for (int y = 0; y < buttonHeight; y++)
        {
            for (int x = 0; x < buttonWidth; x++)
            {
                float grain = (Hash01(x + 1807, y + 911) - 0.5f) * 9f;
                float scratch = scratches[(y * 2) * width + x * 2].a / 255f;
                float pit = Hash01(x + 20, y + 400) > 0.98f ? 38f : 0f;
                byte steel = ToByte(247f + grain - scratch * 75f - pit);
                buttonFace[y * buttonWidth + x] =
                    new Color32(steel, steel, steel, 255);
            }
        }

        SaveTexture(ScratchTexture, scratches, width, height, true);
        SaveTexture(BloodTexture, blood, width, height, true);
        return new FaceTextures
        {
            title = SaveTexture(TitleFaceTexture, titleFace, width, height, false),
            button = SaveTexture(ButtonFaceTexture, buttonFace,
                buttonWidth, buttonHeight, false)
        };
    }

    private static void PaintBloodCluster(Color32[] pixels, int width, int height,
        float x, float y, float radius, System.Random random)
    {
        PaintOval(pixels, width, height, x, y, radius, 9f,
            new Color32(92, 20, 25, 180));
        PaintStroke(pixels, width, height, x - radius * 0.6f, y + 4f,
            x + radius * 0.72f, y - 4f, 2.5f,
            new Color32(55, 11, 16, 174));
        for (int i = 0; i < 48; i++)
        {
            float dx = ((float)random.NextDouble() * 2f - 1f) * radius * 1.7f;
            float dy = ((float)random.NextDouble() * 2f - 1f) * 27f;
            float spot = 1f + (float)random.NextDouble() * 5.4f;
            PaintOval(pixels, width, height, x + dx, y + dy,
                spot, spot * (0.35f + (float)random.NextDouble() * 0.8f),
                new Color32(94, 22, 28, (byte)random.Next(90, 205)));
        }
        for (int i = 0; i < 3; i++)
        {
            float dripX = x + (float)(random.NextDouble() * 2.0 - 1.0) * radius;
            float dripLength = 18f + random.Next(49);
            PaintStroke(pixels, width, height, dripX, y - 2f,
                dripX + 2f, y - dripLength, 1.1f + i * 0.3f,
                new Color32(74, 14, 19, 135));
            PaintOval(pixels, width, height, dripX + 2f, y - dripLength,
                2.2f + i * 0.5f, 3.5f + i * 0.3f,
                new Color32(65, 11, 16, 155));
        }
    }

    private static void PaintStroke(Color32[] pixels, int width, int height,
        float x0, float y0, float x1, float y1, float radius, Color32 color)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(
            Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) /
            Mathf.Max(0.8f, radius * 0.7f)));
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            PaintOval(pixels, width, height, Mathf.Lerp(x0, x1, t),
                Mathf.Lerp(y0, y1, t), radius, radius * 0.7f, color);
        }
    }

    private static void PaintOval(Color32[] pixels, int width, int height,
        float cx, float cy, float rx, float ry, Color32 color)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(cx - rx - 1f));
        int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(cx + rx + 1f));
        int minY = Mathf.Max(0, Mathf.FloorToInt(cy - ry - 1f));
        int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(cy + ry + 1f));
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = (x - cx) / rx;
                float dy = (y - cy) / ry;
                float coverage = Mathf.Clamp01((1.12f - dx * dx - dy * dy) * 3f);
                if (coverage <= 0f) continue;
                byte alpha = ToByte(color.a * coverage);
                if (alpha == 0) continue;
                int index = y * width + x;
                Color32 source = new Color32(color.r, color.g, color.b, alpha);
                pixels[index] = BlendOver(pixels[index], source);
            }
        }
    }

    private static Color32 BlendOver(Color32 destination, Color32 source)
    {
        float sourceAlpha = source.a / 255f;
        float destinationAlpha = destination.a / 255f;
        float alpha = sourceAlpha + destinationAlpha * (1f - sourceAlpha);
        if (alpha <= 0f) return new Color32(0, 0, 0, 0);
        float oldWeight = destinationAlpha * (1f - sourceAlpha);
        return new Color32(
            ToByte((source.r * sourceAlpha + destination.r * oldWeight) / alpha),
            ToByte((source.g * sourceAlpha + destination.g * oldWeight) / alpha),
            ToByte((source.b * sourceAlpha + destination.b * oldWeight) / alpha),
            ToByte(alpha * 255f));
    }

    private static float Hash01(int x, int y)
    {
        unchecked
        {
            uint hash = (uint)(x * 374761393 + y * 668265263);
            hash = (hash ^ (hash >> 13)) * 1274126177u;
            return (hash ^ (hash >> 16)) / (float)uint.MaxValue;
        }
    }

    private static float SmoothNoise(float x, float y)
    {
        int ix = Mathf.FloorToInt(x);
        int iy = Mathf.FloorToInt(y);
        float tx = Mathf.SmoothStep(0f, 1f, x - ix);
        float ty = Mathf.SmoothStep(0f, 1f, y - iy);
        return Mathf.Lerp(Mathf.Lerp(Hash01(ix, iy), Hash01(ix + 1, iy), tx),
            Mathf.Lerp(Hash01(ix, iy + 1), Hash01(ix + 1, iy + 1), tx), ty);
    }

    private static byte ToByte(float value)
    {
        return (byte)Mathf.RoundToInt(Mathf.Clamp(value, 0f, 255f));
    }

    private static Texture2D SaveTexture(string assetPath, Color32[] pixels,
        int width, int height, bool transparent)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        byte[] png = texture.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(texture);

        string absolutePath = Path.Combine(Application.dataPath,
            assetPath.Substring("Assets/".Length));
        bool changed = !File.Exists(absolutePath) || !SameBytes(File.ReadAllBytes(absolutePath), png);
        if (changed)
        {
            File.WriteAllBytes(absolutePath, png);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("Could not import title art: " + assetPath);
        if (importer.textureType != TextureImporterType.Default ||
            importer.alphaIsTransparency != transparent ||
            importer.mipmapEnabled ||
            importer.textureCompression != TextureImporterCompression.Uncompressed ||
            importer.wrapMode != TextureWrapMode.Clamp ||
            importer.filterMode != FilterMode.Bilinear ||
            !importer.sRGBTexture)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = transparent;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
        }
        Texture2D imported = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (imported == null)
            throw new InvalidOperationException("Title art texture is unavailable: " + assetPath);
        return imported;
    }

    private static bool SameBytes(byte[] left, byte[] right)
    {
        if (left.Length != right.Length) return false;
        for (int i = 0; i < left.Length; i++)
            if (left[i] != right[i]) return false;
        return true;
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

        // TMP's TryAddCharacters can return false when every requested glyph
        // is already cached (nothing new to add). Query the final coverage.
        font.TryAddCharacters(characters, out _);
        if (!font.HasCharacters(characters, out uint[] missingUnicodes))
            throw new InvalidOperationException("TMP font " + assetName +
                " lacks required glyphs: " + string.Join(", ", missingUnicodes));

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
        float underlaySoftness, Texture2D faceTexture = null,
        bool redGlow = false)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = material == null;
        if (isNew)
            material = new Material(font.material) { name = Path.GetFileNameWithoutExtension(path) };
        else
            Undo.RecordObject(material, "Update Main Menu TMP Material");

        if (faceTexture != null)
        {
            Shader fullSdf = AssetDatabase.LoadAssetAtPath<Shader>(FullSdfShader);
            if (fullSdf == null)
                throw new InvalidOperationException("The full TMP distance-field shader is missing.");
            material.shader = fullSdf;
            if (!material.HasProperty("_FaceTex"))
                throw new InvalidOperationException("TMP shader has no _FaceTex slot: " + fullSdf.name);
            material.SetTexture("_FaceTex", faceTexture);
            SetColor(material, "_FaceColor", Color.white);
        }

        // Switching from TMP mobile to full SDF must retain the font atlas.
        Texture atlas = font.material != null ? font.material.GetTexture("_MainTex") : null;
        if (atlas == null)
            throw new InvalidOperationException("TMP font atlas is missing: " + font.name);
        material.SetTexture("_MainTex", atlas);
        SetFloat(material, "_TextureWidth", atlas.width);
        SetFloat(material, "_TextureHeight", atlas.height);
        if (font.material.HasProperty("_GradientScale"))
            SetFloat(material, "_GradientScale", font.material.GetFloat("_GradientScale"));
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
        if (redGlow)
        {
            SetColor(material, "_GlowColor", new Color(0.34f, 0.018f, 0.024f, 0.18f));
            SetFloat(material, "_GlowOffset", 0.015f);
            SetFloat(material, "_GlowInner", 0.025f);
            SetFloat(material, "_GlowOuter", 0.06f);
            SetFloat(material, "_GlowPower", 0.62f);
            material.EnableKeyword("GLOW_ON");
        }
        else
            material.DisableKeyword("GLOW_ON");
        if (isNew) AssetDatabase.CreateAsset(material, path);
        EditorUtility.SetDirty(material);
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
