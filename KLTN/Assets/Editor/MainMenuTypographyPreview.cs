using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Captures the authored Main Menu at its reference resolution in Edit mode.
/// The source scene and its camera are never changed. Temporary objects live
/// only in an isolated preview scene and the PNG is written outside Assets.
/// </summary>
public static class MainMenuTypographyPreview
{
    private const int Width = 1920;
    private const int Height = 1080;
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";
    private const string DefaultOutputPath = "Temp/MainMenuTypography.preview.png";

    [MenuItem("ECHO PROTOCOL/Main Menu/Capture Typography Preview")]
    public static void CaptureOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            Debug.LogError("[MainMenuTypography] Open Assets/Scenes/MainMenu.unity before capturing a preview.");
            return;
        }
        Capture(scene, DefaultOutputPath);
    }

    public static void Capture(Scene sourceScene, string outputPath)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Main Menu preview requires Unity Edit mode after import finishes.");
        if (!sourceScene.IsValid() || !sourceScene.isLoaded)
            throw new ArgumentException("The Main Menu scene must be loaded.", nameof(sourceScene));
        if (sourceScene.isDirty)
            throw new InvalidOperationException("Save MainMenu before capturing a preview of its authored state.");
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("A PNG output path is required.", nameof(outputPath));

        GameObject sourceCanvas = null;
        foreach (GameObject root in sourceScene.GetRootGameObjects())
        {
            if (root.name == "MainMenuCanvas")
            {
                sourceCanvas = root;
                break;
            }
        }
        if (sourceCanvas == null || sourceCanvas.GetComponent<Canvas>() == null)
            throw new InvalidOperationException("MainMenuCanvas is missing from the loaded scene.");
        Transform background = sourceCanvas.transform.Find("background");
        if (background == null || background.GetComponent<Image>()?.sprite == null)
            throw new InvalidOperationException("MainMenuCanvas/background sprite is missing; preview would be misleading.");

        Scene previewScene = EditorSceneManager.NewPreviewScene();
        RenderTexture target = null;
        Texture2D image = null;
        Camera camera = null;
        RenderTexture previousActive = RenderTexture.active;
        try
        {
            // Instantiate under a preview-scene parent so the clone never
            // appears in, or marks dirty, the user's loaded MainMenu scene.
            var holder = new GameObject("Main Menu Preview Holder");
            SceneManager.MoveGameObjectToScene(holder, previewScene);
            GameObject clone = Object.Instantiate(sourceCanvas, holder.transform, false);
            clone.name = "MainMenuCanvas (Preview)";
            clone.transform.SetParent(null, false);
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;
            clone.transform.localScale = Vector3.one;
            foreach (MonoBehaviour behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
            {
                // UI components author the pixels. Game controllers and effects
                // belong to Play mode and must not initialize for this capture.
                if (behaviour is Graphic || behaviour is CanvasScaler ||
                    behaviour is AspectRatioFitter || behaviour is LayoutGroup ||
                    behaviour is LayoutElement || behaviour is ContentSizeFitter ||
                    behaviour is Mask || behaviour is RectMask2D ||
                    behaviour is Selectable)
                    continue;
                behaviour.enabled = false;
            }

            var cameraObject = new GameObject("Main Menu Preview Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, previewScene);
            camera = cameraObject.GetComponent<Camera>();
            camera.scene = previewScene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(previewScene);
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowHDR = false;
            camera.allowMSAA = false;

            Canvas canvas = clone.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;
            canvas.updateRectTransformForStandalone = StandaloneRenderResize.Enabled;

            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;

            // The first offscreen render establishes the canvas dimensions for
            // the target texture; the second captures the updated layout.
            Canvas.ForceUpdateCanvases();
            Render(camera, target);
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text label in clone.GetComponentsInChildren<TMP_Text>())
                label.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
            Render(camera, target);

            RenderTexture.active = target;
            image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            string absolutePath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ??
                throw new InvalidOperationException("Preview output directory is missing."));
            File.WriteAllBytes(absolutePath, image.EncodeToPNG());
            Debug.Log("[MainMenuTypography] Saved Edit-mode preview: " + absolutePath);
        }
        finally
        {
            RenderTexture.active = previousActive;
            if (camera != null) camera.targetTexture = null;
            if (image != null) Object.DestroyImmediate(image);
            if (target != null)
            {
                target.Release();
                Object.DestroyImmediate(target);
            }
            if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }

    private static void Render(Camera camera, RenderTexture target)
    {
        var request = new RenderPipeline.StandardRequest { destination = target };
        if (RenderPipeline.SupportsRenderRequest(camera, request))
            RenderPipeline.SubmitRenderRequest(camera, request);
        else
            camera.Render();
    }
}
