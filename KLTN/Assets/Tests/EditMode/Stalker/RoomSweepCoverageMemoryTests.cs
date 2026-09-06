using System;
using System.Collections.Generic;
using System.Reflection;
using EchoProtocol.AI.Common.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class RoomSweepCoverageMemoryTests
    {
        [Test]
        public void STK_RoomSweep_RegionGraphInverseLookup_ReturnsSortedNodeIds()
        {
            var graph = CreateRegionGraph(new[]
            {
                RegionId.Invalid,
                new RegionId(1),
                RegionId.Invalid,
                new RegionId(1),
                RegionId.Invalid,
                new RegionId(2),
                RegionId.Invalid,
                new RegionId(2)
            });

            Assert.That(TryGetSpatialNodeIdsForRegion(graph, new RegionId(1), out var regionOneNodeIds), Is.True);
            Assert.That(regionOneNodeIds, Is.EqualTo(new[] { 1, 3 }));
            Assert.That(TryGetSpatialNodeIdsForRegion(graph, new RegionId(2), out var regionTwoNodeIds), Is.True);
            Assert.That(regionTwoNodeIds, Is.EqualTo(new[] { 5, 7 }));
        }

        [Test]
        public void STK_RoomSweep_RegionGraphInverseLookup_MissingOrInvalidRegionReturnsEmpty()
        {
            var graph = CreateRegionGraph(new[] { new RegionId(1) });

            Assert.That(TryGetSpatialNodeIdsForRegion(graph, RegionId.Invalid, out var invalidNodeIds), Is.False);
            Assert.That(invalidNodeIds, Is.Empty);
            Assert.That(TryGetSpatialNodeIdsForRegion(graph, new RegionId(9), out var missingNodeIds), Is.False);
            Assert.That(missingNodeIds, Is.Empty);
        }

        [Test]
        public void STK_RoomSweep_RegisterRegion_DeduplicatesSortsAndRejectsInvalidProbes()
        {
            var memory = CreateMemory();

            RegisterRegion(memory, new RegionId(1), 4, -1, 2, 4, 1);

            Assert.That(GetInt(memory, "GetTotalProbeCount", new RegionId(1)), Is.EqualTo(3));
            Assert.That(TryGetUnobservedProbeNodeIds(memory, new RegionId(1), out var nodeIds), Is.True);
            Assert.That(nodeIds, Is.EqualTo(new[] { 1, 2, 4 }));
        }

        [Test]
        public void STK_RoomSweep_RegisterRegion_InvalidRegionDoesNotCreateState()
        {
            var memory = CreateMemory();

            RegisterRegion(memory, RegionId.Invalid, 1, 2);

            Assert.That(GetInt(memory, "GetTotalProbeCount", RegionId.Invalid), Is.EqualTo(0));
            Assert.That(TryGetUnobservedProbeNodeIds(memory, RegionId.Invalid, out var nodeIds), Is.False);
            Assert.That(nodeIds, Is.Empty);
        }

        [Test]
        public void STK_RoomSweep_MarkObserved_AcceptsOnlyRegisteredProbeIds()
        {
            var memory = CreateMemory();
            RegisterRegion(memory, new RegionId(1), 1, 2);
            RegisterRegion(memory, new RegionId(2), 3);

            Assert.That(MarkObserved(memory, new RegionId(1), 1), Is.True);
            Assert.That(MarkObserved(memory, new RegionId(1), 3), Is.False);
            Assert.That(MarkObserved(memory, new RegionId(3), 1), Is.False);
            Assert.That(IsObserved(memory, new RegionId(1), 1), Is.True);
            Assert.That(IsObserved(memory, new RegionId(2), 3), Is.False);
        }

        [Test]
        public void STK_RoomSweep_MarkObserved_IsIdempotent()
        {
            var memory = CreateMemory();
            RegisterRegion(memory, new RegionId(1), 1, 2);

            Assert.That(MarkObserved(memory, new RegionId(1), 1), Is.True);
            Assert.That(MarkObserved(memory, new RegionId(1), 1), Is.False);

            Assert.That(GetInt(memory, "GetObservedCount", new RegionId(1)), Is.EqualTo(1));
            Assert.That(GetInt(memory, "GetRemainingProbeCount", new RegionId(1)), Is.EqualTo(1));
        }

        [Test]
        public void STK_RoomSweep_RegionCoverage_IsIndependentPerRegion()
        {
            var memory = CreateMemory();
            RegisterRegion(memory, new RegionId(1), 1, 2);
            RegisterRegion(memory, new RegionId(2), 3, 4);

            MarkObserved(memory, new RegionId(1), 1);

            Assert.That(GetInt(memory, "GetObservedCount", new RegionId(1)), Is.EqualTo(1));
            Assert.That(GetInt(memory, "GetObservedCount", new RegionId(2)), Is.EqualTo(0));
            Assert.That(GetInt(memory, "GetRemainingProbeCount", new RegionId(2)), Is.EqualTo(2));
        }

        [Test]
        public void STK_RoomSweep_CountsAndCoverageRatio_AreBasedOnRegisteredProbes()
        {
            var memory = CreateMemory();
            var regionId = new RegionId(1);
            RegisterRegion(memory, regionId, 1, 2, 3, 4);

            Assert.That(GetInt(memory, "GetObservedCount", regionId), Is.EqualTo(0));
            Assert.That(GetInt(memory, "GetTotalProbeCount", regionId), Is.EqualTo(4));
            Assert.That(GetInt(memory, "GetRemainingProbeCount", regionId), Is.EqualTo(4));
            Assert.That(GetFloat(memory, "GetCoverage01", regionId), Is.EqualTo(0f));

            MarkObserved(memory, regionId, 1);

            Assert.That(GetInt(memory, "GetObservedCount", regionId), Is.EqualTo(1));
            Assert.That(GetInt(memory, "GetRemainingProbeCount", regionId), Is.EqualTo(3));
            Assert.That(GetFloat(memory, "GetCoverage01", regionId), Is.EqualTo(0.25f));

            MarkObserved(memory, regionId, new[] { 2, 3, 4 });

            Assert.That(GetInt(memory, "GetObservedCount", regionId), Is.EqualTo(4));
            Assert.That(GetInt(memory, "GetRemainingProbeCount", regionId), Is.EqualTo(0));
            Assert.That(GetFloat(memory, "GetCoverage01", regionId), Is.EqualTo(1f));
        }

        [Test]
        public void STK_RoomSweep_ZeroProbeRegion_IsNotAutomaticallyCoveredOrCleared()
        {
            var memory = CreateMemory();
            var regionId = new RegionId(1);

            RegisterRegion(memory, regionId);

            Assert.That(GetInt(memory, "GetTotalProbeCount", regionId), Is.EqualTo(0));
            Assert.That(GetFloat(memory, "GetCoverage01", regionId), Is.EqualTo(0f));
            Assert.That(IsRegionCleared(memory, regionId), Is.False);
        }

        [Test]
        public void STK_RoomSweep_FullCoverage_DoesNotAutomaticallyClearRegion()
        {
            var memory = CreateMemory();
            var regionId = new RegionId(1);
            RegisterRegion(memory, regionId, 1, 2);

            MarkObserved(memory, regionId, new[] { 1, 2 });

            Assert.That(GetFloat(memory, "GetCoverage01", regionId), Is.EqualTo(1f));
            Assert.That(IsRegionCleared(memory, regionId), Is.False);
        }

        [Test]
        public void STK_RoomSweep_MarkRegionCleared_IsExplicit()
        {
            var memory = CreateMemory();
            var regionId = new RegionId(1);
            RegisterRegion(memory, regionId, 1);

            MarkRegionCleared(memory, regionId);

            Assert.That(IsRegionCleared(memory, regionId), Is.True);
        }

        [Test]
        public void STK_RoomSweep_ResetRegion_ClearsOneRegionOnly()
        {
            var memory = CreateMemory();
            var regionOne = new RegionId(1);
            var regionTwo = new RegionId(2);
            RegisterRegion(memory, regionOne, 1, 2);
            RegisterRegion(memory, regionTwo, 3, 4);
            MarkObserved(memory, regionOne, 1);
            MarkObserved(memory, regionTwo, 3);
            MarkRegionCleared(memory, regionOne);
            MarkRegionCleared(memory, regionTwo);

            ResetRegion(memory, regionOne);

            Assert.That(GetInt(memory, "GetTotalProbeCount", regionOne), Is.EqualTo(2));
            Assert.That(GetInt(memory, "GetObservedCount", regionOne), Is.EqualTo(0));
            Assert.That(IsRegionCleared(memory, regionOne), Is.False);
            Assert.That(GetInt(memory, "GetObservedCount", regionTwo), Is.EqualTo(1));
            Assert.That(IsRegionCleared(memory, regionTwo), Is.True);
        }

        [Test]
        public void STK_RoomSweep_Reset_ClearsAllCoverageState()
        {
            var memory = CreateMemory();
            RegisterRegion(memory, new RegionId(1), 1, 2);
            MarkObserved(memory, new RegionId(1), 1);
            MarkRegionCleared(memory, new RegionId(1));

            Invoke(memory, "Reset", Type.EmptyTypes);

            Assert.That(GetInt(memory, "GetTotalProbeCount", new RegionId(1)), Is.EqualTo(0));
            Assert.That(GetInt(memory, "GetObservedCount", new RegionId(1)), Is.EqualTo(0));
            Assert.That(IsRegionCleared(memory, new RegionId(1)), Is.False);
            Assert.That(TryGetUnobservedProbeNodeIds(memory, new RegionId(1), out var nodeIds), Is.False);
            Assert.That(nodeIds, Is.Empty);
        }

        [Test]
        public void STK_RoomSweep_ReRegisterRegion_ReplacesProbesPreservesIntersectingObservationsAndResetsCleared()
        {
            var memory = CreateMemory();
            var regionId = new RegionId(1);
            RegisterRegion(memory, regionId, 1, 2, 3);
            MarkObserved(memory, regionId, 2);
            MarkObserved(memory, regionId, 3);
            MarkRegionCleared(memory, regionId);

            RegisterRegion(memory, regionId, 2, 4, 4, -1);

            Assert.That(GetInt(memory, "GetTotalProbeCount", regionId), Is.EqualTo(2));
            Assert.That(GetInt(memory, "GetObservedCount", regionId), Is.EqualTo(1));
            Assert.That(IsObserved(memory, regionId, 2), Is.True);
            Assert.That(IsObserved(memory, regionId, 3), Is.False);
            Assert.That(IsRegionCleared(memory, regionId), Is.False);
            Assert.That(TryGetUnobservedProbeNodeIds(memory, regionId, out var unobserved), Is.True);
            Assert.That(unobserved, Is.EqualTo(new[] { 4 }));
        }

        [Test]
        public void STK_RoomSweep_RegisterRegion_DoesNotTransferProbeOwnedByAnotherRegion()
        {
            var memory = CreateMemory();
            RegisterRegion(memory, new RegionId(1), 1, 2);

            RegisterRegion(memory, new RegionId(2), 2, 3);

            Assert.That(GetInt(memory, "GetTotalProbeCount", new RegionId(1)), Is.EqualTo(2));
            Assert.That(GetInt(memory, "GetTotalProbeCount", new RegionId(2)), Is.EqualTo(1));
            Assert.That(TryGetUnobservedProbeNodeIds(memory, new RegionId(2), out var regionTwoNodes), Is.True);
            Assert.That(regionTwoNodes, Is.EqualTo(new[] { 3 }));
        }

        private static object CreateMemory()
        {
            return Activator.CreateInstance(RoomSweepCoverageMemoryType);
        }

        private static void RegisterRegion(object memory, RegionId regionId, params int[] nodeIds)
        {
            Invoke(memory, "RegisterRegion", new[] { typeof(RegionId), typeof(IReadOnlyCollection<int>) }, regionId, nodeIds);
        }

        private static bool MarkObserved(object memory, RegionId regionId, int nodeId)
        {
            return (bool)Invoke(memory, "MarkObserved", new[] { typeof(RegionId), typeof(int) }, regionId, nodeId);
        }

        private static int MarkObserved(object memory, RegionId regionId, IReadOnlyCollection<int> nodeIds)
        {
            return (int)Invoke(memory, "MarkObserved", new[] { typeof(RegionId), typeof(IReadOnlyCollection<int>) }, regionId, nodeIds);
        }

        private static bool IsObserved(object memory, RegionId regionId, int nodeId)
        {
            return (bool)Invoke(memory, "IsObserved", new[] { typeof(RegionId), typeof(int) }, regionId, nodeId);
        }

        private static int GetInt(object memory, string methodName, RegionId regionId)
        {
            return (int)Invoke(memory, methodName, new[] { typeof(RegionId) }, regionId);
        }

        private static float GetFloat(object memory, string methodName, RegionId regionId)
        {
            return (float)Invoke(memory, methodName, new[] { typeof(RegionId) }, regionId);
        }

        private static bool IsRegionCleared(object memory, RegionId regionId)
        {
            return (bool)Invoke(memory, "IsRegionCleared", new[] { typeof(RegionId) }, regionId);
        }

        private static void MarkRegionCleared(object memory, RegionId regionId)
        {
            Invoke(memory, "MarkRegionCleared", new[] { typeof(RegionId) }, regionId);
        }

        private static void ResetRegion(object memory, RegionId regionId)
        {
            Invoke(memory, "ResetRegion", new[] { typeof(RegionId) }, regionId);
        }

        private static bool TryGetUnobservedProbeNodeIds(object memory, RegionId regionId, out IReadOnlyList<int> nodeIds)
        {
            var args = new object[] { regionId, null };
            var result = (bool)Invoke(memory, "TryGetUnobservedProbeNodeIds", new[] { typeof(RegionId), typeof(IReadOnlyList<int>).MakeByRefType() }, args);
            nodeIds = (IReadOnlyList<int>)args[1];
            return result;
        }

        private static bool TryGetSpatialNodeIdsForRegion(object graph, RegionId regionId, out IReadOnlyList<int> nodeIds)
        {
            var args = new object[] { regionId, null };
            var result = (bool)Invoke(graph, "TryGetSpatialNodeIdsForRegion", new[] { typeof(RegionId), typeof(IReadOnlyList<int>).MakeByRefType() }, args);
            nodeIds = (IReadOnlyList<int>)args[1];
            return result;
        }

        private static object CreateRegionGraph(IReadOnlyList<RegionId> nodeToRegionMap)
        {
            return Activator.CreateInstance(
                RegionGraphType,
                RegionNodeArray(
                    RegionNode(new RegionId(1)),
                    RegionNode(new RegionId(2))),
                nodeToRegionMap,
                CreateCompatibilityIdentity(nodeToRegionMap.Count),
                1);
        }

        private static object CreateCompatibilityIdentity(int nodeCount)
        {
            var nodes = Array.CreateInstance(NodeType, nodeCount);
            for (var i = 0; i < nodeCount; i++)
            {
                nodes.SetValue(Node(i, new Vector3(i, 0f, 0f)), i);
            }

            var graph = Activator.CreateInstance(GraphType, nodes);
            return GetProperty(graph, "CompatibilityIdentity");
        }

        private static object Node(int id, Vector3 position, params int[] neighbors)
        {
            return Activator.CreateInstance(NodeType, id, position, 0, id, id * 3, id * 3 + 1, id * 3 + 2, new List<int>(neighbors));
        }

        private static object RegionNode(RegionId regionId)
        {
            return Activator.CreateInstance(RegionNodeType, regionId, RegionEdgeArray());
        }

        private static Array RegionNodeArray(params object[] values) => ToArray(RegionNodeType, values);
        private static Array RegionEdgeArray(params object[] values) => ToArray(RegionEdgeType, values);

        private static Array ToArray(Type elementType, object[] values)
        {
            var array = Array.CreateInstance(elementType, values?.Length ?? 0);
            for (var i = 0; i < array.Length; i++)
            {
                array.SetValue(values[i], i);
            }

            return array;
        }

        private static object Invoke(object target, string methodName, Type[] parameterTypes, params object[] args)
        {
            var type = target as Type ?? target.GetType();
            var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static, null, parameterTypes, null);
            Assert.That(method, Is.Not.Null, $"Missing method '{methodName}' on '{type.FullName}'.");
            return method.Invoke(target is Type ? null : target, args);
        }

        private static object GetProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Missing property '{propertyName}' on '{target.GetType().FullName}'.");
            return property.GetValue(target);
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

            var assemblyCSharp = Assembly.Load("Assembly-CSharp");
            var loadedType = assemblyCSharp.GetType(fullTypeName, false);
            if (loadedType != null)
            {
                return loadedType;
            }

            Assert.Fail($"Could not find production type '{fullTypeName}'.");
            return null;
        }

        private static Type GraphType => ResolveType("EchoProtocol.AI.Stalker.Spatial.NavMeshSpatialGraph");
        private static Type NodeType => ResolveType("EchoProtocol.AI.Stalker.Spatial.SpatialNode");
        private static Type RegionGraphType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RegionGraph");
        private static Type RegionNodeType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RegionNode");
        private static Type RegionEdgeType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RegionEdge");
        private static Type RoomSweepCoverageMemoryType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RoomSweepCoverageMemory");
    }
}
