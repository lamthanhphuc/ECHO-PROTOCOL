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
    public sealed class StalkerAttackFlowPlayModeTests
    {
        private const string StalkerControllerTypeName = "EchoProtocol.AI.Stalker.StalkerController";
        private const string StalkerVisionSensorTypeName = "EchoProtocol.AI.Stalker.StalkerVisionSensor";
        private const int MaxDetectFrames = 5;
        private const int MaxChaseFrames = 30;
        private const int MaxAttackFrames = 5;
        private const int MaxTimedRecoverFrames = 300;
        private const int MaxPreRecoveryWindowFrames = 600;
        private const float FloatTolerance = 0.0001f;
        private const float AttackRange = 2f;
        private const float AttackWindup = 0.15f;
        private const float AttackRecovery = 0.5f;
        private const float AttackToRecoverTimeout = 1f;
        private const float PreRecoveryObservationWindow = 0.2f;

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
        public IEnumerator STK_R_011_AttackAfterWindup_EntersRecover()
        {
            var fixture = CreateFixture();
            fixture.Stalker.SetActive(true);

            yield return ReachChase(fixture);

            fixture.PlayerDummy.transform.position = new Vector3(0f, 1f, 1f);
            yield return WaitUntilState(fixture.Controller, "ATTACK", MaxAttackFrames);

            AssertState(fixture.Controller, "ATTACK");
            Assert.That(GetTransformProperty(fixture.Controller, "CurrentTarget"), Is.SameAs(fixture.PlayerDummy.transform));
            Assert.That(GetEnumPropertyName(fixture.Controller, "LastAttackResult"), Is.EqualTo("None"));
            Assert.That(GetFloatProperty(fixture.Controller, "AttackElapsedTime"), Is.LessThan(AttackWindup));

            yield return WaitUntilStateWithinGameTime(fixture.Controller, "RECOVER", AttackToRecoverTimeout, MaxTimedRecoverFrames);

            AssertState(fixture.Controller, "RECOVER");
            Assert.That(GetEnumPropertyName(fixture.Controller, "LastAttackResult"), Is.EqualTo("Hit"));
            Assert.That(GetTransformProperty(fixture.Controller, "CurrentTarget"), Is.SameAs(fixture.PlayerDummy.transform));
        }

        [UnityTest]
        public IEnumerator STK_R_012_Recover_DoesNotExitBeforeRecoveryDuration()
        {
            var fixture = CreateFixture();
            fixture.Stalker.SetActive(true);

            yield return ReachChase(fixture);

            fixture.PlayerDummy.transform.position = new Vector3(0f, 1f, 1f);
            yield return WaitUntilState(fixture.Controller, "ATTACK", MaxAttackFrames);
            yield return WaitUntilStateWithinGameTime(fixture.Controller, "RECOVER", AttackToRecoverTimeout, MaxTimedRecoverFrames);

            var observedTime = 0f;
            var frameCount = 0;
            while (observedTime < PreRecoveryObservationWindow && frameCount < MaxPreRecoveryWindowFrames)
            {
                AssertState(fixture.Controller, "RECOVER");
                Assert.That(GetFloatProperty(fixture.Controller, "RecoverElapsedTime"), Is.LessThan(AttackRecovery));

                yield return null;

                observedTime += Time.deltaTime;
                frameCount++;
            }

            Assert.That(
                observedTime,
                Is.GreaterThanOrEqualTo(PreRecoveryObservationWindow),
                $"RECOVER pre-duration observation reached the {MaxPreRecoveryWindowFrames}-frame safety cap before accumulating {PreRecoveryObservationWindow:0.###} gameplay seconds. ObservedTime={observedTime:0.###}, ObservedFrames={frameCount}, CurrentState={GetEnumPropertyName(fixture.Controller, "CurrentState")}, RecoverElapsedTime={GetFloatProperty(fixture.Controller, "RecoverElapsedTime"):0.###}.");
            AssertState(fixture.Controller, "RECOVER");
            Assert.That(GetFloatProperty(fixture.Controller, "RecoverElapsedTime"), Is.LessThan(AttackRecovery));
        }

        private StalkerFixture CreateFixture()
        {
            var controllerType = ResolveType(StalkerControllerTypeName);
            var visionSensorType = ResolveType(StalkerVisionSensorTypeName);

            var stalker = new GameObject("STK_Test_Attack_Stalker");
            stalker.SetActive(false);
            stalker.transform.position = Vector3.zero;
            _createdObjects.Add(stalker);

            var visionOrigin = new GameObject("VisionOrigin");
            visionOrigin.transform.SetParent(stalker.transform, false);
            visionOrigin.transform.localPosition = new Vector3(0f, 1f, 0f);
            visionOrigin.transform.localRotation = Quaternion.identity;

            var playerDummy = new GameObject("STK_Test_PlayerDummy");
            playerDummy.transform.position = new Vector3(0f, 1f, 5f);
            _createdObjects.Add(playerDummy);

            var visionSensor = stalker.AddComponent(visionSensorType);
            var controller = stalker.AddComponent(controllerType);
            var navMeshAgent = stalker.GetComponent<NavMeshAgent>();
            Assert.That(navMeshAgent, Is.Not.Null, "StalkerController RequireComponent must ensure a NavMeshAgent exists.");
            navMeshAgent.enabled = false;

            var noLosBlockers = default(LayerMask);
            noLosBlockers.value = 0;

            SetPrivateField(visionSensor, "visionOrigin", visionOrigin.transform);
            SetPrivateField(visionSensor, "candidate", playerDummy.transform);
            SetPrivateField(visionSensor, "visionDistance", 15f);
            SetPrivateField(visionSensor, "visionAngle", 90f);
            SetPrivateField(visionSensor, "losBlockerMask", noLosBlockers);

            SetPrivateField(controller, "visionSensor", visionSensor);
            SetPrivateField(controller, "detectionMeterFull", 0.1f);
            SetPrivateField(controller, "detectionFillRate", 10f);
            SetPrivateField(controller, "detectionDecayRate", 0f);
            SetPrivateField(controller, "attackRange", AttackRange);
            SetPrivateField(controller, "attackWindup", AttackWindup);
            SetPrivateField(controller, "attackRecovery", AttackRecovery);

            return new StalkerFixture(stalker, playerDummy, controller);
        }

        private static IEnumerator ReachChase(StalkerFixture fixture)
        {
            yield return WaitUntilState(fixture.Controller, "DETECT", MaxDetectFrames);
            yield return WaitUntilState(fixture.Controller, "CHASE", MaxChaseFrames);
            AssertState(fixture.Controller, "CHASE");
        }

        private static IEnumerator WaitUntilState(Component controller, string expectedStateName, int maxFrames)
        {
            for (var frame = 0; frame < maxFrames; frame++)
            {
                yield return null;

                if (GetEnumPropertyName(controller, "CurrentState") == expectedStateName)
                {
                    yield break;
                }
            }

            Assert.Fail($"Expected Stalker CurrentState '{expectedStateName}' within {maxFrames} frames, but was '{GetEnumPropertyName(controller, "CurrentState")}'.");
        }

        private static IEnumerator WaitUntilStateWithinGameTime(Component controller, string expectedStateName, float maxGameTime, int maxFrames)
        {
            var elapsedGameTime = 0f;
            var frameCount = 0;

            while (GetEnumPropertyName(controller, "CurrentState") != expectedStateName
                && elapsedGameTime < maxGameTime
                && frameCount < maxFrames)
            {
                yield return null;

                elapsedGameTime += Time.deltaTime;
                frameCount++;
            }

            Assert.That(
                GetEnumPropertyName(controller, "CurrentState"),
                Is.EqualTo(expectedStateName),
                $"Expected Stalker CurrentState '{expectedStateName}' within {maxGameTime:0.###} gameplay seconds and {maxFrames} frames, but was '{GetEnumPropertyName(controller, "CurrentState")}' after {elapsedGameTime:0.###} gameplay seconds and {frameCount} frames.");
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

        private static void SetPrivateField(Component component, string fieldName, object value)
        {
            var field = component.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private serialized field '{fieldName}' on '{component.GetType().FullName}'.");

            field.SetValue(component, value);
        }

        private static object GetProperty(Component component, string propertyName)
        {
            var property = component.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Missing public property '{propertyName}' on '{component.GetType().FullName}'.");

            return property.GetValue(component);
        }

        private static string GetEnumPropertyName(Component component, string propertyName)
        {
            var value = GetProperty(component, propertyName);
            Assert.That(value, Is.Not.Null, $"Property '{propertyName}' returned null.");
            Assert.That(value.GetType().IsEnum, Is.True, $"Property '{propertyName}' must return an enum value.");

            return value.ToString();
        }

        private static float GetFloatProperty(Component component, string propertyName)
        {
            var value = GetProperty(component, propertyName);
            Assert.That(value, Is.TypeOf<float>(), $"Property '{propertyName}' must return float.");
            return (float)value;
        }

        private static Transform GetTransformProperty(Component component, string propertyName)
        {
            var value = GetProperty(component, propertyName);
            if (value == null)
            {
                return null;
            }

            Assert.That(value, Is.TypeOf<Transform>(), $"Property '{propertyName}' must return Transform.");
            return (Transform)value;
        }

        private static void AssertState(Component controller, string expectedStateName)
        {
            Assert.That(GetEnumPropertyName(controller, "CurrentState"), Is.EqualTo(expectedStateName));
        }

        private readonly struct StalkerFixture
        {
            public StalkerFixture(GameObject stalker, GameObject playerDummy, Component controller)
            {
                Stalker = stalker;
                PlayerDummy = playerDummy;
                Controller = controller;
            }

            public GameObject Stalker { get; }
            public GameObject PlayerDummy { get; }
            public Component Controller { get; }
        }
    }
}
