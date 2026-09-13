using System;
using System.Collections.Generic;
using System.Reflection;
using EchoProtocol.AI.Common.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class RoomSweepPlannerTests
    {
        [Test]
        public void STK_RoomSweepPlanner_BeginRegion_AcceptsRoomAndRegistersOnlyThatRegionProbes()
        {
            var spatialGraph = CreateSpatialGraph(
                Node(0, 1),
                Node(1, 0),
                Node(2, 3),
                Node(3, 2));
            var regionSeven = new RegionId(7);
            var regionEight = new RegionId(8);
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { regionSeven, regionSeven, regionEight, regionEight },
                RoomRegion(regionSeven, 2, "Zone01/RoomA"),
                RoomRegion(regionEight, 2, "Zone01/RoomA"));
            var memory = CreateMemory();
            var planner = CreatePlanner(spatialGraph, regionGraph, memory);

            Assert.That(TryBeginRegion(planner, regionSeven), Is.True);

            Assert.That(GetRegionIdProperty(planner, "CurrentRegionId"), Is.EqualTo(regionSeven));
            Assert.That(GetMemoryInt(memory, "GetTotalProbeCount", regionSeven), Is.EqualTo(2));
            Assert.That(GetMemoryInt(memory, "GetTotalProbeCount", regionEight), Is.EqualTo(0));
            Assert.That(TryGetUnobservedProbeNodeIds(memory, regionSeven, out var probes), Is.True);
            Assert.That(probes, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(GetMemoryInt(memory, "GetObservedCount", regionSeven), Is.EqualTo(0));
            Assert.That(IsRegionCleared(memory, regionSeven), Is.False);
        }

        [Test]
        public void STK_RoomSweepPlanner_BeginRegion_RejectsNonRoomLegacyInvalidAndMissingRegions()
        {
            var spatialGraph = CreateSpatialGraph(Node(0), Node(1), Node(2), Node(3));
            var room = new RegionId(1);
            var route = new RegionId(2);
            var isolated = new RegionId(3);
            var legacy = new RegionId(4);
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { room, route, isolated, legacy },
                RoomRegion(room, 1, "Zone01/Room"),
                SemanticRegion(route, 2, "Zone01/Route", "Zone01", "Route"),
                SemanticRegion(isolated, 3, "Isolated", "Isolated", "IsolatedIsland"),
                LegacyRegion(legacy));
            var planner = CreatePlanner(spatialGraph, regionGraph, CreateMemory());

            Assert.That(TryBeginRegion(planner, route), Is.False);
            Assert.That(GetBoolProperty(planner, "HasActiveRegion"), Is.False);
            Assert.That(TryBeginRegion(planner, isolated), Is.False);
            Assert.That(GetBoolProperty(planner, "HasActiveRegion"), Is.False);
            Assert.That(TryBeginRegion(planner, legacy), Is.False);
            Assert.That(GetBoolProperty(planner, "HasActiveRegion"), Is.False);
            Assert.That(TryBeginRegion(planner, RegionId.Invalid), Is.False);
            Assert.That(GetBoolProperty(planner, "HasActiveRegion"), Is.False);
            Assert.That(TryBeginRegion(planner, new RegionId(99)), Is.False);
            Assert.That(GetBoolProperty(planner, "HasActiveRegion"), Is.False);
        }

        [Test]
        public void STK_RoomSweepPlanner_SelectNextProbe_IgnoresObservedAndRejectedWithDeterministicHopCost()
        {
            var spatialGraph = CreateSpatialGraph(
                Node(0, 1, 2),
                Node(1, 0),
                Node(2, 0, 3),
                Node(3, 2));
            var region = new RegionId(1);
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { RegionId.Invalid, region, region, region },
                RoomRegion(region, 1, "Zone01/Room"));
            var memory = CreateMemory();
            var planner = CreatePlanner(spatialGraph, regionGraph, memory);
            Assert.That(TryBeginRegion(planner, region), Is.True);
            Assert.That(MarkObserved(memory, region, 1), Is.True);

            Assert.That(TrySelectNextProbe(planner, 0, out var firstTarget), Is.True);
            Assert.That(firstTarget, Is.EqualTo(2));

            Assert.That(RejectProbe(planner, 2), Is.True);
            Assert.That(RejectProbe(planner, 2), Is.False);
            Assert.That(GetIntProperty(planner, "RejectedProbeCount"), Is.EqualTo(1));
            Assert.That(GetMemoryInt(memory, "GetObservedCount", region), Is.EqualTo(1));
            Assert.That(GetMemoryInt(memory, "GetRemainingProbeCount", region), Is.EqualTo(2));

            Assert.That(TrySelectNextProbe(planner, 0, out var secondTarget), Is.True);
            Assert.That(secondTarget, Is.EqualTo(3));
        }

        [Test]
        public void STK_RoomSweepPlanner_EqualCostCandidates_SelectLowestNodeId()
        {
            var spatialGraph = CreateSpatialGraph(
                Node(0, 1, 2),
                Node(1, 0),
                Node(2, 0));
            var region = new RegionId(1);
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { RegionId.Invalid, region, region },
                RoomRegion(region, 1, "Zone01/Room"));
            var planner = CreatePlanner(spatialGraph, regionGraph, CreateMemory());
            Assert.That(TryBeginRegion(planner, region), Is.True);

            Assert.That(TrySelectNextProbe(planner, 0, out var target), Is.True);

            Assert.That(target, Is.EqualTo(1));
        }

        [Test]
        public void STK_RoomSweepPlanner_InvalidCurrentNode_SelectsLowestEligibleProbe()
        {
            var spatialGraph = CreateSpatialGraph(Node(0), Node(1), Node(2), Node(3), Node(4), Node(5), Node(6), Node(7));
            var region = new RegionId(1);
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { RegionId.Invalid, RegionId.Invalid, RegionId.Invalid, RegionId.Invalid, RegionId.Invalid, region, RegionId.Invalid, region },
                RoomRegion(region, 1, "Zone01/Room"));
            var planner = CreatePlanner(spatialGraph, regionGraph, CreateMemory());
            Assert.That(TryBeginRegion(planner, region), Is.True);

            Assert.That(TrySelectNextProbe(planner, -1, out var target), Is.True);

            Assert.That(target, Is.EqualTo(5));
        }

        [Test]
        public void STK_RoomSweepPlanner_RejectionsClearWhenStartingDifferentRoom()
        {
            var spatialGraph = CreateSpatialGraph(
                Node(0, 1),
                Node(1, 0),
                Node(2, 3),
                Node(3, 2));
            var regionSeven = new RegionId(7);
            var regionEight = new RegionId(8);
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { regionSeven, regionSeven, regionEight, regionEight },
                RoomRegion(regionSeven, 2, "Zone01/RoomA"),
                RoomRegion(regionEight, 3, "Zone01/RoomB"));
            var planner = CreatePlanner(spatialGraph, regionGraph, CreateMemory());
            Assert.That(TryBeginRegion(planner, regionSeven), Is.True);
            Assert.That(RejectProbe(planner, 0), Is.True);
            Assert.That(GetIntProperty(planner, "RejectedProbeCount"), Is.EqualTo(1));

            Assert.That(TryBeginRegion(planner, regionEight), Is.True);

            Assert.That(GetIntProperty(planner, "RejectedProbeCount"), Is.EqualTo(0));
            Assert.That(TrySelectNextProbe(planner, -1, out var target), Is.True);
            Assert.That(target, Is.EqualTo(2));
        }

        [Test]
        public void STK_RoomSweepPlanner_ReEnteringSameRoom_PreservesObservationsAndClearsRejections()
        {
            var spatialGraph = CreateSpatialGraph(Node(0, 1), Node(1, 0));
            var region = new RegionId(1);
            var regionGraph = CreateRegionGraph(spatialGraph, new[] { region, region }, RoomRegion(region, 1, "Zone01/Room"));
            var memory = CreateMemory();
            var planner = CreatePlanner(spatialGraph, regionGraph, memory);
            Assert.That(TryBeginRegion(planner, region), Is.True);
            Assert.That(MarkObserved(memory, region, 0), Is.True);
            Assert.That(RejectProbe(planner, 1), Is.True);

            Assert.That(TryBeginRegion(planner, region), Is.True);

            Assert.That(GetMemoryInt(memory, "GetObservedCount", region), Is.EqualTo(1));
            Assert.That(GetIntProperty(planner, "RejectedProbeCount"), Is.EqualTo(0));
            Assert.That(TrySelectNextProbe(planner, 0, out var target), Is.True);
            Assert.That(target, Is.EqualTo(1));
        }

        [Test]
        public void STK_RoomSweepPlanner_FullyObservedAndExhausted_AreDistinct()
        {
            var spatialGraph = CreateSpatialGraph(Node(0, 1), Node(1, 0), Node(2));
            var room = new RegionId(1);
            var emptyRoom = new RegionId(2);
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { room, room, RegionId.Invalid },
                RoomRegion(room, 1, "Zone01/Room"),
                RoomRegion(emptyRoom, 2, "Zone01/EmptyRoom"));
            var memory = CreateMemory();
            var planner = CreatePlanner(spatialGraph, regionGraph, memory);

            Assert.That(TryBeginRegion(planner, room), Is.True);
            Assert.That(GetBoolProperty(planner, "IsFullyObserved"), Is.False);
            Assert.That(GetBoolProperty(planner, "IsExhausted"), Is.False);

            Assert.That(RejectProbe(planner, 0), Is.True);
            Assert.That(RejectProbe(planner, 1), Is.True);
            Assert.That(GetBoolProperty(planner, "IsFullyObserved"), Is.False);
            Assert.That(GetBoolProperty(planner, "IsExhausted"), Is.True);

            ClearRejectedProbes(planner);
            Assert.That(MarkObserved(memory, room, new[] { 0, 1 }), Is.EqualTo(2));
            Assert.That(GetBoolProperty(planner, "IsFullyObserved"), Is.True);
            Assert.That(GetBoolProperty(planner, "IsExhausted"), Is.False);
            Assert.That(IsRegionCleared(memory, room), Is.False);

            Assert.That(TryBeginRegion(planner, emptyRoom), Is.True);
            Assert.That(GetBoolProperty(planner, "IsFullyObserved"), Is.False);
            Assert.That(GetBoolProperty(planner, "IsExhausted"), Is.False);
        }

        [Test]
        public void STK_RoomSweepPlanner_RejectProbe_AcceptsOnlyActiveRegisteredUnobservedProbe()
        {
            var spatialGraph = CreateSpatialGraph(Node(0), Node(1), Node(2));
            var regionOne = new RegionId(1);
            var regionTwo = new RegionId(2);
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { regionOne, regionTwo, RegionId.Invalid },
                RoomRegion(regionOne, 1, "Zone01/RoomA"),
                RoomRegion(regionTwo, 2, "Zone01/RoomB"));
            var memory = CreateMemory();
            var planner = CreatePlanner(spatialGraph, regionGraph, memory);
            Assert.That(TryBeginRegion(planner, regionOne), Is.True);
            Assert.That(MarkObserved(memory, regionOne, 0), Is.True);

            Assert.That(RejectProbe(planner, 0), Is.False);
            Assert.That(RejectProbe(planner, 1), Is.False);
            Assert.That(RejectProbe(planner, 2), Is.False);
            Assert.That(GetMemoryInt(memory, "GetObservedCount", regionOne), Is.EqualTo(1));
        }

        [Test]
        public void STK_RoomSweepPlanner_SeededProbeSelection_IsRepeatableAndVariesAcrossSeeds()
        {
            var spatialGraph = CreateSpatialGraph(
                Node(0, 1, 2, 3),
                Node(1, 0),
                Node(2, 0),
                Node(3, 0));
            var room = new RegionId(1);
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { RegionId.Invalid, room, room, room },
                RoomRegion(room, 1, "Zone01/Room"));

            var constructor =
                RoomSweepPlannerType.GetConstructor(
                    new[]
                    {
                        GraphType,
                        RegionGraphType,
                        RoomSweepCoverageMemoryType,
                        typeof(int)
                    });

            Assert.That(
                constructor,
                Is.Not.Null,
                "Seeded RoomSweepPlanner constructor is missing.");

            var firstPlanner = constructor.Invoke(new object[]
            {
                spatialGraph,
                regionGraph,
                CreateMemory(),
                7
            });
            var secondPlanner = constructor.Invoke(new object[]
            {
                spatialGraph,
                regionGraph,
                CreateMemory(),
                7
            });

            Assert.That(TryBeginRegion(firstPlanner, room), Is.True);
            Assert.That(TryBeginRegion(secondPlanner, room), Is.True);
            Assert.That(TrySelectNextProbe(firstPlanner, 0, out var firstTarget), Is.True);
            Assert.That(TrySelectNextProbe(secondPlanner, 0, out var secondTarget), Is.True);
            Assert.That(secondTarget, Is.EqualTo(firstTarget));

            var validProbeIds = new HashSet<int> { 1, 2, 3 };
            var selectedProbeIds = new HashSet<int>();
            for (var seed = 0; seed < 64; seed++)
            {
                var planner = constructor.Invoke(new object[]
                {
                    spatialGraph,
                    regionGraph,
                    CreateMemory(),
                    seed
                });

                Assert.That(TryBeginRegion(planner, room), Is.True);
                Assert.That(TrySelectNextProbe(planner, 0, out var target), Is.True);
                Assert.That(validProbeIds.Contains(target), Is.True);
                selectedProbeIds.Add(target);
            }

            Assert.That(selectedProbeIds.Count, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void STK_RoomSweepPlanner_ConfigureVariation_PreservesActiveRoomAndRejectedProbeState()
        {
            var spatialGraph = CreateSpatialGraph(
                Node(0, 1, 2, 3),
                Node(1, 0),
                Node(2, 0),
                Node(3, 0));
            var room = new RegionId(1);
            var regionGraph = CreateRegionGraph(
                spatialGraph,
                new[] { RegionId.Invalid, room, room, room },
                RoomRegion(room, 1, "Zone01/Room"));
            var planner = CreatePlanner(
                spatialGraph,
                regionGraph,
                CreateMemory());

            Assert.That(TryBeginRegion(planner, room), Is.True);
            Assert.That(RejectProbe(planner, 1), Is.True);

            var configureMethod =
                RoomSweepPlannerType.GetMethod(
                    "ConfigureVariation",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(int) },
                    null);

            Assert.That(
                configureMethod,
                Is.Not.Null,
                "RoomSweepPlanner.ConfigureVariation(int) is missing.");

            configureMethod.Invoke(
                planner,
                new object[] { 123 });

            Assert.That(GetRegionIdProperty(planner, "CurrentRegionId"), Is.EqualTo(room));
            Assert.That(GetBoolProperty(planner, "HasActiveRegion"), Is.True);
            Assert.That(GetIntProperty(planner, "RejectedProbeCount"), Is.EqualTo(1));
            Assert.That(TrySelectNextProbe(planner, 0, out var target), Is.True);
            Assert.That(target, Is.Not.EqualTo(1));
            Assert.That(target == 2 || target == 3, Is.True);
        }

        private static object CreatePlanner(object spatialGraph, object regionGraph, object memory)
        {
            return Activator.CreateInstance(RoomSweepPlannerType, spatialGraph, regionGraph, memory);
        }

        private static object CreateMemory()
        {
            return Activator.CreateInstance(RoomSweepCoverageMemoryType);
        }

        private static bool TryBeginRegion(object planner, RegionId regionId)
        {
            return (bool)Invoke(planner, "TryBeginRegion", new[] { typeof(RegionId) }, regionId);
        }

        private static bool TrySelectNextProbe(object planner, int currentSpatialNodeId, out int targetSpatialNodeId)
        {
            var args = new object[] { currentSpatialNodeId, -1 };
            var result = (bool)Invoke(planner, "TrySelectNextProbe", new[] { typeof(int), typeof(int).MakeByRefType() }, args);
            targetSpatialNodeId = (int)args[1];
            return result;
        }

        private static bool RejectProbe(object planner, int spatialNodeId)
        {
            return (bool)Invoke(planner, "RejectProbe", new[] { typeof(int) }, spatialNodeId);
        }

        private static void ClearRejectedProbes(object planner)
        {
            Invoke(planner, "ClearRejectedProbes", Type.EmptyTypes);
        }

        private static bool MarkObserved(object memory, RegionId regionId, int nodeId)
        {
            return (bool)Invoke(memory, "MarkObserved", new[] { typeof(RegionId), typeof(int) }, regionId, nodeId);
        }

        private static int MarkObserved(object memory, RegionId regionId, IReadOnlyCollection<int> nodeIds)
        {
            return (int)Invoke(memory, "MarkObserved", new[] { typeof(RegionId), typeof(IReadOnlyCollection<int>) }, regionId, nodeIds);
        }

        private static int GetMemoryInt(object memory, string methodName, RegionId regionId)
        {
            return (int)Invoke(memory, methodName, new[] { typeof(RegionId) }, regionId);
        }

        private static bool IsRegionCleared(object memory, RegionId regionId)
        {
            return (bool)Invoke(memory, "IsRegionCleared", new[] { typeof(RegionId) }, regionId);
        }

        private static bool TryGetUnobservedProbeNodeIds(object memory, RegionId regionId, out IReadOnlyList<int> nodeIds)
        {
            var args = new object[] { regionId, null };
            var result = (bool)Invoke(memory, "TryGetUnobservedProbeNodeIds", new[] { typeof(RegionId), typeof(IReadOnlyList<int>).MakeByRefType() }, args);
            nodeIds = (IReadOnlyList<int>)args[1];
            return result;
        }

        private static bool GetBoolProperty(object target, string propertyName)
        {
            return (bool)GetProperty(target, propertyName);
        }

        private static int GetIntProperty(object target, string propertyName)
        {
            return (int)GetProperty(target, propertyName);
        }

        private static RegionId GetRegionIdProperty(object target, string propertyName)
        {
            return (RegionId)GetProperty(target, propertyName);
        }

        private static object CreateSpatialGraph(params object[] nodes)
        {
            return Activator.CreateInstance(GraphType, ToArray(NodeType, nodes));
        }

        private static object CreateRegionGraph(object spatialGraph, IReadOnlyList<RegionId> nodeToRegionMap, params object[] regionNodes)
        {
            return Activator.CreateInstance(
                RegionGraphType,
                ToArray(RegionNodeType, regionNodes),
                nodeToRegionMap,
                GetProperty(spatialGraph, "CompatibilityIdentity"),
                1);
        }

        private static object Node(int id, params int[] neighbors)
        {
            return Activator.CreateInstance(
                NodeType,
                id,
                new Vector3(id, 0f, 0f),
                0,
                id,
                id * 3,
                id * 3 + 1,
                id * 3 + 2,
                new List<int>(neighbors));
        }

        private static object RoomRegion(RegionId regionId, int sourceIndex, string sourcePath)
        {
            return SemanticRegion(regionId, sourceIndex, sourcePath, "Zone01", "Room");
        }

        private static object SemanticRegion(RegionId regionId, int sourceIndex, string sourcePath, string zone, string kind)
        {
            return Activator.CreateInstance(
                RegionNodeType,
                regionId,
                Array.CreateInstance(RegionEdgeType, 0),
                Metadata(sourceIndex, sourcePath, zone, kind));
        }

        private static object LegacyRegion(RegionId regionId)
        {
            return Activator.CreateInstance(RegionNodeType, regionId, Array.CreateInstance(RegionEdgeType, 0));
        }

        private static object Metadata(int sourceIndex, string sourcePath, string zone, string kind)
        {
            return Activator.CreateInstance(
                RegionSemanticMetadataType,
                sourceIndex,
                sourcePath,
                Enum.Parse(RegionSemanticZoneType, zone),
                Enum.Parse(RegionSemanticKindType, kind));
        }

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
        private static Type RegionSemanticMetadataType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RegionSemanticMetadata");
        private static Type RegionSemanticZoneType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RegionSemanticZone");
        private static Type RegionSemanticKindType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RegionSemanticKind");
        private static Type RoomSweepCoverageMemoryType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RoomSweepCoverageMemory");
        private static Type RoomSweepPlannerType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RoomSweepPlanner");
    }
}
