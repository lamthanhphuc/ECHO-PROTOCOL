using System;
using System.IO;
using EchoProtocol.UI.MainMenu;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EchoProtocol.Editor
{
    public static class StoreLivePreviewAssetBuilder
    {
        private const string OutputDirectory =
            "Assets/Resources/Shop/LivePreview";

        [MenuItem(
            "ECHO Protocol/Shop/Build Live Preview Prefabs")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Exit Play mode before building Store preview prefabs.");
            }

            EnsureOutputDirectory();

            BuildCharacters();
            BuildTeamTools();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[ECHO] Store live preview prefabs PASS.");
        }

        public static void BuildAllBatch()
        {
            BuildAll();
        }

        private static void BuildCharacters()
        {
            foreach (var item
                     in CharacterShopAssets.Items)
            {
                BuildPreviewPrefab(
                    item.PrefabPath,
                    ResourceToAssetPath(
                        item.LivePreviewResource));
            }
        }

        private static void BuildTeamTools()
        {
            foreach (var item
                     in TeamToolShopAssets.Items)
            {
                BuildPreviewPrefab(
                    item.PrefabPath,
                    ResourceToAssetPath(
                        item.LivePreviewResource));
            }
        }

        private static void BuildPreviewPrefab(
            string sourcePath,
            string outputPath)
        {
            var source =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    sourcePath);

            if (source == null)
            {
                throw new FileNotFoundException(
                    "Store preview source prefab missing.",
                    sourcePath);
            }

            GameObject instance =
                null;

            try
            {
                instance =
                    PrefabUtility.InstantiatePrefab(
                        source)
                    as GameObject;

                if (instance == null)
                {
                    throw new InvalidOperationException(
                        "Could not instantiate preview source: " +
                        sourcePath);
                }

                instance.name =
                    Path.GetFileNameWithoutExtension(
                        outputPath);

                StripGameplayComponents(
                    instance);

                var saved =
                    PrefabUtility.SaveAsPrefabAsset(
                        instance,
                        outputPath);

                if (saved == null)
                {
                    throw new InvalidOperationException(
                        "Could not save preview prefab: " +
                        outputPath);
                }

                Debug.Log(
                    "[ECHO] Preview prefab: " +
                    outputPath);
            }
            finally
            {
                if (instance != null)
                {
                    Object.DestroyImmediate(
                        instance);
                }
            }
        }

        private static void StripGameplayComponents(
            GameObject root)
        {
            // Store preview assets must contain visuals only.
            // Animator is intentionally retained so characters
            // can play their default idle animation.

            foreach (var component
                     in root.GetComponentsInChildren<
                         MonoBehaviour>(true))
            {
                Object.DestroyImmediate(
                    component);
            }

            foreach (var component
                     in root.GetComponentsInChildren<
                         Collider>(true))
            {
                Object.DestroyImmediate(
                    component);
            }

            foreach (var component
                     in root.GetComponentsInChildren<
                         Rigidbody>(true))
            {
                Object.DestroyImmediate(
                    component);
            }

            foreach (var component
                     in root.GetComponentsInChildren<
                         CharacterController>(true))
            {
                Object.DestroyImmediate(
                    component);
            }

            foreach (var component
                     in root.GetComponentsInChildren<
                         AudioSource>(true))
            {
                Object.DestroyImmediate(
                    component);
            }

            foreach (var component
                     in root.GetComponentsInChildren<
                         AudioListener>(true))
            {
                Object.DestroyImmediate(
                    component);
            }

            foreach (var component
                     in root.GetComponentsInChildren<
                         Camera>(true))
            {
                Object.DestroyImmediate(
                    component);
            }

            foreach (var component
                     in root.GetComponentsInChildren<
                         Light>(true))
            {
                Object.DestroyImmediate(
                    component);
            }

            foreach (var component
                     in root.GetComponentsInChildren<
                         ParticleSystem>(true))
            {
                Object.DestroyImmediate(
                    component);
            }
        }

        private static string ResourceToAssetPath(
            string resourcePath)
        {
            return
                "Assets/Resources/" +
                resourcePath +
                ".prefab";
        }

        private static void EnsureOutputDirectory()
        {
            string projectRoot =
                Directory.GetParent(
                    Application.dataPath)
                    .FullName;

            string fullPath =
                Path.Combine(
                    projectRoot,
                    OutputDirectory);

            Directory.CreateDirectory(
                fullPath);

            AssetDatabase.Refresh();
        }
    }
}
