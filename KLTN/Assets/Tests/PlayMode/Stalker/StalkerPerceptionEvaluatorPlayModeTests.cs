using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerPerceptionEvaluatorPlayModeTests
    {
        private const string PlayerIdTypeName = "EchoProtocol.AI.Common.PlayerId";
        private const string AiSimulationTimeTypeName = "EchoProtocol.AI.Common.AiSimulationTime";
        private const string StalkerVisionSensorTypeName = "EchoProtocol.AI.Stalker.StalkerVisionSensor";
        private const string StalkerPerceptionTargetSnapshotTypeName = "EchoProtocol.AI.Stalker.StalkerPerceptionTargetSnapshot";
        private const string StalkerPerceptionEvaluatorTypeName = "EchoProtocol.AI.Stalker.StalkerPerceptionEvaluator";
        private const string StalkerTargetCandidateTypeName = "EchoProtocol.AI.Stalker.StalkerTargetCandidate";
        private const string StalkerTargetSelectorTypeName = "EchoProtocol.AI.Stalker.StalkerTargetSelector";
        private const string StalkerTargetEligibilitySnapshotTypeName = "EchoProtocol.AI.Stalker.StalkerTargetEligibilitySnapshot";
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
        public IEnumerator STK_PER_VisibleTarget_ConvertsPhysicalObservationToTypedVisionObservation()
        {
            var fixture = CreateSensorFixture();
            var root = CreateCandidate("STK_PER_TargetRoot", new Vector3(0f, 1f, 4f));
            var sample = CreateChild("VisionTargetPoint", root, Vector3.zero);
            var observedAt = CreateAiSimulationTime(12, 0.2d);
            SetSensorFields(fixture.Sensor, fixture.Origin, null, 10f, 90f, 0);
            Physics.SyncTransforms();

            var results = CreateCandidateResultList();
            var targets = CreateTargetList(CreateTargetSnapshot(7, sample, root, CreateEligibilitySnapshot()));
            var count = CollectVisibleTargetCandidates(fixture.Sensor, targets, observedAt, results);
            var candidate = GetListItem(results, 0);
            var observation = GetProperty(candidate, "Observation");
            var eligibility = GetProperty(candidate, "Eligibility");
            var expectedDirection = (sample.position - fixture.Origin.position).normalized;
            var expectedDistance = Vector3.Distance(fixture.Origin.position, sample.position);

            Assert.That(count, Is.EqualTo(1));
            AssertPlayerIdValue(GetProperty(observation, "PlayerId"), 7);
            Assert.That(GetVector3Property(observation, "ObservedPosition"), Is.EqualTo(sample.position));
            Assert.That(Vector3.Distance(GetVector3Property(observation, "ObservedDirection"), expectedDirection), Is.LessThan(FloatTolerance));
            Assert.That(GetFloatProperty(observation, "Distance"), Is.EqualTo(expectedDistance).Within(FloatTolerance));
            AssertAiSimulationTime(GetProperty(observation, "ObservedAt"), 12, 0.2d);
            Assert.That(GetBoolProperty(eligibility, "Eligible"), Is.True);
            Assert.That(GetProperty(eligibility, "Reason").ToString(), Is.EqualTo("Eligible"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_PER_ParentRootColliderChildVisionTargetPoint_RemainsVisible()
        {
            var fixture = CreateSensorFixture();
            var rootObject = CreatePrimitive("STK_PER_PlayerRoot", PrimitiveType.Cube, new Vector3(0f, 1f, 5f));
            var sample = CreateChild("VisionTargetPoint", rootObject.transform, Vector3.zero);
            var blockerMask = 1 << rootObject.layer;
            SetSensorFields(fixture.Sensor, fixture.Origin, null, 10f, 90f, blockerMask);
            Physics.SyncTransforms();

            var results = CreateCandidateResultList();
            var targets = CreateTargetList(CreateTargetSnapshot(1, sample, rootObject.transform, CreateEligibilitySnapshot()));
            var count = CollectVisibleTargetCandidates(fixture.Sensor, targets, CreateAiSimulationTime(1, 0d), results);
            var observation = GetProperty(GetListItem(results, 0), "Observation");

            Assert.That(count, Is.EqualTo(1));
            Assert.That(GetVector3Property(observation, "ObservedPosition"), Is.EqualTo(sample.position));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_PER_ExternalWallBlocksTarget_CandidateOmitted()
        {
            var fixture = CreateSensorFixture();
            var rootObject = CreatePrimitive("STK_PER_BlockedPlayerRoot", PrimitiveType.Cube, new Vector3(0f, 1f, 5f));
            var sample = CreateChild("VisionTargetPoint", rootObject.transform, Vector3.zero);
            var blocker = CreatePrimitive("STK_PER_ExternalWall", PrimitiveType.Cube, new Vector3(0f, 1f, 2.5f));
            blocker.layer = rootObject.layer;
            var blockerMask = 1 << blocker.layer;
            SetSensorFields(fixture.Sensor, fixture.Origin, null, 10f, 90f, blockerMask);
            Physics.SyncTransforms();

            var results = CreateCandidateResultList();
            var targets = CreateTargetList(CreateTargetSnapshot(1, sample, rootObject.transform, CreateEligibilitySnapshot()));
            var count = CollectVisibleTargetCandidates(fixture.Sensor, targets, CreateAiSimulationTime(1, 0d), results);

            Assert.That(count, Is.EqualTo(0));
            Assert.That(GetListCount(results), Is.EqualTo(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_PER_PhysicallyVisibleDownedTarget_RemainsResultWithDownedEligibility()
        {
            var fixture = CreateSensorFixture();
            var root = CreateCandidate("STK_PER_DownedRoot", new Vector3(0f, 1f, 4f));
            var sample = CreateChild("VisionTargetPoint", root, Vector3.zero);
            SetSensorFields(fixture.Sensor, fixture.Origin, null, 10f, 90f, 0);
            Physics.SyncTransforms();

            var results = CreateCandidateResultList();
            var downedEligibility = CreateEligibilitySnapshot(isDowned: true);
            var targets = CreateTargetList(CreateTargetSnapshot(1, sample, root, downedEligibility));
            var count = CollectVisibleTargetCandidates(fixture.Sensor, targets, CreateAiSimulationTime(1, 0d), results);
            var eligibility = GetProperty(GetListItem(results, 0), "Eligibility");

            Assert.That(count, Is.EqualTo(1));
            Assert.That(GetBoolProperty(eligibility, "Eligible"), Is.False);
            Assert.That(GetProperty(eligibility, "Reason").ToString(), Is.EqualTo("Downed"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_PER_SelectorExcludesNearerIneligibleAndSelectsFartherEligible()
        {
            var fixture = CreateSensorFixture();
            var downedRoot = CreateCandidate("STK_PER_NearDowned", new Vector3(0f, 1f, 2f));
            var eligibleRoot = CreateCandidate("STK_PER_FarEligible", new Vector3(0f, 1f, 4f));
            SetSensorFields(fixture.Sensor, fixture.Origin, null, 10f, 90f, 0);
            Physics.SyncTransforms();

            var results = CreateCandidateResultList();
            var targets = CreateTargetList(
                CreateTargetSnapshot(1, downedRoot, downedRoot, CreateEligibilitySnapshot(isDowned: true)),
                CreateTargetSnapshot(2, eligibleRoot, eligibleRoot, CreateEligibilitySnapshot()));
            var count = CollectVisibleTargetCandidates(fixture.Sensor, targets, CreateAiSimulationTime(1, 0d), results);
            var selected = SelectNearestEligibleVisible(results);

            Assert.That(count, Is.EqualTo(2));
            AssertPlayerIdValue(GetProperty(selected, "PlayerId"), 2);
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_PER_TwoEligibleVisibleTargets_PreserveInputOrder()
        {
            var fixture = CreateSensorFixture();
            var first = CreateCandidate("STK_PER_InputFirst", new Vector3(1f, 1f, 4f));
            var second = CreateCandidate("STK_PER_InputSecond", new Vector3(-1f, 1f, 3f));
            SetSensorFields(fixture.Sensor, fixture.Origin, null, 10f, 90f, 0);
            Physics.SyncTransforms();

            var results = CreateCandidateResultList();
            var targets = CreateTargetList(
                CreateTargetSnapshot(5, first, first, CreateEligibilitySnapshot()),
                CreateTargetSnapshot(2, second, second, CreateEligibilitySnapshot()));
            var count = CollectVisibleTargetCandidates(fixture.Sensor, targets, CreateAiSimulationTime(1, 0d), results);

            Assert.That(count, Is.EqualTo(2));
            AssertPlayerIdValue(GetProperty(GetProperty(GetListItem(results, 0), "Observation"), "PlayerId"), 5);
            AssertPlayerIdValue(GetProperty(GetProperty(GetListItem(results, 1), "Observation"), "PlayerId"), 2);
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_PER_InvalidAiSimulationTime_FailsClosedAndClearsResults()
        {
            var fixture = CreateSensorFixture();
            var target = CreateCandidate("STK_PER_InvalidTimeTarget", new Vector3(0f, 1f, 4f));
            SetSensorFields(fixture.Sensor, fixture.Origin, null, 10f, 90f, 0);
            Physics.SyncTransforms();

            var results = CreateCandidateResultList();
            var targets = CreateTargetList(CreateTargetSnapshot(1, target, target, CreateEligibilitySnapshot()));
            Assert.That(CollectVisibleTargetCandidates(fixture.Sensor, targets, CreateAiSimulationTime(1, 0d), results), Is.EqualTo(1));

            var count = CollectVisibleTargetCandidates(fixture.Sensor, targets, GetStaticProperty(ResolveType(AiSimulationTimeTypeName), "Invalid"), results);

            Assert.That(count, Is.EqualTo(0));
            Assert.That(GetListCount(results), Is.EqualTo(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator STK_PER_InvalidSampleRootRelationship_DoesNotProduceCandidate()
        {
            var fixture = CreateSensorFixture();
            var root = CreateCandidate("STK_PER_InvalidRelationRoot", new Vector3(0f, 1f, 4f));
            var unrelatedSample = CreateCandidate("STK_PER_InvalidRelationSample", new Vector3(0f, 1f, 4f));
            SetSensorFields(fixture.Sensor, fixture.Origin, null, 10f, 90f, 0);
            Physics.SyncTransforms();

            var results = CreateCandidateResultList();
            var targets = CreateTargetList(CreateTargetSnapshot(1, unrelatedSample, root, CreateEligibilitySnapshot()));
            var count = CollectVisibleTargetCandidates(fixture.Sensor, targets, CreateAiSimulationTime(1, 0d), results);

            Assert.That(count, Is.EqualTo(0));
            Assert.That(GetListCount(results), Is.EqualTo(0));
            yield return null;
        }

        private SensorFixture CreateSensorFixture()
        {
            var sensorType = ResolveType(StalkerVisionSensorTypeName);
            var root = new GameObject("STK_PER_VisionSensorRoot");
            _createdObjects.Add(root);

            var originObject = new GameObject("STK_PER_VisionOrigin");
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

        private static Transform CreateChild(string name, Transform parent, Vector3 localPosition)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            return child.transform;
        }

        private static object CreatePlayerId(int value)
        {
            return Activator.CreateInstance(ResolveType(PlayerIdTypeName), value);
        }

        private static object CreateAiSimulationTime(long tick, double seconds)
        {
            return Activator.CreateInstance(ResolveType(AiSimulationTimeTypeName), tick, seconds);
        }

        private static object CreateEligibilitySnapshot(
            bool isInActiveSession = true,
            bool isConnected = true,
            bool isDowned = false,
            bool isEliminated = false,
            bool hasOtherInvalidGameplayState = false)
        {
            return Activator.CreateInstance(
                ResolveType(StalkerTargetEligibilitySnapshotTypeName),
                isInActiveSession,
                isConnected,
                isDowned,
                isEliminated,
                hasOtherInvalidGameplayState);
        }

        private static object CreateTargetSnapshot(
            int playerId,
            Transform targetSample,
            Transform targetHierarchyRoot,
            object eligibilitySnapshot)
        {
            return Activator.CreateInstance(
                ResolveType(StalkerPerceptionTargetSnapshotTypeName),
                CreatePlayerId(playerId),
                targetSample,
                targetHierarchyRoot,
                eligibilitySnapshot);
        }

        private static object CreateTargetList(params object[] targets)
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(ResolveType(StalkerPerceptionTargetSnapshotTypeName)));
            for (var i = 0; i < targets.Length; i++)
            {
                list.Add(targets[i]);
            }

            return list;
        }

        private static object CreateCandidateResultList()
        {
            return Activator.CreateInstance(typeof(List<>).MakeGenericType(ResolveType(StalkerTargetCandidateTypeName)));
        }

        private static int CollectVisibleTargetCandidates(Component sensor, object targets, object observedAt, object results)
        {
            var method = ResolveType(StalkerPerceptionEvaluatorTypeName).GetMethod(
                "CollectVisibleTargetCandidates",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "Missing StalkerPerceptionEvaluator.CollectVisibleTargetCandidates.");

            var count = method.Invoke(null, new[] { sensor, targets, observedAt, results });
            Assert.That(count, Is.TypeOf<int>());
            return (int)count;
        }

        private static object SelectNearestEligibleVisible(object candidates)
        {
            var method = ResolveType(StalkerTargetSelectorTypeName).GetMethod(
                "TrySelectNearestEligibleVisible",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "Missing StalkerTargetSelector.TrySelectNearestEligibleVisible.");

            var args = new object[] { candidates, 0f, null };
            var accepted = method.Invoke(null, args);
            Assert.That(accepted, Is.EqualTo(true));
            return args[2];
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

        private static object GetStaticProperty(Type type, string propertyName)
        {
            var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
            Assert.That(property, Is.Not.Null, $"Missing static property '{propertyName}' on '{type.FullName}'.");

            return property.GetValue(null);
        }

        private static void AssertPlayerIdValue(object playerId, int expected)
        {
            Assert.That(GetBoolProperty(playerId, "IsValid"), Is.True);
            Assert.That((int)GetProperty(playerId, "Value"), Is.EqualTo(expected));
        }

        private static void AssertAiSimulationTime(object observedAt, long expectedTick, double expectedSeconds)
        {
            Assert.That(GetBoolProperty(observedAt, "IsValid"), Is.True);
            Assert.That((long)GetProperty(observedAt, "Tick"), Is.EqualTo(expectedTick));
            Assert.That((double)GetProperty(observedAt, "Seconds"), Is.EqualTo(expectedSeconds));
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
