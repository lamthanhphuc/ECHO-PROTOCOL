using System;
using System.Collections.Generic;
using System.Reflection;
using EchoProtocol.AI.Common;
using EchoProtocol.AI.Common.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerSpatialCanonicalTests
    {
        [Test]
        public void STK_P3_StableSpatialIds_DefaultIsInvalid_AndSortsDeterministically()
        {
            Assert.That(RegionId.Invalid.IsValid, Is.False);
            Assert.That(DoorId.Invalid.IsValid, Is.False);
            Assert.That(GameplayZoneId.Invalid.IsValid, Is.False);

            var ids = new[] { new RegionId(3), new RegionId(1), new RegionId(2) };
            Array.Sort(ids);

            Assert.That(ids[0].Value, Is.EqualTo(1));
            Assert.That(ids[1].Value, Is.EqualTo(2));
            Assert.That(ids[2].Value, Is.EqualTo(3));
        }

        [Test]
        public void STK_P3_SpatialGraphCompatibilityIdentity_ChangesWithTopology()
        {
            var graphA = CreateLineGraph();
            var graphB = Activator.CreateInstance(GraphType, NodeArray(
                Node(0, 0f, 1),
                Node(1, 1f, 0, 2),
                Node(2, 2f, 1, 3),
                Node(3, 3f, 2)));

            Assert.That((bool)GetProperty(GetProperty(graphA, "CompatibilityIdentity"), "IsValid"), Is.True);
            Assert.That(GetProperty(graphA, "CompatibilityIdentity"), Is.Not.EqualTo(GetProperty(graphB, "CompatibilityIdentity")));
        }

        [Test]
        public void STK_P3_RegionGraph_RejectsCompatibilityMismatchBeforeNodeMapUse()
        {
            var graph = CreateLineGraph();
            var staleIdentity = Activator.CreateInstance(CompatibilityIdentityType, (ulong)GetProperty(GetProperty(graph, "CompatibilityIdentity"), "Value") + 1UL);
            var staleRegionGraph = CreateRegionGraph(staleIdentity);

            var validation = Invoke(RegionGraphType, "Validate", new[] { RegionGraphType, GraphType }, staleRegionGraph, graph);

            Assert.That(validation.ToString(), Is.EqualTo("SpatialGraphCompatibilityMismatch"));
        }

        [Test]
        public void STK_P3_CoverageMemory_SelectionDoesNotCountPhysicalVisit()
        {
            var graph = CreateLineGraph();
            var regionGraph = CreateRegionGraph(GetProperty(graph, "CompatibilityIdentity"));
            var coverage = Activator.CreateInstance(CoverageMemoryType, GetIntProperty(graph, "NodeCount"), regionGraph);
            var planner = Activator.CreateInstance(GlobalPatrolPlannerType, regionGraph, coverage);
            var args = new object[] { new RegionId(1), RegionId.Invalid, null };

            Assert.That((bool)Invoke(planner, "TryGetOrCreateObjective", TryGetObjectiveSignature, args), Is.True);

            var objective = args[2];
            Assert.That((RegionId)GetProperty(objective, "TargetRegionId"), Is.EqualTo(new RegionId(2)));
            Assert.That(Invoke(coverage, "GetRegionVisitCount", new[] { typeof(RegionId) }, new RegionId(2)), Is.EqualTo(0));
            Assert.That(Invoke(coverage, "GetNodeVisitCount", new[] { typeof(int) }, 2), Is.EqualTo(0));
        }

        [Test]
        public void STK_P3_CoverageMemory_PhysicalArrivalUpdatesNodeAndMappedRegionOncePerArrival()
        {
            var graph = CreateLineGraph();
            var regionGraph = CreateRegionGraph(GetProperty(graph, "CompatibilityIdentity"));
            var coverage = Activator.CreateInstance(CoverageMemoryType, GetIntProperty(graph, "NodeCount"), regionGraph);

            var visit = Invoke(coverage, "RecordPhysicalNodeArrival", new[] { typeof(int), typeof(float) }, 2, 10f);

            Assert.That((bool)GetProperty(visit, "IsValid"), Is.True);
            Assert.That(GetProperty(visit, "NodeVisitCount"), Is.EqualTo(1));
            Assert.That((bool)GetProperty(visit, "RegionUpdated"), Is.True);
            Assert.That((RegionId)GetProperty(visit, "RegionId"), Is.EqualTo(new RegionId(2)));
            Assert.That(Invoke(coverage, "GetRegionVisitCount", new[] { typeof(RegionId) }, new RegionId(2)), Is.EqualTo(1));
        }

        [Test]
        public void STK_P3_GlobalPatrolPlanner_PersistsObjectiveUntilTargetRegionVisited()
        {
            var graph = CreateLineGraph();
            var regionGraph = CreateRegionGraph(GetProperty(graph, "CompatibilityIdentity"));
            var coverage = Activator.CreateInstance(CoverageMemoryType, GetIntProperty(graph, "NodeCount"), regionGraph);
            var planner = Activator.CreateInstance(GlobalPatrolPlannerType, regionGraph, coverage);
            var firstArgs = new object[] { new RegionId(1), RegionId.Invalid, null };
            var secondArgs = new object[] { new RegionId(1), RegionId.Invalid, null };

            Assert.That((bool)Invoke(planner, "TryGetOrCreateObjective", TryGetObjectiveSignature, firstArgs), Is.True);
            Assert.That((bool)Invoke(planner, "TryGetOrCreateObjective", TryGetObjectiveSignature, secondArgs), Is.True);

            Assert.That(GetProperty(secondArgs[2], "TargetRegionId"), Is.EqualTo(GetProperty(firstArgs[2], "TargetRegionId")));
            Invoke(coverage, "RecordPhysicalNodeArrival", new[] { typeof(int), typeof(float) }, 2, 12f);

            var nextArgs = new object[] { new RegionId(2), new RegionId(1), null };
            Assert.That((bool)Invoke(planner, "TryGetOrCreateObjective", TryGetObjectiveSignature, nextArgs), Is.True);
            Assert.That((RegionId)GetProperty(nextArgs[2], "TargetRegionId"), Is.EqualTo(new RegionId(1)));
        }

        [Test]
        public void STK_P4_GlobalPatrolPlanner_SelectsAlternateGlobalBeforeExhaustion()
        {
            var graph = CreateThreeRegionLineGraph();
            var regionGraph = CreateThreeRegionGraph(GetProperty(graph, "CompatibilityIdentity"));
            var coverage = Activator.CreateInstance(CoverageMemoryType, GetIntProperty(graph, "NodeCount"), regionGraph);
            var planner = Activator.CreateInstance(GlobalPatrolPlannerType, regionGraph, coverage);
            var rejected = new HashSet<RegionId> { new RegionId(2) };
            var args = new object[] { new RegionId(1), RegionId.Invalid, rejected, null };

            Assert.That((bool)Invoke(planner, "TryGetOrCreateObjective", TryGetObjectiveWithRejectedSignature, args), Is.True);

            var objective = args[3];
            Assert.That((RegionId)GetProperty(objective, "TargetRegionId"), Is.EqualTo(new RegionId(3)));
            Assert.That((RegionId)GetProperty(objective, "NextRegionId"), Is.EqualTo(new RegionId(2)));
        }

        [Test]
        public void STK_P4_GlobalPatrolPlanner_ReturnsFalseOnlyAfterReachableGlobalObjectivesExhausted()
        {
            var graph = CreateThreeRegionLineGraph();
            var regionGraph = CreateThreeRegionGraph(GetProperty(graph, "CompatibilityIdentity"));
            var coverage = Activator.CreateInstance(CoverageMemoryType, GetIntProperty(graph, "NodeCount"), regionGraph);
            var planner = Activator.CreateInstance(GlobalPatrolPlannerType, regionGraph, coverage);
            var rejected = new HashSet<RegionId> { new RegionId(2), new RegionId(3) };
            var args = new object[] { new RegionId(1), RegionId.Invalid, rejected, null };

            Assert.That((bool)Invoke(planner, "TryGetOrCreateObjective", TryGetObjectiveWithRejectedSignature, args), Is.False);
        }

        [Test]
        public void STK_P4_TopologyRelevance_ChaseUsesChaseDestinationBeforeStalePatrolBlackboard()
        {
            var fixture = CreateTopologyControllerFixture("CHASE");
            try
            {
                SetBlackboardDestination(fixture.Controller, 1);
                SetPrivateField(fixture.Controller, "_hasLastChaseRequestedDestination", true);
                SetPrivateField(fixture.Controller, "_lastChaseRequestedDestination", new Vector3(20f, 0f, 0f));

                Assert.That(IsTopologyEdgeRelevant(fixture.Controller, new RegionId(1), new RegionId(2)), Is.False);
                Assert.That(IsTopologyEdgeRelevant(fixture.Controller, new RegionId(1), new RegionId(3)), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixture.GameObject);
            }
        }

        [Test]
        public void STK_P4_TopologyRelevance_SearchLkpUsesFrozenOriginBeforeStalePatrolBlackboard()
        {
            var fixture = CreateTopologyControllerFixture("SEARCH");
            try
            {
                SetBlackboardDestination(fixture.Controller, 1);
                SetPrivateField(fixture.Controller, "searchCandidateNodeId", -1);
                SetPrivateField(fixture.Controller, "_searchContext", CreateSearchContext(7, new Vector3(20f, 0f, 0f), Vector3.forward, new RegionId(3)));

                Assert.That(IsTopologyEdgeRelevant(fixture.Controller, new RegionId(1), new RegionId(2)), Is.False);
                Assert.That(IsTopologyEdgeRelevant(fixture.Controller, new RegionId(1), new RegionId(3)), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixture.GameObject);
            }
        }

        [Test]
        public void STK_P4_TopologyRelevance_SearchCandidateUsesCandidateBeforeStalePatrolBlackboard()
        {
            var fixture = CreateTopologyControllerFixture("SEARCH");
            try
            {
                SetBlackboardDestination(fixture.Controller, 1);
                SetPrivateField(fixture.Controller, "searchCandidateNodeId", 2);
                SetPrivateField(fixture.Controller, "_searchContext", CreateSearchContext(8, Vector3.zero, Vector3.forward, new RegionId(1)));

                Assert.That(IsTopologyEdgeRelevant(fixture.Controller, new RegionId(1), new RegionId(2)), Is.False);
                Assert.That(IsTopologyEdgeRelevant(fixture.Controller, new RegionId(1), new RegionId(3)), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixture.GameObject);
            }
        }

        [Test]
        public void STK_P4_TopologyRelevance_PatrolUsesActivePatrolDestinationOnly()
        {
            var fixture = CreateTopologyControllerFixture("PATROL");
            try
            {
                SetBlackboardDestination(fixture.Controller, 1);

                Assert.That(IsTopologyEdgeRelevant(fixture.Controller, new RegionId(1), new RegionId(2)), Is.True);
                Assert.That(IsTopologyEdgeRelevant(fixture.Controller, new RegionId(1), new RegionId(3)), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixture.GameObject);
            }
        }

        [Test]
        public void STK_P4_TopologyRelevance_PathSegmentSamplingDetectsIntermediateRegionBetweenAdjacentEndpoints()
        {
            var fixture = CreateIntermediateSampleTopologyControllerFixture("PATROL");
            try
            {
                var corners = new[] { Vector3.zero, new Vector3(10f, 0f, 0f) };

                var usesIntermediateEdge = (bool)InvokePrivate(
                    fixture.Controller,
                    "TryPathSegmentsUseTopologyEdge",
                    new[] { typeof(IReadOnlyList<Vector3>), typeof(RegionId), typeof(RegionId) },
                    corners,
                    new RegionId(1),
                    new RegionId(3));

                Assert.That(usesIntermediateEdge, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixture.GameObject);
            }
        }

        [Test]
        public void STK_P4_RegionGraph_SetEdgeOpenNoOpDoesNotReportTopologyChange()
        {
            var graph = CreateBranchGraph();
            var regionGraph = CreateBranchRegionGraph(GetProperty(graph, "CompatibilityIdentity"));

            Assert.That((bool)Invoke(regionGraph, "TrySetEdgeOpen", new[] { typeof(RegionId), typeof(RegionId), typeof(bool) }, new RegionId(1), new RegionId(2), false), Is.True);
            Assert.That((bool)Invoke(regionGraph, "TrySetEdgeOpen", new[] { typeof(RegionId), typeof(RegionId), typeof(bool) }, new RegionId(1), new RegionId(2), false), Is.False);
        }

        [Test]
        public void STK_P3_LocalPatrolSelector_RequiresCompletePath()
        {
            var graph = CreateLineGraph();
            var regionGraph = CreateRegionGraph(GetProperty(graph, "CompatibilityIdentity"));
            var coverage = Activator.CreateInstance(CoverageMemoryType, GetIntProperty(graph, "NodeCount"), regionGraph);
            var validator = Delegate.CreateDelegate(PatrolPathValidatorType, typeof(StalkerSpatialCanonicalTests).GetMethod(nameof(IsXLessThanTwo), BindingFlags.Static | BindingFlags.NonPublic));
            var selector = Activator.CreateInstance(LocalPatrolSelectorType, graph, regionGraph, coverage, 3, validator);
            var args = new object[] { 0, -1, new RegionId(2), null };

            var selected = (bool)Invoke(selector, "TrySelect", new[] { typeof(int), typeof(int), typeof(RegionId), LocalPatrolSelectionType.MakeByRefType() }, args);

            Assert.That(selected, Is.False);
        }

        private static bool IsXLessThanTwo(Vector3 destination) => destination.x < 2f;

        private static object CreateLineGraph()
        {
            return Activator.CreateInstance(GraphType, NodeArray(
                Node(0, 0f, 1),
                Node(1, 1f, 0, 2),
                Node(2, 2f, 1)));
        }

        private static object CreateBranchGraph()
        {
            return Activator.CreateInstance(GraphType, NodeArray(
                Node(0, 0f, 1, 2),
                Node(1, 10f, 0),
                Node(2, 20f, 0)));
        }

        private static object CreateIntermediateSampleGraph()
        {
            return Activator.CreateInstance(GraphType, NodeArray(
                Node(0, 0f, 1, 2),
                Node(1, 10f, 0, 2),
                Node(2, 5f, 0, 1)));
        }

        private static object CreateThreeRegionLineGraph()
        {
            return Activator.CreateInstance(GraphType, NodeArray(
                Node(0, 0f, 1),
                Node(1, 1f, 0, 2),
                Node(2, 2f, 1)));
        }

        private static object CreateRegionGraph(object identity)
        {
            return Activator.CreateInstance(
                RegionGraphType,
                RegionNodeArray(
                    RegionNode(new RegionId(1), Edge(new RegionId(2), DoorId.Invalid)),
                    RegionNode(new RegionId(2), Edge(new RegionId(1), DoorId.Invalid))),
                new[] { new RegionId(1), new RegionId(1), new RegionId(2) },
                identity,
                1);
        }

        private static object CreateBranchRegionGraph(object identity)
        {
            return Activator.CreateInstance(
                RegionGraphType,
                RegionNodeArray(
                    RegionNode(new RegionId(1), Edge(new RegionId(2), DoorId.Invalid), Edge(new RegionId(3), DoorId.Invalid)),
                    RegionNode(new RegionId(2), Edge(new RegionId(1), DoorId.Invalid)),
                    RegionNode(new RegionId(3), Edge(new RegionId(1), DoorId.Invalid))),
                new[] { new RegionId(1), new RegionId(2), new RegionId(3) },
                identity,
                1);
        }

        private static object CreateIntermediateSampleRegionGraph(object identity)
        {
            return Activator.CreateInstance(
                RegionGraphType,
                RegionNodeArray(
                    RegionNode(new RegionId(1), Edge(new RegionId(2), DoorId.Invalid), Edge(new RegionId(3), DoorId.Invalid)),
                    RegionNode(new RegionId(2), Edge(new RegionId(1), DoorId.Invalid), Edge(new RegionId(3), DoorId.Invalid)),
                    RegionNode(new RegionId(3), Edge(new RegionId(1), DoorId.Invalid), Edge(new RegionId(2), DoorId.Invalid))),
                new[] { new RegionId(1), new RegionId(2), new RegionId(3) },
                identity,
                1);
        }

        private static object CreateThreeRegionGraph(object identity)
        {
            return Activator.CreateInstance(
                RegionGraphType,
                RegionNodeArray(
                    RegionNode(new RegionId(1), Edge(new RegionId(2), DoorId.Invalid)),
                    RegionNode(new RegionId(2), Edge(new RegionId(1), DoorId.Invalid), Edge(new RegionId(3), DoorId.Invalid)),
                    RegionNode(new RegionId(3), Edge(new RegionId(2), DoorId.Invalid))),
                new[] { new RegionId(1), new RegionId(2), new RegionId(3) },
                identity,
                1);
        }

        private static object Node(int id, float x, params int[] neighbors)
        {
            return Activator.CreateInstance(NodeType, id, new Vector3(x, 0f, 0f), 0, id, id * 3, id * 3 + 1, id * 3 + 2, new List<int>(neighbors));
        }

        private static object RegionNode(RegionId regionId, params object[] edges)
        {
            return Activator.CreateInstance(RegionNodeType, regionId, RegionEdgeArray(edges));
        }

        private static object Edge(RegionId toRegionId, DoorId doorId)
        {
            return Activator.CreateInstance(RegionEdgeType, toRegionId, doorId);
        }

        private static Array NodeArray(params object[] values) => ToArray(NodeType, values);
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

        private static object GetProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Missing property '{propertyName}' on '{target.GetType().FullName}'.");
            return property.GetValue(target);
        }

        private static int GetIntProperty(object target, string propertyName) => (int)GetProperty(target, propertyName);

        private static object Invoke(object target, string methodName, Type[] parameterTypes, params object[] args)
        {
            var type = target as Type ?? target.GetType();
            var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static, null, parameterTypes, null);
            Assert.That(method, Is.Not.Null, $"Missing method '{methodName}' on '{type.FullName}'.");
            return method.Invoke(target is Type ? null : target, args);
        }

        private static object InvokePrivate(object target, string methodName, Type[] parameterTypes, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic, null, parameterTypes, null);
            Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}' on '{target.GetType().FullName}'.");
            return method.Invoke(target, args);
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}' on '{target.GetType().FullName}'.");
            return field.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}' on '{target.GetType().FullName}'.");
            field.SetValue(target, value);
        }

        private static void SetBlackboardDestination(object controller, int nodeId)
        {
            var blackboard = GetPrivateField(controller, "_blackboard");
            GetProperty(blackboard, "DestinationSpatialNodeId");
            blackboard.GetType().GetProperty("DestinationSpatialNodeId").SetValue(blackboard, nodeId);
        }

        private static bool IsTopologyEdgeRelevant(object controller, RegionId from, RegionId to)
        {
            return (bool)InvokePrivate(
                controller,
                "IsTopologyEdgeRelevantToCurrentNavigation",
                new[] { typeof(RegionId), typeof(RegionId) },
                from,
                to);
        }

        private static object CreateSearchContext(long episodeId, Vector3 lkp, Vector3 direction, RegionId regionId)
        {
            return Activator.CreateInstance(
                SearchContextType,
                Activator.CreateInstance(SearchEpisodeIdType, episodeId),
                lkp,
                direction,
                new AiSimulationTime(episodeId, episodeId * 0.1d),
                regionId);
        }

        private static TopologyControllerFixture CreateTopologyControllerFixture(string stateName)
        {
            var graph = CreateBranchGraph();
            var regionGraph = CreateBranchRegionGraph(GetProperty(graph, "CompatibilityIdentity"));
            var gameObject = new GameObject("STK_P4_TopologyRelevance_Controller");
            var controller = gameObject.AddComponent(ResolveType("EchoProtocol.AI.Stalker.StalkerController"));
            gameObject.transform.position = Vector3.zero;

            SetPrivateField(controller, "currentState", Enum.Parse(ResolveType("EchoProtocol.AI.Stalker.StalkerState"), stateName));
            SetPrivateField(controller, "_spatialPatrolGraph", graph);
            SetPrivateField(controller, "_regionGraph", regionGraph);
            SetPrivateField(controller, "searchCandidateNodeId", -1);
            return new TopologyControllerFixture(gameObject, controller);
        }

        private static TopologyControllerFixture CreateLineTopologyControllerFixture(string stateName)
        {
            var graph = CreateThreeRegionLineGraph();
            var regionGraph = CreateThreeRegionGraph(GetProperty(graph, "CompatibilityIdentity"));
            var gameObject = new GameObject("STK_P4_LineTopologyRelevance_Controller");
            var controller = gameObject.AddComponent(ResolveType("EchoProtocol.AI.Stalker.StalkerController"));
            gameObject.transform.position = Vector3.zero;

            SetPrivateField(controller, "currentState", Enum.Parse(ResolveType("EchoProtocol.AI.Stalker.StalkerState"), stateName));
            SetPrivateField(controller, "_spatialPatrolGraph", graph);
            SetPrivateField(controller, "_regionGraph", regionGraph);
            SetPrivateField(controller, "searchCandidateNodeId", -1);
            return new TopologyControllerFixture(gameObject, controller);
        }

        private static TopologyControllerFixture CreateIntermediateSampleTopologyControllerFixture(string stateName)
        {
            var graph = CreateIntermediateSampleGraph();
            var regionGraph = CreateIntermediateSampleRegionGraph(GetProperty(graph, "CompatibilityIdentity"));
            var gameObject = new GameObject("STK_P4_IntermediateSampleTopology_Controller");
            var controller = gameObject.AddComponent(ResolveType("EchoProtocol.AI.Stalker.StalkerController"));
            gameObject.transform.position = Vector3.zero;

            SetPrivateField(controller, "currentState", Enum.Parse(ResolveType("EchoProtocol.AI.Stalker.StalkerState"), stateName));
            SetPrivateField(controller, "_spatialPatrolGraph", graph);
            SetPrivateField(controller, "_regionGraph", regionGraph);
            SetPrivateField(controller, "searchCandidateNodeId", -1);
            return new TopologyControllerFixture(gameObject, controller);
        }

        private readonly struct TopologyControllerFixture
        {
            public TopologyControllerFixture(GameObject gameObject, object controller)
            {
                GameObject = gameObject;
                Controller = controller;
            }

            public GameObject GameObject { get; }
            public object Controller { get; }
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
        private static Type CompatibilityIdentityType => ResolveType("EchoProtocol.AI.Stalker.Spatial.SpatialGraphCompatibilityIdentity");
        private static Type CoverageMemoryType => ResolveType("EchoProtocol.AI.Stalker.Spatial.CoverageMemory");
        private static Type SearchContextType => ResolveType("EchoProtocol.AI.Stalker.StalkerSearchContext");
        private static Type SearchEpisodeIdType => ResolveType("EchoProtocol.AI.Stalker.SearchEpisodeId");
        private static Type GlobalPatrolPlannerType => ResolveType("EchoProtocol.AI.Stalker.Spatial.GlobalPatrolPlanner");
        private static Type GlobalPatrolObjectiveType => ResolveType("EchoProtocol.AI.Stalker.Spatial.GlobalPatrolObjective");
        private static Type LocalPatrolSelectorType => ResolveType("EchoProtocol.AI.Stalker.Spatial.LocalPatrolSelector");
        private static Type LocalPatrolSelectionType => ResolveType("EchoProtocol.AI.Stalker.Spatial.LocalPatrolSelection");
        private static Type PatrolPathValidatorType => ResolveType("EchoProtocol.AI.Stalker.Spatial.PatrolPathValidator");
        private static Type[] TryGetObjectiveSignature => new[] { typeof(RegionId), typeof(RegionId), GlobalPatrolObjectiveType.MakeByRefType() };
        private static Type[] TryGetObjectiveWithRejectedSignature => new[] { typeof(RegionId), typeof(RegionId), typeof(ISet<RegionId>), GlobalPatrolObjectiveType.MakeByRefType() };
    }
}
