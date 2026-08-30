using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using EchoProtocol.AI.Common;
using EchoProtocol.AI.Common.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerSearchCanonicalTests
    {
        [Test]
        public void STK_P3_SearchContext_FreezesImmutableOriginDataAndEpisodeId()
        {
            var context = CreateSearchContext(7, new Vector3(3f, 0f, 4f), Vector3.right, new RegionId(2));

            Invoke(context, "RecordCandidateAttempt", new[] { typeof(int) }, 1);
            Invoke(context, "RecordCandidateAttempt", new[] { typeof(int) }, 2);

            Assert.That((long)GetProperty(GetProperty(context, "EpisodeId"), "Value"), Is.EqualTo(7L));
            Assert.That((Vector3)GetProperty(context, "SearchOriginLKP"), Is.EqualTo(new Vector3(3f, 0f, 4f)));
            Assert.That((Vector3)GetProperty(context, "SearchOriginDirection"), Is.EqualTo(Vector3.right));
            Assert.That((RegionId)GetProperty(context, "SearchOriginRegionId"), Is.EqualTo(new RegionId(2)));
        }

        [Test]
        public void STK_P3_SearchPlanner_UsesEndpointRadiusNotPathGeometryRadius()
        {
            var graph = CreateGraph(4f);
            var context = CreateSearchContext(1, Vector3.zero, Vector3.right, RegionId.Invalid);
            var planner = CreateSearchPlanner(graph, "Complete");
            var args = new object[] { context, 5f, 0, -1, null };

            var selected = (bool)Invoke(planner, "TrySelectCandidate", TrySelectCandidateSignature, args);

            Assert.That(selected, Is.True);
            Assert.That(GetProperty(GetProperty(args[4], "DestinationNode"), "Id"), Is.EqualTo(1));
        }

        [Test]
        public void STK_P3_SearchPlanner_RejectsEndpointOutsideRadius()
        {
            var graph = CreateGraph(6f);
            var context = CreateSearchContext(1, Vector3.zero, Vector3.right, RegionId.Invalid);
            var planner = CreateSearchPlanner(graph, "Complete");
            var args = new object[] { context, 5f, 0, -1, null };

            var selected = (bool)Invoke(planner, "TrySelectCandidate", TrySelectCandidateSignature, args);

            Assert.That(selected, Is.False);
            Assert.That(GetProperty(planner, "LastRejectReason").ToString(), Is.EqualTo("OutsideSearchRadius"));
        }

        [Test]
        public void STK_P3_SearchPlanner_RejectsPartialPath()
        {
            var graph = CreateGraph(2f);
            var context = CreateSearchContext(1, Vector3.zero, Vector3.right, RegionId.Invalid);
            var planner = CreateSearchPlanner(graph, "Partial");
            var args = new object[] { context, 5f, 0, -1, null };

            var selected = (bool)Invoke(planner, "TrySelectCandidate", TrySelectCandidateSignature, args);

            Assert.That(selected, Is.False);
            Assert.That(GetProperty(planner, "LastRejectReason").ToString(), Is.EqualTo("PathPartial"));
        }

        private static object CreateSearchPlanner(object graph, string statusName)
        {
            var coverage = Activator.CreateInstance(CoverageMemoryType, GetProperty(graph, "NodeCount"));
            var evaluator = CreateSearchPathEvaluator(statusName);
            return Activator.CreateInstance(SearchPlannerType, graph, null, coverage, evaluator);
        }

        private static Delegate CreateSearchPathEvaluator(string statusName)
        {
            var method = new DynamicMethod(
                "SearchPathEvaluatorStub",
                NavigationEvaluationStatusType,
                new[] { typeof(Vector3) },
                typeof(StalkerSearchCanonicalTests).Module);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldc_I4, (int)Enum.Parse(NavigationEvaluationStatusType, statusName));
            il.Emit(OpCodes.Ret);
            return method.CreateDelegate(SearchPathEvaluatorType);
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

        private static object CreateGraph(float candidateX)
        {
            return Activator.CreateInstance(GraphType, NodeArray(
                Node(0, Vector3.zero, 1),
                Node(1, new Vector3(candidateX, 0f, 0f), 0)));
        }

        private static object Node(int id, Vector3 position, params int[] neighbors)
        {
            return Activator.CreateInstance(NodeType, id, position, 0, id, id * 3, id * 3 + 1, id * 3 + 2, new List<int>(neighbors));
        }

        private static Array NodeArray(params object[] values)
        {
            var array = Array.CreateInstance(NodeType, values.Length);
            for (var i = 0; i < values.Length; i++)
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

        private static object Invoke(object target, string methodName, Type[] parameterTypes, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public, null, parameterTypes, null);
            Assert.That(method, Is.Not.Null, $"Missing method '{methodName}' on '{target.GetType().FullName}'.");
            return method.Invoke(target, args);
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
        private static Type CoverageMemoryType => ResolveType("EchoProtocol.AI.Stalker.Spatial.CoverageMemory");
        private static Type SearchContextType => ResolveType("EchoProtocol.AI.Stalker.StalkerSearchContext");
        private static Type SearchEpisodeIdType => ResolveType("EchoProtocol.AI.Stalker.SearchEpisodeId");
        private static Type SearchPlannerType => ResolveType("EchoProtocol.AI.Stalker.StalkerSearchPlanner");
        private static Type SearchSelectionType => ResolveType("EchoProtocol.AI.Stalker.SearchCandidateSelection");
        private static Type SearchPathEvaluatorType => ResolveType("EchoProtocol.AI.Stalker.SearchPathEvaluator");
        private static Type NavigationEvaluationStatusType => ResolveType("EchoProtocol.AI.Stalker.NavigationEvaluationStatus");
        private static Type[] TrySelectCandidateSignature => new[] { SearchContextType, typeof(float), typeof(int), typeof(int), SearchSelectionType.MakeByRefType() };
    }
}
