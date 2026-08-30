using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerAttackControllerTests
    {
        private const string PlayerIdTypeName = "EchoProtocol.AI.Common.PlayerId";
        private const string AiSimulationTimeTypeName = "EchoProtocol.AI.Common.AiSimulationTime";
        private const string AiSimulationStepTypeName = "EchoProtocol.AI.Common.AiSimulationStep";
        private const string AttackControllerTypeName = "EchoProtocol.AI.Stalker.StalkerAttackController";
        private const string AttackEpisodeIdTypeName = "EchoProtocol.AI.Stalker.StalkerAttackEpisodeId";
        private const string AttackTargetSnapshotTypeName = "EchoProtocol.AI.Stalker.StalkerAttackTargetSnapshot";
        private const string AttackResolutionResultTypeName = "EchoProtocol.AI.Stalker.StalkerAttackResolutionResult";
        private const string AttackOutcomeTypeName = "EchoProtocol.AI.Stalker.StalkerAttackOutcome";
        private const string ConsequenceSinkTypeName = "EchoProtocol.AI.Stalker.IPlayerAttackConsequenceSink";
        private const string DiagnosticSinkTypeName = "EchoProtocol.AI.Stalker.StalkerDiagnosticAttackConsequenceSink";

        [Test]
        public void STK_ATTACK_EpisodeId_DefaultInvalid_AndDeterministicComparable()
        {
            var idType = ResolveType(AttackEpisodeIdTypeName);
            var invalid = Activator.CreateInstance(idType);
            var one = Activator.CreateInstance(idType, 1L);
            var anotherOne = Activator.CreateInstance(idType, 1L);
            var two = Activator.CreateInstance(idType, 2L);

            Assert.That(GetBoolProperty(invalid, "IsValid"), Is.False);
            Assert.That(GetBoolProperty(one, "IsValid"), Is.True);
            Assert.That(one, Is.EqualTo(anotherOne));
            Assert.That(one.GetHashCode(), Is.EqualTo(anotherOne.GetHashCode()));
            Assert.That((int)InvokeMethod(one, "CompareTo", new[] { idType }, new[] { two }), Is.LessThan(0));
        }

        [Test]
        public void STK_ATTACK_BeginAttack_CreatesOneStableEpisode_WithFrozenTargetAndStartTime()
        {
            var controller = CreateController();
            var playerId = CreatePlayerId(7);
            var step = CreateStep(42L, 12.5d, 0.1f);

            var episode = BeginAttack(controller, true, playerId, step);
            var duplicate = BeginAttack(controller, true, playerId, CreateStep(43L, 12.6d, 0.1f));

            Assert.That(GetBoolProperty(GetProperty(episode, "EpisodeId"), "IsValid"), Is.True);
            Assert.That(GetProperty(duplicate, "EpisodeId"), Is.EqualTo(GetProperty(episode, "EpisodeId")));
            AssertPlayerIdValue(GetProperty(episode, "TargetIdAtEntry"), 7);
            Assert.That(GetProperty(episode, "StartedAt"), Is.EqualTo(CreateTime(42L, 12.5d)));
            Assert.That(GetBoolProperty(episode, "HitMomentResolved"), Is.False);
            Assert.That(GetEnumPropertyName(episode, "Outcome", AttackOutcomeTypeName), Is.EqualTo("None"));
        }

        [Test]
        public void STK_ATTACK_ResolveHitMoment_HitCommitsGuardBeforeDuplicateSideEffects()
        {
            var controller = CreateController();
            var playerId = CreatePlayerId(1);
            var step = CreateStep(10L, 1d, 0.25f);
            var episode = BeginAttack(controller, true, playerId, step);
            InvokeMethod(controller, "AdvanceWindup", new[] { typeof(float) }, new object[] { 0.25f });
            var episodeId = GetProperty(episode, "EpisodeId");
            var sink = Activator.CreateInstance(ResolveType(DiagnosticSinkTypeName));
            var snapshot = CreateTargetSnapshot(1, true, new Vector3(0f, 0f, 1f), true);

            var first = ResolveHitMoment(controller, true, episodeId, Vector3.zero, 2f, snapshot, sink, step);
            var second = ResolveHitMoment(controller, true, episodeId, Vector3.zero, 2f, snapshot, sink, step);

            Assert.That(GetEnumName(first, AttackResolutionResultTypeName), Is.EqualTo("ResolvedHit"));
            Assert.That(GetEnumName(second, AttackResolutionResultTypeName), Is.EqualTo("AlreadyResolved"));
            Assert.That(GetIntProperty(sink, "CallCount"), Is.EqualTo(1));
            Assert.That(GetBoolProperty(controller, "HitMomentResolved"), Is.True);
            Assert.That(GetEnumPropertyName(controller, "Outcome", AttackOutcomeTypeName), Is.EqualTo("Hit"));
            Assert.That(GetIntProperty(controller, "ResolutionCount"), Is.EqualTo(1));
        }

        [Test]
        public void STK_ATTACK_ResolveHitMoment_HitsWithoutConsequenceSink_WhenTargetIsValidCorrectAndInRange()
        {
            var controller = CreateController();
            var step = CreateStep(11L, 1.1d, 0.25f);
            var episode = BeginAttack(controller, true, CreatePlayerId(1), step);
            var snapshot = CreateTargetSnapshot(1, true, Vector3.forward, false);

            var result = ResolveHitMoment(
                controller,
                true,
                GetProperty(episode, "EpisodeId"),
                Vector3.zero,
                2f,
                snapshot,
                null,
                step);

            Assert.That(GetEnumName(result, AttackResolutionResultTypeName), Is.EqualTo("ResolvedHit"));
            Assert.That(GetEnumPropertyName(controller, "Outcome", AttackOutcomeTypeName), Is.EqualTo("Hit"));
            Assert.That(GetIntProperty(controller, "ResolutionCount"), Is.EqualTo(1));
        }

        [Test]
        public void STK_ATTACK_ResolveHitMoment_MissesForOutOfRangeInvalidOrWrongTarget()
        {
            AssertMiss(CreateTargetSnapshot(1, true, new Vector3(0f, 0f, 5f), true));
            AssertMiss(CreateTargetSnapshot(1, false, new Vector3(0f, 0f, 1f), true));
            AssertMiss(CreateTargetSnapshot(2, true, new Vector3(0f, 0f, 1f), true));
        }

        [Test]
        public void STK_ATTACK_ResolveHitMoment_FailsClosedWithoutActiveEpisodeOrAuthority()
        {
            var controller = CreateController();
            var playerId = CreatePlayerId(1);
            var step = CreateStep(1L, 1d, 0.1f);
            var invalidId = Activator.CreateInstance(ResolveType(AttackEpisodeIdTypeName));

            var noEpisode = ResolveHitMoment(
                controller,
                true,
                invalidId,
                Vector3.zero,
                2f,
                CreateTargetSnapshot(1, true, Vector3.forward, true),
                null,
                step);

            BeginAttack(controller, true, playerId, step);
            var activeId = GetProperty(controller, "ActiveEpisodeId");
            var proxy = ResolveHitMoment(
                controller,
                false,
                activeId,
                Vector3.zero,
                2f,
                CreateTargetSnapshot(1, true, Vector3.forward, true),
                null,
                step);

            Assert.That(GetEnumName(noEpisode, AttackResolutionResultTypeName), Is.EqualTo("NoActiveEpisode"));
            Assert.That(GetEnumName(proxy, AttackResolutionResultTypeName), Is.EqualTo("NotStateAuthority"));
            Assert.That(GetBoolProperty(controller, "HitMomentResolved"), Is.False);
        }

        [Test]
        public void STK_ATTACK_DiagnosticSink_LeavesProductionLifeStateBindingExplicit()
        {
            var sinkType = ResolveType(DiagnosticSinkTypeName);

            Assert.That(
                sinkType.GetField("ProductionBindingStatus", BindingFlags.Public | BindingFlags.Static).GetRawConstantValue(),
                Is.EqualTo("PLAYER_LIFE_STATE_BINDING_REQUIRED"));
        }

        private static void AssertMiss(object snapshot)
        {
            var controller = CreateController();
            var step = CreateStep(2L, 2d, 0.1f);
            var episode = BeginAttack(controller, true, CreatePlayerId(1), step);
            var sink = Activator.CreateInstance(ResolveType(DiagnosticSinkTypeName));

            var result = ResolveHitMoment(
                controller,
                true,
                GetProperty(episode, "EpisodeId"),
                Vector3.zero,
                2f,
                snapshot,
                sink,
                step);

            Assert.That(GetEnumName(result, AttackResolutionResultTypeName), Is.EqualTo("ResolvedMiss"));
            Assert.That(GetEnumPropertyName(controller, "Outcome", AttackOutcomeTypeName), Is.EqualTo("Miss"));
            Assert.That(GetIntProperty(sink, "CallCount"), Is.EqualTo(0));
        }

        private static object CreateController()
        {
            return Activator.CreateInstance(ResolveType(AttackControllerTypeName));
        }

        private static object BeginAttack(object controller, bool hasAuthority, object playerId, object step)
        {
            return InvokeMethod(
                controller,
                "BeginAttack",
                new[] { typeof(bool), ResolveType(PlayerIdTypeName), ResolveType(AiSimulationStepTypeName) },
                new[] { hasAuthority, playerId, step });
        }

        private static object ResolveHitMoment(
            object controller,
            bool hasAuthority,
            object episodeId,
            Vector3 stalkerPosition,
            float attackRange,
            object snapshot,
            object sink,
            object step)
        {
            return InvokeMethod(
                controller,
                "ResolveHitMoment",
                new[]
                {
                    typeof(bool),
                    ResolveType(AttackEpisodeIdTypeName),
                    typeof(Vector3),
                    typeof(float),
                    ResolveType(AttackTargetSnapshotTypeName),
                    ResolveType(ConsequenceSinkTypeName),
                    ResolveType(AiSimulationStepTypeName)
                },
                new[] { hasAuthority, episodeId, stalkerPosition, attackRange, snapshot, sink, step });
        }

        private static object CreateTargetSnapshot(int playerId, bool valid, Vector3 position, bool hasConsequenceReceiver)
        {
            return Activator.CreateInstance(
                ResolveType(AttackTargetSnapshotTypeName),
                CreatePlayerId(playerId),
                valid,
                position,
                hasConsequenceReceiver);
        }

        private static object CreatePlayerId(int value)
        {
            return Activator.CreateInstance(ResolveType(PlayerIdTypeName), value);
        }

        private static object CreateTime(long tick, double seconds)
        {
            return Activator.CreateInstance(ResolveType(AiSimulationTimeTypeName), tick, seconds);
        }

        private static object CreateStep(long tick, double seconds, float deltaSeconds)
        {
            return Activator.CreateInstance(ResolveType(AiSimulationStepTypeName), CreateTime(tick, seconds), deltaSeconds);
        }

        private static object InvokeMethod(object target, string methodName, Type[] parameterTypes, object[] args)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                parameterTypes,
                null);
            Assert.That(method, Is.Not.Null, $"Missing public method '{methodName}' on '{target.GetType().FullName}'.");
            return method.Invoke(target, args);
        }

        private static object GetProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Missing public property '{propertyName}' on '{target.GetType().FullName}'.");
            return property.GetValue(target);
        }

        private static bool GetBoolProperty(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.TypeOf<bool>());
            return (bool)value;
        }

        private static int GetIntProperty(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.TypeOf<int>());
            return (int)value;
        }

        private static string GetEnumPropertyName(object target, string propertyName, string expectedTypeName)
        {
            return GetEnumName(GetProperty(target, propertyName), expectedTypeName);
        }

        private static string GetEnumName(object value, string expectedTypeName)
        {
            Assert.That(value, Is.Not.Null);
            Assert.That(value.GetType(), Is.EqualTo(ResolveType(expectedTypeName)));
            return value.ToString();
        }

        private static void AssertPlayerIdValue(object playerId, int expected)
        {
            Assert.That(GetBoolProperty(playerId, "IsValid"), Is.True);
            Assert.That((int)GetProperty(playerId, "Value"), Is.EqualTo(expected));
        }

        private static Type ResolveType(string fullTypeName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                var type = assemblies[i].GetType(fullTypeName, false);
                if (type != null)
                {
                    return type;
                }
            }

            Assert.Fail($"Could not find production type '{fullTypeName}' in the loaded Unity AppDomain.");
            return null;
        }
    }
}
