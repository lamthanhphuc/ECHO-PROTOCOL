using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerMultiCandidateVisionPlayModeTests
    {
        private const string StalkerVisionSensorTypeName = "EchoProtocol.AI.Stalker.StalkerVisionSensor";
        private const string PhysicalObservationTypeName = "EchoProtocol.AI.Stalker.StalkerPhysicalVisionObservation";
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
        public IEnumerator STK_VIS_LegacySingleCandidateRefresh_RemainsCompatible()
        {
            var fixture = CreateSensorFixture();
            var candidate = CreateCandidate("STK_Test_LegacyCandidate", new Vector3(0f, 1f, 4f));
            SetSensorFields(fixture.Sensor, fixture.Origin, candidate, 10f, 90f, 0);
            Physics.SyncTransforms();

            var visible = InvokeInstanceMethod(fixture.Sensor, "RefreshVisibility", Type.EmptyTypes, Array.Empty<object>());

            Assert.That(visible, Is.EqualTo(true));
            Assert.That(GetBoolProperty(fixture.Sensor, "IsCandidateVisible"), Is.True);
            Assert.That(GetVector3Property(fixture.Sensor, "LastObservedPosition"), Is.EqualTo(candidate.position));
            Assert.That(GetTransformProperty(fixture.Sensor, "Candidate"), Is.SameAs(candidate));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_VIS_MultipleVisibleCandidates_AreAllCollectedWithoutRanking()
        {
            var fixture = CreateSensorFixture();
            var farther = CreateCandidate("STK_Test_VisibleFarther", new Vector3(0f, 1f, 5f));
            var nearer = CreateCandidate("STK_Test_VisibleNearer", new Vector3(0f, 1f, 2f));
            SetSensorFields(fixture.Sensor, fixture.Origin, null, 10f, 90f, 0);
            Physics.SyncTransforms();

            var results = CreateObservationList();
            var count = CollectVisibleCandidates(fixture.Sensor, new List<Transform> { farther, nearer }, results);

            Assert.That(count, Is.EqualTo(2));
            Assert.That(GetListCount(results), Is.EqualTo(2));
            Assert.That(GetTransformProperty(GetListItem(results, 0), "Candidate"), Is.SameAs(farther));
            Assert.That(GetTransformProperty(GetListItem(results, 1), "Candidate"), Is.SameAs(nearer));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_VIS_MultiCandidateCollection_FiltersByVisionDistance()
        {
            var fixture = CreateSensorFixture();
            var inside = CreateCandidate("STK_Test_InsideDistance", new Vector3(0f, 1f, 3f));
            var outside = CreateCandidate("STK_Test_OutsideDistance", new Vector3(0f, 1f, 8f));
            SetSensorFields(fixture.Sensor, fixture.Origin, null, 5f, 90f, 0);
            Physics.SyncTransforms();

            var results = CreateObservationList();
            var count = CollectVisibleCandidates(fixture.Sensor, new List<Transform> { inside, outside }, results);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(GetTransformProperty(GetListItem(results, 0), "Candidate"), Is.SameAs(inside));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_VIS_MultiCandidateCollection_FiltersOutsideFieldOfView()
        {
            var fixture = CreateSensorFixture();
            var inCone = CreateCandidate("STK_Test_InCone", new Vector3(0f, 1f, 4f));
            var outsideCone = CreateCandidate("STK_Test_OutsideCone", new Vector3(4f, 1f, 0f));
            SetSensorFields(fixture.Sensor, fixture.Origin, null, 10f, 90f, 0);
            Physics.SyncTransforms();

            var results = CreateObservationList();
            var count = CollectVisibleCandidates(fixture.Sensor, new List<Transform> { outsideCone, inCone }, results);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(GetTransformProperty(GetListItem(results, 0), "Candidate"), Is.SameAs(inCone));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_VIS_MultiCandidateCollection_FiltersOccludedCandidate()
        {
            var fixture = CreateSensorFixture();
            var clear = CreateCandidate("STK_Test_ClearCandidate", new Vector3(-2f, 1f, 5f));
            var occluded = CreateCandidate("STK_Test_OccludedCandidate", new Vector3(2f, 1f, 5f));
            var blocker = CreatePrimitive("STK_Test_LOS_Blocker", PrimitiveType.Cube, new Vector3(1f, 1f, 2.5f));
            blocker.layer = 0;
            var blockerMask = 1 << blocker.layer;
            SetSensorFields(fixture.Sensor, fixture.Origin, null, 10f, 90f, blockerMask);
            Physics.SyncTransforms();
            AssertRayHitsTransform(fixture.Origin.position, occluded.position, blocker.transform, blockerMask);
            AssertRayDoesNotHitTransform(fixture.Origin.position, clear.position, blocker.transform, blockerMask);

            var results = CreateObservationList();
            var count = CollectVisibleCandidates(fixture.Sensor, new List<Transform> { clear, occluded }, results);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(GetTransformProperty(GetListItem(results, 0), "Candidate"), Is.SameAs(clear));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_VIS_PhysicalObservation_UsesOriginToCandidateBearingAndDistance()
        {
            var fixture = CreateSensorFixture();
            var candidate = CreateCandidate("STK_Test_PhysicalObservationCandidate", new Vector3(3f, 1f, 4f));
            SetSensorFields(fixture.Sensor, fixture.Origin, null, 10f, 120f, 0);
            Physics.SyncTransforms();

            var accepted = TryEvaluateCandidate(fixture.Sensor, candidate, out var observation);
            var expectedDirection = (candidate.position - fixture.Origin.position).normalized;
            var expectedDistance = Vector3.Distance(fixture.Origin.position, candidate.position);

            Assert.That(accepted, Is.True);
            Assert.That(GetTransformProperty(observation, "Candidate"), Is.SameAs(candidate));
            Assert.That(GetVector3Property(observation, "ObservedPosition"), Is.EqualTo(candidate.position));
            Assert.That(Vector3.Distance(GetVector3Property(observation, "ObservedDirection"), expectedDirection), Is.LessThan(FloatTolerance));
            Assert.That(GetFloatProperty(observation, "Distance"), Is.EqualTo(expectedDistance).Within(FloatTolerance));
            yield return null;
        }

        private SensorFixture CreateSensorFixture()
        {
            var sensorType = ResolveType(StalkerVisionSensorTypeName);
            var root = new GameObject("STK_Test_VisionSensorRoot");
            _createdObjects.Add(root);

            var originObject = new GameObject("STK_Test_VisionOrigin");
            originObject.transform.SetParent(root.transform, false);
            originObject.transform.localPosition = new Vector3(0f, 1f, 0f);
            originObject.transform.localRotation = Quaternion.identity;

            var sensor = root.AddComponent(sensorType);
            return new SensorFixture((Component)sensor, originObject.transform);
        }

        private Transform CreateCandidate(string name, Vector3 position)
        {
            var candidate = new GameObject(name);
            candidate.transform.position = position;
            _createdObjects.Add(candidate);
            return candidate.transform;
        }

        private GameObject CreatePrimitive(string name, PrimitiveType primitiveType, Vector3 position)
        {
            var primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = name;
            primitive.transform.position = position;
            _createdObjects.Add(primitive);
            return primitive;
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

        private static object CreateObservationList()
        {
            return Activator.CreateInstance(typeof(List<>).MakeGenericType(ResolveType(PhysicalObservationTypeName)));
        }

        private static int CollectVisibleCandidates(Component sensor, List<Transform> candidates, object results)
        {
            var method = sensor.GetType().GetMethod(
                "CollectVisibleCandidates",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, "Missing StalkerVisionSensor.CollectVisibleCandidates.");

            var count = method.Invoke(sensor, new[] { candidates, results });
            Assert.That(count, Is.TypeOf<int>());
            return (int)count;
        }

        private static bool TryEvaluateCandidate(Component sensor, Transform candidate, out object observation)
        {
            var method = sensor.GetType().GetMethod(
                "TryEvaluateCandidate",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, "Missing StalkerVisionSensor.TryEvaluateCandidate.");

            var args = new object[] { candidate, null };
            var accepted = method.Invoke(sensor, args);
            Assert.That(accepted, Is.TypeOf<bool>());
            observation = args[1];
            return (bool)accepted;
        }

        private static void AssertRayHitsTransform(Vector3 origin, Vector3 targetPosition, Transform expectedHit, int mask)
        {
            Assert.That(RayHitsTransform(origin, targetPosition, expectedHit, mask), Is.True);
        }

        private static void AssertRayDoesNotHitTransform(Vector3 origin, Vector3 targetPosition, Transform unexpectedHit, int mask)
        {
            Assert.That(RayHitsTransform(origin, targetPosition, unexpectedHit, mask), Is.False);
        }

        private static bool RayHitsTransform(Vector3 origin, Vector3 targetPosition, Transform hitTransform, int mask)
        {
            var toTarget = targetPosition - origin;
            var hits = Physics.RaycastAll(
                origin,
                toTarget.normalized,
                toTarget.magnitude,
                mask,
                QueryTriggerInteraction.Ignore);

            for (var i = 0; i < hits.Length; i++)
            {
                if (hits[i].transform == hitTransform)
                {
                    return true;
                }
            }

            return false;
        }

        private static object InvokeInstanceMethod(object target, string methodName, Type[] parameterTypes, object[] args)
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

        private static int GetListCount(object list)
        {
            return (int)list.GetType().GetProperty("Count").GetValue(list);
        }

        private static object GetListItem(object list, int index)
        {
            return list.GetType().GetProperty("Item").GetValue(list, new object[] { index });
        }

        private static bool GetBoolProperty(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.TypeOf<bool>(), $"Property '{propertyName}' must return bool.");
            return (bool)value;
        }

        private static float GetFloatProperty(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.TypeOf<float>(), $"Property '{propertyName}' must return float.");
            return (float)value;
        }

        private static Transform GetTransformProperty(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.TypeOf<Transform>(), $"Property '{propertyName}' must return Transform.");
            return (Transform)value;
        }

        private static Vector3 GetVector3Property(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.TypeOf<Vector3>(), $"Property '{propertyName}' must return Vector3.");
            return (Vector3)value;
        }

        private static object GetProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Missing public property '{propertyName}' on '{target.GetType().FullName}'.");

            return property.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}' on '{target.GetType().FullName}'.");

            field.SetValue(target, value);
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

        private readonly struct SensorFixture
        {
            public SensorFixture(Component sensor, Transform origin)
            {
                Sensor = sensor;
                Origin = origin;
            }

            public Component Sensor { get; }

            public Transform Origin { get; }
        }
    }
}
