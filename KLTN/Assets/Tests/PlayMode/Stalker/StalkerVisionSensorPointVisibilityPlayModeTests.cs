using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerVisionSensorPointVisibilityPlayModeTests
    {
        private const string StalkerVisionSensorTypeName = "EchoProtocol.AI.Stalker.StalkerVisionSensor";
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
        public IEnumerator STK_VISION_PointForwardInRangeAndCone_IsVisible()
        {
            var sensor = CreateSensor();

            yield return null;

            Assert.That(CanSeePoint(sensor, new Vector3(0f, 1f, 5f)), Is.True);
        }

        [UnityTest]
        public IEnumerator STK_VISION_PointBeyondRange_IsNotVisible()
        {
            var sensor = CreateSensor(visionDistance: 5f);

            yield return null;

            Assert.That(CanSeePoint(sensor, new Vector3(0f, 1f, 5.1f)), Is.False);
        }

        [UnityTest]
        public IEnumerator STK_VISION_PointOnConeBoundary_IsVisible()
        {
            var sensor = CreateSensor(visionAngle: 90f);

            yield return null;

            Assert.That(CanSeePoint(sensor, new Vector3(5f, 1f, 5f)), Is.True);
        }

        [UnityTest]
        public IEnumerator STK_VISION_PointOutsideCone_IsNotVisible()
        {
            var sensor = CreateSensor(visionAngle: 90f);

            yield return null;

            Assert.That(CanSeePoint(sensor, new Vector3(6f, 1f, 5f)), Is.False);
        }

        [UnityTest]
        public IEnumerator STK_VISION_PointBehindSensor_IsNotVisible()
        {
            var sensor = CreateSensor();

            yield return null;

            Assert.That(CanSeePoint(sensor, new Vector3(0f, 1f, -2f)), Is.False);
        }

        [UnityTest]
        public IEnumerator STK_VISION_BlockerBetweenOriginAndPoint_IsNotVisible()
        {
            var sensor = CreateSensor();
            CreateCube("STK_VISION_Blocker", new Vector3(0f, 1f, 2.5f), Vector3.one);

            yield return null;
            Physics.SyncTransforms();

            Assert.That(CanSeePoint(sensor, new Vector3(0f, 1f, 5f)), Is.False);
        }

        [UnityTest]
        public IEnumerator STK_VISION_SelfColliderBetweenOriginAndPoint_IsIgnored()
        {
            var sensor = CreateSensor();
            var selfBlocker = CreateCube("STK_VISION_SelfCollider", new Vector3(0f, 1f, 2.5f), Vector3.one);
            selfBlocker.transform.SetParent(((Component)sensor).transform, true);

            yield return null;
            Physics.SyncTransforms();

            Assert.That(CanSeePoint(sensor, new Vector3(0f, 1f, 5f)), Is.True);
        }

        [UnityTest]
        public IEnumerator STK_VISION_SortedRaycastHits_IgnoreSelfButStillFindFartherBlocker()
        {
            var sensor = CreateSensor();
            var selfBlocker = CreateCube("STK_VISION_SelfColliderNear", new Vector3(0f, 1f, 1.5f), Vector3.one);
            selfBlocker.transform.SetParent(((Component)sensor).transform, true);
            CreateCube("STK_VISION_BlockerFar", new Vector3(0f, 1f, 3.5f), Vector3.one);

            yield return null;
            Physics.SyncTransforms();

            Assert.That(CanSeePoint(sensor, new Vector3(0f, 1f, 5f)), Is.False);
        }

        [UnityTest]
        public IEnumerator STK_VISION_PointAtVisionOrigin_IsVisibleWithoutRaycast()
        {
            var sensor = CreateSensor();
            CreateCube("STK_VISION_OriginBlocker", new Vector3(0f, 1f, 0f), Vector3.one);

            yield return null;
            Physics.SyncTransforms();

            Assert.That(CanSeePoint(sensor, new Vector3(0f, 1f, 0f)), Is.True);
        }

        [UnityTest]
        public IEnumerator STK_VISION_GroundObservationPoint_UsesVisionOriginHeightOffset()
        {
            var sensor = CreateSensor(originLocalPosition: new Vector3(0f, 1.6f, 0f));

            yield return null;

            var observationPoint = GetObservationPointForGroundPoint(sensor, new Vector3(3f, 0f, 4f));
            Assert.That(observationPoint.x, Is.EqualTo(3f).Within(0.001f));
            Assert.That(observationPoint.y, Is.EqualTo(1.6f).Within(0.001f));
            Assert.That(observationPoint.z, Is.EqualTo(4f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator STK_VISION_GroundObservationPoint_UsesRelativeHeightWhenRootMoves()
        {
            var sensor = CreateSensor(
                rootPosition: new Vector3(0f, 10f, 0f),
                originLocalPosition: new Vector3(0f, 1.6f, 0f));

            yield return null;

            var observationPoint = GetObservationPointForGroundPoint(sensor, new Vector3(3f, 5f, 4f));
            Assert.That(observationPoint.x, Is.EqualTo(3f).Within(0.001f));
            Assert.That(observationPoint.y, Is.EqualTo(6.6f).Within(0.001f));
            Assert.That(observationPoint.z, Is.EqualTo(4f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator STK_VISION_GroundObservationPoint_NullOriginReturnsGroundPoint()
        {
            var sensor = CreateSensor();
            SetPrivateField(sensor, "visionOrigin", null);
            var groundPoint = new Vector3(3f, 5f, 4f);

            yield return null;

            Assert.That(GetObservationPointForGroundPoint(sensor, groundPoint), Is.EqualTo(groundPoint));
        }

        private object CreateSensor(
            float visionDistance = 10f,
            float visionAngle = 90f,
            Vector3? rootPosition = null,
            Vector3? originLocalPosition = null)
        {
            var stalker = new GameObject("STK_VISION_Stalker");
            _createdObjects.Add(stalker);
            stalker.transform.position = rootPosition ?? Vector3.zero;

            var origin = new GameObject("STK_VISION_Origin");
            origin.transform.SetParent(stalker.transform, false);
            origin.transform.localPosition = originLocalPosition ?? new Vector3(0f, 1f, 0f);
            origin.transform.localRotation = Quaternion.identity;
            _createdObjects.Add(origin);

            var sensor = stalker.AddComponent(ResolveType(StalkerVisionSensorTypeName));
            SetPrivateField(sensor, "visionOrigin", origin.transform);
            SetPrivateField(sensor, "visionDistance", visionDistance);
            SetPrivateField(sensor, "visionAngle", visionAngle);
            var blockerMask = default(LayerMask);
            blockerMask.value = Physics.DefaultRaycastLayers;
            SetPrivateField(sensor, "losBlockerMask", blockerMask);
            return sensor;
        }

        private GameObject CreateCube(string name, Vector3 position, Vector3 scale)
        {
            var gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gameObject.name = name;
            gameObject.transform.position = position;
            gameObject.transform.localScale = scale;
            _createdObjects.Add(gameObject);
            return gameObject;
        }

        private static bool CanSeePoint(object sensor, Vector3 worldPoint)
        {
            return (bool)Invoke(sensor, "CanSeePoint", new[] { typeof(Vector3) }, worldPoint);
        }

        private static Vector3 GetObservationPointForGroundPoint(object sensor, Vector3 groundPoint)
        {
            return (Vector3)Invoke(sensor, "GetObservationPointForGroundPoint", new[] { typeof(Vector3) }, groundPoint);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}' on '{target.GetType().FullName}'.");
            field.SetValue(target, value);
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

            var assemblyCSharp = Assembly.Load("Assembly-CSharp");
            var loadedType = assemblyCSharp.GetType(fullTypeName, false);
            if (loadedType != null)
            {
                return loadedType;
            }

            Assert.Fail($"Could not find production type '{fullTypeName}'.");
            return null;
        }
    }
}
