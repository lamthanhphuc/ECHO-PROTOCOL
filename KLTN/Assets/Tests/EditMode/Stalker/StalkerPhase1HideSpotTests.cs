using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using EchoProtocol.AI.Common;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerPhase1HideSpotTests
    {
        private const string CandidateTypeName = "EchoProtocol.AI.Stalker.StalkerHideSpotCandidate";
        private const string MemoryTypeName = "EchoProtocol.AI.Stalker.StalkerHideSpotMemory";
        private const string SelectorTypeName = "EchoProtocol.AI.Stalker.StalkerHideSpotSelector";
        private const string SelectorConfigTypeName = "EchoProtocol.AI.Stalker.StalkerHideSpotSelectorConfig";
        private const string InspectionConfigTypeName = "EchoProtocol.AI.Stalker.StalkerHideSpotInspectionConfig";
        private const string InspectionResultTypeName = "EchoProtocol.AI.Stalker.StalkerHideSpotInspectionResult";
        private const string InvestigationTypeName = "EchoProtocol.AI.Stalker.StalkerHidingInvestigation";
        private const string ResolverTypeName = "EchoProtocol.AI.Stalker.IStalkerHideSpotInspectionResolver";
        private const string ControllerTypeName =
            "EchoProtocol.AI.Stalker.StalkerController";
        private const string InvestigationTickResultTypeName =
            "EchoProtocol.AI.Stalker.StalkerHidingInvestigationTickResult";
        private const string InvestigationTickStatusTypeName =
            "EchoProtocol.AI.Stalker.StalkerHidingInvestigationTickStatus";
        private const string EligibilityResultTypeName =
            "EchoProtocol.AI.Stalker.StalkerTargetEligibilityResult";
        private const string EligibilityReasonTypeName =
            "EchoProtocol.AI.Stalker.StalkerTargetEligibilityReason";

        [Test]
        public void STK_HIDE_SEL_SelectorContractContainsNoOccupancyOrHiddenPlayerInputs()
        {
            var names = ResolveType(CandidateTypeName)
                .GetProperties()
                .Select(property => property.Name)
                .ToArray();

            CollectionAssert.DoesNotContain(names, "IsOccupied");
            CollectionAssert.DoesNotContain(names, "Occupant");
            CollectionAssert.DoesNotContain(names, "PlayerId");
            CollectionAssert.DoesNotContain(names, "Transform");
        }

        [Test]
        public void STK_HIDE_SEL_LkpProximitySelectsNearestLegalCandidate()
        {
            var selection = Select(
                Vector3.zero,
                CreateMemory(),
                CreateSelectorConfig(searchRadius: 20f),
                Candidate(10, new Vector3(8f, 0f, 0f)),
                Candidate(20, new Vector3(2f, 0f, 0f)));

            Assert.That(StableId(GetProperty(selection, "Candidate")), Is.EqualTo(20UL));
        }

        [Test]
        public void STK_HIDE_SEL_HistoricalConfirmedUseCanBiasButNotDominateInfinitely()
        {
            var memory = CreateMemory();
            Invoke(memory, "RecordConfirmedUse", 10UL, TimeAt(1d));

            var selection = Select(
                Vector3.zero,
                memory,
                CreateSelectorConfig(searchRadius: 20f, confirmedUseBias: 2f),
                Candidate(10, new Vector3(5f, 0f, 0f)),
                Candidate(20, new Vector3(4f, 0f, 0f)));

            Assert.That(StableId(GetProperty(selection, "Candidate")), Is.EqualTo(10UL));

            for (var i = 0; i < 99; i++)
            {
                Invoke(
                    memory,
                    "RecordConfirmedUse",
                    10UL,
                    TimeAt(2d + i));
            }

            selection = Select(
                Vector3.zero,
                memory,
                CreateSelectorConfig(searchRadius: 25f, confirmedUseBias: 2f),
                Candidate(10, new Vector3(20f, 0f, 0f)),
                Candidate(20, new Vector3(1f, 0f, 0f)));

            Assert.That(StableId(GetProperty(selection, "Candidate")), Is.EqualTo(20UL));
        }

        [Test]
        public void STK_HIDE_SEL_ReinspectionCooldownRejectsRecentlyEmptySpot()
        {
            var memory = CreateMemory();
            Invoke(memory, "RecordEmptyInspection", 10UL, TimeAt(9d));

            var selection = Select(
                Vector3.zero,
                memory,
                CreateSelectorConfig(searchRadius: 20f, reinspectCooldown: 5f),
                10f,
                Candidate(10, new Vector3(1f, 0f, 0f)),
                Candidate(20, new Vector3(3f, 0f, 0f)));

            Assert.That(StableId(GetProperty(selection, "Candidate")), Is.EqualTo(20UL));
        }

        [Test]
        public void STK_HIDE_SEL_StableTieBreakIgnoresEnumerationOrder()
        {
            var memory = CreateMemory();
            var first = Select(
                Vector3.zero,
                memory,
                CreateSelectorConfig(searchRadius: 20f, distanceTieEpsilon: 0.1f),
                Candidate(20, new Vector3(2f, 0f, 0f)),
                Candidate(10, new Vector3(2f, 0f, 0f)));

            var second = Select(
                Vector3.zero,
                memory,
                CreateSelectorConfig(searchRadius: 20f, distanceTieEpsilon: 0.1f),
                Candidate(10, new Vector3(2f, 0f, 0f)),
                Candidate(20, new Vector3(2f, 0f, 0f)));

            Assert.That(StableId(GetProperty(first, "Candidate")), Is.EqualTo(10UL));
            Assert.That(StableId(GetProperty(second, "Candidate")), Is.EqualTo(10UL));
        }

        [Test]
        public void STK_HIDE_SEL_NoCandidatesReturnsFalse()
        {
            var selector = Activator.CreateInstance(ResolveType(SelectorTypeName));
            var args = new[]
            {
                (object)Vector3.zero,
                CreateCandidateList(),
                CreateMemory(),
                0f,
                CreateSelectorConfig(),
                null,
            };

            Assert.That((bool)Invoke(selector, "TrySelect", args), Is.False);
        }

        [Test]
        public void STK_HIDE_INSPECT_TooFarDoesNotQueryOccupancyResolver()
        {
            var investigation = CreateInvestigation(
                CreateMemory(),
                CreateResolverProxy(CreateEmptyInspectionResult()));
            Invoke(investigation, "Begin", Candidate(10, Vector3.zero));

            var result = Invoke(
                investigation,
                "Tick",
                new Vector3(5f, 0f, 0f),
                10f,
                TimeAt(10d),
                CreateInspectionConfig(distance: 1f, duration: 0f));

            Assert.That(StatusName(result), Is.EqualTo("MovingToSpot"));
            Assert.That(ResolverProxy.ResolveCount, Is.EqualTo(0));
        }

        [Test]
        public void STK_HIDE_INSPECT_AtValidRangeMayQueryOccupancyResolver()
        {
            var investigation = CreateInvestigation(
                CreateMemory(),
                CreateResolverProxy(CreateEmptyInspectionResult()));
            Invoke(investigation, "Begin", Candidate(10, Vector3.zero));

            var result = Invoke(
                investigation,
                "Tick",
                new Vector3(0.5f, 0f, 0f),
                0.2f,
                TimeAt(10d),
                CreateInspectionConfig(distance: 1f, duration: 0.1f));

            Assert.That(StatusName(result), Is.EqualTo("Empty"));
            Assert.That(ResolverProxy.ResolveCount, Is.EqualTo(1));
        }

        [Test]
        public void STK_HIDE_INSPECT_EmptyInspectionRecordsEmptyAndDoesNotFabricateTarget()
        {
            var memory = CreateMemory();
            var investigation = CreateInvestigation(
                memory,
                CreateResolverProxy(CreateEmptyInspectionResult()));
            Invoke(investigation, "Begin", Candidate(10, Vector3.zero));

            var result = Invoke(
                investigation,
                "Tick",
                Vector3.zero,
                1f,
                TimeAt(12d),
                CreateInspectionConfig(distance: 1f, duration: 0f));

            var snapshot = Invoke(memory, "GetSnapshot", 10UL);
            var inspection = GetProperty(result, "InspectionResult");
            Assert.That(StatusName(result), Is.EqualTo("Empty"));
            Assert.That((bool)GetProperty(inspection, "Occupied"), Is.False);
            Assert.That(GetBoolProperty(GetProperty(inspection, "ConfirmedPlayerId"), "IsValid"), Is.False);
            Assert.That(GetProperty(snapshot, "ConsecutiveEmptyInspections"), Is.EqualTo(1));
            Assert.That(((AiSimulationTime)GetProperty(snapshot, "LastInspectedTime")).Seconds, Is.EqualTo(12d));
        }

        [Test]
        public void STK_HIDE_INSPECT_OccupiedInspectionLegallyRevealsIdentityAndRecordsConfirmedUse()
        {
            var memory = CreateMemory();
            var occupied = CreateOccupiedInspectionResult(
                new PlayerId(7),
                new Vector3(1f, 0f, 0f),
                Vector3.forward);
            var investigation = CreateInvestigation(
                memory,
                CreateResolverProxy(occupied));
            Invoke(investigation, "Begin", Candidate(10, Vector3.zero));

            var result = Invoke(
                investigation,
                "Tick",
                Vector3.zero,
                1f,
                TimeAt(14d),
                CreateInspectionConfig(distance: 1f, duration: 0f));

            var snapshot = Invoke(memory, "GetSnapshot", 10UL);
            var inspection = GetProperty(result, "InspectionResult");
            Assert.That(StatusName(result), Is.EqualTo("Occupied"));
            Assert.That(GetProperty(GetProperty(inspection, "ConfirmedPlayerId"), "Value"), Is.EqualTo(7));
            Assert.That(GetProperty(snapshot, "ConfirmedUseCount"), Is.EqualTo(1));
            Assert.That(GetProperty(snapshot, "ConsecutiveEmptyInspections"), Is.EqualTo(0));
        }

        [Test]
        public void STK_HIDE_INSPECT_IneligibleOccupiedPlayerDoesNotBecomeChaseTarget()
        {
            var gameObject = new GameObject("StalkerPhase1EligibilityTest");

            try
            {
                var controller =
                    gameObject.AddComponent(
                        ResolveType(ControllerTypeName));

                var eligibilityResultType =
                    ResolveType(EligibilityResultTypeName);

                var eligibilityReasonType =
                    ResolveType(EligibilityReasonTypeName);

                var downedReason =
                    Enum.Parse(
                        eligibilityReasonType,
                        "Downed");

                var ineligibleMethod =
                    eligibilityResultType.GetMethod(
                        "Ineligible",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { eligibilityReasonType },
                        null);

                Assert.That(ineligibleMethod, Is.Not.Null);

                var ineligible =
                    ineligibleMethod.Invoke(
                        null,
                        new[] { downedReason });

                var occupiedByMethod =
                    ResolveType(InspectionResultTypeName)
                        .GetMethod(
                            "OccupiedBy",
                            BindingFlags.Public | BindingFlags.Static,
                            null,
                            new[]
                            {
                                typeof(PlayerId),
                                typeof(Vector3),
                                typeof(Vector3),
                                eligibilityResultType
                            },
                            null);

                Assert.That(occupiedByMethod, Is.Not.Null);

                var inspectionResult =
                    occupiedByMethod.Invoke(
                        null,
                        new object[]
                        {
                            new PlayerId(7),
                            new Vector3(1f, 0f, 0f),
                            Vector3.forward,
                            ineligible
                        });

                var occupiedStatus =
                    Enum.Parse(
                        ResolveType(InvestigationTickStatusTypeName),
                        "Occupied");

                var tickResult =
                    Activator.CreateInstance(
                        ResolveType(InvestigationTickResultTypeName),
                        occupiedStatus,
                        Candidate(10UL, Vector3.zero),
                        inspectionResult);

                Invoke(
                    controller,
                    "HandleOccupiedHideSpotInspection",
                    tickResult);

                var currentTargetId =
                    GetProperty(controller, "CurrentTargetId");

                Assert.That(
                    GetBoolProperty(currentTargetId, "IsValid"),
                    Is.False);

                Assert.That(
                    GetProperty(controller, "CurrentState").ToString(),
                    Is.Not.EqualTo("CHASE"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void STK_HIDE_INSPECT_LostNavigationCancelsActiveInvestigation()
        {
            var gameObject =
                new GameObject("StalkerPhase1LostNavigationTest");

            try
            {
                var controller =
                    gameObject.AddComponent(
                        ResolveType(ControllerTypeName));

                Invoke(
                    controller,
                    "InitializeHidingInvestigation");

                var investigationField =
                    controller.GetType().GetField(
                        "_hidingInvestigation",
                        BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(investigationField, Is.Not.Null);

                var investigation =
                    investigationField.GetValue(controller);

                Assert.That(investigation, Is.Not.Null);

                Invoke(
                    investigation,
                    "Begin",
                    Candidate(
                        10UL,
                        new Vector3(10f, 0f, 0f)));

                Assert.That(
                    (bool)GetProperty(
                        investigation,
                        "HasActiveCandidate"),
                    Is.True);

                var handled =
                    (bool)Invoke(
                        controller,
                        "TickHidingInvestigationIfActive");

                Assert.That(handled, Is.False);

                Assert.That(
                    (bool)GetProperty(
                        investigation,
                        "HasActiveCandidate"),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void STK_HIDE_INSPECT_UnresolvedOccupantDoesNotBecomeFalseEmptyInspection()
        {
            var spotObject =
                new GameObject("UnresolvedOccupantHideSpot");

            var occupantObject =
                new GameObject("UnresolvedOccupant");

            try
            {
                var hidingSpot =
                    spotObject.AddComponent(
                        ResolveType("HidingSpot"));

                var occupant =
                    occupantObject.AddComponent(
                        ResolveType("PlayerHidingController"));

                var occupantField =
                    hidingSpot.GetType().GetField(
                        "_occupant",
                        BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(occupantField, Is.Not.Null);

                occupantField.SetValue(
                    hidingSpot,
                    occupant);

                var adapter =
                    Activator.CreateInstance(
                        ResolveType(
                            "EchoProtocol.AI.Stalker.StalkerUnityHideSpotAdapter"));

                var candidates =
                    CreateCandidateList();

                var candidateCount =
                    (int)Invoke(
                        adapter,
                        "CollectCandidates",
                        candidates);

                Assert.That(candidateCount, Is.EqualTo(1));

                var args = new object[]
                {
                    candidates[0],
                    null
                };

                var resolved =
                    (bool)Invoke(
                        adapter,
                        "TryResolveInspection",
                        args);

                Assert.That(resolved, Is.False);

                var result = args[1];

                Assert.That(result, Is.Not.Null);

                Assert.That(
                    (bool)GetProperty(result, "Inspected"),
                    Is.False);

                Assert.That(
                    (bool)GetProperty(result, "Occupied"),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(occupantObject);
                UnityEngine.Object.DestroyImmediate(spotObject);
            }
        }

        [Test]
        public void STK_HIDE_SEL_CandidateHasNoHiddenTransformOrCurrentPlayerPosition()
        {
            var properties = ResolveType(CandidateTypeName).GetProperties();

            Assert.That(properties.Any(property => property.PropertyType == typeof(Transform)), Is.False);
            Assert.That(
                properties.Any(property => property.Name.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0),
                Is.False);
        }

        [Test]
        public void STK_HIDE_SEL_SameInputsAndMemoryAreDeterministic()
        {
            var memory = CreateMemory();
            Invoke(memory, "RecordConfirmedUse", 30UL, TimeAt(2d));
            Invoke(memory, "RecordEmptyInspection", 20UL, TimeAt(3d));
            var candidates = new[]
            {
                Candidate(30, new Vector3(4f, 0f, 0f)),
                Candidate(20, new Vector3(2f, 0f, 0f)),
                Candidate(10, new Vector3(3f, 0f, 0f)),
            };

            var first = Select(Vector3.zero, memory, CreateSelectorConfig(searchRadius: 20f), candidates);
            var second = Select(Vector3.zero, memory, CreateSelectorConfig(searchRadius: 20f), candidates);

            Assert.That(StableId(GetProperty(second, "Candidate")), Is.EqualTo(StableId(GetProperty(first, "Candidate"))));
            Assert.That(GetProperty(second, "Score"), Is.EqualTo(GetProperty(first, "Score")));
        }

        private static object Select(Vector3 anchor, object memory, object config, params object[] candidates)
        {
            return Select(anchor, memory, config, 100f, candidates);
        }

        private static object Select(
            Vector3 anchor,
            object memory,
            object config,
            float currentTimeSeconds,
            params object[] candidates)
        {
            var selector = Activator.CreateInstance(ResolveType(SelectorTypeName));
            var args = new[]
            {
                (object)anchor,
                CreateCandidateList(candidates),
                memory,
                currentTimeSeconds,
                config,
                null,
            };

            Assert.That((bool)Invoke(selector, "TrySelect", args), Is.True);
            return args[5];
        }

        private static object Candidate(ulong stableId, Vector3 position)
        {
            return Activator.CreateInstance(
                ResolveType(CandidateTypeName),
                stableId,
                position,
                position,
                true);
        }

        private static IList CreateCandidateList(params object[] candidates)
        {
            var list = (IList)Activator.CreateInstance(
                typeof(System.Collections.Generic.List<>)
                    .MakeGenericType(ResolveType(CandidateTypeName)));

            foreach (var candidate in candidates)
            {
                list.Add(candidate);
            }

            return list;
        }

        private static object CreateMemory()
        {
            return Activator.CreateInstance(ResolveType(MemoryTypeName));
        }

        private static object CreateSelectorConfig(
            float searchRadius = 10f,
            float confirmedUseBias = 0f,
            float emptyInspectionPenalty = 1f,
            float reinspectCooldown = 0f,
            float distanceTieEpsilon = 0f)
        {
            return Activator.CreateInstance(
                ResolveType(SelectorConfigTypeName),
                searchRadius,
                confirmedUseBias,
                emptyInspectionPenalty,
                reinspectCooldown,
                distanceTieEpsilon);
        }

        private static object CreateInspectionConfig(float distance, float duration)
        {
            return Activator.CreateInstance(ResolveType(InspectionConfigTypeName), distance, duration);
        }

        private static object CreateEmptyInspectionResult()
        {
            return ResolveType(InspectionResultTypeName)
                .GetMethod("Empty", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, Array.Empty<object>());
        }

        private static object CreateOccupiedInspectionResult(PlayerId playerId, Vector3 position, Vector3 direction)
        {
            return ResolveType(InspectionResultTypeName)
                .GetMethod(
                    "OccupiedBy",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[]
                    {
                        typeof(PlayerId),
                        typeof(Vector3),
                        typeof(Vector3)
                    },
                    null)
                .Invoke(null, new object[] { playerId, position, direction });
        }

        private static object CreateInvestigation(object memory, object resolver)
        {
            return Activator.CreateInstance(ResolveType(InvestigationTypeName), memory, resolver);
        }

        private static object CreateResolverProxy(object result)
        {
            ResolverProxy.ResolveCount = 0;
            ResolverProxy.Result = result;

            var createMethod = typeof(DispatchProxy)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method => method.Name == "Create"
                    && method.GetGenericArguments().Length == 2);

            return createMethod
                .MakeGenericMethod(ResolveType(ResolverTypeName), typeof(ResolverProxy))
                .Invoke(null, null);
        }

        private static string StatusName(object tickResult)
        {
            return GetProperty(tickResult, "Status").ToString();
        }

        private static ulong StableId(object candidate)
        {
            return (ulong)GetProperty(candidate, "StableId");
        }

        private static AiSimulationTime TimeAt(double seconds)
        {
            return new AiSimulationTime((long)(seconds * 60d), seconds);
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing method '{methodName}' on '{target.GetType().FullName}'.");
            return method.Invoke(target, args);
        }

        private static object GetProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Missing property '{propertyName}' on '{target.GetType().FullName}'.");
            return property.GetValue(target);
        }

        private static bool GetBoolProperty(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.TypeOf<bool>());
            return (bool)value;
        }

        private static Type ResolveType(string fullTypeName)
        {
            var type = Type.GetType($"{fullTypeName}, Assembly-CSharp")
                ?? Type.GetType(fullTypeName);

            Assert.That(type, Is.Not.Null, $"Could not resolve type '{fullTypeName}'.");
            return type;
        }

        public class ResolverProxy : DispatchProxy
        {
            public static object Result { get; set; }
            public static int ResolveCount { get; set; }

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (targetMethod.Name == "TryResolveInspection")
                {
                    ResolveCount++;
                    args[1] = Result;
                    return true;
                }

                throw new NotSupportedException(targetMethod.Name);
            }
        }
    }
}
