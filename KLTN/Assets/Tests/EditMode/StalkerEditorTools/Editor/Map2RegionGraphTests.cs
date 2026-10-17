#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EchoProtocol.AI.Stalker;
using EchoProtocol.AI.Stalker.Spatial;
using EchoProtocol.AI.Stalker.Spatial.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.EditorTools.Tests
{
    public sealed class Map2RegionGraphTests
    {
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in _created) UnityEngine.Object.DestroyImmediate(item);
            _created.Clear();
        }

        [Test]
        public void Map2_BakeKeepsDisconnectedIslandsOutOfPatrolZone()
        {
            var graph = Graph();
            var report = Map2RegionGraphAuthoring.BuildReport(graph,
                new[] { Region(1, "Room_West", 0.5f, 2f), Region(2, "Room_East", 2.5f, 2f) }, Vector3.zero, _ => true);
            Assert.That(report.CanBakeRuntimeAsset, Is.True, report.ToDisplayString());
            Assert.That(RegionGraph.Validate(report.RuntimeGraph, graph), Is.EqualTo(RegionGraphValidationFailure.None));
            Assert.That(report.RuntimeGraph.Regions.Count(r => r.SemanticMetadata.Kind == RegionSemanticKind.Room), Is.EqualTo(2));
            Assert.That(report.RuntimeGraph.TryGetNodeSemanticMetadata(4, out var island), Is.True);
            Assert.That(island.Zone, Is.EqualTo(RegionSemanticZone.Isolated));
            Assert.That(island.Kind, Is.EqualTo(RegionSemanticKind.IsolatedIsland));
        }

        [Test]
        public void Map2_BakeRejectsOverlappingRegionEvidence()
        {
            var report = Map2RegionGraphAuthoring.BuildReport(Graph(),
                new[] { Region(1, "Room_West", 1f, 4f), Region(2, "Room_East", 1f, 4f) }, Vector3.zero, _ => true);
            Assert.That(report.CanBakeRuntimeAsset, Is.False);
            Assert.That(report.UnresolvedSeedOverlapNodeIds, Is.Not.Empty);
        }

        [Test]
        public void Map2_BakeRejectsNativeUnreachableNodesAndCutsFalseRegionConnections()
        {
            var report = Map2RegionGraphAuthoring.BuildReport(Graph(),
                new[] { Region(1, "Room_District", 1.5f, 4f) }, Vector3.zero, position => position.x < 1.5f);
            Assert.That(report.CanBakeRuntimeAsset, Is.True, report.ToDisplayString());
            Assert.That(report.RuntimeGraph.TryGetNodeSemanticMetadata(2, out var rejected), Is.True);
            Assert.That(rejected.Zone, Is.EqualTo(RegionSemanticZone.Isolated));
            Assert.That(report.RuntimeGraph.TryGetRegionForNode(0, out var start), Is.True);
            Assert.That(report.RuntimeGraph.TryGetRegionForNode(2, out var blocked), Is.True);
            Assert.That(report.RuntimeGraph.TryGetRouteHopCost(start, blocked, out _), Is.False);
        }

        [Test]
        public void Map2_OverrideIsInstanceOnlyAndMustPrecedeGraphInitialization()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/StalkerNetwork.prefab");
            var baseline = new SerializedObject(prefab.GetComponent<StalkerController>()).FindProperty("regionGraphAsset").objectReferenceValue;
            var instance = new GameObject("Map2 override test");
            instance.SetActive(false);
            _created.Add(instance);
            var controller = instance.AddComponent<StalkerController>();
            var asset = ScriptableObject.CreateInstance<RegionGraphAsset>();
            _created.Add(asset);

            Assert.Throws<ArgumentNullException>(() => controller.ConfigureRegionGraph(null, RegionSemanticZone.Zone01));
            Assert.Throws<ArgumentOutOfRangeException>(() => controller.ConfigureRegionGraph(asset, RegionSemanticZone.Unknown));
            controller.ConfigureRegionGraph(asset, RegionSemanticZone.Zone01);
            Assert.That(new SerializedObject(controller).FindProperty("regionGraphAsset").objectReferenceValue, Is.SameAs(asset));
            Assert.That(new SerializedObject(prefab.GetComponent<StalkerController>()).FindProperty("regionGraphAsset").objectReferenceValue, Is.SameAs(baseline));

            typeof(StalkerController).GetField("_spatialPatrolGraph", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, Graph());
            Assert.Throws<InvalidOperationException>(() => controller.ConfigureRegionGraph(asset, RegionSemanticZone.Zone01));
        }

        private RegionDefinition Region(int id, string name, float x, float width)
        {
            var go = new GameObject(name);
            _created.Add(go);
            go.transform.position = new Vector3(x, 0f, 0f);
            var region = go.AddComponent<RegionDefinition>();
            var serialized = new SerializedObject(region);
            serialized.FindProperty("regionId").intValue = id;
            serialized.FindProperty("localBounds").boundsValue = new Bounds(Vector3.zero, new Vector3(width, 4f, 4f));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return region;
        }

        private static NavMeshSpatialGraph Graph() => new NavMeshSpatialGraph(new[]
        {
            Node(0, 0f, 1), Node(1, 1f, 0, 2), Node(2, 2f, 1, 3), Node(3, 3f, 2), Node(4, 100f)
        });

        private static SpatialNode Node(int id, float x, params int[] neighbors) =>
            new SpatialNode(id, new Vector3(x, 0f, 0f), 0, id, id * 3, id * 3 + 1, id * 3 + 2, neighbors);
    }
}
#endif
