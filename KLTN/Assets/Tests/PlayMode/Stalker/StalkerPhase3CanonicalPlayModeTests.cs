using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using EchoProtocol.AI.Common;
using EchoProtocol.AI.Common.Spatial;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerPhase3CanonicalPlayModeTests
    {
        private readonly List<GameObject> _createdObjects = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (var i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                {
                    UnityEngine.Object.Destroy(_createdObjects[i]);
                }
            }

            _createdObjects.Clear();
            yield return null;
        }

        [UnityTest] public IEnumerator STK_P_020_Phase3FixtureBakeProducesCompatibleMultiRegionGraph()
        {
            var graph = CreateFixtureGraph();
            var bake = BakeFixture(graph);
            var regionGraph = GetProperty(bake, "Graph");
            var spatialNodeCount = (int)GetProperty(graph, "NodeCount");

            Assert.That((bool)GetProperty(bake, "Succeeded"), Is.True);
            Assert.That(regionGraph, Is.Not.Null, "RegionGraphBakeResult.Graph must be non-null after a successful Phase 3 fixture bake.");
            Assert.That(GetCollectionCount(GetProperty(regionGraph, "Regions"), "RegionGraph.Regions"), Is.EqualTo(3));
            Assert.That(GetProperty(regionGraph, "NodeMappingCount"), Is.EqualTo(spatialNodeCount));
            Assert.That(GetProperty(regionGraph, "CompatibilityIdentity"), Is.EqualTo(GetProperty(graph, "CompatibilityIdentity")));
            Assert.That(Invoke(RegionGraphType, "Validate", new[] { RegionGraphType, GraphType }, regionGraph, graph).ToString(), Is.EqualTo("None"));

            var mappedRegionIds = new HashSet<RegionId>();
            for (var nodeId = 0; nodeId < spatialNodeCount; nodeId++)
            {
                var args = new object[] { nodeId, null };
                Assert.That((bool)Invoke(regionGraph, "TryGetRegionForNode", new[] { typeof(int), typeof(RegionId).MakeByRefType() }, args), Is.True);
                Assert.That(((RegionId)args[1]).IsValid, Is.True);
                mappedRegionIds.Add((RegionId)args[1]);
            }

            Assert.That(mappedRegionIds.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(mappedRegionIds.Contains(new RegionId(1)), Is.True);
            yield return null;
        }

        [UnityTest] public IEnumerator STK_P_021_ConfidenceSpatialUsesCanonicalGlobalObjective()
        {
            var graph = CreateFixtureGraph();
            var regionGraph = GetProperty(BakeFixture(graph), "Graph");
            var coverage = Activator.CreateInstance(CoverageMemoryType, (int)GetProperty(graph, "NodeCount"), regionGraph);
            var planner = Activator.CreateInstance(GlobalPatrolPlannerType, regionGraph, coverage);
            var args = new object[] { new RegionId(1), RegionId.Invalid, null };

            Assert.That((bool)Invoke(planner, "TryGetOrCreateObjective", TryGetObjectiveSignature, args), Is.True);
            Assert.That((RegionId)GetProperty(args[2], "TargetRegionId"), Is.EqualTo(new RegionId(2)));
            Assert.That((RegionId)GetProperty(args[2], "NextRegionId"), Is.EqualTo(new RegionId(2)));
            yield return null;
        }

        [UnityTest] public IEnumerator STK_P_022_GlobalObjectivePersistsUntilPhysicalVisit()
        {
            var graph = CreateFixtureGraph();
            var regionGraph = GetProperty(BakeFixture(graph), "Graph");
            var coverage = Activator.CreateInstance(CoverageMemoryType, (int)GetProperty(graph, "NodeCount"), regionGraph);
            var planner = Activator.CreateInstance(GlobalPatrolPlannerType, regionGraph, coverage);
            var first = new object[] { new RegionId(1), RegionId.Invalid, null };
            var second = new object[] { new RegionId(1), RegionId.Invalid, null };

            Assert.That((bool)Invoke(planner, "TryGetOrCreateObjective", TryGetObjectiveSignature, first), Is.True);
            Assert.That((bool)Invoke(planner, "TryGetOrCreateObjective", TryGetObjectiveSignature, second), Is.True);
            Invoke(coverage, "RecordPhysicalNodeArrival", new[] { typeof(int), typeof(float) }, 2, 4f);
            var third = new object[] { new RegionId(2), new RegionId(1), null };
            Assert.That((bool)Invoke(planner, "TryGetOrCreateObjective", TryGetObjectiveSignature, third), Is.True);

            Assert.That(GetProperty(second[2], "TargetRegionId"), Is.EqualTo(GetProperty(first[2], "TargetRegionId")));
            Assert.That(GetProperty(third[2], "TargetRegionId"), Is.Not.EqualTo(GetProperty(first[2], "TargetRegionId")));
            yield return null;
        }

        [UnityTest] public IEnumerator STK_P_023_PatrolSelectionDoesNotMutateCoverage()
        {
            var graph = CreateFixtureGraph();
            var regionGraph = GetProperty(BakeFixture(graph), "Graph");
            var coverage = Activator.CreateInstance(CoverageMemoryType, (int)GetProperty(graph, "NodeCount"), regionGraph);
            var validator = CreatePathValidator("Complete");
            var selector = Activator.CreateInstance(LocalPatrolSelectorType, graph, regionGraph, coverage, 4, validator);
            var args = new object[] { 0, -1, new RegionId(2), null };

            Assert.That((bool)Invoke(selector, "TrySelect", TryLocalSelectSignature, args), Is.True);
            Assert.That(Invoke(coverage, "GetNodeVisitCount", new[] { typeof(int) }, 2), Is.EqualTo(0));
            Assert.That(Invoke(coverage, "GetRegionVisitCount", new[] { typeof(RegionId) }, new RegionId(2)), Is.EqualTo(0));
            yield return null;
        }

        [UnityTest] public IEnumerator STK_P_024_PhysicalArrivalOnlyUpdatesCoverage()
        {
            var graph = CreateFixtureGraph();
            var regionGraph = GetProperty(BakeFixture(graph), "Graph");
            var coverage = Activator.CreateInstance(CoverageMemoryType, (int)GetProperty(graph, "NodeCount"), regionGraph);
            var arrival = Invoke(coverage, "RecordPhysicalNodeArrival", new[] { typeof(int), typeof(float) }, 2, 5f);

            Assert.That((bool)GetProperty(arrival, "IsValid"), Is.True);
            Assert.That(Invoke(coverage, "GetNodeVisitCount", new[] { typeof(int) }, 2), Is.EqualTo(1));
            Assert.That(Invoke(coverage, "GetRegionVisitCount", new[] { typeof(RegionId) }, new RegionId(2)), Is.EqualTo(1));
            yield return null;
        }

        [UnityTest] public IEnumerator STK_P_025_IncompatibleGraphFailsClosedWithFallbackReason()
        {
            var graph = CreateFixtureGraph();
            var staleIdentity = Activator.CreateInstance(CompatibilityIdentityType, (ulong)GetProperty(GetProperty(graph, "CompatibilityIdentity"), "Value") + 1UL);
            var staleGraph = Activator.CreateInstance(RegionGraphType, RegionNodeArray(RegionNode(new RegionId(1))), RegionIdArray(new RegionId(1), new RegionId(1), new RegionId(1), new RegionId(1), new RegionId(1), new RegionId(1)), staleIdentity, 1);

            Assert.That(Invoke(RegionGraphType, "Validate", new[] { RegionGraphType, GraphType }, staleGraph, graph).ToString(), Is.EqualTo("SpatialGraphCompatibilityMismatch"));
            yield return null;
        }

        [UnityTest] public IEnumerator STK_P_026_EdgeCloseAndReopenControlsRouteAvailability()
        {
            var graph = CreateFixtureGraph();
            var regionGraph = GetProperty(BakeFixture(graph), "Graph");
            var routeArgs = new object[] { new RegionId(1), new RegionId(3), null };

            Assert.That((bool)Invoke(regionGraph, "TryGetRouteHopCost", TryGetRouteHopCostSignature, routeArgs), Is.True);
            Assert.That(routeArgs[2], Is.EqualTo(2));
            Assert.That((bool)Invoke(regionGraph, "TrySetEdgeOpen", new[] { typeof(RegionId), typeof(RegionId), typeof(bool) }, new RegionId(2), new RegionId(3), false), Is.True);
            Assert.That((bool)Invoke(regionGraph, "TryGetRouteHopCost", TryGetRouteHopCostSignature, new object[] { new RegionId(1), new RegionId(3), null }), Is.False);
            Assert.That((bool)Invoke(regionGraph, "TrySetEdgeOpen", new[] { typeof(RegionId), typeof(RegionId), typeof(bool) }, new RegionId(2), new RegionId(3), true), Is.True);
            Assert.That((bool)Invoke(regionGraph, "TryGetRouteHopCost", TryGetRouteHopCostSignature, new object[] { new RegionId(1), new RegionId(3), null }), Is.True);
            yield return null;
        }

        [UnityTest] public IEnumerator STK_P_031_SearchContextFreezesLkpDirectionAndEpisodeId()
        {
            var context = CreateSearchContext(31, new Vector3(2f, 0f, 1f), Vector3.right, new RegionId(1));

            Invoke(context, "RecordCandidateAttempt", new[] { typeof(int) }, 1);
            Invoke(context, "RecordPhysicalCandidateArrival", new[] { typeof(int) }, 1);

            Assert.That((long)GetProperty(GetProperty(context, "EpisodeId"), "Value"), Is.EqualTo(31L));
            Assert.That((Vector3)GetProperty(context, "SearchOriginLKP"), Is.EqualTo(new Vector3(2f, 0f, 1f)));
            Assert.That((Vector3)GetProperty(context, "SearchOriginDirection"), Is.EqualTo(Vector3.right));
            yield return null;
        }

        [UnityTest] public IEnumerator STK_P_032_SearchRadiusAppliesToEndpointOnlyAndRejectsPartialPath()
        {
            var graph = CreateFixtureGraph();
            var planner = CreateSearchPlanner(graph, "PartialForXGreaterThanTwo");
            var args = new object[] { CreateSearchContext(1, Vector3.zero, Vector3.right, RegionId.Invalid), 10f, 0, -1, null };

            Assert.That((bool)Invoke(planner, "TrySelectCandidate", TrySearchSelectSignature, args), Is.True);
            Assert.That(GetProperty(planner, "LastRejectReason").ToString(), Is.EqualTo("PathPartial"));
            yield return null;
        }

        [UnityTest] public IEnumerator STK_P_035_SearchNoCandidateHoldsSearchUntilTimeout()
        {
            var fixture = CreateControllerFixture();
            SetState(fixture.Controller, "SEARCH");
            Invoke(fixture.Memory, "SetCurrentTarget", new[] { typeof(PlayerId) }, new PlayerId(1));
            Invoke(fixture.Memory, "TryAcceptCurrentTargetObservation", new[] { VisionObservationType }, CreateObservation(1, new Vector3(0f, 0f, 1f), 1));
            SetPrivateField(fixture.Controller, "searchDuration", 10f);

            Simulate(fixture.Controller, 0.5f, null, TargetStatusList(CreateStatus(1, true)));

            Assert.That(GetProperty(fixture.Controller, "CurrentState").ToString(), Is.EqualTo("SEARCH"));
            Assert.That((PlayerId)GetProperty(fixture.Controller, "CurrentTargetId"), Is.EqualTo(new PlayerId(1)));
            yield return null;
        }

        [UnityTest] public IEnumerator STK_P_036_SameTargetReacquisitionBeatsAlternateVisibleTarget()
        {
            var fixture = SetupSearchControllerWithTarget();
            Simulate(fixture.Controller, 0.1f, TargetCandidateList(CreateCandidate(2, true), CreateCandidate(1, true)), TargetStatusList(CreateStatus(1, true), CreateStatus(2, true)));

            Assert.That(GetProperty(fixture.Controller, "CurrentState").ToString(), Is.EqualTo("CHASE"));
            Assert.That((PlayerId)GetProperty(fixture.Controller, "CurrentTargetId"), Is.EqualTo(new PlayerId(1)));
            yield return null;
        }

        [UnityTest] public IEnumerator STK_P_037_OtherVisibleEligibleTargetGoesToDetectNotChase()
        {
            var fixture = SetupSearchControllerWithTarget();
            Simulate(fixture.Controller, 0.1f, TargetCandidateList(CreateCandidate(2, true)), TargetStatusList(CreateStatus(1, true), CreateStatus(2, true)));

            Assert.That(GetProperty(fixture.Controller, "CurrentState").ToString(), Is.EqualTo("DETECT"));
            Assert.That((PlayerId)GetProperty(fixture.Controller, "DetectionTargetId"), Is.EqualTo(new PlayerId(2)));
            Assert.That(((PlayerId)GetProperty(fixture.Controller, "CurrentTargetId")).IsValid, Is.False);
            yield return null;
        }

        [UnityTest] public IEnumerator STK_P_038_InvalidCurrentTargetWithAlternateVisibleGoesToDetect()
        {
            var fixture = SetupSearchControllerWithTarget();
            Simulate(fixture.Controller, 0.1f, TargetCandidateList(CreateCandidate(2, true)), TargetStatusList(CreateStatus(1, false), CreateStatus(2, true)));

            Assert.That(GetProperty(fixture.Controller, "CurrentState").ToString(), Is.EqualTo("DETECT"));
            Assert.That((PlayerId)GetProperty(fixture.Controller, "DetectionTargetId"), Is.EqualTo(new PlayerId(2)));
            yield return null;
        }

        [UnityTest] public IEnumerator STK_P_041_InvalidCurrentTargetWithNoVisibleReturnsToPatrol()
        {
            var fixture = SetupSearchControllerWithTarget();
            Simulate(fixture.Controller, 0.1f, null, TargetStatusList(CreateStatus(1, false)));

            Assert.That(GetProperty(fixture.Controller, "CurrentState").ToString(), Is.EqualTo("PATROL"));
            Assert.That(((PlayerId)GetProperty(fixture.Controller, "CurrentTargetId")).IsValid, Is.False);
            yield return null;
        }

        [UnityTest] public IEnumerator STK_P_042_SearchTimeoutClearsTargetAndEpisodeState()
        {
            var fixture = SetupSearchControllerWithTarget();
            SetPrivateField(fixture.Controller, "searchDuration", 0.1f);
            Simulate(fixture.Controller, 0.2f, null, TargetStatusList(CreateStatus(1, true)));

            Assert.That(GetProperty(fixture.Controller, "CurrentState").ToString(), Is.EqualTo("PATROL"));
            Assert.That(((PlayerId)GetProperty(fixture.Controller, "CurrentTargetId")).IsValid, Is.False);
            Assert.That(((PlayerId)GetProperty(fixture.Controller, "DetectionTargetId")).IsValid, Is.False);
            Assert.That((bool)GetProperty(GetProperty(fixture.Controller, "ActiveSearchEpisodeId"), "IsValid"), Is.False);
            yield return null;
        }

        [UnityTest] public IEnumerator STK_P_043_PatrolDetectChaseSearchPatrolRegressionPreservesFrozenLkp()
        {
            var fixture = CreateControllerFixture();
            SetState(fixture.Controller, "PATROL");

            Simulate(fixture.Controller, 0.1f, TargetCandidateList(CreateCandidate(1, true)), TargetStatusList(CreateStatus(1, true)));
            Assert.That(GetProperty(fixture.Controller, "CurrentState").ToString(), Is.EqualTo("DETECT"));
            Simulate(fixture.Controller, 1f, TargetCandidateList(CreateCandidate(1, true)), TargetStatusList(CreateStatus(1, true)));
            Assert.That(GetProperty(fixture.Controller, "CurrentState").ToString(), Is.EqualTo("CHASE"));
            var frozenLkp = (Vector3)GetProperty(fixture.Controller, "LastKnownPosition");

            Simulate(fixture.Controller, 0.1f, null, TargetStatusList(CreateStatus(1, true)));
            Assert.That(GetProperty(fixture.Controller, "CurrentState").ToString(), Is.EqualTo("SEARCH"));
            Simulate(fixture.Controller, 0.1f, null, TargetStatusList(CreateStatus(1, true)));
            Assert.That((Vector3)GetProperty(fixture.Controller, "LastKnownPosition"), Is.EqualTo(frozenLkp));
            SetPrivateField(fixture.Controller, "searchDuration", 0.1f);
            Simulate(fixture.Controller, 0.2f, null, TargetStatusList(CreateStatus(1, true)));
            Assert.That(GetProperty(fixture.Controller, "CurrentState").ToString(), Is.EqualTo("PATROL"));
            yield return null;
        }

        private ControllerFixture SetupSearchControllerWithTarget()
        {
            var fixture = CreateControllerFixture();
            SetState(fixture.Controller, "SEARCH");
            Invoke(fixture.Memory, "SetCurrentTarget", new[] { typeof(PlayerId) }, new PlayerId(1));
            Invoke(fixture.Memory, "TryAcceptCurrentTargetObservation", new[] { VisionObservationType }, CreateObservation(1, Vector3.forward, 1));
            return fixture;
        }

        private ControllerFixture CreateControllerFixture()
        {
            var root = new GameObject("STK_P3_Controller");
            _createdObjects.Add(root);
            root.AddComponent<NavMeshAgent>().enabled = false;
            var controller = root.AddComponent(StalkerControllerType);
            ((Behaviour)controller).enabled = false;
            SetPrivateField(controller, "detectionMeterFull", 0.5f);
            SetPrivateField(controller, "detectionFillRate", 1f);
            return new ControllerFixture(controller, GetPrivateField(controller, "_memory"));
        }

        private static object BakeFixture(object graph)
        {
            return Invoke(BakeUtilityType, "Bake", new[] { GraphType, DefinitionArrayType, EdgeBakeArrayType, typeof(int) },
                graph,
                DefinitionArray(
                    Definition(new RegionId(1), new Vector3(-5f, 0f, 0f), new Vector3(4f, 3f, 3f)),
                    Definition(new RegionId(2), new Vector3(0f, 0f, 0f), new Vector3(4f, 3f, 3f)),
                    Definition(new RegionId(3), new Vector3(5f, 0f, 0f), new Vector3(4f, 3f, 3f))),
                EdgeBakeArray(
                    EdgeBake(new RegionId(1), new RegionId(2), new DoorId(1)),
                    EdgeBake(new RegionId(2), new RegionId(1), new DoorId(1)),
                    EdgeBake(new RegionId(2), new RegionId(3), new DoorId(2)),
                    EdgeBake(new RegionId(3), new RegionId(2), new DoorId(2))),
                1);
        }

        private static object CreateFixtureGraph()
        {
            return Activator.CreateInstance(GraphType, NodeArray(
                Node(0, -6f, 1),
                Node(1, -4f, 0, 2),
                Node(2, -1f, 1, 3),
                Node(3, 1f, 2, 4),
                Node(4, 4f, 3, 5),
                Node(5, 6f, 4)));
        }

        private static object CreateSearchPlanner(object graph, string mode)
        {
            var coverage = Activator.CreateInstance(CoverageMemoryType, (int)GetProperty(graph, "NodeCount"));
            return Activator.CreateInstance(SearchPlannerType, graph, null, coverage, CreateSearchPathEvaluator(mode));
        }

        private static object CreateSearchContext(long episodeId, Vector3 lkp, Vector3 direction, RegionId regionId)
        {
            return Activator.CreateInstance(SearchContextType, Activator.CreateInstance(SearchEpisodeIdType, episodeId), lkp, direction, new AiSimulationTime(episodeId, episodeId * 0.1d), regionId);
        }

        private static object CreateCandidate(int playerId, bool eligible)
        {
            return Activator.CreateInstance(TargetCandidateType, CreateObservation(playerId, new Vector3(playerId, 0f, 2f), playerId), CreateEligibility(eligible));
        }

        private static object CreateStatus(int playerId, bool eligible)
        {
            return Activator.CreateInstance(TargetStatusType, new PlayerId(playerId), CreateEligibility(eligible));
        }

        private static object CreateEligibility(bool eligible)
        {
            if (eligible)
            {
                return Invoke(TargetEligibilityResultType, "EligibleTarget", Type.EmptyTypes);
            }

            return Invoke(TargetEligibilityResultType, "Ineligible", new[] { TargetEligibilityReasonType }, Enum.Parse(TargetEligibilityReasonType, "Eliminated"));
        }

        private static object CreateObservation(int playerId, Vector3 position, long tick)
        {
            return Activator.CreateInstance(VisionObservationType, new PlayerId(playerId), position, position.normalized, new AiSimulationTime(tick, tick * 0.1d), position.magnitude);
        }

        private static void Simulate(Component controller, float deltaSeconds, object candidates, object statuses)
        {
            var input = Activator.CreateInstance(SimulationInputType, new AiSimulationStep(new AiSimulationTime(1, 0.1d), deltaSeconds), candidates, statuses);
            Assert.That(Invoke(controller, "Simulate", new[] { SimulationInputType }, input), Is.EqualTo(true));
        }

        private static object Node(int id, float x, params int[] neighbors)
        {
            return Activator.CreateInstance(NodeType, id, new Vector3(x, 0f, 0f), 0, id, id * 3, id * 3 + 1, id * 3 + 2, new List<int>(neighbors));
        }

        private static object RegionNode(RegionId regionId)
        {
            return Activator.CreateInstance(RegionNodeType, regionId, RegionEdgeArray());
        }

        private static object Definition(RegionId regionId, Vector3 center, Vector3 size) => Activator.CreateInstance(DefinitionType, regionId, new Bounds(center, size));
        private static object EdgeBake(RegionId from, RegionId to, DoorId door) => Activator.CreateInstance(EdgeBakeType, from, to, door);

        private static object TargetCandidateList(params object[] values) => ToGenericList(TargetCandidateType, values);
        private static object TargetStatusList(params object[] values) => ToGenericList(TargetStatusType, values);
        private static Array NodeArray(params object[] values) => ToArray(NodeType, values);
        private static Array RegionNodeArray(params object[] values) => ToArray(RegionNodeType, values);
        private static Array RegionEdgeArray(params object[] values) => ToArray(RegionEdgeType, values);
        private static Array DefinitionArray(params object[] values) => ToArray(DefinitionType, values);
        private static Array EdgeBakeArray(params object[] values) => ToArray(EdgeBakeType, values);
        private static Array RegionIdArray(params RegionId[] values) => values;

        private static object ToGenericList(Type elementType, object[] values)
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
            for (var i = 0; i < values.Length; i++)
            {
                list.Add(values[i]);
            }

            return list;
        }

        private static Array ToArray(Type elementType, object[] values)
        {
            var array = Array.CreateInstance(elementType, values.Length);
            for (var i = 0; i < values.Length; i++)
            {
                array.SetValue(values[i], i);
            }

            return array;
        }

        private static Delegate CreatePathValidator(string mode)
        {
            return CreateBoolVector3Delegate(PatrolPathValidatorType, mode);
        }

        private static Delegate CreateSearchPathEvaluator(string mode)
        {
            return CreateEnumVector3Delegate(SearchPathEvaluatorType, NavigationEvaluationStatusType, mode);
        }

        private static Delegate CreateBoolVector3Delegate(Type delegateType, string mode)
        {
            var method = new DynamicMethod("PatrolPathValidatorStub", typeof(bool), new[] { typeof(Vector3) }, typeof(StalkerPhase3CanonicalPlayModeTests).Module);
            var il = method.GetILGenerator();
            il.Emit(mode == "Complete" ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ret);
            return method.CreateDelegate(delegateType);
        }

        private static Delegate CreateEnumVector3Delegate(Type delegateType, Type enumType, string mode)
        {
            var method = new DynamicMethod("SearchPathEvaluatorStub", enumType, new[] { typeof(Vector3) }, typeof(StalkerPhase3CanonicalPlayModeTests).Module);
            var il = method.GetILGenerator();
            if (mode == "PartialForXGreaterThanTwo")
            {
                var complete = il.DefineLabel();
                il.Emit(OpCodes.Ldarga_S, 0);
                il.Emit(OpCodes.Ldfld, typeof(Vector3).GetField("x"));
                il.Emit(OpCodes.Ldc_R4, 2f);
                il.Emit(OpCodes.Ble_Un_S, complete);
                il.Emit(OpCodes.Ldc_I4, (int)Enum.Parse(enumType, "Partial"));
                il.Emit(OpCodes.Ret);
                il.MarkLabel(complete);
            }
            else
            {
                il.Emit(OpCodes.Ldc_I4, (int)Enum.Parse(enumType, mode));
                il.Emit(OpCodes.Ret);
                return method.CreateDelegate(delegateType);
            }

            il.Emit(OpCodes.Ldc_I4, (int)Enum.Parse(enumType, "Complete"));
            il.Emit(OpCodes.Ret);
            return method.CreateDelegate(delegateType);
        }

        private static int GetCollectionCount(object collection, string memberName)
        {
            Assert.That(collection, Is.Not.Null, $"{memberName} resolved to null.");
            if (collection is ICollection collectionInterface)
            {
                return collectionInterface.Count;
            }

            var type = collection.GetType();
            var countProperty = type.GetProperty("Count", BindingFlags.Instance | BindingFlags.Public);
            if (countProperty != null)
            {
                return (int)countProperty.GetValue(collection);
            }

            var lengthProperty = type.GetProperty("Length", BindingFlags.Instance | BindingFlags.Public);
            if (lengthProperty != null)
            {
                return (int)lengthProperty.GetValue(collection);
            }

            Assert.Fail($"{memberName} resolved to unsupported collection type '{type.FullName}' with no ICollection, Count, or Length.");
            return -1;
        }

        private static object GetProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Missing property '{propertyName}' on '{target.GetType().FullName}'.");
            return property.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}' on '{target.GetType().FullName}'.");
            field.SetValue(target, value);
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}' on '{target.GetType().FullName}'.");
            return field.GetValue(target);
        }

        private static void SetState(Component controller, string stateName)
        {
            SetPrivateField(controller, "currentState", Enum.Parse(StalkerStateType, stateName));
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

        private static Type StalkerControllerType => ResolveType("EchoProtocol.AI.Stalker.StalkerController");
        private static Type StalkerStateType => ResolveType("EchoProtocol.AI.Stalker.StalkerState");
        private static Type StalkerMemoryType => ResolveType("EchoProtocol.AI.Stalker.StalkerMemory");
        private static Type VisionObservationType => ResolveType("EchoProtocol.AI.Stalker.VisionObservation");
        private static Type SimulationInputType => ResolveType("EchoProtocol.AI.Stalker.StalkerSimulationInput");
        private static Type TargetCandidateType => ResolveType("EchoProtocol.AI.Stalker.StalkerTargetCandidate");
        private static Type TargetStatusType => ResolveType("EchoProtocol.AI.Stalker.StalkerTargetStatus");
        private static Type TargetEligibilityResultType => ResolveType("EchoProtocol.AI.Stalker.StalkerTargetEligibilityResult");
        private static Type TargetEligibilityReasonType => ResolveType("EchoProtocol.AI.Stalker.StalkerTargetEligibilityReason");
        private static Type GraphType => ResolveType("EchoProtocol.AI.Stalker.Spatial.NavMeshSpatialGraph");
        private static Type NodeType => ResolveType("EchoProtocol.AI.Stalker.Spatial.SpatialNode");
        private static Type RegionGraphType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RegionGraph");
        private static Type RegionNodeType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RegionNode");
        private static Type RegionEdgeType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RegionEdge");
        private static Type CompatibilityIdentityType => ResolveType("EchoProtocol.AI.Stalker.Spatial.SpatialGraphCompatibilityIdentity");
        private static Type CoverageMemoryType => ResolveType("EchoProtocol.AI.Stalker.Spatial.CoverageMemory");
        private static Type GlobalPatrolPlannerType => ResolveType("EchoProtocol.AI.Stalker.Spatial.GlobalPatrolPlanner");
        private static Type GlobalPatrolObjectiveType => ResolveType("EchoProtocol.AI.Stalker.Spatial.GlobalPatrolObjective");
        private static Type LocalPatrolSelectorType => ResolveType("EchoProtocol.AI.Stalker.Spatial.LocalPatrolSelector");
        private static Type LocalPatrolSelectionType => ResolveType("EchoProtocol.AI.Stalker.Spatial.LocalPatrolSelection");
        private static Type PatrolPathValidatorType => ResolveType("EchoProtocol.AI.Stalker.Spatial.PatrolPathValidator");
        private static Type BakeUtilityType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RegionGraphBakeUtility");
        private static Type DefinitionType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RegionDefinitionBakeData");
        private static Type DefinitionArrayType => DefinitionType.MakeArrayType();
        private static Type EdgeBakeType => ResolveType("EchoProtocol.AI.Stalker.Spatial.RegionEdgeBakeData");
        private static Type EdgeBakeArrayType => EdgeBakeType.MakeArrayType();
        private static Type SearchContextType => ResolveType("EchoProtocol.AI.Stalker.StalkerSearchContext");
        private static Type SearchEpisodeIdType => ResolveType("EchoProtocol.AI.Stalker.SearchEpisodeId");
        private static Type SearchPlannerType => ResolveType("EchoProtocol.AI.Stalker.StalkerSearchPlanner");
        private static Type SearchSelectionType => ResolveType("EchoProtocol.AI.Stalker.SearchCandidateSelection");
        private static Type SearchPathEvaluatorType => ResolveType("EchoProtocol.AI.Stalker.SearchPathEvaluator");
        private static Type NavigationEvaluationStatusType => ResolveType("EchoProtocol.AI.Stalker.NavigationEvaluationStatus");
        private static Type[] TryGetObjectiveSignature => new[] { typeof(RegionId), typeof(RegionId), GlobalPatrolObjectiveType.MakeByRefType() };
        private static Type[] TryLocalSelectSignature => new[] { typeof(int), typeof(int), typeof(RegionId), LocalPatrolSelectionType.MakeByRefType() };
        private static Type[] TryGetRouteHopCostSignature => new[] { typeof(RegionId), typeof(RegionId), typeof(int).MakeByRefType() };
        private static Type[] TrySearchSelectSignature => new[] { SearchContextType, typeof(float), typeof(int), typeof(int), SearchSelectionType.MakeByRefType() };

        private readonly struct ControllerFixture
        {
            public ControllerFixture(Component controller, object memory)
            {
                Controller = controller;
                Memory = memory;
            }

            public Component Controller { get; }
            public object Memory { get; }
        }
    }
}
