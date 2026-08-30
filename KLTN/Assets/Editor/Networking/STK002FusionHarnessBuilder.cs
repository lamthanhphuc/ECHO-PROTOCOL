using System.IO;
using EchoProtocol.AI.Stalker;
using EchoProtocol.AI.Stalker.Networking;
using EchoProtocol.AI.Stalker.Networking.Diagnostics;
using EchoProtocol.Networking.Diagnostics;
using Fusion;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace EchoProtocol.Editor.Networking
{
    public static class STK002FusionHarnessBuilder
    {
        public const string HarnessScenePath = "Assets/Scenes/Networking/STK002FusionHarness.unity";
        public const string RunnerPrefabPath = "Assets/Prefabs/NetworkRunner.prefab";
        public const string StalkerPrefabPath = "Assets/Prefabs/StalkerNetwork.prefab";
        public const string BuildOutputPath = "Builds/STK002Fusion/EchoProtocol-STK002Fusion.exe";

        [MenuItem("Tools/ECHO Protocol/STK-002/Create Harness Assets")]
        public static void EnsureHarnessAssetsExist()
        {
            EnsureStalkerPrefabExists();
            EnsureHarnessSceneExists();
        }

        [MenuItem("Tools/ECHO Protocol/STK-002/Create Stalker Network Prefab")]
        public static void EnsureStalkerPrefabExists()
        {
            if (AssetDatabase.LoadAssetAtPath<NetworkObject>(StalkerPrefabPath) != null)
            {
                EnsureFusionPrefabLabel(StalkerPrefabPath);
                Debug.Log($"STK2|PREFAB_CREATE_SKIP|reason=AlreadyExists|path={StalkerPrefabPath}");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(StalkerPrefabPath));

            var root = new GameObject("StalkerNetwork");
            var visionOrigin = new GameObject("VisionOrigin");
            visionOrigin.transform.SetParent(root.transform, false);
            visionOrigin.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            try
            {
                var networkObject = root.AddComponent<NetworkObject>();
                root.AddComponent<NetworkTransform>();
                root.AddComponent<NavMeshAgent>();
                var sensor = root.AddComponent<StalkerVisionSensor>();
                var controller = root.AddComponent<StalkerController>();
                var runtime = root.AddComponent<StalkerFusionRuntime>();

                SetSerializedReference(sensor, "visionOrigin", visionOrigin.transform);
                SetSerializedReference(controller, "visionSensor", sensor);
                SetSerializedReference(runtime, "controller", controller);
                SetSerializedReference(runtime, "visionSensor", sensor);

                PrefabUtility.SaveAsPrefabAsset(root, StalkerPrefabPath);
                EnsureFusionPrefabLabel(StalkerPrefabPath);
                Debug.Log($"STK2|PREFAB_CREATE_OK|path={StalkerPrefabPath}|networkObject={networkObject != null}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [MenuItem("Tools/ECHO Protocol/STK-002/Create Harness Scene")]
        public static void EnsureHarnessSceneExists()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(HarnessScenePath) != null)
            {
                Debug.Log($"STK2|SCENE_CREATE_SKIP|reason=AlreadyExists|path={HarnessScenePath}");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(HarnessScenePath));

            Scene scene = default;
            var sceneCreated = false;

            try
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                scene.name = "STK002FusionHarness";
                sceneCreated = true;

                CreateCamera(scene);
                CreateLight(scene);
                CreateFloor(scene);
                var spawnPoint = CreateMarker(scene, "STK002_StalkerSpawn", new Vector3(0f, 0f, -4f));
                var visibleMarker = CreateMarker(scene, "STK002_PlayerVisibleMarker", new Vector3(0f, 1f, 4f));
                var hiddenMarker = CreateMarker(scene, "STK002_PlayerHiddenMarker", new Vector3(0f, 1f, 6f));
                var occluder = CreateOccluder(scene);
                var navMeshSurface = CreateNavMeshSurface(scene);
                CreateHarnessRoot(scene, spawnPoint, visibleMarker, hiddenMarker, occluder, navMeshSurface);

                if (!EditorSceneManager.SaveScene(scene, HarnessScenePath))
                {
                    Debug.LogError($"STK2|SCENE_CREATE_FAIL|reason=SaveFailed|path={HarnessScenePath}");
                    return;
                }

                Debug.Log($"STK2|SCENE_CREATE_OK|path={HarnessScenePath}");
                AssetDatabase.ImportAsset(HarnessScenePath);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"STK2|SCENE_CREATE_FAIL|reason=Exception|message={ex.Message}");
            }
            finally
            {
                if (sceneCreated && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [MenuItem("Tools/ECHO Protocol/STK-002/Build Harness")]
        public static void BuildHarness()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(HarnessScenePath) == null)
            {
                Debug.LogError($"STK2|BUILD_FAIL|reason=MissingHarnessScene|path={HarnessScenePath}|action=Tools/ECHO Protocol/STK-002/Create Harness Assets");
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
            cameraObject.transform.position = new Vector3(0f, 7f, -10f);
            cameraObject.transform.rotation = Quaternion.Euler(35f, 0f, 0f);
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
            floor.name = "STK002_HarnessFloor";
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(14f, 0.1f, 14f);
            GameObjectUtility.SetStaticEditorFlags(floor, StaticEditorFlags.NavigationStatic);
        }

        private static Transform CreateMarker(Scene scene, string name, Vector3 position)
        {
            var marker = new GameObject(name);
            SceneManager.MoveGameObjectToScene(marker, scene);
            marker.transform.position = position;
            return marker.transform;
        }

        private static GameObject CreateOccluder(Scene scene)
        {
            var occluder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(occluder, scene);
            occluder.name = "STK002_NoCheatOccluder";
            occluder.transform.position = new Vector3(0f, 1f, 0f);
            occluder.transform.localScale = new Vector3(3f, 2f, 0.25f);
            occluder.SetActive(false);
            return occluder;
        }

        private static NavMeshSurface CreateNavMeshSurface(Scene scene)
        {
            var surfaceObject = new GameObject("STK002_NavMeshSurface");
            SceneManager.MoveGameObjectToScene(surfaceObject, scene);
            var surface = surfaceObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.size = new Vector3(16f, 8f, 16f);
            surface.center = new Vector3(0f, 1f, 0f);
            return surface;
        }

        private static void CreateHarnessRoot(
            Scene scene,
            Transform stalkerSpawnPoint,
            Transform visiblePlayerMarker,
            Transform hiddenPlayerMarker,
            GameObject noCheatOccluder,
            NavMeshSurface navMeshSurface)
        {
            var runnerPrefab = AssetDatabase.LoadAssetAtPath<NetworkRunner>(RunnerPrefabPath);
            var stalkerPrefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(StalkerPrefabPath);
            if (runnerPrefab == null)
            {
                throw new FileNotFoundException($"Missing runner prefab at {RunnerPrefabPath}.");
            }

            if (stalkerPrefab == null)
            {
                throw new FileNotFoundException($"Missing Stalker prefab at {StalkerPrefabPath}.");
            }

            var root = new GameObject("STK002FusionHarness");
            SceneManager.MoveGameObjectToScene(root, scene);
            var controller = root.AddComponent<FusionC2CHarnessController>();
            root.AddComponent<FusionPlayerLifecycleProbe>();
            var stalkerProbe = root.AddComponent<StalkerFusionC2CHarnessProbe>();

            SetSerializedReference(controller, "runnerPrefab", runnerPrefab);
            SetSerializedReference(stalkerProbe, "controller", controller);
            SetSerializedReference(stalkerProbe, "stalkerPrefab", stalkerPrefab);
            SetSerializedReference(stalkerProbe, "navMeshSurface", navMeshSurface);
            SetSerializedReference(stalkerProbe, "stalkerSpawnPoint", stalkerSpawnPoint);
            SetSerializedReference(stalkerProbe, "visiblePlayerMarker", visiblePlayerMarker);
            SetSerializedReference(stalkerProbe, "hiddenPlayerMarker", hiddenPlayerMarker);
            SetSerializedReference(stalkerProbe, "noCheatOccluder", noCheatOccluder);
        }

        private static void EnsureFusionPrefabLabel(string path)
        {
            var labels = AssetDatabase.GetLabels(AssetDatabase.LoadAssetAtPath<Object>(path));
            for (var i = 0; i < labels.Length; i++)
            {
                if (labels[i] == "FusionPrefab")
                {
                    return;
                }
            }

            ArrayUtility.Add(ref labels, "FusionPrefab");
            AssetDatabase.SetLabels(AssetDatabase.LoadAssetAtPath<Object>(path), labels);
        }

        private static void SetSerializedReference(Object target, string propertyName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
