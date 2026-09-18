using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerChaseSearchHidingTests
    {
        private const string StalkerControllerTypeName = "EchoProtocol.AI.Stalker.StalkerController";
        private const string StalkerStateTypeName = "EchoProtocol.AI.Stalker.StalkerState";
        private const string StalkerTargetStatusTypeName = "EchoProtocol.AI.Stalker.StalkerTargetStatus";
        private const string StalkerTargetCandidateTypeName = "EchoProtocol.AI.Stalker.StalkerTargetCandidate";
        private const string StalkerTargetEligibilityResultTypeName = "EchoProtocol.AI.Stalker.StalkerTargetEligibilityResult";
        private const string StalkerTargetEligibilityReasonTypeName = "EchoProtocol.AI.Stalker.StalkerTargetEligibilityReason";
        private const string VisionObservationTypeName = "EchoProtocol.AI.Stalker.VisionObservation";
        private const string StalkerHideSpotCandidateTypeName = "EchoProtocol.AI.Stalker.StalkerHideSpotCandidate";
        private const string PlayerIdTypeName = "EchoProtocol.AI.Common.PlayerId";
        private const string AiSimulationTimeTypeName = "EchoProtocol.AI.Common.AiSimulationTime";

        private readonly List<GameObject> _createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (var i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void STK_CHASE_HiddenCurrentTarget_EntersSearchAndPreservesLastKnownPosition()
        {
            var controller = CreateController();
            var targetId = CreatePlayerId(1);
            var lastKnownPosition = new Vector3(4f, 0f, 2f);
            SeedTargetMemory(controller, targetId, lastKnownPosition);
            SetTypedFrame(controller, CreateStatusList(CreateHiddenStatus(targetId)));
            SetState(controller, "CHASE");

            InvokePrivate(controller, "TickChaseTyped");

            Assert.That(GetProperty(controller, "CurrentState").ToString(), Is.EqualTo("SEARCH"));
            AssertPlayerIdValue(GetProperty(controller, "CurrentTargetId"), 1);
            Assert.That(GetProperty(controller, "HasLastKnownPosition"), Is.EqualTo(true));
            Assert.That(GetMemoryLastKnownPosition(controller), Is.EqualTo(lastKnownPosition));
        }

        [Test]
        public void STK_RECOVER_HiddenCurrentTarget_EntersSearchAfterRecovery()
        {
            var controller = CreateController();
            var targetId = CreatePlayerId(1);
            var lastKnownPosition = new Vector3(3f, 0f, 5f);
            SeedTargetMemory(controller, targetId, lastKnownPosition);
            SetTypedFrame(controller, CreateStatusList(CreateHiddenStatus(targetId)));
            SetState(controller, "RECOVER");
            SetField(controller, "attackRecovery", 0f);
            SetField(controller, "_currentSimulationDeltaSeconds", 0.1f);

            InvokePrivate(controller, "TickRecoverTyped");

            Assert.That(GetProperty(controller, "CurrentState").ToString(), Is.EqualTo("SEARCH"));
            AssertPlayerIdValue(GetProperty(controller, "CurrentTargetId"), 1);
            Assert.That(GetProperty(controller, "HasLastKnownPosition"), Is.EqualTo(true));
            Assert.That(GetMemoryLastKnownPosition(controller), Is.EqualTo(lastKnownPosition));
        }

        [Test]
        public void STK_SEARCH_HiddenCurrentTarget_RemainsSearchSubjectWithoutVisualRefresh()
        {
            var controller = CreateController();
            var targetId = CreatePlayerId(1);
            var lastKnownPosition = new Vector3(2f, 0f, 6f);
            SeedTargetMemory(controller, targetId, lastKnownPosition);
            SetTypedFrame(
                controller,
                CreateStatusList(CreateHiddenStatus(targetId)),
                CreateCandidateList(CreateVisibleCandidate(targetId, new Vector3(20f, 0f, 20f))));
            SetState(controller, "SEARCH");
            SetField(controller, "searchDuration", 10f);
            SetField(controller, "_currentSimulationDeltaSeconds", 0.1f);

            InvokePrivate(controller, "TickSearchTyped");

            Assert.That(GetProperty(controller, "CurrentState").ToString(), Is.EqualTo("SEARCH"));
            AssertPlayerIdValue(GetProperty(controller, "CurrentTargetId"), 1);
            Assert.That(GetProperty(controller, "HasLastKnownPosition"), Is.EqualTo(true));
            Assert.That(GetMemoryLastKnownPosition(controller), Is.EqualTo(lastKnownPosition));
        }

        [Test]
        public void STK_SEARCH_HideInvestigation_DoesNotPauseSearchTimeout()
        {
            var controller = CreateController();
            var targetId = CreatePlayerId(1);
            SeedTargetMemory(controller, targetId, new Vector3(1f, 0f, 1f));
            SetTypedFrame(controller, CreateStatusList(CreateHiddenStatus(targetId)));
            SetState(controller, "SEARCH");
            SetField(controller, "searchDuration", 1f);
            SetField(controller, "searchElapsedTime", 0.95f);
            SetField(controller, "_currentSimulationDeltaSeconds", 0.1f);

            InvokePrivate(controller, "InitializeHidingInvestigation");
            var investigation = GetField(controller, "_hidingInvestigation");
            InvokeInstance(
                investigation,
                "Begin",
                new[] { ResolveType(StalkerHideSpotCandidateTypeName) },
                Activator.CreateInstance(
                    ResolveType(StalkerHideSpotCandidateTypeName),
                    1UL,
                    Vector3.forward,
                    Vector3.forward,
                    true));

            InvokePrivate(controller, "TickSearchTyped");

            Assert.That(GetProperty(controller, "CurrentState").ToString(), Is.EqualTo("PATROL"));
        }

        [Test]
        public void STK_RECOVER_NonHiddenInvalidTarget_StillFallsBackToPatrol()
        {
            var controller = CreateController();
            var targetId = CreatePlayerId(1);
            SeedTargetMemory(controller, targetId, new Vector3(1f, 0f, 1f));
            SetTypedFrame(
                controller,
                CreateStatusList(CreateStatus(targetId, CreateIneligibleResult("Downed"))));
            SetState(controller, "RECOVER");
            SetField(controller, "attackRecovery", 0f);
            SetField(controller, "_currentSimulationDeltaSeconds", 0.1f);

            InvokePrivate(controller, "TickRecoverTyped");

            Assert.That(GetProperty(controller, "CurrentState").ToString(), Is.EqualTo("PATROL"));
            Assert.That(GetBoolProperty(GetProperty(controller, "CurrentTargetId"), "IsValid"), Is.False);
        }

        private Component CreateController()
        {
            var gameObject = new GameObject("STK_ChaseSearchHiding_Controller");
            _createdObjects.Add(gameObject);
            return gameObject.AddComponent(ResolveType(StalkerControllerTypeName));
        }

        private static void SetState(object controller, string stateName)
        {
            SetField(controller, "currentState", Enum.Parse(ResolveType(StalkerStateTypeName), stateName));
        }

        private static object CreateHiddenStatus(object playerId)
        {
            return CreateStatus(playerId, CreateIneligibleResult("OtherGameplayState"), true);
        }

        private static object CreateStatus(object playerId, object eligibility, bool isHidden = false)
        {
            return Activator.CreateInstance(
                ResolveType(StalkerTargetStatusTypeName),
                playerId,
                eligibility,
                isHidden);
        }

        private static object CreateVisibleCandidate(object playerId, Vector3 position)
        {
            var observation = Activator.CreateInstance(
                ResolveType(VisionObservationTypeName),
                playerId,
                position,
                Vector3.forward,
                Activator.CreateInstance(ResolveType(AiSimulationTimeTypeName), 1L, 1d),
                position.magnitude);

            return Activator.CreateInstance(
                ResolveType(StalkerTargetCandidateTypeName),
                observation,
                CreateEligibleResult());
        }

        private static IList CreateStatusList(params object[] statuses)
        {
            var list = (IList)Activator.CreateInstance(
                typeof(List<>).MakeGenericType(ResolveType(StalkerTargetStatusTypeName)));
            for (var i = 0; i < statuses.Length; i++)
            {
                list.Add(statuses[i]);
            }

            return list;
        }

        private static IList CreateCandidateList(params object[] candidates)
        {
            var list = (IList)Activator.CreateInstance(
                typeof(List<>).MakeGenericType(ResolveType(StalkerTargetCandidateTypeName)));
            for (var i = 0; i < candidates.Length; i++)
            {
                list.Add(candidates[i]);
            }

            return list;
        }

        private static void SeedTargetMemory(object controller, object targetId, Vector3 lastKnownPosition)
        {
            var memory = GetField(controller, "_memory");
            InvokeInstance(
                memory,
                "SetCurrentTarget",
                new[] { ResolveType(PlayerIdTypeName) },
                targetId);

            var observation = Activator.CreateInstance(
                ResolveType(VisionObservationTypeName),
                targetId,
                lastKnownPosition,
                Vector3.forward,
                Activator.CreateInstance(ResolveType(AiSimulationTimeTypeName), 1L, 1d),
                lastKnownPosition.magnitude);

            InvokeInstance(
                memory,
                "TryAcceptCurrentTargetObservation",
                new[] { ResolveType(VisionObservationTypeName) },
                observation);
        }

        private static Vector3 GetMemoryLastKnownPosition(object controller)
        {
            var memory = GetField(controller, "_memory");
            var property = memory.GetType().GetProperty(
                "LastKnownPosition",
                BindingFlags.Instance | BindingFlags.Public);

            Assert.That(property, Is.Not.Null);

            return (Vector3)property.GetValue(memory);
        }

        private static void SetTypedFrame(object controller, object statuses, object candidates = null)
        {
            SetField(controller, "_currentTargetStatuses", statuses);
            SetField(controller, "_currentVisibleTargetCandidates", candidates);
        }

        private static object CreatePlayerId(int value)
        {
            return Activator.CreateInstance(ResolveType(PlayerIdTypeName), value);
        }

        private static object CreateEligibleResult()
        {
            return ResolveType(StalkerTargetEligibilityResultTypeName)
                .GetMethod("EligibleTarget", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, Array.Empty<object>());
        }

        private static object CreateIneligibleResult(string reasonName)
        {
            var reason = Enum.Parse(ResolveType(StalkerTargetEligibilityReasonTypeName), reasonName);
            return ResolveType(StalkerTargetEligibilityResultTypeName)
                .GetMethod("Ineligible", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new[] { reason });
        }

        private static object GetField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
            return field.GetValue(target);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
            field.SetValue(target, value);
        }

        private static object GetProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Missing public property '{propertyName}'.");
            return property.GetValue(target);
        }

        private static bool GetBoolProperty(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.TypeOf<bool>());
            return (bool)value;
        }

        private static void AssertPlayerIdValue(object playerId, int expectedValue)
        {
            Assert.That(GetBoolProperty(playerId, "IsValid"), Is.True);
            Assert.That(GetProperty(playerId, "Value"), Is.EqualTo(expectedValue));
        }

        private static void InvokePrivate(object target, string methodName)
        {
            InvokeInstance(target, methodName, Type.EmptyTypes);
        }

        private static object InvokeInstance(object target, string methodName, Type[] parameterTypes, params object[] arguments)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                parameterTypes,
                null);
            Assert.That(method, Is.Not.Null, $"Missing method '{methodName}'.");
            return method.Invoke(target, arguments);
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

            Assert.Fail($"Could not find production type '{fullTypeName}' in loaded Unity AppDomain.");
            return null;
        }
    }
}
