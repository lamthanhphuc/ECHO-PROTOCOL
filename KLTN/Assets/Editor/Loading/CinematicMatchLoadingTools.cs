using System;
using System.IO;
using EchoProtocol.Networking.Loading;
using EchoProtocol.UI.Loading;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace EchoProtocol.Editor.Loading
{
    /// <summary>Non-destructive authoring and offscreen preview tools; never opens a network session.</summary>
    public static class CinematicMatchLoadingTools
    {
        internal const string ThemePath = "Assets/Resources/UI/CinematicMatchLoadingTheme.asset";
        private const string Typography = "Assets/Resources/UI/MainMenuTypography/";

        [MenuItem("ECHO PROTOCOL/Loading Screen/Select or Create Theme")]
        public static void SelectTheme() => Selection.activeObject = EnsureTheme();

        public static CinematicMatchLoadingTheme EnsureTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<CinematicMatchLoadingTheme>(ThemePath);
            if (theme != null) return theme;
            Directory.CreateDirectory("Assets/Resources/UI");
            theme = ScriptableObject.CreateInstance<CinematicMatchLoadingTheme>();
            AssetDatabase.CreateAsset(theme, ThemePath);
            var serialized = new SerializedObject(theme);
            SetReference(serialized, "background", AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Scenes/UI/ChatGPT Image 12_38_10 11 thg 9, 2026.png"));
            SetReference(serialized, "titleFont", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Typography + "IndustrialTitleTMP.asset"));
            SetReference(serialized, "titleMaterial", AssetDatabase.LoadAssetAtPath<Material>(Typography + "TitleMetal.mat"));
            SetReference(serialized, "headingFont", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Typography + "OswaldTMP.asset"));
            SetReference(serialized, "telemetryFont", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Typography + "JetBrainsMonoTMP.asset"));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            return theme;
        }

        private static void SetReference(SerializedObject target, string name, Object value) =>
            target.FindProperty(name).objectReferenceValue = value;

        [MenuItem("ECHO PROTOCOL/Loading Screen/Render Preview - Scene Loading")]
        public static void RenderSceneLoading() => RenderBoth(MatchLoadingPhase.SceneLoading, 0.68f, false, 1, 4);

        [MenuItem("ECHO PROTOCOL/Loading Screen/Render Preview - Waiting For Players")]
        public static void RenderWaiting() => RenderBoth(MatchLoadingPhase.GameplayReadiness, 1f, true, 2, 4);

        [MenuItem("ECHO PROTOCOL/Loading Screen/Render Preview - Recovery")]
        public static void RenderRecovery() => RenderBoth(MatchLoadingPhase.Failed, 0.67f, false, 1, 4,
            "The host disconnected before the session was ready. Return to the menu to reconnect.");

        [MenuItem("ECHO PROTOCOL/Loading Screen/Render Preview - Complete")]
        public static void RenderComplete() => RenderBoth(MatchLoadingPhase.Complete, 1f, true, 4, 4);

        public static void RenderBoth(MatchLoadingPhase phase, float progress, bool localReady, int ready, int required, string error = "")
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Stop Play mode and wait for compilation before rendering a loading preview.");
            var theme = EnsureTheme();
            var scene = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null;
            Texture2D capture = null;
            var previousTarget = RenderTexture.active;
            Camera camera = null;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CinematicMatchLoadingReferenceBuilder.PrefabPath);
                var owner = prefab != null ? Object.Instantiate(prefab)
                    : new GameObject("CinematicMatchLoadingPreview", typeof(RectTransform));
                owner.name = "CinematicMatchLoadingPreview";
                SceneManager.MoveGameObjectToScene(owner, scene);
                var view = owner.GetComponent<CinematicMatchLoadingScreen>();
                if (view == null) view = owner.AddComponent<CinematicMatchLoadingScreen>();
                view.Preview(theme, phase, progress, localReady, ready, required, error);
                var cameraOwner = new GameObject("LoadingPreviewCamera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraOwner, scene);
                camera = cameraOwner.GetComponent<Camera>();
                camera.scene = scene;
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 30;
                var canvas = owner.GetComponent<Canvas>();
                owner.GetComponent<CanvasScaler>().enabled = false;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 5;
                int[] widths = { 1920, 1280 };
                int[] heights = { 1080, 960 };
                Directory.CreateDirectory("Temp");
                for (int i = 0; i < widths.Length; i++)
                {
                    target = new RenderTexture(widths[i], heights[i], 24, RenderTextureFormat.ARGB32);
                    target.Create();
                    camera.targetTexture = target;
                    canvas.scaleFactor = Mathf.Min(widths[i] / 1920f, heights[i] / 1080f);
                    // The first render establishes offscreen canvas dimensions before final layout.
                    Canvas.ForceUpdateCanvases();
                    Render(camera, target);
                    Canvas.ForceUpdateCanvases();
                    foreach (var label in owner.GetComponentsInChildren<TMP_Text>(true)) label.ForceMeshUpdate();
                    Canvas.ForceUpdateCanvases();
                    Render(camera, target);
                    RenderTexture.active = target;
                    capture = new Texture2D(widths[i], heights[i], TextureFormat.RGB24, false);
                    capture.ReadPixels(new Rect(0, 0, widths[i], heights[i]), 0, 0);
                    capture.Apply();
                    string output = $"Temp/MatchLoading-{phase}-{widths[i]}x{heights[i]}.png";
                    File.WriteAllBytes(output, capture.EncodeToPNG());
                    Debug.Log("[MatchLoadingPreview] " + Path.GetFullPath(output));
                    Object.DestroyImmediate(capture);
                    capture = null;
                    camera.targetTexture = null;
                    RenderTexture.active = previousTarget;
                    target.Release();
                    Object.DestroyImmediate(target);
                    target = null;
                }
            }
            finally
            {
                RenderTexture.active = previousTarget;
                if (camera != null) camera.targetTexture = null;
                if (capture != null) Object.DestroyImmediate(capture);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void Render(Camera camera, RenderTexture target)
        {
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
            else camera.Render();
        }
    }
}
