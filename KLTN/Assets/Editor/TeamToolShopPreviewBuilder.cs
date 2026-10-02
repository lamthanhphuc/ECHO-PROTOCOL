using System;
using System.IO;
using System.Linq;
using EchoProtocol.UI.MainMenu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace EchoProtocol.Editor
{
    /// <summary>Bakes imported prefab artwork and checks the Store Terminal in an isolated scene.</summary>
    [InitializeOnLoad]
    public static class TeamToolShopPreviewBuilder
    {
        private const string RequestPath = "Temp/TeamToolShop.preview.request";
        private const string ResultPath = "Temp/TeamToolShop.preview.result.txt";

        static TeamToolShopPreviewBuilder() => EditorApplication.update += RunRequest;

        private static void RunRequest()
        {
            if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(RequestPath);
            try
            {
                BakeThumbnails();
                ValidateStore();
                File.WriteAllText(ResultPath, "PASS: four prefab thumbnails; content and Close do not overlap; 1920x1080 and 1280x720 previews rendered.");
            }
            catch (Exception error)
            {
                File.WriteAllText(ResultPath, "FAIL: " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("ECHO Protocol/Shop/Bake Prefab Thumbnails")]
        public static void BakeThumbnails()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before baking shop thumbnails.");

            foreach (var item in TeamToolShopAssets.Items)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(item.PrefabPath);
                if (prefab == null) throw new FileNotFoundException(item.PrefabPath);
                string path = "Assets/Resources/" + item.ThumbnailResource + ".png";
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                BakePrefab(prefab, path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 512;
                importer.SaveAndReimport();
            }
        }

        private static void BakePrefab(GameObject prefab, string path)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                // Copy only geometry: never run pickup, physics, audio or Fusion behaviours for a thumbnail.
                var model = CopyGeometry(prefab.transform, null);
                model.transform.position = Vector3.zero;
                SceneManager.MoveGameObjectToScene(model, scene);
                var renderers = model.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) throw new InvalidOperationException("No geometry in " + prefab.name);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);

                var camera = CreateCamera(scene);
                camera.orthographic = true;
                float radius = Mathf.Max(0.01f, bounds.extents.magnitude);
                camera.transform.position = bounds.center + new Vector3(0.7f, 0.45f, -1f).normalized * radius * 4f;
                camera.transform.LookAt(bounds.center);
                float extent = 0f;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                    var local = camera.transform.InverseTransformPoint(corner);
                    extent = Mathf.Max(extent, Mathf.Abs(local.x), Mathf.Abs(local.y));
                }
                camera.orthographicSize = extent * 1.13f;
                camera.farClipPlane = Mathf.Max(30f, radius * 10f);
                AddLight(scene, new Vector3(35f, -30f, 0f), 3.2f);
                AddLight(scene, new Vector3(325f, 140f, 0f), 2.2f);
                Capture(camera, 512, 512, path);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static GameObject CopyGeometry(Transform source, Transform parent)
        {
            var copy = new GameObject(source.name);
            copy.transform.SetParent(parent, false);
            copy.transform.localPosition = source.localPosition;
            copy.transform.localRotation = source.localRotation;
            copy.transform.localScale = source.localScale;
            var sourceRenderer = source.GetComponent<MeshRenderer>();
            var sourceFilter = source.GetComponent<MeshFilter>();
            var skin = source.GetComponent<SkinnedMeshRenderer>();
            Mesh mesh = sourceFilter != null ? sourceFilter.sharedMesh : skin != null ? skin.sharedMesh : null;
            Renderer renderer = sourceRenderer != null ? sourceRenderer : skin;
            if (mesh != null && renderer != null && renderer.enabled)
            {
                copy.AddComponent<MeshFilter>().sharedMesh = mesh;
                var target = copy.AddComponent<MeshRenderer>();
                target.sharedMaterials = renderer.sharedMaterials;
                target.shadowCastingMode = ShadowCastingMode.Off;
                target.receiveShadows = false;
            }
            foreach (Transform child in source)
                if (child.gameObject.activeSelf) CopyGeometry(child, copy.transform);
            return copy;
        }

        private static void AddLight(Scene scene, Vector3 angles, float intensity)
        {
            var owner = new GameObject("Thumbnail Light", typeof(Light));
            SceneManager.MoveGameObjectToScene(owner, scene);
            owner.transform.rotation = Quaternion.Euler(angles);
            var light = owner.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        [MenuItem("ECHO Protocol/Shop/Validate Store Preview")]
        public static void ValidateStore()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before validating the store.");
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/MainMenu.unity");
            try
            {
                var transforms = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                var shop = transforms.First(t => t.name == "ShopPopup");
                var topUp = transforms.FirstOrDefault(t => t.name == "TopUpPopup");
                if (topUp != null) topUp.gameObject.SetActive(false);
                shop.gameObject.SetActive(true);
                var presenter = shop.gameObject.AddComponent<TeamToolShopPanel>();
                presenter.PreviewLayout();
                var camera = CreateCamera(scene);
                foreach (var canvas in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Canvas>(true)))
                {
                    if (!canvas.isRootCanvas) continue;
                    var scaler = canvas.GetComponent<CanvasScaler>();
                    if (scaler != null) scaler.enabled = false;
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 5f;
                }

                int[] widths = { 1920, 1280 };
                int[] heights = { 1080, 720 };
                for (int i = 0; i < widths.Length; i++)
                {
                    foreach (var canvas in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Canvas>(true)))
                        if (canvas.isRootCanvas) canvas.scaleFactor = widths[i] / 1920f;
                    Capture(camera, widths[i], heights[i], $"Temp/StoreTerminal-{widths[i]}x{heights[i]}.png");
                    var window = shop.Find("Window");
                    var content = window.Find("TeamTools") as RectTransform;
                    var close = window.Find("CloseButton") as RectTransform;
                    if (WorldRect(content).Overlaps(WorldRect(close)))
                        throw new InvalidOperationException("Store content overlaps Close at " + widths[i]);
                    var thumbnails = content.GetComponentsInChildren<RawImage>();
                    if (thumbnails.Length != 4 || thumbnails.Any(t => t.texture == null))
                        throw new InvalidOperationException("The four imported prefab thumbnails must all be visible.");
                    foreach (var button in content.GetComponentsInChildren<Button>())
                        if (WorldRect((RectTransform)button.transform).Overlaps(WorldRect(close)))
                            throw new InvalidOperationException("A Buy button overlaps Close.");
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static Rect WorldRect(RectTransform transform)
        {
            var corners = new Vector3[4];
            transform.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static Camera CreateCamera(Scene scene)
        {
            var owner = new GameObject("Shop Preview Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(owner, scene);
            var camera = owner.GetComponent<Camera>();
            camera.scene = scene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.065f, 0.08f, 1f);
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 30f;
            return camera;
        }

        private static void Capture(Camera camera, int width, int height, string path)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                target.Create();
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
                else camera.Render();
                RenderTexture.active = target;
                image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                if (image != null) Object.DestroyImmediate(image);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }
    }
}
