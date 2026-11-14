using System;
using System.IO;
using EchoProtocol.UI.MainMenu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace EchoProtocol.Editor
{
    public static class CharacterShopPreviewBuilder
    {
        [MenuItem("ECHO Protocol/Shop/Bake Character Thumbnails")]
        public static void BakeCharacterThumbnails() => BakeCharacterThumbnails(null);

        [MenuItem("ECHO Protocol/Shop/Bake Cursed Priest Thumbnail")]
        public static void BakeCursedPriestThumbnail() => BakeCharacterThumbnails(2);

        private static void BakeCharacterThumbnails(int? characterId)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Exit Play mode before baking character thumbnails.");
            }

            foreach (var item in CharacterShopAssets.Items)
            {
                if (characterId.HasValue && item.CharacterId != characterId.Value) continue;

                var prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        item.PrefabPath);

                if (prefab == null)
                {
                    throw new FileNotFoundException(
                        item.PrefabPath);
                }

                string path =
                    "Assets/Resources/" +
                    item.ThumbnailResource +
                    ".png";

                Directory.CreateDirectory(
                    Path.GetDirectoryName(path));

                BakePrefab(
                    prefab,
                    path);

                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceSynchronousImport);

                var importer =
                    AssetImporter.GetAtPath(path)
                    as TextureImporter;

                if (importer == null)
                {
                    throw new InvalidOperationException(
                        "Texture importer missing: " +
                        path);
                }

                importer.textureType =
                    TextureImporterType.Default;

                importer.mipmapEnabled =
                    false;

                importer.wrapMode =
                    TextureWrapMode.Clamp;

                importer.textureCompression =
                    TextureImporterCompression.Uncompressed;

                importer.maxTextureSize =
                    512;

                importer.SaveAndReimport();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[ECHO] Character shop thumbnails PASS.");
        }

        public static void BakeThumbnailsBatch()
        {
            BakeCharacterThumbnails();
        }

        private static void BakePrefab(
            GameObject prefab,
            string path)
        {
            var scene =
                EditorSceneManager.NewPreviewScene();

            RenderTexture target =
                null;

            Texture2D image =
                null;

            try
            {
                var model =
                    PrefabUtility.InstantiatePrefab(
                        prefab,
                        scene)
                    as GameObject;

                if (model == null)
                {
                    throw new InvalidOperationException(
                        "Could not instantiate " +
                        prefab.name);
                }

                model.transform.position =
                    Vector3.zero;

                if (AssetDatabase.GetAssetPath(prefab).EndsWith("/PF_CursedPriestVisual.prefab", StringComparison.Ordinal) && model.GetComponent<Animator>() is Animator animator
                    && animator.runtimeAnimatorController != null)
                {
                    animator.applyRootMotion = false;
                    var clips = Resources.LoadAll<AnimationClip>("Characters/Sad Idle1");
                    if (clips.Length == 0) throw new InvalidOperationException("Missing Sad Idle 1.");
                    clips[0].SampleAnimation(model, clips[0].length * 0.35f);

                }

                if (AssetDatabase.GetAssetPath(prefab).EndsWith("/PF_JammoVisual.prefab", StringComparison.Ordinal)
                    && model.GetComponent<Animator>() is Animator jammo)
                {
                    jammo.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Characters/AOC_JammoPlayer");
                    jammo.applyRootMotion = false;
                    var idle = Resources.Load<AnimationClip>("Characters/a_Idle_Jammo_Humanoid");
                    if (idle != null) idle.SampleAnimation(model, idle.length * 0.25f);

                }

                model.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

                // Character shop portrait must not render
                // first-person arms/meshes.
                foreach (var renderer in
                         model.GetComponentsInChildren<
                             Renderer>(true))
                {
                    if (renderer.name.IndexOf(
                            "FirstPerson",
                            StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        renderer.enabled =
                            false;
                    }
                }

                foreach (var collider in
                         model.GetComponentsInChildren<
                             Collider>(true))
                {
                    collider.enabled =
                        false;
                }

                foreach (var body in
                         model.GetComponentsInChildren<
                             Rigidbody>(true))
                {
                    body.isKinematic =
                        true;

                    body.detectCollisions =
                        false;
                }

                var renderers =
                    model.GetComponentsInChildren<
                        Renderer>(true);

                Bounds bounds =
                    default;

                bool found =
                    false;

                foreach (var renderer in renderers)
                {
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                        continue;

                    if (!found)
                    {
                        bounds =
                            renderer.bounds;

                        found =
                            true;
                    }
                    else
                    {
                        bounds.Encapsulate(
                            renderer.bounds);
                    }
                }

                if (!found)
                {
                    throw new InvalidOperationException(
                        "No visible geometry in " +
                        prefab.name);
                }

                var cameraOwner =
                    new GameObject(
                        "Character Preview Camera",
                        typeof(Camera));

                SceneManager.MoveGameObjectToScene(
                    cameraOwner,
                    scene);

                var camera =
                    cameraOwner.GetComponent<Camera>();

                camera.scene =
                    scene;

                camera.overrideSceneCullingMask =
                    EditorSceneManager.GetSceneCullingMask(
                        scene);

                camera.enabled =
                    false;

                camera.clearFlags =
                    CameraClearFlags.SolidColor;

                camera.backgroundColor =
                    new Color(
                        0.025f,
                        0.035f,
                        0.038f,
                        1f);

                camera.nearClipPlane =
                    0.01f;

                camera.farClipPlane =
                    100f;
                camera.fieldOfView =
                    24f;

                // Fit the complete character inside the square portrait.
                // Calculate camera distance from the actual renderer bounds
                // instead of using a fixed distance based only on height.
                const float framingPadding =
                    1.22f;

                float halfHeight =
                    Mathf.Max(
                        bounds.extents.y,
                        0.25f) *
                    framingPadding;

                float halfWidth =
                    Mathf.Max(
                        bounds.extents.x,
                        0.25f) *
                    framingPadding;

                float verticalFov =
                    camera.fieldOfView *
                    Mathf.Deg2Rad;

                float horizontalFov =
                    2f *
                    Mathf.Atan(
                        Mathf.Tan(
                            verticalFov * 0.5f) *
                        camera.aspect);

                float distanceForHeight =
                    halfHeight /
                    Mathf.Tan(
                        verticalFov * 0.5f);

                float distanceForWidth =
                    halfWidth /
                    Mathf.Tan(
                        horizontalFov * 0.5f);

                float distance =
                    Mathf.Max(
                        distanceForHeight,
                        distanceForWidth);

                // Aim slightly above geometric center so the portrait
                // keeps a little more visual space above the head.
                Vector3 targetPoint = bounds.center;
                Vector3 viewDirection = Vector3.back;

                camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x) * 1.18f;
                camera.transform.position =
                    targetPoint +
                    viewDirection *
                    distance;

                camera.transform.LookAt(
                    targetPoint);

                AddLight(
                    scene,
                    new Vector3(
                        35f,
                        -35f,
                        0f),
                    2.8f);

                AddLight(
                    scene,
                    new Vector3(
                        325f,
                        145f,
                        0f),
                    1.8f);

                target =
                    new RenderTexture(
                        512,
                        512,
                        24,
                        RenderTextureFormat.ARGB32);

                target.Create();

                camera.targetTexture =
                    target;

                var previous =
                    RenderTexture.active;

                try
                {
                    var request =
                        new RenderPipeline.StandardRequest
                        {
                            destination =
                                target
                        };

                    if (RenderPipeline.SupportsRenderRequest(
                            camera,
                            request))
                    {
                        RenderPipeline.SubmitRenderRequest(
                            camera,
                            request);
                    }
                    else
                    {
                        camera.Render();
                    }

                    RenderTexture.active =
                        target;

                    image =
                        new Texture2D(
                            512,
                            512,
                            TextureFormat.RGB24,
                            false);

                    image.ReadPixels(
                        new Rect(
                            0,
                            0,
                            512,
                            512),
                        0,
                        0);

                    var pixels = image.GetPixels32();
                    var background = pixels[0];
                    int left = 512, right = -1, bottom = 512, top = -1;
                    for (int y = 0; y < 512; y++)
                    for (int x = 0; x < 512; x++)
                    {
                        var pixel = pixels[y * 512 + x];
                        if (Mathf.Abs(pixel.r - background.r) + Mathf.Abs(pixel.g - background.g)
                            + Mathf.Abs(pixel.b - background.b) < 12) continue;
                        left = Mathf.Min(left, x);
                        right = Mathf.Max(right, x);
                        bottom = Mathf.Min(bottom, y);
                        top = Mathf.Max(top, y);
                    }
                    if (right >= left)
                    {
                        int dx = (511 - left - right) / 2;
                        int dy = (511 - bottom - top) / 2;
                        var centered = new Color32[pixels.Length];
                        for (int y = 0; y < 512; y++)
                        for (int x = 0; x < 512; x++)
                        {
                            int sx = x - dx, sy = y - dy;
                            centered[y * 512 + x] = sx >= 0 && sx < 512 && sy >= 0 && sy < 512
                                ? pixels[sy * 512 + sx] : background;
                        }
                        image.SetPixels32(centered);
                    }
                    image.Apply();

                    File.WriteAllBytes(
                        path,
                        image.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active =
                        previous;

                    camera.targetTexture =
                        null;
                }
            }
            finally
            {
                if (image != null)
                {
                    Object.DestroyImmediate(
                        image);
                }

                if (target != null)
                {
                    target.Release();

                    Object.DestroyImmediate(
                        target);
                }

                EditorSceneManager.ClosePreviewScene(
                    scene);
            }
        }

        private static void AddLight(
            Scene scene,
            Vector3 angles,
            float intensity)
        {
            var owner =
                new GameObject(
                    "Character Preview Light",
                    typeof(Light));

            SceneManager.MoveGameObjectToScene(
                owner,
                scene);

            owner.transform.rotation =
                Quaternion.Euler(
                    angles);

            var light =
                owner.GetComponent<Light>();

            light.type =
                LightType.Directional;

            light.intensity =
                intensity;

            light.shadows =
                LightShadows.None;
        }
    }
}
