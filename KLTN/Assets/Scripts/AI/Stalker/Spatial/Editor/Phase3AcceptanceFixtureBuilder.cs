#if UNITY_EDITOR
using System.Collections.Generic;
using EchoProtocol.AI.Common.Spatial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace EchoProtocol.AI.Stalker.Spatial.Editor
{
    public static class Phase3AcceptanceFixtureBuilder
    {
        private const string ScenePath = "Assets/Scenes/AI/AI_Stalker_SpatialV3.unity";
        private const string RegionGraphAssetPath = "Assets/AI/Stalker/Phase3/AI_Stalker_SpatialV3_RegionGraph.asset";

        [MenuItem("Echo Protocol/AI/Build Phase 3 Acceptance Fixture")]
        public static void BuildFixture()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateFloor("Region_A_Floor", new Vector3(-4f, 0f, 0f), new Vector3(6f, 0.1f, 6f));
            CreateFloor("Region_B_Floor", new Vector3(4f, 0f, 0f), new Vector3(6f, 0.1f, 6f));
            CreateFloor("Door_Link_Floor", Vector3.zero, new Vector3(2f, 0.1f, 2f));
            CreateRegionDefinition("Region_A", 1, new Vector3(-4f, 0.5f, 0f), new Vector3(7f, 3f, 7f));
            CreateRegionDefinition("Region_B", 2, new Vector3(4f, 0.5f, 0f), new Vector3(7f, 3f, 7f));

            var graph = BuildFixtureSpatialGraph();
            var bakeResult = RegionGraphBakeUtility.Bake(
                graph,
                new[]
                {
                    new RegionDefinitionBakeData(new RegionId(1), new Bounds(new Vector3(-4f, 0f, 0f), new Vector3(7f, 4f, 7f))),
                    new RegionDefinitionBakeData(new RegionId(2), new Bounds(new Vector3(4f, 0f, 0f), new Vector3(7f, 4f, 7f)))
                },
                new[]
                {
                    new RegionEdgeBakeData(new RegionId(1), new RegionId(2), new DoorId(1)),
                    new RegionEdgeBakeData(new RegionId(2), new RegionId(1), new DoorId(1))
                },
                1);

            if (!bakeResult.Succeeded)
            {
                throw new System.InvalidOperationException($"Phase 3 fixture bake failed: {bakeResult.Diagnostic.Failure}");
            }

            var asset = AssetDatabase.LoadAssetAtPath<RegionGraphAsset>(RegionGraphAssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<RegionGraphAsset>();
                AssetDatabase.CreateAsset(asset, RegionGraphAssetPath);
            }

            asset.ConfigureFromRuntimeGraph(bakeResult.Graph, graph.NodeCount);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            CreateStalker();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        public static NavMeshSpatialGraph BuildFixtureSpatialGraph()
        {
            return new NavMeshSpatialGraph(new[]
            {
                Node(0, -6f, 1),
                Node(1, -3f, 0, 2),
                Node(2, -1f, 1, 3),
                Node(3, 1f, 2, 4),
                Node(4, 3f, 3, 5),
                Node(5, 6f, 4)
            });
        }

        private static SpatialNode Node(int id, float x, params int[] neighbors)
        {
            return new SpatialNode(
                id,
                new Vector3(x, 0f, 0f),
                0,
                id,
                id * 3,
                id * 3 + 1,
                id * 3 + 2,
                new List<int>(neighbors));
        }

        private static void CreateFloor(string name, Vector3 position, Vector3 scale)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = name;
            floor.transform.position = position;
            floor.transform.localScale = scale;
        }

        private static void CreateRegionDefinition(string name, int id, Vector3 position, Vector3 size)
        {
            var region = new GameObject(name);
            region.transform.position = position;
            var definition = region.AddComponent<RegionDefinition>();
            var serializedObject = new SerializedObject(definition);
            serializedObject.FindProperty("regionId").intValue = id;
            serializedObject.FindProperty("localBounds").boundsValue = new Bounds(Vector3.zero, size);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateStalker()
        {
            var stalker = new GameObject("Stalker_ConfidenceSpatial_Phase3");
            stalker.transform.position = new Vector3(-6f, 0f, 0f);
            var agent = stalker.AddComponent<NavMeshAgent>();
            agent.radius = 0.25f;
            agent.height = 1.8f;
            var controller = stalker.AddComponent<StalkerController>();
            var serializedObject = new SerializedObject(controller);
            serializedObject.FindProperty("patrolMode").enumValueIndex = (int)StalkerPatrolMode.ConfidenceSpatial;
            serializedObject.FindProperty("regionGraphAsset").objectReferenceValue = AssetDatabase.LoadAssetAtPath<RegionGraphAsset>(RegionGraphAssetPath);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
