using System;
using System.IO;
using System.Linq;
using EchoProtocol.Audio;
using EchoProtocol.Editor.Cinematics;
using EchoProtocol.UI;
using EchoProtocol.UI.Credits;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEditor.Build.Profile;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EchoProtocol.Editor.Credits
{
    [InitializeOnLoad]
    public static class CreditsSceneSetup
    {
        public const string ScenePath = "Assets/Scenes/AfterCredit.unity";
        public const string DataPath = "Assets/Resources/UI/Credits/EchoCreditsData.asset";
        private const string RequestPath = "Temp/Credits.request";
        private const string ReportPath = "Temp/Credits.report.txt";
        private const string FlowTestsPending = "EchoProtocol.Credits.FlowTestsPending";
        private static double _nextPoll;
        private static CaptureSession _capture;

        static CreditsSceneSetup()
        {
            EditorApplication.update += ProcessRequest;
            AssemblyReloadEvents.beforeAssemblyReload += () => _capture?.Dispose();
            if (SessionState.GetBool(FlowTestsPending, false)) RegisterFlowCallbacks();
        }

        private static void ProcessRequest()
        {
            if (_capture != null)
            {
                if (!EditorApplication.isCompiling && !EditorApplication.isUpdating) _capture.Tick();
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.timeSinceStartup < _nextPoll || !File.Exists(RequestPath)) return;
            _nextPoll = EditorApplication.timeSinceStartup + 0.2;
            if (!OpeningCutsceneSceneSetup.TryConsumeRequest(RequestPath, out var command)) return;
            try
            {
                if (command == "build") Build();
                else if (command == "tests") RunTests();
                else if (command == "refresh") AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                else if (command == "capture") CapturePreviews();
                else if (command == "flow-tests") RunFlowTests();
                else throw new ArgumentException("Unknown credits request: " + command);
            }
            catch (Exception error) { File.WriteAllText(ReportPath, error.ToString()); Debug.LogException(error); }
        }

        [MenuItem("ECHO Protocol/Credits/Build AfterCredit Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before building credits.");
            var data = AssetDatabase.LoadAssetAtPath<CreditsData>(DataPath);
            if (data == null || data.sections.Length != 16)
                throw new InvalidOperationException("Restore the official 16-section EchoCreditsData.asset before building AfterCredit.");
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = File.Exists(ScenePath) ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var existing = scene.GetRootGameObjects().FirstOrDefault(root => root.GetComponent<CreditsController>() != null);
                if (existing != null)
                {
                    var state = new SerializedObject(existing.GetComponent<CreditsController>());
                    var dataRef = state.FindProperty("_data");
                    bool changed = dataRef.objectReferenceValue == null;
                    if (changed)
                    {
                        dataRef.objectReferenceValue = data;
                        state.ApplyModifiedPropertiesWithoutUndo();
                    }
                    var existingProgress = existing.GetComponentsInChildren<Image>(true).FirstOrDefault(image => image.name == "SkipProgress");
                    if (existingProgress != null && existingProgress.sprite == null)
                    {
                        existingProgress.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
                        changed = true;
                    }
                    if (changed)
                    {
                        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, ScenePath);
                    }
                    EnsureBuildList();
                    AssetDatabase.SaveAssets();
                    File.WriteAllText(ReportPath, "AfterCredit already configured; preserving Inspector edits.");
                    return;
                }
                // Presentation loads additively and must not contribute cameras, lights, or an extra listener.
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var camera in root.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                    foreach (var listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
                    foreach (var light in root.GetComponentsInChildren<Light>(true)) light.enabled = false;
                }
                var canvasRoot = new GameObject("CinematicEndCredits", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                SceneManager.MoveGameObjectToScene(canvasRoot, scene);
                var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
                var scaler = canvasRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
                var rootRect = canvasRoot.GetComponent<RectTransform>();
                rootRect.sizeDelta = new Vector2(1920, 1080);
                Image("BlackBackground", rootRect, Vector2.zero, Vector2.one, new Color32(5, 6, 8, 255)).raycastTarget = true;
                var viewport = Rect("CreditsViewport", rootRect, new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.93f));
                viewport.gameObject.AddComponent<RectMask2D>();
                var content = Rect("RollingContent", viewport, new Vector2(0, 1), Vector2.one);
                content.pivot = new Vector2(0.5f, 1);
                var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.childAlignment = TextAnchor.UpperCenter; layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
                content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var group = content.gameObject.AddComponent<CanvasGroup>(); group.alpha = 0; group.blocksRaycasts = false;
                var fade = Rect("TopAndBottomEdgeFade", viewport, Vector2.zero, Vector2.one).gameObject.AddComponent<CreditsEdgeFade>();
                fade.raycastTarget = false;
                var hint = Label("HoldSpaceToSkip", rootRect, new Vector2(0.69f, 0.045f), new Vector2(0.95f, 0.08f));
                hint.text = "HOLD SPACE TO SKIP"; hint.fontSize = 23; hint.alignment = TextAlignmentOptions.MidlineRight;
                var progressTrack = Image("SkipProgressTrack", rootRect, new Vector2(0.82f, 0.028f), new Vector2(0.95f, 0.031f), new Color(0.18f, 0.18f, 0.18f));
                var progress = Image("SkipProgress", progressTrack.transform, Vector2.zero, Vector2.one, new Color(0.8f, 0.8f, 0.8f));
                progress.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
                progress.type = UnityEngine.UI.Image.Type.Filled; progress.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
                progress.fillAmount = 0; progress.raycastTarget = false;
                var audio = new GameObject("BackgroundMusic", typeof(AudioSource), typeof(AudioListener)); audio.transform.SetParent(rootRect, false);
                var music = audio.GetComponent<AudioSource>(); music.playOnAwake = false; music.spatialBlend = 0; music.loop = true;
                var fallbackListener = audio.GetComponent<AudioListener>(); fallbackListener.enabled = false;
                var catalog = Resources.Load<GameAudioCatalog>("GameAudioCatalog");
                var drone = catalog != null ? catalog.Find("horror_ambience/tension_drone_loop") : null;
                var controller = canvasRoot.AddComponent<CreditsController>();
                controller.Configure(data, UiTypographyTheme.BodyTmp, UiTypographyTheme.ButtonTmp, viewport, content,
                    group, music, fallbackListener, hint, progress, drone);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, ScenePath);
                EnsureBuildList(); AssetDatabase.SaveAssets();
                File.WriteAllText(ReportPath, "AfterCredit saved: local rolling TMP credits, verified names, scene references and build scene list.");
                Debug.Log("[Credits] AfterCredit scene configured and saved.");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void EnsureBuildList()
        {
            // scenes reflects the active profile too; also retain the scene in the global list.
            EditorBuildSettings.globalScenes = WithCredits(EditorBuildSettings.globalScenes);
            var scenes = EditorBuildSettings.scenes.ToList();
            var index = scenes.FindIndex(scene => scene.path == ScenePath);
            if (index >= 0) scenes[index] = new EditorBuildSettingsScene(ScenePath, true);
            else scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            var profile = BuildProfile.GetActiveBuildProfile();
            if (profile != null && profile.overrideGlobalScenes)
            {
                profile.scenes = WithCredits(profile.scenes); EditorUtility.SetDirty(profile);
            }
        }

        private static EditorBuildSettingsScene[] WithCredits(EditorBuildSettingsScene[] source)
        {
            var scenes = (source ?? Array.Empty<EditorBuildSettingsScene>()).ToList();
            int index = scenes.FindIndex(scene => scene.path == ScenePath);
            if (index < 0) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            else scenes[index] = new EditorBuildSettingsScene(ScenePath, true);
            return scenes.ToArray();
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform; rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
        }

        private static Image Image(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var image = Rect(name, parent, min, max).gameObject.AddComponent<Image>(); image.color = color;
            image.raycastTarget = false; return image;
        }

        private static TMP_Text Label(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var label = Rect(name, parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
            UiTypographyTheme.Apply(label); label.color = new Color32(157, 157, 170, 255); label.raycastTarget = false;
            return label;
        }

        [MenuItem("ECHO Protocol/Credits/Render Final Credits Previews")]
        public static void CapturePreviews()
        {
            if (_capture != null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            _capture = new CaptureSession();
        }

        private sealed class CaptureSession : IDisposable
        {
            private readonly Scene _scene;
            private readonly CreditsController _controller;
            private readonly Camera _camera;
            private readonly Canvas _canvas;
            private readonly System.Collections.Generic.Dictionary<Canvas, bool> _otherOverlays = new();
            private readonly (string Section, int Index, int Width, int Height)[] _shots =
            {
                ("opening", 0, 1920, 1080),
                ("development-team", 1, 1920, 1080),
                ("game-design", 3, 1920, 1080),
                ("engineering", 5, 2560, 1440),
                ("audio", 9, 3840, 2160),
                ("playtesters", 11, 2560, 1080),
                ("powered-by", 13, 1920, 1080),
                ("environment-vehicles", 14, 1920, 1080),
                ("asset-attributions", 15, 2560, 1440),
                ("legal", 16, 3840, 2160),
                ("special-thanks", 17, 1280, 720),
                ("closing", 18, 960, 540),
                ("compact-window", 5, 1024, 768)
            };
            private RenderTexture _texture;
            private AsyncGPUReadbackRequest _readback;
            private int _index, _settle;
            private bool _pending, _disposed;
            private string _error;
            private double _progressAt;

            public CaptureSession()
            {
                _scene = EditorSceneManager.OpenPreviewScene(ScenePath);
                _controller = _scene.GetRootGameObjects().Select(root => root.GetComponent<CreditsController>()).Single(value => value != null);
                var obj = new GameObject("Credits Preview Camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(obj, _scene);
                _camera = obj.GetComponent<Camera>(); _camera.enabled = false; _camera.scene = _scene;
                _camera.cameraType = CameraType.Game; _camera.transform.position = new Vector3(0, 0, -10);
                _camera.clearFlags = CameraClearFlags.SolidColor; _camera.backgroundColor = Color.black;
                _camera.orthographic = true; _camera.orthographicSize = 540;
                _camera.nearClipPlane = 0.1f; _camera.farClipPlane = 100;
                _camera.allowHDR = _camera.allowMSAA = _camera.useOcclusionCulling = false;
                _canvas = _controller.GetComponent<Canvas>(); _canvas.renderMode = RenderMode.WorldSpace;
                _canvas.worldCamera = _camera;
                var canvasRect = _canvas.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(1920, 1080); canvasRect.position = Vector3.zero;
                canvasRect.rotation = Quaternion.identity; canvasRect.localScale = Vector3.one;
                foreach (var other in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
                {
                    if (other == _canvas || other.gameObject.scene == _scene || other.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                    _otherOverlays[other] = other.enabled; other.enabled = false;
                }
                Directory.CreateDirectory("Temp/Credits");
                File.WriteAllText("Temp/Credits/preview-layout.csv", "section,width,height,designScale,viewportWidth,viewportHeight\n");
                Prepare();
            }

            private void Prepare()
            {
                var shot = _shots[_index];
                _texture = new RenderTexture(shot.Width, shot.Height, 24); _texture.Create();
                _camera.targetTexture = _texture;
                var rect = _canvas.GetComponent<RectTransform>();
                float scale = Mathf.Sqrt(shot.Width / 1920f * shot.Height / 1080f);
                rect.sizeDelta = new Vector2(shot.Width, shot.Height) / scale;
                _controller.ApplySafeArea(new Rect(0, 0, shot.Width, shot.Height), new Vector2(shot.Width, shot.Height));
                Canvas.ForceUpdateCanvases();
                _controller.PreviewSection(shot.Index);
                foreach (var label in _controller.Content.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate(true);
                Canvas.ForceUpdateCanvases();
                // Switching an imported overlay Canvas can update its root transform during layout.
                // Frame its settled rect, rather than assuming its saved screen origin is world zero.
                _camera.transform.position = rect.TransformPoint(rect.rect.center) - rect.forward * 10;
                _camera.transform.rotation = rect.rotation;
                _camera.orthographicSize = rect.rect.height * rect.lossyScale.y * 0.5f;
                _settle = 4; _progressAt = EditorApplication.timeSinceStartup;
            }

            public void Tick()
            {
                if (_disposed) return;
                if (_error != null || _index >= _shots.Length || EditorApplication.timeSinceStartup - _progressAt > 90)
                {
                    File.WriteAllText(ReportPath, _error ?? (_index >= _shots.Length
                        ? $"{_shots.Length} Final Credits previews rendered in Temp/Credits."
                        : "Credits preview timed out; scene assets preserved."));
                    Dispose(); _capture = null; return;
                }
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                if (_pending) { _readback.Update(); return; }
                if (_settle-- > 0) return;
                try
                {
                    var request = new UniversalRenderPipeline.SingleCameraRequest { destination = _texture };
                    if (!RenderPipeline.SupportsRenderRequest(_camera, request)) throw new InvalidOperationException("URP preview request unavailable.");
                    _pending = true; RenderPipeline.SubmitRenderRequest(_camera, request);
                    _readback = AsyncGPUReadback.Request(_texture, 0, TextureFormat.RGB24, result =>
                    {
                        if (_disposed) return;
                        try
                        {
                            if (result.hasError) throw new InvalidOperationException("Credits preview GPU readback failed.");
                            var raw = result.GetData<byte>(); bool visible = false;
                            for (int p = 0; p < raw.Length; p++) if (raw[p] > 32) { visible = true; break; }
                            if (!visible) throw new InvalidOperationException("Credits preview contains no visible UI; no successful visual validation recorded.");
                            var shot = _shots[_index];
                            var pixels = new Texture2D(shot.Width, shot.Height, TextureFormat.RGB24, false);
                            try
                            {
                                pixels.LoadRawTextureData(result.GetData<byte>()); pixels.Apply();
                                File.WriteAllBytes($"Temp/Credits/{shot.Section}-{shot.Width}x{shot.Height}.png", pixels.EncodeToPNG());
                            }
                            finally { UnityEngine.Object.DestroyImmediate(pixels); }
                            float scale = Mathf.Sqrt(shot.Width / 1920f * shot.Height / 1080f);
                            File.AppendAllText("Temp/Credits/preview-layout.csv",
                                FormattableString.Invariant($"{shot.Section},{shot.Width},{shot.Height},{scale:F3},{_controller.Viewport.rect.width:F2},{_controller.Viewport.rect.height:F2}\n"));
                            _camera.targetTexture = null; UnityEngine.Object.DestroyImmediate(_texture); _texture = null;
                            _index++;
                            if (_index < _shots.Length) Prepare();
                        }
                        catch (Exception error) { _error = error.ToString(); }
                        finally { _pending = false; }
                    });
                    _readback.forcePlayerLoopUpdate = true;
                }
                catch (Exception error) { _error = error.ToString(); _pending = false; }
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                if (_camera != null) _camera.targetTexture = null;
                if (_texture != null) UnityEngine.Object.DestroyImmediate(_texture);
                foreach (var pair in _otherOverlays) if (pair.Key != null) pair.Key.enabled = pair.Value;
                _otherOverlays.Clear();
                if (_scene.IsValid() && _scene.isLoaded) EditorSceneManager.ClosePreviewScene(_scene);
            }
        }

        [MenuItem("ECHO Protocol/Credits/Run Credits And Result Tests")]
        public static void RunTests()
        {
            File.WriteAllText("Temp/Credits.test-failures.txt", string.Empty);
            var api = ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new Results(api));
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
                groupNames = new[] { "CreditsTests", "MatchResultOutboxSerializationTests", "GameLanguageTests", "OpeningCutsceneRequestFileTests" }
            }) { runSynchronously = true });
        }

        [MenuItem("ECHO Protocol/Credits/Run Local Result Credits Lobby PlayMode Test")]
        public static void RunFlowTests()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.WriteAllText("Temp/Credits.flow-test-failures.txt", string.Empty);
            SessionState.SetBool(FlowTestsPending, true);
            var api = RegisterFlowCallbacks();
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode,
                groupNames = new[] { "CreditsFlowPlayModeTests" } }));
        }

        private static TestRunnerApi RegisterFlowCallbacks()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Results(api, true)); return api;
        }

        private sealed class Results : ICallbacks
        {
            private readonly TestRunnerApi _api;
            private readonly bool _flow;
            public Results(TestRunnerApi api, bool flow = false) { _api = api; _flow = flow; }
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.HasChildren && result.FailCount > 0)
                    File.AppendAllText(_flow ? "Temp/Credits.flow-test-failures.txt" : "Temp/Credits.test-failures.txt",
                        result.FullName + "\n" + result.Message + "\n");
            }
            public void RunFinished(ITestResultAdaptor result)
            {
                File.WriteAllText(_flow ? "Temp/Credits.flow-tests.xml" : "Temp/Credits.tests.xml", result.ToXml().OuterXml);
                if (_flow) SessionState.SetBool(FlowTestsPending, false);
                File.WriteAllText(ReportPath, $"Tests: {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped.");
                Debug.Log($"[Credits] Tests: {result.PassCount} passed, {result.FailCount} failed.");
                _api.UnregisterCallbacks(this); UnityEngine.Object.DestroyImmediate(_api);
            }
        }
    }
}
