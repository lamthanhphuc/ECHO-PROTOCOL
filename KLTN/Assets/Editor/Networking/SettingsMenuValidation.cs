using System.IO;
using EchoProtocol.Voice;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EchoProtocol.Editor.Networking
{
    /// <summary>Runs explicit settings checks in the current Editor without changing the gameplay scene.</summary>
    [InitializeOnLoad]
    public static class SettingsMenuValidation
    {
        private const string RequestPath = "Temp/SettingsMenu.validation.request";
        static SettingsMenuValidation() => EditorApplication.update += RunRequest;

        private static void RunRequest()
        {
            if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string request = File.ReadAllText(RequestPath).Trim();
            File.Delete(RequestPath);
            try
            {
                if (request == "tests") RunTests();
                else { VoiceSettingsCanvasBuilder.Rebuild(); RenderPreview(); }
            }
            catch (System.Exception error)
            {
                File.WriteAllText("Temp/SettingsMenu.validation.error.txt", error.ToString());
                Debug.LogException(error);
            }
        }

        [MenuItem("ECHO Protocol/Tests/Settings Controls")]
        public static void RunTests()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Results(api));
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                testNames = new[] { "EchoProtocol.Tests.EditMode.Controls.GameplayInputSettingsTests", "EchoProtocol.Player.Tests.VoiceChatRulesTests" }
            }) { runSynchronously = true });
        }

        private sealed class Results : ICallbacks
        {
            private readonly TestRunnerApi _api;
            public Results(TestRunnerApi api) => _api = api;
            public void RunStarted(ITestAdaptor tests) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                TestRunnerApi.SaveResultToFile(result, "Temp/SettingsMenu.Tests.xml");
                File.WriteAllText("Temp/SettingsMenu.Tests.txt", $"Passed={result.PassCount}; Failed={result.FailCount}; Skipped={result.SkipCount}");
                _api.UnregisterCallbacks(this);
                Object.DestroyImmediate(_api);
            }
        }

        [MenuItem("ECHO Protocol/Tests/Render Settings Preview")]
        public static void RenderPreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null;
            Texture2D capture = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                var root = VoiceSettingsCanvasFactory.Create();
                SceneManager.MoveGameObjectToScene(root, scene);
                root.transform.Find("Overlay").gameObject.SetActive(true);
                var owner = new GameObject("SettingsPreviewCamera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(owner, scene);
                var camera = owner.GetComponent<Camera>();
                camera.scene = scene;
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 30f;
                var canvas = root.GetComponent<Canvas>();
                root.GetComponent<CanvasScaler>().enabled = false;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 5;
                foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
                    if (graphic.GetComponent<CanvasRenderer>() == null)
                        throw new System.InvalidOperationException("Missing CanvasRenderer: " + graphic.name);
                int[] widths = { 1920, 1280 };
                int[] heights = { 1080, 960 };
                for (int i = 0; i < widths.Length; i++)
                {
                    target = new RenderTexture(widths[i], heights[i], 24, RenderTextureFormat.ARGB32);
                    target.Create();
                    camera.targetTexture = target;
                    canvas.scaleFactor = Mathf.Min(widths[i] / 1920f, heights[i] / 1080f);
                    Canvas.ForceUpdateCanvases();
                    foreach (var label in root.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate();
                    Canvas.ForceUpdateCanvases();
                    var request = new RenderPipeline.StandardRequest { destination = target };
                    if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
                    else camera.Render();
                    RenderTexture.active = target;
                    capture = new Texture2D(widths[i], heights[i], TextureFormat.RGB24, false);
                    capture.ReadPixels(new Rect(0, 0, widths[i], heights[i]), 0, 0);
                    capture.Apply();
                    File.WriteAllBytes($"Temp/SettingsMenu-{widths[i]}x{heights[i]}.png", capture.EncodeToPNG());
                    Object.DestroyImmediate(capture);
                    capture = null;
                    camera.targetTexture = null;
                    RenderTexture.active = previous;
                    target.Release();
                    Object.DestroyImmediate(target);
                    target = null;
                }
                File.WriteAllText("Temp/SettingsMenu.preview.txt", "Rendered settings at 1920x1080 and 1280x960 in an isolated preview scene.");
            }
            finally
            {
                RenderTexture.active = previous;
                if (capture != null) Object.DestroyImmediate(capture);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
