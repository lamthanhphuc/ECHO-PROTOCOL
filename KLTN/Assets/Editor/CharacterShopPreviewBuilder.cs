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
        public static void BakeCharacterThumbnails()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Exit Play mode before baking character thumbnails.");
            }

            foreach (var item in CharacterShopAssets.Items)
            {
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
                    if (!renderer.enabled)
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
                Vector3 targetPoint =
                    bounds.center +
                    Vector3.up *
                    bounds.size.y *
                    0.025f;

                // Slight three-quarter angle like the existing shop portrait.
                Vector3 viewDirection =
                    new Vector3(
                        0.28f,
                        0.04f,
                        -1f)
                    .normalized;

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
