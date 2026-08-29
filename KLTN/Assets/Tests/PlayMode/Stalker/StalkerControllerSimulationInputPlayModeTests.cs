using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerControllerSimulationInputPlayModeTests
    {
        private const string PlayerIdTypeName = "EchoProtocol.AI.Common.PlayerId";
        private const string AiSimulationTimeTypeName = "EchoProtocol.AI.Common.AiSimulationTime";
        private const string AiSimulationStepTypeName = "EchoProtocol.AI.Common.AiSimulationStep";
        private const string StalkerControllerTypeName = "EchoProtocol.AI.Stalker.StalkerController";
        private const string StalkerVisionSensorTypeName = "EchoProtocol.AI.Stalker.StalkerVisionSensor";
        private const string StalkerSimulationInputTypeName = "EchoProtocol.AI.Stalker.StalkerSimulationInput";
        private const string StalkerTargetCandidateTypeName = "EchoProtocol.AI.Stalker.StalkerTargetCandidate";
        private const string StalkerTargetStatusTypeName = "EchoProtocol.AI.Stalker.StalkerTargetStatus";
        private const string StalkerTargetEligibilityResultTypeName = "EchoProtocol.AI.Stalker.StalkerTargetEligibilityResult";
        private const string VisionObservationTypeName = "EchoProtocol.AI.Stalker.VisionObservation";
        private const float FloatTolerance = 0.0001f;

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

        [UnityTest]
        public IEnumerator STK_SIM_InvalidSimulationStep_FailsClosedWithoutMutation()
        {
            var fixture = CreateFixture();
            SetState(fixture.Controller, "DETECT");
            SetPrivateField(fixture.Controller, "detectionTarget", fixture.Target);
            SetPrivateField(fixture.Controller, "detectionMeter", 0.5f);

            var accepted = Simulate(fixture.Controller, CreateSimulationInput(GetStaticProperty(ResolveType(AiSimulationStepTypeName), "Invalid"), null));

            Assert.That(accepted, Is.False);
            Assert.That(GetEnumPropertyName(fixture.Controller, "CurrentState"), Is.EqualTo("DETECT"));
            Assert.That(GetFloatProperty(fixture.Controller, "DetectionMeter"), Is.EqualTo(0.5f).Within(FloatTolerance));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_SIM_DetectVisibleTarget_UsesExplicitFillDelta()
        {
            var fixture = CreateFixture();
            SetState(fixture.Controller, "DETECT");
            SetPrivateField(fixture.Controller, "detectionTarget", fixture.Target);
            SetPrivateField(fixture.Controller, "detectionMeter", 0f);
            SetPrivateField(fixture.Controller, "detectionMeterFull", 10f);
            SetPrivateField(fixture.Controller, "detectionFillRate", 2f);
            Physics.SyncTransforms();

            Assert.That(Simulate(fixture.Controller, CreateSimulationInput(0.25f, null)), Is.True);

            Assert.That(GetFloatProperty(fixture.Controller, "DetectionMeter"), Is.EqualTo(0.5f).Within(FloatTolerance));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_SIM_DetectHiddenTarget_UsesExplicitDecayDelta()
        {
            var fixture = CreateFixture();
            SetState(fixture.Controller, "DETECT");
            SetPrivateField(fixture.Controller, "detectionTarget", fixture.Target);
            SetPrivateField(fixture.Controller, "detectionMeter", 1f);
            SetPrivateField(fixture.Controller, "detectionDecayRate", 2f);
            SetPrivateField(fixture.VisionSensor, "candidate", null);

            Assert.That(Simulate(fixture.Controller, CreateSimulationInput(0.25f, null)), Is.True);

            Assert.That(GetEnumPropertyName(fixture.Controller, "CurrentState"), Is.EqualTo("DETECT"));
            Assert.That(GetFloatProperty(fixture.Controller, "DetectionMeter"), Is.EqualTo(0.5f).Within(FloatTolerance));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_SIM_SearchTimer_UsesExplicitDelta()
        {
            var fixture = CreateFixture();
            SetState(fixture.Controller, "SEARCH");
            SetPrivateField(fixture.Controller, "currentTarget", fixture.Target);
            SetPrivateField(fixture.Controller, "searchElapsedTime", 0f);
            SetPrivateField(fixture.Controller, "searchDuration", 10f);
            SetPrivateField(fixture.VisionSensor, "candidate", null);

            Assert.That(Simulate(fixture.Controller, CreateSimulationInput(0.25f, null)), Is.True);

            Assert.That(GetEnumPropertyName(fixture.Controller, "CurrentState"), Is.EqualTo("SEARCH"));
            Assert.That(GetFloatProperty(fixture.Controller, "SearchElapsedTime"), Is.EqualTo(0.25f).Within(FloatTolerance));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_SIM_AttackWindup_UsesExplicitDeltaOnce()
        {
            var fixture = CreateFixture();
            SetState(fixture.Controller, "ATTACK");
            SetPrivateField(fixture.Controller, "currentTarget", fixture.Target);
            SetPrivateField(fixture.Controller, "attackElapsedTime", 0f);
            SetPrivateField(fixture.Controller, "attackWindup", 10f);

            Assert.That(Simulate(fixture.Controller, CreateSimulationInput(0.25f, null)), Is.True);

            Assert.That(GetEnumPropertyName(fixture.Controller, "CurrentState"), Is.EqualTo("ATTACK"));
            Assert.That(GetFloatProperty(fixture.Controller, "AttackElapsedTime"), Is.EqualTo(0.25f).Within(FloatTolerance));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_SIM_RecoverTimer_UsesExplicitDeltaOnce()
        {
            var fixture = CreateFixture();
            SetState(fixture.Controller, "RECOVER");
            SetPrivateField(fixture.Controller, "currentTarget", fixture.Target);
            SetPrivateField(fixture.Controller, "recoverElapsedTime", 0f);
            SetPrivateField(fixture.Controller, "attackRecovery", 10f);

            Assert.That(Simulate(fixture.Controller, CreateSimulationInput(0.25f, null)), Is.True);

            Assert.That(GetEnumPropertyName(fixture.Controller, "CurrentState"), Is.EqualTo("RECOVER"));
            Assert.That(GetFloatProperty(fixture.Controller, "RecoverElapsedTime"), Is.EqualTo(0.25f).Within(FloatTolerance));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_SIM_DetectPromotion_TicksOnlyStartingState()
        {
            var fixture = CreateFixture(new Vector3(0f, 1f, 1f));
            SetState(fixture.Controller, "DETECT");
            SetPrivateField(fixture.Controller, "detectionTarget", fixture.Target);
            SetPrivateField(fixture.Controller, "detectionMeter", 0f);
            SetPrivateField(fixture.Controller, "detectionMeterFull", 0.1f);
            SetPrivateField(fixture.Controller, "detectionFillRate", 1f);
            SetPrivateField(fixture.Controller, "attackRange", 5f);
            Physics.SyncTransforms();

            Assert.That(Simulate(fixture.Controller, CreateSimulationInput(0.25f, null)), Is.True);

            Assert.That(GetEnumPropertyName(fixture.Controller, "CurrentState"), Is.EqualTo("CHASE"));
            Assert.That(GetFloatProperty(fixture.Controller, "AttackElapsedTime"), Is.EqualTo(0f).Within(FloatTolerance));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_SIM_VisibleTargetCandidates_AreNotMutatedBySimulate()
        {
            var fixture = CreateFixture();
            var candidates = CreateTargetCandidateList(CreateTargetCandidate(3));
            var statuses = CreateTargetStatusList(CreateTargetStatus(3));
            var firstBefore = GetListItem(candidates, 0);
            var firstStatusBefore = GetListItem(statuses, 0);

            Assert.That(Simulate(fixture.Controller, CreateSimulationInput(0.25f, candidates, statuses)), Is.True);

            Assert.That(GetListCount(candidates), Is.EqualTo(1));
            Assert.That(GetListItem(candidates, 0), Is.EqualTo(firstBefore));
            Assert.That(GetListCount(statuses), Is.EqualTo(1));
            Assert.That(GetListItem(statuses, 0), Is.EqualTo(firstStatusBefore));
            Assert.That(GetPrivateField(fixture.Controller, "_currentVisibleTargetCandidates"), Is.Null);
            Assert.That(GetPrivateField(fixture.Controller, "_currentTargetStatuses"), Is.Null);
            Assert.That((float)GetPrivateField(fixture.Controller, "_currentSimulationDeltaSeconds"), Is.EqualTo(0f));
            Assert.That((double)GetPrivateField(fixture.Controller, "_currentSimulationSeconds"), Is.EqualTo(0d));
            Assert.That((bool)GetPrivateField(fixture.Controller, "_isSimulating"), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_SIM_DynamicSpatialPatrol_OnEnable_DoesNotPerformTimeDependentPlanning()
        {
            var controllerType = ResolveType(StalkerControllerTypeName);
            var stalker = new GameObject("STK_SIM_DynamicSpatialStalker");
            stalker.SetActive(false);
            _createdObjects.Add(stalker);

            var controller = (Component)stalker.AddComponent(controllerType);
            stalker.GetComponent<NavMeshAgent>().enabled = false;
            SetPrivateField(controller, "patrolMode", Enum.Parse(ResolveType("EchoProtocol.AI.Stalker.StalkerPatrolMode"), "DynamicSpatial"));
            SetState(controller, "PATROL");

            stalker.SetActive(true);
            yield return null;

            Assert.That((bool)GetPrivateField(controller, "_spatialPatrolInitializationAttempted"), Is.False);
            Assert.That(GetFloatProperty(controller, "LastPatrolScore"), Is.EqualTo(0f));
            Assert.That((int)GetProperty(controller, "PlannerRunCount"), Is.EqualTo(0));

            var confidenceSpatialStalker = new GameObject("STK_SIM_ConfidenceSpatialStalker");
            confidenceSpatialStalker.SetActive(false);
            _createdObjects.Add(confidenceSpatialStalker);

            var confidenceSpatialController = (Component)confidenceSpatialStalker.AddComponent(controllerType);
            confidenceSpatialStalker.GetComponent<NavMeshAgent>().enabled = false;
            SetPrivateField(confidenceSpatialController, "patrolMode", Enum.Parse(ResolveType("EchoProtocol.AI.Stalker.StalkerPatrolMode"), "ConfidenceSpatial"));
            SetState(confidenceSpatialController, "PATROL");

            confidenceSpatialStalker.SetActive(true);
            yield return null;

            Assert.That((bool)GetPrivateField(confidenceSpatialController, "_spatialPatrolInitializationAttempted"), Is.False);
            yield return null;
        }

        private StalkerFixture CreateFixture()
        {
            return CreateFixture(new Vector3(0f, 1f, 4f));
        }

        private StalkerFixture CreateFixture(Vector3 targetPosition)
        {
            var controllerType = ResolveType(StalkerControllerTypeName);
            var visionSensorType = ResolveType(StalkerVisionSensorTypeName);
            var stalker = new GameObject("STK_SIM_Stalker");
            _createdObjects.Add(stalker);

            var originObject = new GameObject("STK_SIM_VisionOrigin");
            originObject.transform.SetParent(stalker.transform, false);
            originObject.transform.localPosition = new Vector3(0f, 1f, 0f);
            originObject.transform.localRotation = Quaternion.identity;

            var target = new GameObject("STK_SIM_Target");
            target.transform.position = targetPosition;
            _createdObjects.Add(target);

            var visionSensor = (Component)stalker.AddComponent(visionSensorType);
            var controller = (Component)stalker.AddComponent(controllerType);
            var navMeshAgent = stalker.GetComponent<NavMeshAgent>();
            Assert.That(navMeshAgent, Is.Not.Null);
            navMeshAgent.enabled = false;
            ((Behaviour)controller).enabled = false;

            SetSensorFields(visionSensor, originObject.transform, target.transform, 15f, 90f, 0);
            SetPrivateField(controller, "visionSensor", visionSensor);
            return new StalkerFixture(controller, visionSensor, target.transform);
        }

        private static object CreateSimulationInput(float deltaSeconds, object candidates)
        {
            return CreateSimulationInput(deltaSeconds, candidates, null);
        }

        private static object CreateSimulationInput(float deltaSeconds, object candidates, object statuses)
        {
            return CreateSimulationInput(
                Activator.CreateInstance(
                    ResolveType(AiSimulationStepTypeName),
                    Activator.CreateInstance(ResolveType(AiSimulationTimeTypeName), 1L, 0d),
                    deltaSeconds),
                candidates,
                statuses);
        }

        private static object CreateSimulationInput(object step, object candidates)
        {
            return CreateSimulationInput(step, candidates, null);
        }

        private static object CreateSimulationInput(object step, object candidates, object statuses)
        {
            return Activator.CreateInstance(ResolveType(StalkerSimulationInputTypeName), step, candidates, statuses);
        }

        private static object CreateTargetCandidateList(object candidate)
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(ResolveType(StalkerTargetCandidateTypeName)));
            list.Add(candidate);
            return list;
        }

        private static object CreateTargetCandidate(int playerId)
        {
            var observation = Activator.CreateInstance(
                ResolveType(VisionObservationTypeName),
                Activator.CreateInstance(ResolveType(PlayerIdTypeName), playerId),
                new Vector3(0f, 1f, 4f),
                Vector3.forward,
                Activator.CreateInstance(ResolveType(AiSimulationTimeTypeName), 1L, 0d),
                4f);
            var eligibility = ResolveType(StalkerTargetEligibilityResultTypeName)
                .GetMethod("EligibleTarget", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, Array.Empty<object>());

            return Activator.CreateInstance(ResolveType(StalkerTargetCandidateTypeName), observation, eligibility);
        }

        private static object CreateTargetStatusList(object status)
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(ResolveType(StalkerTargetStatusTypeName)));
            list.Add(status);
            return list;
        }

        private static object CreateTargetStatus(int playerId)
        {
            var eligibility = ResolveType(StalkerTargetEligibilityResultTypeName)
                .GetMethod("EligibleTarget", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, Array.Empty<object>());

            return Activator.CreateInstance(
                ResolveType(StalkerTargetStatusTypeName),
                Activator.CreateInstance(ResolveType(PlayerIdTypeName), playerId),
                eligibility);
        }

        private static bool Simulate(Component controller, object simulationInput)
        {
            var method = controller.GetType().GetMethod("Simulate", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, "Missing StalkerController.Simulate.");

            var result = method.Invoke(controller, new[] { simulationInput });
            Assert.That(result, Is.TypeOf<bool>());
            return (bool)result;
        }

        private static Transform CreateChild(string name, Transform parent, Vector3 localPosition)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            return child.transform;
        }

        private static void SetSensorFields(
            Component sensor,
            Transform visionOrigin,
            Transform candidate,
            float visionDistance,
            float visionAngle,
            int losBlockerMask)
        {
            var mask = default(LayerMask);
            mask.value = losBlockerMask;

            SetPrivateField(sensor, "visionOrigin", visionOrigin);
            SetPrivateField(sensor, "candidate", candidate);
            SetPrivateField(sensor, "visionDistance", visionDistance);
            SetPrivateField(sensor, "visionAngle", visionAngle);
            SetPrivateField(sensor, "losBlockerMask", mask);
        }

        private static void SetState(Component controller, string stateName)
        {
            var stateType = ResolveType("EchoProtocol.AI.Stalker.StalkerState");
            SetPrivateField(controller, "currentState", Enum.Parse(stateType, stateName));
        }

        private static int GetListCount(object list)
        {
            return (int)list.GetType().GetProperty("Count").GetValue(list);
        }

        private static object GetListItem(object list, int index)
        {
            return list.GetType().GetProperty("Item").GetValue(list, new object[] { index });
        }

        private static float GetFloatProperty(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.TypeOf<float>(), $"Property '{propertyName}' must return float.");
            return (float)value;
        }

        private static string GetEnumPropertyName(object target, string propertyName)
        {
            return GetProperty(target, propertyName).ToString();
        }

        private static object GetProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Missing public property '{propertyName}' on '{target.GetType().FullName}'.");
            return property.GetValue(target);
        }

        private static object GetStaticProperty(Type type, string propertyName)
        {
            var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
            Assert.That(property, Is.Not.Null, $"Missing static property '{propertyName}' on '{type.FullName}'.");
            return property.GetValue(null);
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

        private readonly struct StalkerFixture
        {
            public StalkerFixture(Component controller, Component visionSensor, Transform target)
            {
                Controller = controller;
                VisionSensor = visionSensor;
                Target = target;
            }

            public Component Controller { get; }

            public Component VisionSensor { get; }

            public Transform Target { get; }
        }
    }
}
