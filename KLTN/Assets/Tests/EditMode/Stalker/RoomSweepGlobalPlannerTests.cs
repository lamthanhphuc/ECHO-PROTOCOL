using System;
using System.Collections.Generic;
using System.Reflection;
using EchoProtocol.AI.Common.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class RoomSweepGlobalPlannerTests
    {
        [Test]
        public void STK_RoomSweepGlobalPlanner_SelectsNearestReachableUnclearedRoom()
        {
            var current = new RegionId(1);
            var nearRoom = new RegionId(2);
            var transit = new RegionId(3);
            var farRoom = new RegionId(4);
            var graph = CreateRegionGraph(
                new[] { current, nearRoom, transit, farRoom },
                RoomRegion(current, 1, "Zone01/Current", nearRoom, transit),
                RoomRegion(nearRoom, 2, "Zone01/Near", current),
                RouteRegion(transit, 3, "Zone01/Route", current, farRoom),
                RoomRegion(farRoom, 4, "Zone01/Far", transit));
            var memory = CreateMemory();
            RegisterRegion(memory, current, 0);
            MarkRegionCleared(memory, current);
            var planner = CreatePlanner(graph, memory);

            Assert.That(TryGetOrCreateObjective(planner, current, out var objective), Is.True);

            AssertObjective(objective, nearRoom, nearRoom);
        }

        [Test]
        public void STK_RoomSweepGlobalPlanner_EqualHopCostRooms_TieBreakByLowerRegionId()
        {
            var current = new RegionId(10);
            var lowerRoom = new RegionId(2);
            var higherRoom = new RegionId(5);
            var graph = CreateRegionGraph(
                new[] { current, lowerRoom, higherRoom },
                RoomRegion(current, 1, "Zone01/Current", higherRoom, lowerRoom),
                RoomRegion(higherRoom, 3, "Zone01/Higher", current),
                RoomRegion(lowerRoom, 2, "Zone01/Lower", current));
            var memory = CreateMemory();
            RegisterRegion(memory, current, 0);
            MarkRegionCleared(memory, current);
            var planner = CreatePlanner(graph, memory);

            Assert.That(TryGetOrCreateObjective(planner, current, out var objective), Is.True);

            AssertObjective(objective, lowerRoom, lowerRoom);
        }

        [Test]
        public void STK_RoomSweepGlobalPlanner_TargetEligibility_SkipsNonRoomsDisabledUnreachableClearedAndRejected()
        {
            var current = new RegionId(1);
            var route = new RegionId(2);
            var isolated = new RegionId(3);
            var legacy = new RegionId(4);
            var disabled = new RegionId(5);
            var unreachable = new RegionId(6);
            var cleared = new RegionId(7);
            var rejected = new RegionId(8);
            var selected = new RegionId(9);
            var graph = CreateRegionGraph(
                new[] { current, route, isolated, legacy, disabled, unreachable, cleared, rejected, selected },
                RoomRegion(current, 1, "Zone01/Current", route, isolated, legacy, disabled, cleared, rejected, selected),
                RouteRegion(route, 2, "Zone01/Route", current),
                IsolatedRegion(isolated, 3, "Isolated", current),
                LegacyRegion(legacy, current),
                RoomRegion(disabled, 5, "Zone01/Disabled", current),
                RoomRegion(unreachable, 6, "Zone01/Unreachable"),
                RoomRegion(cleared, 7, "Zone01/Cleared", current),
                RoomRegion(rejected, 8, "Zone01/Rejected", current),
                RoomRegion(selected, 9, "Zone01/Selected", current));
            SetRegionEnabled(graph, disabled, false);
            var memory = CreateMemory();
            RegisterRegion(memory, cleared, 6);
            MarkRegionCleared(memory, cleared);
            var rejectedSet = new HashSet<RegionId> { rejected };
            var planner = CreatePlanner(graph, memory);

            Assert.That(TryGetOrCreateObjective(planner, current, rejectedSet, out var objective), Is.True);

            AssertObjective(objective, selected, selected);
        }

        [Test]
        public void STK_RoomSweepGlobalPlanner_UnregisteredRoom_IsStillSelectableAsUncleared()
        {
            var current = new RegionId(1);
            var unregisteredRoom = new RegionId(2);
            var graph = CreateRegionGraph(
                new[] { current, unregisteredRoom },
                RoomRegion(current, 1, "Zone01/Current", unregisteredRoom),
                RoomRegion(unregisteredRoom, 2, "Zone01/Unregistered", current));
            var planner = CreatePlanner(graph, CreateMemory());

            Assert.That(TryGetOrCreateObjective(planner, current, out var objective), Is.True);

            AssertObjective(objective, unregisteredRoom, unregisteredRoom);
        }

        [Test]
        public void STK_RoomSweepGlobalPlanner_RoutesCanBeTransitAndObjectivePersistsAcrossRouteHops()
        {
            var roomOne = new RegionId(1);
            var routeTwo = new RegionId(2);
            var routeThree = new RegionId(3);
            var roomFour = new RegionId(4);
            var graph = CreateRegionGraph(
                new[] { roomOne, routeTwo, routeThree, roomFour },
                RoomRegion(roomOne, 1, "Zone01/Room1", routeTwo),
                RouteRegion(routeTwo, 2, "Zone01/Route2", roomOne, routeThree),
                RouteRegion(routeThree, 3, "Zone01/Route3", routeTwo, roomFour),
                RoomRegion(roomFour, 4, "Zone01/Room4", routeThree));
            var memory = CreateMemory();
            RegisterRegion(memory, roomOne, 0);
            MarkRegionCleared(memory, roomOne);
            var planner = CreatePlanner(graph, memory);

            Assert.That(TryGetOrCreateObjective(planner, roomOne, out var fromRoom), Is.True);
            AssertObjective(fromRoom, roomFour, routeTwo);

            Assert.That(TryGetOrCreateObjective(planner, routeTwo, out var fromRouteTwo), Is.True);
            AssertObjective(fromRouteTwo, roomFour, routeThree);

            Assert.That(TryGetOrCreateObjective(planner, routeThree, out var fromRouteThree), Is.True);
            AssertObjective(fromRouteThree, roomFour, roomFour);
        }

        [Test]
        public void STK_RoomSweepGlobalPlanner_TargetReached_ReturnsFalseAndDoesNotImmediatelySelectAnotherRoom()
        {
            var roomOne = new RegionId(1);
            var routeTwo = new RegionId(2);
            var roomThree = new RegionId(3);
            var roomFour = new RegionId(4);
            var graph = CreateRegionGraph(
                new[] { roomOne, routeTwo, roomThree, roomFour },
                RoomRegion(roomOne, 1, "Zone01/Room1", routeTwo),
                RouteRegion(routeTwo, 2, "Zone01/Route2", roomOne, roomThree),
                RoomRegion(roomThree, 3, "Zone01/Room3", routeTwo, roomFour),
                RoomRegion(roomFour, 4, "Zone01/Room4", roomThree));
            var memory = CreateMemory();
            RegisterRegion(memory, roomOne, 0);
            MarkRegionCleared(memory, roomOne);
            var planner = CreatePlanner(graph, memory);
            Assert.That(TryGetOrCreateObjective(planner, roomOne, out var objective), Is.True);
            AssertObjective(objective, roomThree, routeTwo);

            Assert.That(TryGetOrCreateObjective(planner, roomThree, out var reachedObjective), Is.False);

            Assert.That(GetObjectiveBool(reachedObjective, "IsValid"), Is.False);
            Assert.That(GetInvalidationReason(planner), Is.EqualTo("TargetReached"));
        }

        [Test]
        public void STK_RoomSweepGlobalPlanner_ClearedActiveTarget_SelectsAnotherUnclearedRoom()
        {
            var current = new RegionId(1);
            var firstTarget = new RegionId(2);
            var secondTarget = new RegionId(3);
            var graph = CreateRegionGraph(
                new[] { current, firstTarget, secondTarget },
                RoomRegion(current, 1, "Zone01/Current", firstTarget, secondTarget),
                RoomRegion(firstTarget, 2, "Zone01/First", current),
                RoomRegion(secondTarget, 3, "Zone01/Second", current));
            var memory = CreateMemory();
            var planner = CreatePlanner(graph, memory);
            Assert.That(TryGetOrCreateObjective(planner, current, out var first), Is.True);
            AssertObjective(first, firstTarget, firstTarget);
            RegisterRegion(memory, firstTarget, 1);
            MarkRegionCleared(memory, firstTarget);

            Assert.That(TryGetOrCreateObjective(planner, current, out var second), Is.True);

            AssertObjective(second, secondTarget, secondTarget);
            Assert.That(GetInvalidationReason(planner), Is.EqualTo("TargetCleared"));
        }

        [Test]
        public void STK_RoomSweepGlobalPlanner_UnreachableActiveTarget_SelectsAnotherReachableRoom()
        {
            var current = new RegionId(1);
            var route = new RegionId(2);
            var originalTarget = new RegionId(3);
            var fallbackTarget = new RegionId(4);
            var graph = CreateRegionGraph(
                new[] { current, route, originalTarget, fallbackTarget },
                RoomRegion(current, 1, "Zone01/Current", route, fallbackTarget),
                RouteRegion(route, 2, "Zone01/Route", current, originalTarget),
                RoomRegion(originalTarget, 3, "Zone01/Original", route),
                RoomRegion(fallbackTarget, 4, "Zone01/Fallback", current));
            var memory = CreateMemory();
            RegisterRegion(memory, current, 0);
            MarkRegionCleared(memory, current);
            var planner = CreatePlanner(graph, memory);
            Assert.That(TryGetOrCreateObjective(planner, current, out var first), Is.True);
            AssertObjective(first, fallbackTarget, fallbackTarget);
            var rejected = new HashSet<RegionId> { fallbackTarget };
            Assert.That(TryGetOrCreateObjective(planner, current, rejected, out var original), Is.True);
            AssertObjective(original, originalTarget, route);

            SetEdgeOpen(graph, route, originalTarget, false);
            rejected.Clear();
            Assert.That(TryGetOrCreateObjective(planner, route, rejected, out var fallback), Is.True);

            AssertObjective(fallback, fallbackTarget, current);
            Assert.That(GetInvalidationReason(planner), Is.EqualTo("TargetUnreachable"));
        }

        [Test]
        public void STK_RoomSweepGlobalPlanner_NoReachableUnclearedRoom_ReturnsInvalidObjective()
        {
            var current = new RegionId(1);
            var route = new RegionId(2);
            var graph = CreateRegionGraph(
                new[] { current, route },
                RoomRegion(current, 1, "Zone01/Current", route),
                RouteRegion(route, 2, "Zone01/Route", current));
            var planner = CreatePlanner(graph, CreateMemory());

            Assert.That(TryGetOrCreateObjective(planner, current, out var objective), Is.False);

            Assert.That(GetObjectiveBool(objective, "IsValid"), Is.False);
        }

        [Test]
        public void STK_RoomSweepGlobalPlanner_SameSourceIndexRegionsRemainIndependent()
        {
            var current = new RegionId(1);
            var regionSeven = new RegionId(7);
            var regionEight = new RegionId(8);
            var graph = CreateRegionGraph(
                new[] { current, regionSeven, regionEight },
                RoomRegion(current, 1, "Zone01/Current", regionSeven, regionEight),
                RoomRegion(regionSeven, 2, "Zone01/SameSourceA", current),
                RoomRegion(regionEight, 2, "Zone01/SameSourceB", current));
            var memory = CreateMemory();
            RegisterRegion(memory, regionSeven, 1);
            MarkRegionCleared(memory, regionSeven);
            var planner = CreatePlanner(graph, memory);

            Assert.That(TryGetOrCreateObjective(planner, current, out var objective), Is.True);

            AssertObjective(objective, regionEight, regionEight);
        }

        [Test]
        public void STK_RoomSweepGlobalPlanner_CurrentRoomIsNotSelectedAsOwnTarget()
        {
            var current = new RegionId(1);
            var graph = CreateRegionGraph(
                new[] { current },
                RoomRegion(current, 1, "Zone01/OnlyRoom"));
            var planner = CreatePlanner(graph, CreateMemory());

            Assert.That(TryGetOrCreateObjective(planner, current, out var objective), Is.False);

            Assert.That(GetObjectiveBool(objective, "IsValid"), Is.False);
        }

        [Test]
        public void STK_RoomSweepGlobalPlanner_SeededNearOptimalSelection_IsRepeatableAndVariesAcrossSeeds()
        {
            var current = new RegionId(1);
            var nearRoom = new RegionId(2);
            var transit = new RegionId(3);
            var secondRoom = new RegionId(4);
            var transit2 = new RegionId(5);
            var farRoom = new RegionId(6);

            var graph = CreateRegionGraph(
                new[] { current, nearRoom, transit, secondRoom, transit2, farRoom },
                RoomRegion(current, 1, "Zone01/Current", nearRoom, transit),
                RoomRegion(nearRoom, 2, "Zone01/Near", current),
                RouteRegion(transit, 3, "Zone01/Transit", current, secondRoom),
                RoomRegion(secondRoom, 4, "Zone01/Second", transit, transit2),
                RouteRegion(transit2, 5, "Zone01/Transit2", secondRoom, farRoom),
                RoomRegion(farRoom, 6, "Zone01/Far", transit2));

            var constructor =
                RoomSweepGlobalPlannerType.GetConstructor(
                    new[]
                    {
                        RegionGraphType,
                        RoomSweepCoverageMemoryType,
                        typeof(int),
                        typeof(int)
                    });

            Assert.That(
                constructor,
                Is.Not.Null,
                "Seeded RoomSweepGlobalPlanner constructor is missing.");

            var firstPlanner = constructor.Invoke(new object[]
            {
                graph,
                CreateMemory(),
                7,
                1
            });

            var secondPlanner = constructor.Invoke(new object[]
            {
                graph,
                CreateMemory(),
                7,
                1
            });

            var firstResult = TryGetOrCreateObjective(
                firstPlanner,
                current,
                out var firstObjective);
            var secondResult = TryGetOrCreateObjective(
                secondPlanner,
                current,
                out var secondObjective);

            Assert.That(firstResult, Is.True);
            Assert.That(secondResult, Is.True);
            Assert.That(
                GetObjectiveRegionId(firstObjective, "TargetRoomRegionId"),
                Is.EqualTo(
                    GetObjectiveRegionId(secondObjective, "TargetRoomRegionId")));

            var selectedTargets = new HashSet<RegionId>();
            for (var seed = 0; seed < 64; seed++)
            {
                var planner = constructor.Invoke(new object[]
                {
                    graph,
                    CreateMemory(),
                    seed,
                    1
                });

                Assert.That(
                    TryGetOrCreateObjective(
                        planner,
                        current,
                        out var objective),
                    Is.True);

                var target = GetObjectiveRegionId(
                    objective,
                    "TargetRoomRegionId");

                Assert.That(
                    target == nearRoom || target == secondRoom,
                    Is.True,
                    "Seeded selection must stay within the near-optimal pool.");
                Assert.That(target, Is.Not.EqualTo(farRoom));
                selectedTargets.Add(target);
            }

            Assert.That(selectedTargets.Count, Is.GreaterThanOrEqualTo(2));
        }

        private static object CreatePlanner(object regionGraph, object memory)
        {
            return Activator.CreateInstance(RoomSweepGlobalPlannerType, regionGraph, memory);
        }

        private static object CreateMemory()
        {
            return Activator.CreateInstance(RoomSweepCoverageMemoryType);
        }

        private static bool TryGetOrCreateObjective(object planner, RegionId currentRegionId, out object objective)
        {
            var args = new object[] { currentRegionId, null };
            var result = (bool)Invoke(
                planner,
                "TryGetOrCreateObjective",
                new[] { typeof(RegionId), RoomSweepGlobalObjectiveType.MakeByRefType() },
                args);
            objective = args[1];
            return result;
        }

        private static bool TryGetOrCreateObjective(
            object planner,
            RegionId currentRegionId,
            ISet<RegionId> rejectedRoomRegionIds,
            out object objective)
        {
            var args = new object[] { currentRegionId, rejectedRoomRegionIds, null };
            var result = (bool)Invoke(
                planner,
                "TryGetOrCreateObjective",
                new[] { typeof(RegionId), typeof(ISet<RegionId>), RoomSweepGlobalObjectiveType.MakeByRefType() },
                args);
            objective = args[2];
            return result;
        }

        private static void AssertObjective(object objective, RegionId targetRoomRegionId, RegionId nextRegionId)
        {
            Assert.That(GetObjectiveBool(objective, "IsValid"), Is.True);
            Assert.That(GetObjectiveRegionId(objective, "TargetRoomRegionId"), Is.EqualTo(targetRoomRegionId));
            Assert.That(GetObjectiveRegionId(objective, "NextRegionId"), Is.EqualTo(nextRegionId));
        }

        private static void RegisterRegion(object memory, RegionId regionId, params int[] nodeIds)
        {
            Invoke(memory, "RegisterRegion", new[] { typeof(RegionId), typeof(IReadOnlyCollection<int>) }, regionId, nodeIds);
        }

        private static void MarkRegionCleared(object memory, RegionId regionId)
        {
            Invoke(memory, "MarkRegionCleared", new[] { typeof(RegionId) }, regionId);
        }

        private static void SetRegionEnabled(object graph, RegionId regionId, bool enabled)
        {
            Invoke(graph, "TrySetRegionEnabled", new[] { typeof(RegionId), typeof(bool) }, regionId, enabled);
        }

        private static void SetEdgeOpen(object graph, RegionId from, RegionId to, bool open)
        {
            Invoke(graph, "TrySetEdgeOpen", new[] { typeof(RegionId), typeof(RegionId), typeof(bool) }, from, to, open);
        }

        private static object CreateRegionGraph(IReadOnlyList<RegionId> nodeToRegionMap, params object[] regionNodes)
        {
            var spatialGraph = CreateSpatialGraph(nodeToRegionMap.Count);
            return Activator.CreateInstance(
                RegionGraphType,
                ToArray(RegionNodeType, regionNodes),
                nodeToRegionMap,
                GetProperty(spatialGraph, "CompatibilityIdentity"),
                1);
        }

        private static object CreateSpatialGraph(int nodeCount)
        {
            var nodes = Array.CreateInstance(NodeType, nodeCount);
            for (var i = 0; i < nodeCount; i++)
            {
                nodes.SetValue(Activator.CreateInstance(
                    NodeType,
                    i,
                    new Vector3(i, 0f, 0f),
                    0,
                    i,
                    i * 3,
                    i * 3 + 1,
                    i * 3 + 2,
                    Array.Empty<int>()), i);
            }

            return Activator.CreateInstance(GraphType, nodes);
        }

        private static object RoomRegion(RegionId regionId, int sourceIndex, string sourcePath, params RegionId[] edges)
        {
            return SemanticRegion(regionId, sourceIndex, sourcePath, "Zone01", "Room", edges);
        }

        private static object RouteRegion(RegionId regionId, int sourceIndex, string sourcePath, params RegionId[] edges)
        {
            return SemanticRegion(regionId, sourceIndex, sourcePath, "Zone01", "Route", edges);
        }

        private static object IsolatedRegion(RegionId regionId, int sourceIndex, string sourcePath, params RegionId[] edges)
        {
            return SemanticRegion(regionId, sourceIndex, sourcePath, "Isolated", "IsolatedIsland", edges);
        }

        private static object SemanticRegion(
            RegionId regionId,
            int sourceIndex,
            string sourcePath,
            string zone,
            string kind,
            params RegionId[] edges)
        {
            return Activator.CreateInstance(
                RegionNodeType,
                regionId,
                EdgeArray(edges),
                Metadata(sourceIndex, sourcePath, zone, kind));
        }

        private static object LegacyRegion(RegionId regionId, params RegionId[] edges)
        {
            return Activator.CreateInstance(RegionNodeType, regionId, EdgeArray(edges));
        }

        private static Array EdgeArray(params RegionId[] regionIds)
        {
            var array = Array.CreateInstance(RegionEdgeType, regionIds?.Length ?? 0);
            for (var i = 0; i < array.Length; i++)
            {
                array.SetValue(Activator.CreateInstance(RegionEdgeType, regionIds[i], DoorId.Invalid), i);
            }

            return array;
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

        private static RegionId GetObjectiveRegionId(object objective, string propertyName)
        {
            return (RegionId)GetProperty(objective, propertyName);
        }

        private static bool GetObjectiveBool(object objective, string propertyName)
        {
            return (bool)GetProperty(objective, propertyName);
        }

        private static string GetInvalidationReason(object planner)
        {
            return GetProperty(planner, "LastInvalidationReason").ToString();
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
        private static Type RoomSweepGlobalPlannerType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RoomSweepGlobalPlanner");
        private static Type RoomSweepGlobalObjectiveType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RoomSweepGlobalObjective");
    }
}
