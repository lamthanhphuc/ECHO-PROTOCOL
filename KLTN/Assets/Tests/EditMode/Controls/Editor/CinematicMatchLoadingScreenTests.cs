using System.Collections.Generic;
using EchoProtocol.Networking.Loading;
using EchoProtocol.Settings;
using EchoProtocol.UI.Loading;
using EchoProtocol.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace EchoProtocol.Tests.EditMode.Controls
{
    public sealed class CinematicMatchLoadingScreenTests
    {
        private GameObject _root;
        private CinematicMatchLoadingScreen _screen;
        private GameObject _cameraOwner;
        private Camera _camera;
        private RenderTexture _target;
        private RenderTexture _previousActiveTarget;

        [SetUp]
        public void SetUp()
        {
            Assert.That(Application.isPlaying, Is.False);
            _previousActiveTarget = RenderTexture.active;
            _root = new GameObject("MatchLoadingScreenTest", typeof(RectTransform));
            _root.hideFlags = HideFlags.HideAndDontSave;
            _screen = _root.AddComponent<CinematicMatchLoadingScreen>();
        }

        [TearDown]
        public void TearDown()
        {
            RenderTexture.active = _previousActiveTarget;
            if (_camera != null) _camera.targetTexture = null;
            if (_root != null) Object.DestroyImmediate(_root);
            if (_cameraOwner != null) Object.DestroyImmediate(_cameraOwner);
            if (_target != null)
            {
                _target.Release();
                Object.DestroyImmediate(_target);
            }
        }

        [Test]
        public void Preview_FromBareRectTransformBuildsVisibleOverlayAndBlocksFullscreenPointerInput()
        {
            Assert.That(_root.GetComponent<Canvas>(), Is.Null,
                "Start without Canvas to catch Unity's destroyed-object/null component regression.");

            Preview(MatchLoadingPhase.SceneLoading, 0.67f);
            Canvas.ForceUpdateCanvases();

            var canvas = _root.GetComponent<Canvas>();
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(canvas.sortingOrder, Is.EqualTo(short.MaxValue));
            Assert.That(_root.GetComponent<GraphicRaycaster>(), Is.Not.Null);
            var blocker = Find<Image>("FullscreenInputBlocker");
            Assert.That(blocker.gameObject.activeInHierarchy, Is.True);
            Assert.That(blocker.raycastTarget, Is.True);
            Assert.That(blocker.color.a, Is.EqualTo(1f));
            var group = blocker.GetComponent<CanvasGroup>();
            Assert.That(group.alpha, Is.EqualTo(1f));
            Assert.That(group.blocksRaycasts && group.interactable, Is.True);

            RenderForRaycasting();
            Assert.That(blocker.depth, Is.GreaterThanOrEqualTo(0),
                "The offscreen render must process the canvas before pointer hit testing.");
            Assert.That(blocker.canvasRenderer.cull, Is.False);
            // Exercise actual GraphicRaycaster hit testing rather than just inspecting its flags.
            AssertPointerHits(blocker.gameObject, new Vector2(_target.width * 0.02f, _target.height * 0.02f));
            AssertPointerHits(blocker.gameObject, new Vector2(_target.width * 0.5f, _target.height * 0.5f));
            AssertPointerHits(blocker.gameObject, new Vector2(_target.width * 0.98f, _target.height * 0.98f));
        }

        [Test]
        public void SceneLoading_PresentsMeasuredPercentageAndMatchingBloodRedBar()
        {
            Preview(MatchLoadingPhase.SceneLoading, 0.67f);

            Assert.That(Label("Percentage").text, Is.EqualTo("67%"));
            var fill = Find<Image>("FullscreenInputBlocker/ProgressTrack/MeasuredSceneProgress");
            Assert.That(fill.gameObject.activeInHierarchy, Is.True);
            Assert.That(fill.type, Is.EqualTo(Image.Type.Filled));
            Assert.That(fill.fillMethod, Is.EqualTo(Image.FillMethod.Horizontal));
            Assert.That(fill.sprite, Is.Not.Null, "A Filled Image needs a real sprite to clip its measured geometry.");
            Assert.That(fill.fillAmount, Is.EqualTo(0.67f).Within(0.0001f));
            RenderForRaycasting();
            var fillPoint = new Vector2(fill.rectTransform.rect.xMin + fill.rectTransform.rect.width * 0.2f,
                fill.rectTransform.rect.center.y);
            var red = ReadPixelAt(fill.rectTransform.TransformPoint(fillPoint));
            Assert.That(red.r, Is.GreaterThan(0.035f), "The measured bar must produce visible pixels.");
            Assert.That(red.r, Is.GreaterThan(red.g * 1.5f), "The rendered progress fill must be blood red.");
            Assert.That(red.r, Is.GreaterThan(red.b * 1.5f));
            Assert.That(Find<Image>("FullscreenInputBlocker/ProgressTrack/IndeterminateSignal")
                .gameObject.activeInHierarchy, Is.False);
        }

        [Test]
        public void UnknownSceneProgress_DoesNotInventPercentageOrMeasuredBar()
        {
            Preview(MatchLoadingPhase.SceneLoading, -1f);

            Assert.That(Label("Percentage").text, Is.Empty);
            Assert.That(Find<Image>("FullscreenInputBlocker/ProgressTrack/MeasuredSceneProgress")
                .gameObject.activeInHierarchy, Is.False);
            Assert.That(Find<Image>("FullscreenInputBlocker/ProgressTrack/IndeterminateSignal")
                .gameObject.activeInHierarchy, Is.True);
        }

        [Test]
        public void LocalSceneAndGameplayReady_ShowWaitingAndPeerCountWithoutHundredPercent()
        {
            Preview(MatchLoadingPhase.GameplayReadiness, 1f, true, 2, 4);

            Assert.That(Label("Phase").text, Is.EqualTo(GameLanguage.Choose("Đang chờ đồng đội…", "Waiting for teammates…")));
            Assert.That(Label("Detail").text, Does.Contain("2/4"));
            Assert.That(Label("Percentage").text, Is.Empty);
            foreach (var label in _root.GetComponentsInChildren<TMP_Text>(true))
                Assert.That(label.text, Does.Not.Contain("100%"));
            Assert.That(Find<Image>("FullscreenInputBlocker/ProgressTrack/MeasuredSceneProgress")
                .gameObject.activeInHierarchy, Is.False);
            Assert.That(Find<Button>("FullscreenInputBlocker/ReturnToMenu").gameObject.activeSelf,
                Is.False);
        }

        [Test]
        public void SceneOperationAtOne_StillDoesNotShowSessionCompletion()
        {
            Preview(MatchLoadingPhase.SceneLoading, 1f);

            Assert.That(Label("Percentage").text, Is.Empty);
            Assert.That(Label("Detail").text, Is.Empty);
            Assert.That(Label("Phase").text, Is.EqualTo(GameLanguage.Choose("Đang chuẩn bị nhiệm vụ…", "Preparing the mission…")));
        }

        [Test]
        public void AuthorizedCompletion_ShowsHundredPercentAndFullProgressBar()
        {
            Preview(MatchLoadingPhase.GameplayReadiness, 1f, true, 2, 4);
            // Exercise runtime easing, rather than Preview's immediate scene-progress sample.
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic;
            typeof(CinematicMatchLoadingScreen).GetField("_preview", flags).SetValue(_screen, false);
            typeof(CinematicMatchLoadingScreen).GetMethod("Present", flags).Invoke(_screen,
                new object[] { MatchLoadingPhase.Complete, "Scene_A", 1f, true, 4, 4, "", 5f });

            Assert.That(Label("Percentage").text, Is.EqualTo("100%"));
            Assert.That(Label("Phase").text, Is.EqualTo(GameLanguage.Choose("Sẵn sàng vào trận", "Ready to enter the match")));
            var fill = Find<Image>("FullscreenInputBlocker/ProgressTrack/MeasuredSceneProgress");
            Assert.That(fill.gameObject.activeInHierarchy, Is.True);
            Assert.That(fill.type, Is.EqualTo(Image.Type.Filled));
            Assert.That(fill.fillAmount, Is.EqualTo(1f));
            Assert.That(Find<Image>("FullscreenInputBlocker/ProgressTrack/IndeterminateSignal")
                .gameObject.activeInHierarchy, Is.False);
        }

        [Test]
        public void Failure_ShowsPlayerMessageAndOffersVisibleInteractableReturnTarget()
        {
            const string reason = "Host disconnected while loading the research sector.";
            _screen.Preview(null, MatchLoadingPhase.Failed, 0.67f, false, 1, 2, reason);
            Canvas.ForceUpdateCanvases();

            Assert.That(Label("Detail").text, Is.EqualTo(GameLanguage.Choose("Vui lòng quay lại và thử lại.", "Please go back and try again.")));
            Assert.That(Label("Percentage").text, Is.Empty);
            var button = Find<Button>("FullscreenInputBlocker/ReturnToMenu");
            Assert.That(button.gameObject.activeInHierarchy, Is.True);
            Assert.That(button.IsInteractable(), Is.True);
            Assert.That(button.targetGraphic, Is.Not.Null);
            Assert.That(button.targetGraphic.raycastTarget, Is.True);
            Assert.That(button.GetComponentInChildren<TMP_Text>().text,
                Is.EqualTo(GameLanguage.Choose("Về menu chính", "Return to main menu")));
            RenderForRaycasting();
            Assert.That(button.targetGraphic.depth, Is.GreaterThanOrEqualTo(0),
                "The recovery graphic must be rendered before pointer hit testing.");
            var rect = button.GetComponent<RectTransform>();
            var point = RectTransformUtility.WorldToScreenPoint(_camera, rect.TransformPoint(rect.rect.center));
            var hits = Raycast(point);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject, Is.EqualTo(button.gameObject),
                "The full-screen blocker must allow the recovery button to receive the click.");
        }

        [Test]
        public void RepeatedPhasePresentation_ReusesOneCanvasAndClearsRecoveryAfterRetry()
        {
            Preview(MatchLoadingPhase.Failed, -1f);
            int childCount = _root.GetComponentsInChildren<Transform>(true).Length;
            Preview(MatchLoadingPhase.BackendAuthorization, -1f);
            Preview(MatchLoadingPhase.SceneLoading, 0.67f);

            Assert.That(_root.GetComponentsInChildren<Canvas>(true).Length, Is.EqualTo(1));
            Assert.That(_root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(childCount));
            Assert.That(Find<Button>("FullscreenInputBlocker/ReturnToMenu").gameObject.activeSelf,
                Is.False);
            Assert.That(Label("Percentage").text, Is.EqualTo("67%"));
        }

        [Test]
        public void LoadingShowsOneStatusAndLocalizedFooter()
        {
            Preview(MatchLoadingPhase.SceneLoading, 0.67f);
            Assert.That(Label("Phase").text, Is.EqualTo(GameLanguage.Choose("Đang tải bản đồ…", "Loading the map…")));
            Assert.That(Label("Footer").text, Is.EqualTo(GameLanguage.Choose("ĐỪNG TIN NHỮNG TÍN HIỆU KỲ LẠ", "DO NOT TRUST STRANGE SIGNALS")));
            Assert.That(_root.transform.Find("FullscreenInputBlocker/PhaseChecklist"), Is.Null);
        }

        [Test]
        public void ReferenceThemeAndPrefab_IncludeSeparateArtworkFontsAndEffectMaterials()
        {
            var theme = Resources.Load<CinematicMatchLoadingTheme>(CinematicMatchLoadingTheme.ResourcePath);
            Assert.That(theme, Is.Not.Null);
            Assert.That(theme.Background, Is.Not.Null);
            Assert.That(theme.Logo, Is.Not.Null);
            Assert.That(theme.Background, Is.Not.SameAs(theme.Logo));
            Assert.That(theme.ProgressSprite, Is.Not.Null);
            Assert.That(theme.LogoMaterial, Is.Not.Null);
            Assert.That(theme.LogoMaterial.shader.name, Is.EqualTo("ECHO/Loading/LogoSignal"));
            Assert.That(theme.ProgressFillMaterial, Is.Not.Null);
            Assert.That(theme.ProgressFillMaterial.shader.name, Is.EqualTo("ECHO/Loading/BloodProgress"));
            Assert.That(theme.AtmosphereMaterial, Is.Not.Null);
            Assert.That(theme.AtmosphereMaterial.shader.name, Is.EqualTo("ECHO/Loading/Atmosphere"));
            Assert.That(theme.HeadingFont, Is.Not.Null);
            Assert.That(theme.HeadingFont.name, Does.Contain("Spectral"));
            Assert.That(theme.TelemetryFont, Is.Not.Null);
            Assert.That(theme.TelemetryFont.faceInfo.familyName, Is.EqualTo("Courier Prime"));
            var prefab = Resources.Load<GameObject>("UI/Loading/CinematicMatchLoadingScreen");
            Assert.That(prefab, Is.Not.Null, "The editable UI resource prefab must ship with the reference design.");
            Assert.That(prefab.GetComponent<CinematicMatchLoadingScreen>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<Canvas>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        }

        [Test]
        public void ResourcePrefab_ReusesSerializedBindingsAndKeepsAssetMaterialsUnchanged()
        {
            Object.DestroyImmediate(_root);
            var prefab = Resources.Load<GameObject>("UI/Loading/CinematicMatchLoadingScreen");
            Assert.That(prefab, Is.Not.Null);
            _root = Object.Instantiate(prefab);
            _root.hideFlags = HideFlags.HideAndDontSave;
            _screen = _root.GetComponent<CinematicMatchLoadingScreen>();
            int children = _root.GetComponentsInChildren<Transform>(true).Length;
            var theme = Resources.Load<CinematicMatchLoadingTheme>(CinematicMatchLoadingTheme.ResourcePath);
            float assetTime = theme.ProgressFillMaterial.GetFloat("_EffectTime");

            Preview(MatchLoadingPhase.SceneLoading, 0.68f);
            var fill = Find<Image>("FullscreenInputBlocker/ProgressTrack/MeasuredSceneProgress");
            Assert.That(fill.fillAmount, Is.EqualTo(0.68f).Within(0.0001f));
            Assert.That(fill.material, Is.Not.SameAs(theme.ProgressFillMaterial));
            Assert.That(fill.material.GetFloat("_EffectTime"), Is.EqualTo(4.5f));
            Preview(MatchLoadingPhase.GameplayReadiness, 1, true, 2, 4);

            Assert.That(_root.GetComponentsInChildren<Canvas>(true).Length, Is.EqualTo(1));
            Assert.That(_root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(children),
                "Presenting a saved prefab must reuse its authored hierarchy and bindings.");
            Assert.That(theme.ProgressFillMaterial.GetFloat("_EffectTime"), Is.EqualTo(assetTime),
                "Animation must never mutate a shared project material.");
            Assert.That(Label("Phase").text, Is.EqualTo(GameLanguage.Choose("Đang chờ đồng đội…", "Waiting for teammates…")));
        }

        private void Preview(MatchLoadingPhase phase, float progress, bool localReady = false,
            int ready = 0, int required = 2) =>
            _screen.Preview(null, phase, progress, localReady, ready, required);

        private TMP_Text Label(string name) => Find<TMP_Text>("FullscreenInputBlocker/" + name);

        private T Find<T>(string path) where T : Component
        {
            var child = _root.transform.Find(path);
            Assert.That(child, Is.Not.Null, path);
            var component = child.GetComponent<T>();
            Assert.That(component, Is.Not.Null, path);
            return component;
        }

        private void AssertPointerHits(GameObject expected, Vector2 position)
        {
            var hits = Raycast(position);
            Assert.That(hits.Exists(hit => hit.gameObject == expected), Is.True,
                "Underlying UI must be blocked at " + position);
        }

        private List<RaycastResult> Raycast(Vector2 position)
        {
            // No EventSystem is created or made current: the raycaster only needs pointer position.
            var pointer = new PointerEventData(null) { position = position };
            var hits = new List<RaycastResult>();
            _root.GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
            return hits;
        }

        private void RenderForRaycasting()
        {
            // An EditMode overlay is not processed by a game render loop. Render this test's
            // canvas offscreen to establish real Graphic depths before using GraphicRaycaster.
            // The original ScreenSpaceOverlay mode is asserted before making this fixture change.
            _cameraOwner = new GameObject("MatchLoadingRaycastTestCamera", typeof(Camera));
            _cameraOwner.hideFlags = HideFlags.HideAndDontSave;
            _camera = _cameraOwner.GetComponent<Camera>();
            _camera.enabled = false;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 30f;
            _camera.cullingMask = 1 << 31;
            foreach (var child in _root.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = 31;
            _target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _target.Create();
            _camera.targetTexture = _target;
            var canvas = _root.GetComponent<Canvas>();
            _root.GetComponent<CanvasScaler>().enabled = false;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _camera;
            canvas.planeDistance = 5f;
            canvas.scaleFactor = 1f;
            Canvas.ForceUpdateCanvases();
            RenderOffscreen();
            Canvas.ForceUpdateCanvases();
            foreach (var label in _root.GetComponentsInChildren<TMP_Text>(true)) label.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
            RenderOffscreen();
        }

        private void RenderOffscreen()
        {
            var request = new RenderPipeline.StandardRequest { destination = _target };
            if (RenderPipeline.SupportsRenderRequest(_camera, request))
                RenderPipeline.SubmitRenderRequest(_camera, request);
            else
                _camera.Render();
        }

        private Color ReadPixelAt(Vector3 worldPosition)
        {
            var point = RectTransformUtility.WorldToScreenPoint(_camera, worldPosition);
            var previous = RenderTexture.active;
            var pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = _target;
                pixel.ReadPixels(new Rect(Mathf.Floor(point.x), Mathf.Floor(point.y), 1, 1), 0, 0);
                pixel.Apply();
                return pixel.GetPixel(0, 0);
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(pixel);
            }
        }
    }
}
