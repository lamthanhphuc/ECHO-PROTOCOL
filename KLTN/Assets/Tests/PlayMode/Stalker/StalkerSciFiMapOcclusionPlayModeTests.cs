using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerSciFiMapOcclusionPlayModeTests
    {
        private const string SciFiSceneName = "SciFi";
        private const string StalkerVisionSensorTypeName = "EchoProtocol.AI.Stalker.StalkerVisionSensor";
        private readonly List<GameObject> _createdObjects = new List<GameObject>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync(SciFiSceneName, LoadSceneMode.Single);
            yield return null;
            Physics.SyncTransforms();
        }

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
        public IEnumerator STK_SciFiRealShelfCollider_BlocksVisionSensorLineOfSight()
        {
            var blocker = FindRealBlockingCollider("Shelf Variation");
            var sensor = CreateSensor(blocker.Bounds, out var target);
            Physics.SyncTransforms();

            var visible = TryEvaluateCandidate(sensor, target.transform);

            Assert.That(
                visible,
                Is.False,
                $"Shelf '{blocker.OwnerName}' collider '{blocker.ColliderName}' did not block LOS.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_SciFiRealLockerCollider_BlocksVisionSensorLineOfSight()
        {
            var blocker = FindRealBlockingCollider("PF_LockerHidingSpot_Clean", "PF_LockerHidingSpot_Rusty");
            var sensor = CreateSensor(blocker.Bounds, out var target);
            Physics.SyncTransforms();

            var visible = TryEvaluateCandidate(sensor, target.transform);

            Assert.That(
                visible,
                Is.False,
                $"Locker '{blocker.OwnerName}' collider '{blocker.ColliderName}' did not block physical LOS.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_SciFiVisionSensor_UnobstructedControl_IsVisible()
        {
            var blocker = FindRealBlockingCollider("Shelf Variation");
            var setup = CalculateOcclusionSetup(blocker.Bounds);
            var sensor = CreateSensorObject(setup.SensorPosition, setup.Direction, out var origin);
            var target = CreateTarget(setup.ControlTargetPosition);
            Physics.SyncTransforms();

            var visible = TryEvaluateCandidate(sensor, target.transform);

            Assert.That(
                visible,
                Is.True,
                $"Control LOS was not visible using shelf '{blocker.OwnerName}' collider '{blocker.ColliderName}'.");
            yield return null;
        }

        private Component CreateSensor(Bounds blockerBounds, out GameObject target)
        {
            var setup = CalculateOcclusionSetup(blockerBounds);
            var sensor = CreateSensorObject(setup.SensorPosition, setup.Direction, out _);
            target = CreateTarget(setup.OccludedTargetPosition);
            return sensor;
        }

        private Component CreateSensorObject(
            Vector3 sensorPosition,
            Vector3 direction,
            out GameObject originObject)
        {
            var sensorObject = new GameObject("STK_SciFi_LOS_TestSensor");
            _createdObjects.Add(sensorObject);
            sensorObject.transform.position = sensorPosition;
            sensorObject.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            originObject = new GameObject("STK_SciFi_LOS_TestOrigin");
            _createdObjects.Add(originObject);
            originObject.transform.SetParent(sensorObject.transform, false);
            originObject.transform.localPosition = Vector3.zero;
            originObject.transform.localRotation = Quaternion.identity;

            var sensor = sensorObject.AddComponent(ResolveType(StalkerVisionSensorTypeName));
            SetPrivateField(sensor, "visionOrigin", originObject.transform);
            SetPrivateField(sensor, "visionDistance", 50f);
            SetPrivateField(sensor, "visionAngle", 120f);
            var blockerMask = default(LayerMask);
            blockerMask.value = Physics.DefaultRaycastLayers;
            SetPrivateField(sensor, "losBlockerMask", blockerMask);
            return sensor;
        }

        private GameObject CreateTarget(Vector3 position)
        {
            var target = new GameObject("STK_SciFi_LOS_TestTarget");
            _createdObjects.Add(target);
            target.transform.position = position;
            return target;
        }

        private static OcclusionSetup CalculateOcclusionSetup(Bounds bounds)
        {
            var direction = bounds.extents.x >= bounds.extents.z
                ? Vector3.right
                : Vector3.forward;
            var extent = direction == Vector3.right ? bounds.extents.x : bounds.extents.z;
            var sensorPosition = bounds.center - direction * (extent + 2f);
            var occludedTargetPosition = bounds.center + direction * (extent + 2f);
            var controlTargetPosition = bounds.center - direction * (extent + 0.5f);
            return new OcclusionSetup(
                sensorPosition,
                direction,
                occludedTargetPosition,
                controlTargetPosition);
        }

        private static BlockingCollider FindRealBlockingCollider(params string[] ownerNameFragments)
        {
            var transforms = UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (var i = 0; i < transforms.Length; i++)
            {
                var owner = transforms[i];
                if (!owner.gameObject.activeInHierarchy || !ContainsAny(owner.name, ownerNameFragments))
                {
                    continue;
                }

                var colliders = owner.GetComponentsInChildren<Collider>(true);
                for (var colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
                {
                    var collider = colliders[colliderIndex];
                    if (collider == null
                        || !collider.enabled
                        || collider.isTrigger
                        || !collider.gameObject.activeInHierarchy
                        || collider.bounds.size.sqrMagnitude <= Mathf.Epsilon)
                    {
                        continue;
                    }

                    return new BlockingCollider(owner.name, collider.name, collider.bounds);
                }
            }

            Assert.Fail($"Could not find an active non-trigger collider under object name containing '{string.Join("' or '", ownerNameFragments)}'.");
            return default;
        }

        private static bool ContainsAny(string value, IReadOnlyList<string> fragments)
        {
            for (var i = 0; i < fragments.Count; i++)
            {
                if (value.IndexOf(fragments[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}' on '{target.GetType().FullName}'.");
            field.SetValue(target, value);
        }

        private static bool TryEvaluateCandidate(Component sensor, Transform target)
        {
            var args = new object[] { target, null };
            var method = sensor.GetType().GetMethod(
                "TryEvaluateCandidate",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(Transform), ResolveType("EchoProtocol.AI.Stalker.StalkerPhysicalVisionObservation").MakeByRefType() },
                null);
            Assert.That(method, Is.Not.Null, "Missing StalkerVisionSensor.TryEvaluateCandidate.");
            return (bool)method.Invoke(sensor, args);
        }

        private static Type ResolveType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            Assert.Fail($"Missing type '{fullName}'.");
            return null;
        }

        private readonly struct BlockingCollider
        {
            public BlockingCollider(string ownerName, string colliderName, Bounds bounds)
            {
                OwnerName = ownerName;
                ColliderName = colliderName;
                Bounds = bounds;
            }

            public string OwnerName { get; }
            public string ColliderName { get; }
            public Bounds Bounds { get; }
        }

        private readonly struct OcclusionSetup
        {
            public OcclusionSetup(
                Vector3 sensorPosition,
                Vector3 direction,
                Vector3 occludedTargetPosition,
                Vector3 controlTargetPosition)
            {
                SensorPosition = sensorPosition;
                Direction = direction;
                OccludedTargetPosition = occludedTargetPosition;
                ControlTargetPosition = controlTargetPosition;
            }

            public Vector3 SensorPosition { get; }
            public Vector3 Direction { get; }
            public Vector3 OccludedTargetPosition { get; }
            public Vector3 ControlTargetPosition { get; }
        }
    }
}
