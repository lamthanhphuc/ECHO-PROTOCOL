using System.IO;
using EchoProtocol.Networking.Diagnostics;
using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.Editor.Networking
{
    public static class FND003C2CHarnessBuilder
    {
        public const string HarnessScenePath = "Assets/Scenes/Networking/FND003C2CHarness.unity";
        public const string RunnerPrefabPath = "Assets/Prefabs/NetworkRunner.prefab";
        public const string BuildOutputPath = "Builds/FND003C2C/EchoProtocol-FND003C2C.exe";

        [MenuItem("Tools/ECHO Protocol/C2C/Create Harness Scene")]
        public static void EnsureHarnessSceneExists()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(HarnessScenePath) != null)
            {
                Debug.Log($"C2C|SCENE_CREATE_SKIP|reason=AlreadyExists|path={HarnessScenePath}");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(HarnessScenePath));

            Scene scene = default;
            var sceneCreated = false;

            try
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                scene.name = "FND003C2CHarness";
                sceneCreated = true;

                CreateCamera(scene);
                CreateLight(scene);
                CreateFloor(scene);
                CreateHarnessRoot(scene);

                if (!EditorSceneManager.SaveScene(scene, HarnessScenePath))
                {
                    Debug.LogError($"C2C|SCENE_CREATE_FAIL|reason=SaveFailed|path={HarnessScenePath}");
                    return;
                }

                Debug.Log($"C2C|SCENE_CREATE_OK|path={HarnessScenePath}");
                AssetDatabase.ImportAsset(HarnessScenePath);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"C2C|SCENE_CREATE_FAIL|reason=Exception|message={ex.Message}");
            }
            finally
            {
                if (sceneCreated && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [MenuItem("Tools/ECHO Protocol/C2C/Build Harness")]
        public static void BuildHarness()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(HarnessScenePath) == null)
            {
                Debug.LogError($"C2C|BUILD_FAIL|reason=MissingHarnessScene|path={HarnessScenePath}|action=Tools/ECHO Protocol/C2C/Create Harness Scene");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(BuildOutputPath));

            var options = new BuildPlayerOptions
            {
                scenes = new[] { HarnessScenePath },
                locationPathName = BuildOutputPath,
                target = EditorUserBuildSettings.activeBuildTarget,
                options = BuildOptions.Development
            };

            BuildPipeline.BuildPlayer(options);
        }

        private static void CreateCamera(Scene scene)
        {
            var cameraObject = new GameObject("Main Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 4f, -8f);
            cameraObject.transform.rotation = Quaternion.Euler(25f, 0f, 0f);
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        private static void CreateLight(Scene scene)
        {
            var lightObject = new GameObject("Directional Light");
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
        }

        private static void CreateFloor(Scene scene)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(floor, scene);
            floor.name = "HarnessFloor";
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(10f, 0.1f, 10f);
        }

        private static void CreateHarnessRoot(Scene scene)
        {
            var runnerPrefab = AssetDatabase.LoadAssetAtPath<NetworkRunner>(RunnerPrefabPath);
            if (runnerPrefab == null)
            {
                throw new FileNotFoundException($"Missing runner prefab at {RunnerPrefabPath}.");
            }

            var root = new GameObject("FND003C2CHarness");
            SceneManager.MoveGameObjectToScene(root, scene);
            var controller = root.AddComponent<FusionC2CHarnessController>();
            root.AddComponent<FusionPlayerLifecycleProbe>();

            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("runnerPrefab").objectReferenceValue = runnerPrefab;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
