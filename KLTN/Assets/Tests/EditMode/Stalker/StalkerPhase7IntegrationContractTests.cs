using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerPhase7IntegrationContractTests
    {
        private const string RuntimePath = "Assets/Scripts/AI/Stalker/Networking/StalkerFusionRuntime.cs";
        private const string LifeStateConsequenceSinkPath = "Assets/Scripts/AI/Stalker/Networking/StalkerNetworkLifeStateConsequenceSink.cs";
        private const string TelemetryAdapterPath = "Assets/Scripts/AI/Stalker/Telemetry/StalkerTelemetryAdapter.cs";
        private const string ProductionTelemetryProducerPath = "Assets/Scripts/AI/Stalker/Telemetry/StalkerProductionTelemetryProducer.cs";
        private const string MatchAuthorityRuntimePath = "Assets/_Project/Scripts/Networking/Authority/MatchAuthorityRuntime.cs";
        private const string DebugSnapshotTypeName = "EchoProtocol.AI.Stalker.Debug.StalkerAIDebugSnapshot";
        private const string PresentationDriverTypeName = "EchoProtocol.AI.Stalker.Networking.StalkerPresentationDriver";
        private const string PresentationStateTypeName = "EchoProtocol.AI.Stalker.Networking.StalkerNetworkPresentationState";
        private const string AttackPhaseTypeName = "EchoProtocol.AI.Stalker.Networking.StalkerNetworkAttackPhase";
        private const string TelemetryAdapterTypeName = "EchoProtocol.AI.Stalker.Telemetry.StalkerTelemetryAdapter";
        private const string TelemetryProducerTypeName = "EchoProtocol.AI.Stalker.Telemetry.IStalkerTelemetryProducer";
        private const string TelemetryIdentityTypeName = "EchoProtocol.AI.Stalker.Telemetry.StalkerTelemetryMonsterIdentity";
        private const string TelemetryPublishResultTypeName = "EchoProtocol.AI.Stalker.Telemetry.StalkerTelemetryPublishResult";
        private const string AttackFactTypeName = "EchoProtocol.AI.Stalker.Telemetry.StalkerAttackResolvedFact";
        private const string SearchFactTypeName = "EchoProtocol.AI.Stalker.Telemetry.StalkerSearchEndedFact";
        private const string SearchOutcomeTypeName = "EchoProtocol.AI.Stalker.Telemetry.StalkerSearchTerminalOutcome";
        private const string AttackEpisodeIdTypeName = "EchoProtocol.AI.Stalker.StalkerAttackEpisodeId";
        private const string SearchEpisodeIdTypeName = "EchoProtocol.AI.Stalker.SearchEpisodeId";
        private const string AttackOutcomeTypeName = "EchoProtocol.AI.Stalker.StalkerAttackOutcome";
        private const string StalkerStateTypeName = "EchoProtocol.AI.Stalker.StalkerState";
        private const string AiSimulationTimeTypeName = "EchoProtocol.AI.Common.AiSimulationTime";

        [Test]
        public void STK_N_011_ReplicatedRuntimeStateOmitsPrivateTargetAndMemoryFields()
        {
            var source = File.ReadAllText(RuntimePath);

            StringAssert.Contains("[Networked] public int ReplicatedSemanticState", source);
            StringAssert.Contains("[Networked] public long ReplicatedAttackEpisodeId", source);
            StringAssert.Contains("[Networked] public int ReplicatedAttackPhase", source);
            StringAssert.Contains("[Networked] public int ReplicatedAttackOutcome", source);
            StringAssert.DoesNotContain("ReplicatedAuthoritativeSimulationCount", source);
            StringAssert.DoesNotContain("[Networked] public UnityEngine.Vector3 LastKnownPosition", source);
            StringAssert.DoesNotContain("[Networked] public int CurrentTarget", source);
            StringAssert.DoesNotContain("[Networked] public int DetectionTarget", source);
            StringAssert.DoesNotContain("[Networked] public UnityEngine.Vector3 LastSeenDirection", source);
        }

        [Test]
        public void STK_N_011_ReplicatedRuntimeStateOmitsPrivateAiKnowledgeByNetworkedAttribute()
        {
            var runtimeType = ResolveType("EchoProtocol.AI.Stalker.Networking.StalkerFusionRuntime");
            var networkedNames = runtimeType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(property => property.GetCustomAttributes(false)
                    .Any(attribute => attribute.GetType().FullName == "Fusion.NetworkedAttribute"))
                .Select(property => property.Name)
                .ToArray();
            var forbiddenTerms = new[]
            {
                "CurrentTarget",
                "DetectionTarget",
                "LastKnown",
                "LKP",
                "LastSeenDirection",
                "SearchContext",
                "CoverageMemory",
                "CoverageHistory",
                "PlannerCandidate",
                "CandidateList",
                "ReasonHistory"
            };

            Assert.That(networkedNames, Does.Contain("ReplicatedSemanticState"));
            Assert.That(networkedNames, Does.Contain("ReplicatedAttackEpisodeId"));
            Assert.That(networkedNames, Has.No.Member("ReplicatedAuthoritativeSimulationCount"));
            foreach (var name in networkedNames)
            {
                foreach (var forbidden in forbiddenTerms)
                {
                    Assert.That(
                        name.IndexOf(forbidden, StringComparison.OrdinalIgnoreCase),
                        Is.EqualTo(-1),
                        $"Networked field '{name}' must not expose private AI knowledge term '{forbidden}'.");
                }
            }
        }

        [Test]
        public void STK_N_009_010_015_PresentationDriverConsumesLateJoinSnapshotIdempotently()
        {
            var driver = Activator.CreateInstance(ResolveType(PresentationDriverTypeName));
            var state = CreatePresentationState(2L, "Windup", false, "None", 10L, -1L);
            var resolvedState = CreatePresentationState(2L, "Recover", true, "Hit", 10L, 12L);

            Assert.That(GetProperty(Invoke(driver, "Consume", new[] { ResolveType(PresentationStateTypeName) }, state), "Changed"), Is.EqualTo(true));
            Assert.That(GetProperty(Invoke(driver, "Consume", new[] { ResolveType(PresentationStateTypeName) }, state), "Changed"), Is.EqualTo(false));
            var resolvedConsume = Invoke(driver, "Consume", new[] { ResolveType(PresentationStateTypeName) }, resolvedState);
            Assert.That(GetProperty(resolvedConsume, "Changed"), Is.EqualTo(true));
            Assert.That(GetProperty(resolvedConsume, "AttackPhaseChanged"), Is.EqualTo(true));
            Assert.That(GetProperty(driver, "ChangeCount"), Is.EqualTo(2));
            Assert.That(GetProperty(driver, "LastConsumedAttackEpisodeId"), Is.EqualTo(CreateAttackEpisodeId(2L)));
            Assert.That(GetProperty(driver, "LastConsumedAttackOutcome").ToString(), Is.EqualTo("Hit"));
        }

        [Test]
        public void STK_N_014_PresentationProgressUpdateIsObservableAndDuplicateSnapshotIsIdempotent()
        {
            var driver = Activator.CreateInstance(ResolveType(PresentationDriverTypeName));
            var first = CreatePresentationState(4L, "Windup", 0.1f, false, "None", 10L, -1L);
            var duplicate = CreatePresentationState(4L, "Windup", 0.1f, false, "None", 10L, -1L);
            var progressed = CreatePresentationState(4L, "Windup", 0.2f, false, "None", 10L, -1L);

            Assert.That(GetProperty(Invoke(driver, "Consume", new[] { ResolveType(PresentationStateTypeName) }, first), "Changed"), Is.EqualTo(true));
            Assert.That(GetProperty(Invoke(driver, "Consume", new[] { ResolveType(PresentationStateTypeName) }, duplicate), "Changed"), Is.EqualTo(false));
            var result = Invoke(driver, "Consume", new[] { ResolveType(PresentationStateTypeName) }, progressed);
            Assert.That(GetProperty(result, "Changed"), Is.EqualTo(true));
            Assert.That(GetProperty(result, "AttackProgressUpdated"), Is.EqualTo(true));
            Assert.That(GetProperty(driver, "ChangeCount"), Is.EqualTo(2));
        }

        [Test]
        public void STK_N_014_SimulationCountIsNotPresentationPayload()
        {
            var presentationStateType = ResolveType(PresentationStateTypeName);

            Assert.That(presentationStateType.GetProperty("AuthoritativeSimulationCount"), Is.Null);
            Assert.That(
                presentationStateType.GetConstructors()
                    .SelectMany(constructor => constructor.GetParameters())
                    .Select(parameter => parameter.Name),
                Has.No.Member("authoritativeSimulationCount"));
        }

        [Test]
        public void STK_OBS_001_DebugSnapshotIsImmutableProjection()
        {
            var snapshotType = ResolveType(DebugSnapshotTypeName);

            Assert.That(snapshotType.IsValueType, Is.True);
            foreach (var property in snapshotType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                Assert.That(property.CanWrite, Is.False, $"{property.Name} must be read-only.");
            }
        }

        [Test]
        public void STK_TEL_001_SearchOutcomeDomainIsCanonical()
        {
            Assert.That(
                Enum.GetNames(ResolveType(SearchOutcomeTypeName)),
                Is.EquivalentTo(new[]
                {
                    "SAME_TARGET_REACQUIRED",
                    "NEW_ELIGIBLE_TARGET_OBSERVED",
                    "TIMEOUT",
                    "CURRENT_TARGET_INVALID_NO_REPLACEMENT"
                }));
        }

        [Test]
        public void STK_FSM_001_StalkerStateDomainRemainsFrozenSixStates()
        {
            Assert.That(
                Enum.GetNames(ResolveType(StalkerStateTypeName)),
                Is.EquivalentTo(new[]
                {
                    "PATROL",
                    "DETECT",
                    "CHASE",
                    "ATTACK",
                    "RECOVER",
                    "SEARCH"
                }));
        }

        [Test]
        public void STK_TEL_004_SearchOutcomeRejectsUnknownNumericEnumValue()
        {
            var invalidFact = Activator.CreateInstance(
                ResolveType(SearchFactTypeName),
                Activator.CreateInstance(ResolveType(SearchEpisodeIdTypeName), 44L),
                Enum.ToObject(ResolveType(SearchOutcomeTypeName), 999),
                CreateTime(30L, 3d));

            Assert.That(GetProperty(invalidFact, "IsValid"), Is.EqualTo(false));

            var adapter = Activator.CreateInstance(ResolveType(TelemetryAdapterTypeName));
            var producer = CreateRecordingTelemetryProducer(
                ParsePublishResult("Accepted"),
                ParsePublishResult("Accepted"),
                out var counters);
            var result = Invoke(
                adapter,
                "TryPublishSearchEnded",
                SearchPublishSignature,
                CreateTelemetryIdentity("stalker-test"),
                invalidFact,
                producer);

            Assert.That(result.ToString(), Is.EqualTo("InvalidOccurrence"));
            Assert.That(Invoke(
                adapter,
                "TryPublishSearchEnded",
                SearchPublishSignature,
                CreateTelemetryIdentity("stalker-test"),
                invalidFact,
                producer).ToString(), Is.EqualTo("AlreadyHandled"));
            Assert.That(counters.SearchCount.GetValue(null), Is.EqualTo(0));
        }

        [Test]
        public void STK_TEL_003_SearchTerminalCommitIsEpisodeKeyedBeforeCleanup()
        {
            var source = File.ReadAllText("Assets/Scripts/AI/Stalker/StalkerController.cs");

            StringAssert.Contains("_lastCommittedSearchEpisodeId == _searchContext.EpisodeId", source);
            StringAssert.Contains("new StalkerSearchEndedFact", source);
            StringAssert.Contains("CommitSearchEnded(StalkerSearchTerminalOutcome.TIMEOUT)", source);
            StringAssert.Contains("CommitSearchEnded(StalkerSearchTerminalOutcome.SAME_TARGET_REACQUIRED)", source);
            StringAssert.Contains("CommitSearchEnded(StalkerSearchTerminalOutcome.NEW_ELIGIBLE_TARGET_OBSERVED)", source);
            StringAssert.Contains("CommitSearchEnded(StalkerSearchTerminalOutcome.CURRENT_TARGET_INVALID_NO_REPLACEMENT)", source);
        }

        [Test]
        public void STK_N_016_TelemetryAdapterDeduplicatesAttackAndSearchOccurrences()
        {
            var adapter = Activator.CreateInstance(ResolveType(TelemetryAdapterTypeName));
            var producer = CreateRecordingTelemetryProducer(
                ParsePublishResult("Accepted"),
                ParsePublishResult("Accepted"),
                out var counters);
            var identity = CreateTelemetryIdentity("stalker-test");
            var attackFact = CreateAttackFact(3L, "Hit", 20L, 2d);
            var searchFact = CreateSearchFact(5L, "TIMEOUT", 30L, 3d);

            Assert.That(Invoke(adapter, "TryPublishAttackResolved", AttackPublishSignature, identity, attackFact, producer).ToString(), Is.EqualTo("Accepted"));
            Assert.That(Invoke(adapter, "TryPublishAttackResolved", AttackPublishSignature, identity, attackFact, producer).ToString(), Is.EqualTo("AlreadyHandled"));
            Assert.That(Invoke(adapter, "TryPublishSearchEnded", SearchPublishSignature, identity, searchFact, producer).ToString(), Is.EqualTo("Accepted"));
            Assert.That(Invoke(adapter, "TryPublishSearchEnded", SearchPublishSignature, identity, searchFact, producer).ToString(), Is.EqualTo("AlreadyHandled"));

            Assert.That(counters.AttackCount.GetValue(null), Is.EqualTo(1));
            Assert.That(counters.SearchCount.GetValue(null), Is.EqualTo(1));
        }

        [Test]
        public void STK_N_016_TelemetryRetryableFailureCanBeOfferedAgain()
        {
            var adapter = Activator.CreateInstance(ResolveType(TelemetryAdapterTypeName));
            var producer = CreateRecordingTelemetryProducer(
                ParsePublishResult("RetryableFailure"),
                ParsePublishResult("Accepted"),
                out var counters);
            var attackFact = CreateAttackFact(7L, "Hit", 20L, 2d);
            var identity = CreateTelemetryIdentity("stalker-test");

            Assert.That(Invoke(adapter, "TryPublishAttackResolved", AttackPublishSignature, identity, attackFact, producer).ToString(), Is.EqualTo("RetryableFailure"));
            Assert.That(Invoke(adapter, "TryPublishAttackResolved", AttackPublishSignature, identity, attackFact, producer).ToString(), Is.EqualTo("Accepted"));
            Assert.That(counters.AttackCount.GetValue(null), Is.EqualTo(2));
        }

        [Test]
        public void STK_N_016_TelemetrySuppressedOccurrenceIsNotBusyRetried()
        {
            var adapter = Activator.CreateInstance(ResolveType(TelemetryAdapterTypeName));
            var producer = CreateRecordingTelemetryProducer(
                ParsePublishResult("Suppressed"),
                ParsePublishResult("Suppressed"),
                out var counters);
            var searchFact = CreateSearchFact(8L, "TIMEOUT", 30L, 3d);
            var identity = CreateTelemetryIdentity("stalker-test");

            Assert.That(Invoke(adapter, "TryPublishSearchEnded", SearchPublishSignature, identity, searchFact, producer).ToString(), Is.EqualTo("Suppressed"));
            Assert.That(Invoke(adapter, "TryPublishSearchEnded", SearchPublishSignature, identity, searchFact, producer).ToString(), Is.EqualTo("AlreadyHandled"));
            Assert.That(counters.SearchCount.GetValue(null), Is.EqualTo(1));
        }

        [Test]
        public void STK_TEL_002_ReservedTargetAndPlayerDownedEventsAreNotOwnedByStalkerAdapter()
        {
            var source = File.ReadAllText(TelemetryAdapterPath);

            StringAssert.DoesNotContain("MONSTER_TARGET_ACQUIRED", source);
            StringAssert.DoesNotContain("MONSTER_TARGET_LOST", source);
            StringAssert.DoesNotContain("PLAYER_DOWNED", source);
            StringAssert.DoesNotContain("TelemetryEvent", source);
            StringAssert.DoesNotContain("eventSequence", source);
            StringAssert.DoesNotContain("matchId", source);
        }

        [Test]
        public void STK_TEL_005_LifeStateConsequenceSinkDoesNotOwnResearchTelemetry()
        {
            var source = File.ReadAllText(LifeStateConsequenceSinkPath);

            StringAssert.Contains("TryApplyMonsterDown", source);
            StringAssert.Contains("TryEliminateForReviveLimit", source);
            StringAssert.DoesNotContain("MatchAuthorityRuntime", source);
            StringAssert.DoesNotContain("RecordStalkerAttackResolved", source);
            StringAssert.DoesNotContain("MONSTER_ATTACK_RESOLVED", source);
            StringAssert.DoesNotContain("PLAYER_DOWNED", source);
        }

        [Test]
        public void STK_TEL_006_CommittedFactsAreTheOnlyRuntimeResearchTelemetrySource()
        {
            var runtimeSource = File.ReadAllText(RuntimePath);
            var sinkSource = File.ReadAllText(LifeStateConsequenceSinkPath);
            var producerSource = File.ReadAllText(ProductionTelemetryProducerPath);

            StringAssert.Contains("PublishCommittedTelemetryFacts", runtimeSource);
            StringAssert.Contains("TryPublishAttackResolved", runtimeSource);
            StringAssert.Contains("TryPublishSearchEnded", runtimeSource);
            StringAssert.Contains("TryRecordStalkerAttackResolved", producerSource);
            StringAssert.Contains("TryRecordStalkerSearchEnded", producerSource);
            StringAssert.DoesNotContain("RecordStalkerAttackResolved", sinkSource);
            StringAssert.DoesNotContain("TryRecordStalkerAttackResolved", sinkSource);
        }

        [Test]
        public void STK_TEL_007_RuntimePublishesTelemetryOnlyFromHostStateAuthority()
        {
            var source = File.ReadAllText(RuntimePath);

            StringAssert.Contains("!Runner.IsServer", source);
            StringAssert.Contains("!Object.HasStateAuthority", source);
            StringAssert.Contains("BindProductionTelemetryProducer", source);
            StringAssert.Contains("new StalkerProductionTelemetryProducer", source);
            StringAssert.Contains("TelemetryProducer != null && !ReferenceEquals", source);
        }

        [Test]
        public void STK_TEL_008_ProductionBridgeMapsAuthorityResultsWithoutCollapsingRetryableFailures()
        {
            var producerSource = File.ReadAllText(ProductionTelemetryProducerPath);
            var authoritySource = File.ReadAllText(MatchAuthorityRuntimePath);

            StringAssert.Contains("ProductionTelemetryPublishResult.RetryableFailure", authoritySource);
            StringAssert.Contains("ProductionTelemetryPublishResult.Suppressed", authoritySource);
            StringAssert.Contains("ProductionTelemetryPublishResult.InvalidOccurrence", authoritySource);
            StringAssert.Contains("ProductionTelemetryPublishResult.Accepted", authoritySource);
            StringAssert.Contains("TryRecordStalkerAttackResolved", producerSource);
            StringAssert.Contains("TryRecordStalkerSearchEnded", producerSource);
            StringAssert.Contains("StalkerTelemetryPublishResult.RetryableFailure", producerSource);
            StringAssert.Contains("StalkerTelemetryPublishResult.Suppressed", producerSource);
            StringAssert.Contains("StalkerTelemetryPublishResult.InvalidOccurrence", producerSource);
            StringAssert.Contains("StalkerTelemetryPublishResult.Accepted", producerSource);
            StringAssert.Contains("ToUpperInvariant", producerSource);
        }

        private static object CreatePresentationState(
            long episodeId,
            string phase,
            bool resolved,
            string outcome,
            long startedTick,
            long resolvedTick)
        {
            return Activator.CreateInstance(
                ResolveType(PresentationStateTypeName),
                Enum.Parse(ResolveType(StalkerStateTypeName), resolved ? "RECOVER" : "ATTACK"),
                CreateAttackEpisodeId(episodeId),
                Enum.Parse(ResolveType(AttackPhaseTypeName), phase),
                0.5f,
                resolved,
                Enum.Parse(ResolveType(AttackOutcomeTypeName), outcome),
                startedTick,
                resolvedTick);
        }

        private static object CreatePresentationState(
            long episodeId,
            string phase,
            float progressSeconds,
            bool resolved,
            string outcome,
            long startedTick,
            long resolvedTick)
        {
            return Activator.CreateInstance(
                ResolveType(PresentationStateTypeName),
                Enum.Parse(ResolveType(StalkerStateTypeName), resolved ? "RECOVER" : "ATTACK"),
                CreateAttackEpisodeId(episodeId),
                Enum.Parse(ResolveType(AttackPhaseTypeName), phase),
                progressSeconds,
                resolved,
                Enum.Parse(ResolveType(AttackOutcomeTypeName), outcome),
                startedTick,
                resolvedTick);
        }

        private static object CreateAttackFact(long episodeId, string outcome, long tick, double seconds)
        {
            return Activator.CreateInstance(
                ResolveType(AttackFactTypeName),
                CreateAttackEpisodeId(episodeId),
                Enum.Parse(ResolveType(AttackOutcomeTypeName), outcome),
                CreateTime(tick, seconds));
        }

        private static object CreateSearchFact(long episodeId, string outcome, long tick, double seconds)
        {
            return Activator.CreateInstance(
                ResolveType(SearchFactTypeName),
                Activator.CreateInstance(ResolveType(SearchEpisodeIdTypeName), episodeId),
                Enum.Parse(ResolveType(SearchOutcomeTypeName), outcome),
                CreateTime(tick, seconds));
        }

        private static object CreateAttackEpisodeId(long episodeId)
        {
            return Activator.CreateInstance(ResolveType(AttackEpisodeIdTypeName), episodeId);
        }

        private static object CreateTime(long tick, double seconds)
        {
            return Activator.CreateInstance(ResolveType(AiSimulationTimeTypeName), tick, seconds);
        }

        private static object CreateTelemetryIdentity(string value)
        {
            return Activator.CreateInstance(ResolveType(TelemetryIdentityTypeName), value);
        }

        private static object ParsePublishResult(string value)
        {
            return Enum.Parse(ResolveType(TelemetryPublishResultTypeName), value);
        }

        private static object CreateRecordingTelemetryProducer(
            object firstResult,
            object laterResult,
            out RecordingCounterFields counters)
        {
            var interfaceType = ResolveType(TelemetryProducerTypeName);
            var assemblyName = new AssemblyName("StalkerPhase7TelemetryProducerProxy");
            var assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var module = assembly.DefineDynamicModule("Main");
            var type = module.DefineType(
                "RecordingStalkerTelemetryProducer",
                TypeAttributes.Public | TypeAttributes.Sealed);
            type.AddInterfaceImplementation(interfaceType);

            var attackCount = type.DefineField("AttackCount", typeof(int), FieldAttributes.Public | FieldAttributes.Static);
            var searchCount = type.DefineField("SearchCount", typeof(int), FieldAttributes.Public | FieldAttributes.Static);
            var firstResultField = type.DefineField("FirstResult", ResolveType(TelemetryPublishResultTypeName), FieldAttributes.Public | FieldAttributes.Static);
            var laterResultField = type.DefineField("LaterResult", ResolveType(TelemetryPublishResultTypeName), FieldAttributes.Public | FieldAttributes.Static);
            ImplementCounterMethod(type, interfaceType.GetMethod("TryPublishMonsterAttackResolved"), attackCount, firstResultField, laterResultField);
            ImplementCounterMethod(type, interfaceType.GetMethod("TryPublishMonsterSearchEnded"), searchCount, firstResultField, laterResultField);

            var concreteType = type.CreateType();
            concreteType.GetField("FirstResult").SetValue(null, firstResult);
            concreteType.GetField("LaterResult").SetValue(null, laterResult);
            counters = new RecordingCounterFields(
                concreteType.GetField("AttackCount"),
                concreteType.GetField("SearchCount"));
            return Activator.CreateInstance(concreteType);
        }

        private static void ImplementCounterMethod(
            TypeBuilder type,
            MethodInfo interfaceMethod,
            FieldBuilder counter,
            FieldBuilder firstResult,
            FieldBuilder laterResult)
        {
            var parameters = interfaceMethod.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
            var method = type.DefineMethod(
                interfaceMethod.Name,
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final,
                ResolveType(TelemetryPublishResultTypeName),
                parameters);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldsfld, counter);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Stsfld, counter);
            il.Emit(OpCodes.Ldsfld, counter);
            il.Emit(OpCodes.Ldc_I4_1);
            var laterLabel = il.DefineLabel();
            il.Emit(OpCodes.Bgt_S, laterLabel);
            il.Emit(OpCodes.Ldsfld, firstResult);
            il.Emit(OpCodes.Ret);
            il.MarkLabel(laterLabel);
            il.Emit(OpCodes.Ldsfld, laterResult);
            il.Emit(OpCodes.Ret);
            type.DefineMethodOverride(method, interfaceMethod);
        }

        private static object Invoke(object target, string methodName, Type[] parameterTypes, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public, null, parameterTypes, null);
            Assert.That(method, Is.Not.Null, $"Missing method '{methodName}' on '{target.GetType().FullName}'.");
            return method.Invoke(target, args);
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

            Assert.Fail($"Could not find production type '{fullTypeName}'.");
            return null;
        }

        private static Type[] AttackPublishSignature => new[]
        {
            ResolveType(TelemetryIdentityTypeName),
            ResolveType(AttackFactTypeName),
            ResolveType(TelemetryProducerTypeName)
        };

        private static Type[] SearchPublishSignature => new[]
        {
            ResolveType(TelemetryIdentityTypeName),
            ResolveType(SearchFactTypeName),
            ResolveType(TelemetryProducerTypeName)
        };

        private readonly struct RecordingCounterFields
        {
            public RecordingCounterFields(FieldInfo attackCount, FieldInfo searchCount)
            {
                AttackCount = attackCount;
                SearchCount = searchCount;
            }

            public FieldInfo AttackCount { get; }
            public FieldInfo SearchCount { get; }
        }
    }
}
