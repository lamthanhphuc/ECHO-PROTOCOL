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
    public sealed class StalkerFlashlightPerceptionPlayModeTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private NavMeshDataInstance _navMesh;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject gameObject in _objects)
                if (gameObject != null) UnityEngine.Object.Destroy(gameObject);
            _objects.Clear();
            if (_navMesh.valid) _navMesh.Remove();
            yield return null;
        }

        [UnityTest]
        public IEnumerator HiddenBody_VisibleLitSurface_StartsAnonymousSearch()
        {
            var fixture = CreateFixture(new Vector3(2f, 0f, 0f));
            Transform player = Create("Player", new Vector3(4f, 0f, 7f)).transform;
            Transform beam = Create("Beam", new Vector3(4f, 1f, 7f)).transform;
            beam.SetParent(player, true);
            beam.forward = new Vector3(-3f, 0f, -1f);
            Cube("Body blocker", new Vector3(3f, 1f, 3.5f), new Vector3(1f, 3f, 1f));
            Cube("Lit wall", new Vector3(1f, 1f, 6f), new Vector3(0.2f, 2f, 2f));
            Physics.SyncTransforms();

            Assert.That(BodyVisible(fixture.Sensor, player), Is.False);
            Assert.That(TryClue(fixture.Sensor, beam, player, out Vector3 clue), Is.True);
            Assert.That(Simulate(fixture.Controller, Observations(Observation(clue, beam.forward))), Is.True);
            Assert.That(State(fixture.Controller), Is.EqualTo("SEARCH"));
            Assert.That(SearchSource(fixture.Controller), Is.EqualTo("FlashlightClue"));
            Assert.That(IsValid(Read(fixture.Controller, "CurrentTargetId")), Is.False);
            Assert.That(IsValid(Read(fixture.Controller, "DetectionTargetId")), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator OccludedLitSurface_DoesNotCreateClue()
        {
            var fixture = CreateFixture(new Vector3(2f, 0f, 0f));
            Transform player = Create("Player", new Vector3(4f, 0f, 7f)).transform;
            Transform beam = Create("Beam", new Vector3(4f, 1f, 7f)).transform;
            beam.SetParent(player, true);
            beam.forward = new Vector3(-3f, 0f, -1f);
            Cube("Lit wall", new Vector3(1f, 1f, 6f), new Vector3(0.2f, 2f, 2f));
            Cube("Sight blocker", new Vector3(1.5f, 1f, 3f), new Vector3(1f, 3f, 1f));
            Physics.SyncTransforms();

            Assert.That(TryClue(fixture.Sensor, beam, player, out _), Is.False);
            Simulate(fixture.Controller, null);
            Assert.That(State(fixture.Controller), Is.EqualTo("PATROL"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator VisibleBody_WithFlashlight_UsesNormalDetection()
        {
            var fixture = CreateFixture(Vector3.zero);
            var visible = Candidate(2, new Vector3(0f, 1f, 4f));
            Simulate(fixture.Controller, Observations(Observation(new Vector3(1f, 1f, 5f))),
                Candidates(visible));
            Assert.That(State(fixture.Controller), Is.EqualTo("DETECT"));
            Assert.That(SearchSource(fixture.Controller), Is.Null);
            Assert.That(IsValid(Read(fixture.Controller, "DetectionTargetId")), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HiddenFlashlightOff_DoesNotInspectLocker()
        {
            var fixture = CreateFixture(Vector3.zero);
            Create("Locker", new Vector3(0f, 0f, 3f)).AddComponent(Resolve("HidingSpot"));
            Simulate(fixture.Controller, null);
            Assert.That(State(fixture.Controller), Is.EqualTo("PATROL"));
            Assert.That(SearchSource(fixture.Controller), Is.Null);
            Assert.That(Read(ReadPrivate(fixture.Controller, "_navigationObjectiveKey"), "Kind").ToString(),
                Is.Not.EqualTo("HideSpotInspection"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator HiddenFlashlightOn_InspectsMatchingLockerAndForcesExit()
        {
            BuildNavMesh();
            var fixture = CreateFixture(Vector3.zero);
            GameObject locker = Create("Locker", new Vector3(0f, 0f, 3f));
            Component spot = locker.AddComponent(Resolve("HidingSpot"));
            ulong stableId = (ulong)Read(spot, "StableId");
            GameObject player = Create("Hidden player", new Vector3(0f, 0f, 3f));
            Component identity = player.AddComponent(Resolve("EchoProtocol.Player.PlayerRuntimeIdentity"));
            Call(identity, "TryBind", PlayerId(1));
            Component hiding = player.AddComponent(Resolve("PlayerHidingController"));
            Assert.That((bool)Call(hiding, "EnterHiding", spot), Is.True);
            Physics.SyncTransforms();

            Simulate(fixture.Controller, Observations(Observation(player.transform.position,
                Vector3.forward, true, stableId)));
            Assert.That(State(fixture.Controller), Is.EqualTo("SEARCH"));
            Assert.That(SearchSource(fixture.Controller), Is.EqualTo("FlashlightClue"));
            object objective = ReadPrivate(fixture.Controller, "_navigationObjectiveKey");
            Assert.That(Read(objective, "Kind").ToString(), Is.EqualTo("HideSpotInspection"));
            Assert.That(Convert.ToUInt64(Read(objective, "StableEntityId")), Is.EqualTo(stableId));

            Assert.That(fixture.Agent.Warp(locker.transform.position), Is.True);
            for (int tick = 2; tick <= 6 && (bool)Read(hiding, "IsHidden"); tick++)
                Simulate(fixture.Controller, null, null, tick, 0.25f);
            Assert.That((bool)Read(hiding, "IsHidden"), Is.False);
            Assert.That((bool)Call(spot, "IsTemporarilyLockedOut"), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HiddenFlashlightOn_BlockedLockerIsNotInspected()
        {
            var fixture = CreateFixture(Vector3.zero);
            Component spot = Create("Blocked locker", new Vector3(0f, 0f, 5f))
                .AddComponent(Resolve("HidingSpot"));
            Cube("Wall", new Vector3(0f, 1f, 2.5f), new Vector3(3f, 3f, 0.5f));
            Physics.SyncTransforms();
            Simulate(fixture.Controller, Observations(Observation(Vector3.zero,
                Vector3.forward, true, (ulong)Read(spot, "StableId"))));
            Assert.That(State(fixture.Controller), Is.EqualTo("PATROL"));
            Assert.That(SearchSource(fixture.Controller), Is.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator VisiblePlayerB_BeatsPlayerAFlashlightClue()
        {
            var fixture = CreateFixture(Vector3.zero);
            Simulate(fixture.Controller, Observations(Observation(new Vector3(1f, 1f, 5f))),
                Candidates(Candidate(2, new Vector3(0f, 1f, 4f))));
            Assert.That(State(fixture.Controller), Is.EqualTo("DETECT"));
            Assert.That(Convert.ToInt32(Read(Read(fixture.Controller, "DetectionTargetId"), "Value")),
                Is.EqualTo(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ChasePlayerA_DoesNotSwitchToPlayerBFlashlight()
        {
            var fixture = CreateFixture(Vector3.zero);
            SetPrivate(fixture.Controller, "currentState", Enum.Parse(Resolve("EchoProtocol.AI.Stalker.StalkerState"), "CHASE"));
            Call(ReadPrivate(fixture.Controller, "_memory"), "SetCurrentTarget", PlayerId(1));
            SetPrivate(fixture.Controller, "currentTarget", Create("A", new Vector3(0f, 1f, 10f)).transform);
            Simulate(fixture.Controller, Observations(Observation(new Vector3(1f, 1f, 5f))),
                Candidates(Candidate(1, new Vector3(0f, 1f, 10f))));
            Assert.That(State(fixture.Controller), Is.EqualTo("CHASE"));
            Assert.That(Convert.ToInt32(Read(Read(fixture.Controller, "CurrentTargetId"), "Value")),
                Is.EqualTo(1));
            Assert.That(SearchSource(fixture.Controller), Is.Null);
            yield return null;
        }

        private (Component Controller, Component Sensor, NavMeshAgent Agent) CreateFixture(Vector3 position)
        {
            GameObject stalker = Create("Stalker", position);
            GameObject eye = Create("Eye", position + Vector3.up);
            eye.transform.SetParent(stalker.transform, true);
            Component sensor = stalker.AddComponent(Resolve("EchoProtocol.AI.Stalker.StalkerVisionSensor"));
            SetPrivate(sensor, "visionOrigin", eye.transform);
            SetPrivate(sensor, "visionDistance", 30f);
            SetPrivate(sensor, "visionAngle", 140f);
            LayerMask mask = Physics.DefaultRaycastLayers;
            SetPrivate(sensor, "losBlockerMask", mask);
            Component controller = stalker.AddComponent(Resolve("EchoProtocol.AI.Stalker.StalkerController"));
            SetPrivate(controller, "visionSensor", sensor);
            ((Behaviour)controller).enabled = false;
            ((Behaviour)sensor).enabled = false;
            return (controller, sensor, stalker.GetComponent<NavMeshAgent>());
        }

        private void BuildNavMesh()
        {
            var settings = NavMesh.GetSettingsByID(0);
            Assert.That(settings.agentTypeID, Is.Not.EqualTo(-1));
            var source = new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                transform = Matrix4x4.TRS(new Vector3(0f, -0.05f, 0f),
                    Quaternion.identity, Vector3.one),
                size = new Vector3(12f, 0.1f, 12f),
                area = 0
            };
            var data = NavMeshBuilder.BuildNavMeshData(settings,
                new List<NavMeshBuildSource> { source },
                new Bounds(Vector3.zero, new Vector3(14f, 4f, 14f)),
                Vector3.zero, Quaternion.identity);
            Assert.That(data, Is.Not.Null);
            _navMesh = NavMesh.AddNavMeshData(data);
            Assert.That(_navMesh.valid, Is.True);
        }

        private GameObject Create(string name, Vector3 position)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.position = position;
            _objects.Add(gameObject);
            return gameObject;
        }

        private void Cube(string name, Vector3 position, Vector3 scale)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.position = position;
            cube.transform.localScale = scale;
            _objects.Add(cube);
        }

        private static bool BodyVisible(Component sensor, Transform player)
        {
            object[] args = { player, null };
            return (bool)sensor.GetType().GetMethod("TryEvaluateCandidate", new[]
            {
                typeof(Transform), Resolve("EchoProtocol.AI.Stalker.StalkerPhysicalVisionObservation").MakeByRefType()
            }).Invoke(sensor, args);
        }

        private static bool TryClue(Component sensor, Transform beam, Transform owner, out Vector3 point)
        {
            object[] args = { beam, owner, 15f, 66f, null };
            bool visible = (bool)sensor.GetType().GetMethod("TryGetVisibleFlashlightClue")
                .Invoke(sensor, args);
            point = (Vector3)args[4];
            return visible;
        }

        private static object Observation(Vector3 position, Vector3? direction = null,
            bool hidden = false, ulong spotId = 0UL) => Activator.CreateInstance(
            Resolve("EchoProtocol.AI.Stalker.StalkerFlashlightObservation"),
            position, direction ?? Vector3.forward, hidden, spotId);

        private static Array Observations(params object[] values)
        {
            Array array = Array.CreateInstance(Resolve("EchoProtocol.AI.Stalker.StalkerFlashlightObservation"), values.Length);
            for (int i = 0; i < values.Length; i++) array.SetValue(values[i], i);
            return array;
        }

        private static object Candidate(int id, Vector3 position)
        {
            object playerId = PlayerId(id);
            object time = Activator.CreateInstance(Resolve("EchoProtocol.AI.Common.AiSimulationTime"), 1L, 0d);
            object vision = Activator.CreateInstance(Resolve("EchoProtocol.AI.Stalker.VisionObservation"),
                playerId, position, Vector3.forward, time, position.magnitude);
            object eligible = Resolve("EchoProtocol.AI.Stalker.StalkerTargetEligibilityResult")
                .GetMethod("EligibleTarget").Invoke(null, Array.Empty<object>());
            return Activator.CreateInstance(Resolve("EchoProtocol.AI.Stalker.StalkerTargetCandidate"), vision, eligible);
        }

        private static Array Candidates(params object[] values)
        {
            Array array = Array.CreateInstance(Resolve("EchoProtocol.AI.Stalker.StalkerTargetCandidate"), values.Length);
            for (int i = 0; i < values.Length; i++) array.SetValue(values[i], i);
            return array;
        }

        private static bool Simulate(Component controller, Array observations, Array candidates = null,
            int tick = 1, float delta = 0.1f)
        {
            object time = Activator.CreateInstance(Resolve("EchoProtocol.AI.Common.AiSimulationTime"),
                (long)tick, (double)(tick - 1) * delta);
            object step = Activator.CreateInstance(Resolve("EchoProtocol.AI.Common.AiSimulationStep"), time, delta);
            Array statuses = null;
            if (candidates != null)
            {
                statuses = Array.CreateInstance(Resolve("EchoProtocol.AI.Stalker.StalkerTargetStatus"),
                    candidates.Length);
                object eligible = Resolve("EchoProtocol.AI.Stalker.StalkerTargetEligibilityResult")
                    .GetMethod("EligibleTarget").Invoke(null, Array.Empty<object>());
                for (int i = 0; i < candidates.Length; i++)
                    statuses.SetValue(Activator.CreateInstance(
                        Resolve("EchoProtocol.AI.Stalker.StalkerTargetStatus"),
                        Read(Read(candidates.GetValue(i), "Observation"), "PlayerId"), eligible), i);
            }
            object input = Activator.CreateInstance(Resolve("EchoProtocol.AI.Stalker.StalkerSimulationInput"),
                step, candidates, statuses, null, null, default(DateTime), null, null, observations);
            return (bool)Call(controller, "Simulate", input);
        }

        private static string State(Component controller) => Read(controller, "CurrentState").ToString();
        private static string SearchSource(Component controller)
        {
            object context = Read(controller, "ActiveSearchContext");
            return context == null ? null : Read(context, "Source").ToString();
        }

        private static object PlayerId(int id) => Activator.CreateInstance(
            Resolve("EchoProtocol.AI.Common.PlayerId"), id);
        private static bool IsValid(object value) => (bool)Read(value, "IsValid");
        private static object Read(object target, string name) => target.GetType()
            .GetProperty(name, BindingFlags.Public | BindingFlags.Instance).GetValue(target);
        private static object ReadPrivate(object target, string name) => target.GetType()
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
        private static void SetPrivate(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static object Call(object target, string name, params object[] args) => target.GetType()
            .GetMethod(name, BindingFlags.Public | BindingFlags.Instance).Invoke(target, args);
        private static Type Resolve(string name)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(name, false);
                if (type != null) return type;
            }
            Assert.Fail($"Missing production type {name}");
            return null;
        }
    }
}
