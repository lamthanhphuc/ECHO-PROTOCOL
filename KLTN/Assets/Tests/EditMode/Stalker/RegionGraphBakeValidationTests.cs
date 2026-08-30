using System;
using System.Reflection;
using EchoProtocol.AI.Common.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class RegionGraphBakeValidationTests
    {
        [Test]
        public void STK_P3_Bake_RejectsInvalidRegionId()
        {
            var result = Bake(CreateGraph(), DefinitionArray(Definition(RegionId.Invalid, Vector3.zero, Vector3.one)), null, 1);
            AssertFailure(result, "InvalidRegionId");
        }

        [Test]
        public void STK_P3_Bake_RejectsDuplicateRegionId()
        {
            var result = Bake(CreateGraph(), DefinitionArray(
                Definition(new RegionId(1), new Vector3(-1f, 0f, 0f), Vector3.one),
                Definition(new RegionId(1), new Vector3(1f, 0f, 0f), Vector3.one)), null, 1);

            AssertFailure(result, "DuplicateRegionId");
        }

        [Test]
        public void STK_P3_Bake_RejectsZeroRegionNodeMatch()
        {
            var result = Bake(CreateGraph(), DefinitionArray(Definition(new RegionId(1), new Vector3(99f, 0f, 0f), Vector3.one)), null, 1);

            AssertFailure(result, "SpatialNodeMatchedByZeroRegions");
            Assert.That(GetProperty(GetProperty(result, "Diagnostic"), "SpatialNodeId"), Is.EqualTo(0));
        }

        [Test]
        public void STK_P3_Bake_RejectsMultipleRegionNodeMatch()
        {
            var result = Bake(CreateGraph(), DefinitionArray(
                Definition(new RegionId(1), Vector3.zero, new Vector3(10f, 2f, 2f)),
                Definition(new RegionId(2), Vector3.zero, new Vector3(10f, 2f, 2f))), null, 1);

            AssertFailure(result, "SpatialNodeMatchedByMultipleRegions");
        }

        [Test]
        public void STK_P3_Bake_RejectsInvalidRegionEdge()
        {
            var result = Bake(CreateGraph(), Definitions(), EdgeArray(Edge(new RegionId(1), new RegionId(1), DoorId.Invalid)), 1);
            AssertFailure(result, "InvalidRegionEdge");
        }

        [Test]
        public void STK_P3_Bake_RejectsDanglingRegionEdge()
        {
            var result = Bake(CreateGraph(), Definitions(), EdgeArray(Edge(new RegionId(1), new RegionId(3), DoorId.Invalid)), 1);
            AssertFailure(result, "RegionEdgeMissingRegion");
        }

        [Test]
        public void STK_P3_Bake_RejectsMalformedDoorId()
        {
            var result = Bake(CreateGraph(), Definitions(), EdgeArray(Edge(new RegionId(1), new RegionId(2), -5)), 1);
            AssertFailure(result, "MalformedDoorId");
        }

        [Test]
        public void STK_P3_AssetValidation_RejectsInvalidNodeToRegionMap()
        {
            var graph = CreateGraph();
            var asset = ScriptableObject.CreateInstance(RegionGraphAssetType);
            Invoke(asset, "ConfigureForTests", new[] { typeof(int), CompatibilityIdentityType, RegionRecordArrayType, NodeRegionRecordArrayType },
                1, GetProperty(graph, "CompatibilityIdentity"), RegionRecordArray(RegionRecord(1)), NodeRegionRecordArray(NodeRegionRecord(0, 99)));

            var diagnostic = Invoke(asset, "ValidateAgainst", new[] { GraphType }, graph);
            var failure = GetProperty(diagnostic, "Failure").ToString();

            Assert.That(failure, Is.EqualTo("NodeToRegionMapSizeMismatch").Or.EqualTo("InvalidDanglingNodeMapping"));
            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public void STK_P3_RuntimeValidation_RejectsIncompatibleSpatialIdentity()
        {
            var graph = CreateGraph();
            var identity = Activator.CreateInstance(CompatibilityIdentityType, (ulong)GetProperty(GetProperty(graph, "CompatibilityIdentity"), "Value") + 10UL);
            var staleGraph = Activator.CreateInstance(RegionGraphType, RegionNodeArray(RegionNode(new RegionId(1))), RegionIdArray(new RegionId(1), new RegionId(1)), identity, 1);

            var diagnostic = Invoke(BakeUtilityType, "ValidateRuntimeGraph", new[] { RegionGraphType, GraphType }, staleGraph, graph);

            Assert.That(GetProperty(diagnostic, "Failure").ToString(), Is.EqualTo("SpatialGraphCompatibilityMismatch"));
        }

        private static object Bake(object graph, Array definitions, Array edges, int version)
        {
            return Invoke(BakeUtilityType, "Bake", new[] { GraphType, DefinitionArrayType, EdgeArrayType, typeof(int) }, graph, definitions, edges, version);
        }

        private static Array Definitions()
        {
            return DefinitionArray(
                Definition(new RegionId(1), new Vector3(-1f, 0f, 0f), new Vector3(1f, 2f, 2f)),
                Definition(new RegionId(2), new Vector3(1f, 0f, 0f), new Vector3(1f, 2f, 2f)));
        }

        private static object CreateGraph()
        {
            return Activator.CreateInstance(GraphType, NodeArray(
                Node(0, new Vector3(-1f, 0f, 0f), 1),
                Node(1, new Vector3(1f, 0f, 0f), 0)));
        }

        private static object Node(int id, Vector3 position, params int[] neighbors)
        {
            return Activator.CreateInstance(NodeType, id, position, 0, id, id * 3, id * 3 + 1, id * 3 + 2, neighbors);
        }

        private static object Definition(RegionId regionId, Vector3 center, Vector3 size)
        {
            return Activator.CreateInstance(DefinitionType, regionId, new Bounds(center, size));
        }

        private static object Edge(RegionId from, RegionId to, DoorId door) => Activator.CreateInstance(EdgeBakeType, from, to, door);
        private static object Edge(RegionId from, RegionId to, int rawDoorId) => Activator.CreateInstance(EdgeBakeType, from, to, rawDoorId);

        private static object RegionNode(RegionId regionId)
        {
            return Activator.CreateInstance(RegionNodeType, regionId, RegionEdgeArray());
        }

        private static object RegionRecord(int regionId)
        {
            var record = Activator.CreateInstance(RegionRecordType);
            SetField(record, "RegionId", regionId);
            SetField(record, "Edges", RegionEdgeRecordArray());
            return record;
        }

        private static object NodeRegionRecord(int nodeId, int regionId)
        {
            var record = Activator.CreateInstance(NodeRegionRecordType);
            SetField(record, "SpatialNodeId", nodeId);
            SetField(record, "RegionId", regionId);
            return record;
        }

        private static Array NodeArray(params object[] values) => ToArray(NodeType, values);
        private static Array DefinitionArray(params object[] values) => ToArray(DefinitionType, values);
        private static Array EdgeArray(params object[] values) => ToArray(EdgeBakeType, values);
        private static Array RegionNodeArray(params object[] values) => ToArray(RegionNodeType, values);
        private static Array RegionEdgeArray(params object[] values) => ToArray(RegionEdgeType, values);
        private static Array RegionRecordArray(params object[] values) => ToArray(RegionRecordType, values);
        private static Array RegionEdgeRecordArray(params object[] values) => ToArray(RegionEdgeRecordType, values);
        private static Array NodeRegionRecordArray(params object[] values) => ToArray(NodeRegionRecordType, values);
        private static Array RegionIdArray(params RegionId[] values) => values;

        private static Array ToArray(Type elementType, object[] values)
        {
            var array = Array.CreateInstance(elementType, values?.Length ?? 0);
            for (var i = 0; i < array.Length; i++)
            {
                array.SetValue(values[i], i);
            }

            return array;
        }

        private static void AssertFailure(object bakeResult, string expected)
        {
            Assert.That(GetProperty(GetProperty(bakeResult, "Diagnostic"), "Failure").ToString(), Is.EqualTo(expected));
        }

        private static object GetProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Missing property '{propertyName}' on '{target.GetType().FullName}'.");
            return property.GetValue(target);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on '{target.GetType().FullName}'.");
            field.SetValue(target, value);
        }

        private static object Invoke(object target, string methodName, Type[] parameterTypes, params object[] args)
        {
            var type = target as Type ?? target.GetType();
            var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static, null, parameterTypes, null);
            Assert.That(method, Is.Not.Null, $"Missing method '{methodName}' on '{type.FullName}'.");
            return method.Invoke(target is Type ? null : target, args);
        }

        private static Type ResolveType(string fullTypeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullTypeName, false);
                if (type != null)
                {
                    return type;
                }
            }

            Assert.Fail($"Could not find production type '{fullTypeName}'.");
            return null;
        }

        private static Type GraphType => ResolveType("EchoProtocol.AI.Stalker.Spatial.NavMeshSpatialGraph");
        private static Type NodeType => ResolveType("EchoProtocol.AI.Stalker.Spatial.SpatialNode");
        private static Type RegionGraphType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RegionGraph");
        private static Type RegionNodeType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RegionNode");
        private static Type RegionEdgeType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RegionEdge");
        private static Type BakeUtilityType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RegionGraphBakeUtility");
        private static Type DefinitionType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RegionDefinitionBakeData");
        private static Type DefinitionArrayType => DefinitionType.MakeArrayType();
        private static Type EdgeBakeType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RegionEdgeBakeData");
        private static Type EdgeArrayType => EdgeBakeType.MakeArrayType();
        private static Type CompatibilityIdentityType => ResolveType("EchoProtocol.AI.Stalker.Spatial.SpatialGraphCompatibilityIdentity");
        private static Type RegionGraphAssetType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RegionGraphAsset");
        private static Type RegionRecordType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RegionRecord");
        private static Type RegionRecordArrayType => RegionRecordType.MakeArrayType();
        private static Type RegionEdgeRecordType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RegionEdgeRecord");
        private static Type NodeRegionRecordType => ResolveType("EchoProtocol.AI.Stalker.Spatial.NodeRegionRecord");
        private static Type NodeRegionRecordArrayType => NodeRegionRecordType.MakeArrayType();
    }
}
