using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace EchoProtocol.Networking.Tests
{
    public sealed class CreepMinionMovementTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<Object> _objects = new List<Object>();
        private MonoBehaviour _minion;
        private CapsuleCollider _body;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/PF_CreepMinionNetwork.prefab");
            var type = prefab.GetComponents<MonoBehaviour>().Single(component =>
                component != null && component.GetType().FullName == "EchoProtocol.AI.Minions.CreepMinionRuntime").GetType();
            var go = new GameObject("Minion movement test");
            _objects.Add(go);
            go.transform.position = new Vector3(-2f, 1000f, 0f);
            _minion = (MonoBehaviour)go.AddComponent(type);
            _minion.enabled = false;
            var agent = go.GetComponent<NavMeshAgent>();
            agent.enabled = false;
            _body = go.AddComponent<CapsuleCollider>();
            SetBody(.65f, 1.8f, .9f);
            Field("_agent", agent);
            Field("_bodyCollider", _body);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects) Object.DestroyImmediate(go);
            _objects.Clear();
        }

        [TestCase(-2f, 2f)]
        [TestCase(2f, -2f)]
        public void ThinMeshWallBlocksMovementFromEitherSide(float start, float destination)
        {
            MeshWall(new Vector3(0f, 1001f, 0f), new Vector3(.05f, 2f, 3f));
            _minion.transform.position = new Vector3(start, 1000f, 0f);
            Assert.That(Blocked(new Vector3(destination, 1000f, 0f)), Is.True);
        }

        [Test]
        public void StationaryWatchdogWaitsFiveSecondsAndResetsAfterMovement()
        {
            var monitor = _minion.GetType().GetField("_stationaryRecovery", PrivateInstance).GetValue(_minion);
            var observe = monitor.GetType().GetMethod("Observe");
            var state = monitor.GetType().GetProperty("State");
            observe.Invoke(monitor, new object[] { Vector3.zero, 0f, 0f });
            for (int i = 0; i < 19; i++) observe.Invoke(monitor, new object[] { Vector3.zero, 0f, .25f });
            Assert.That(state.GetValue(monitor).ToString(), Is.Not.EqualTo("Stuck"));
            observe.Invoke(monitor, new object[] { Vector3.zero, 0f, .25f });
            Assert.That(state.GetValue(monitor).ToString(), Is.EqualTo("Stuck"));
            observe.Invoke(monitor, new object[] { Vector3.right, 0f, .25f });
            Assert.That(state.GetValue(monitor).ToString(), Is.EqualTo("Moving"));
        }

        [Test]
        public void ShallowWaterRaisesFeetAboveGroundAndIgnoresWaterLayerAndRoof()
        {
            MeshWall(new Vector3(-2f, 1000.1f, 0f), new Vector3(6f, .2f, 6f));
            MeshWall(new Vector3(-2f, 1001.2f, 0f), new Vector3(6f, .2f, 6f));
            var water = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _objects.Add(water);
            water.layer = 4;
            water.transform.position = new Vector3(-2f, 1000.7f, 0f);
            water.transform.localScale = new Vector3(6f, .2f, 6f);
            Physics.SyncTransforms();
            float height = (float)_minion.GetType().GetMethod("ResolveSwimHeight", PrivateInstance)
                .Invoke(_minion, new object[] { 1000.9f });
            Assert.That(height, Is.EqualTo(1000.25f).Within(.002f));
        }

        [Test]
        public void DeepWaterPreservesTheAuthoredSwimDepth()
        {
            float height = (float)_minion.GetType().GetMethod("ResolveSwimHeight", PrivateInstance)
                .Invoke(_minion, new object[] { 1000.9f });
            Assert.That(height, Is.EqualTo(1000f).Within(.002f));
        }

        [Test]
        public void BakedCatfishFootprintFitsOpenDoorAndStillHitsClosedDoor()
        {
            MeshWall(new Vector3(0f, 1001f, -1f), new Vector3(.2f, 2f, 1f));
            MeshWall(new Vector3(0f, 1001f, 1f), new Vector3(.2f, 2f, 1f));
            var destination = new Vector3(2f, 1000f, 0f);
            Assert.That(Blocked(destination), Is.True, "The old 0.65m body does not fit a 1m doorway.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/StalkerNetwork_Lizard.prefab");
            float radius = NavMesh.GetSettingsByID(prefab.GetComponent<NavMeshAgent>().agentTypeID).agentRadius;
            SetBody(radius, 1.8f, .9f);
            Assert.That(Blocked(destination), Is.False);
            MeshWall(new Vector3(0f, 1001f, 0f), new Vector3(.1f, 2f, 1f));
            Assert.That(Blocked(destination), Is.True);
        }

        [TestCase(false, false, false)]
        [TestCase(true, false, true)]
        [TestCase(false, true, true)]
        public void NavigationAllowsBakedStepButNotClosedDoorOrLowCeiling(bool closedDoor, bool lowCeiling, bool expectedBlocked)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/StalkerNetwork_Lizard.prefab");
            var agent = _minion.GetComponent<NavMeshAgent>();
            agent.agentTypeID = prefab.GetComponent<NavMeshAgent>().agentTypeID;
            SetBody(NavMesh.GetSettingsByID(agent.agentTypeID).agentRadius, 1.8f, .9f);
            _minion.transform.position = new Vector3(-1.05f, 1000f, 0f);
            MeshWall(new Vector3(0f, 1000.25f, 0f), new Vector3(1f, .5f, 4f));
            if (closedDoor) MeshWall(new Vector3(-.5f, 1001f, 0f), new Vector3(.1f, 2f, 4f));
            if (lowCeiling) MeshWall(new Vector3(-1f, 1002.2f, 0f), new Vector3(4f, .2f, 4f));
            var destination = new Vector3(-.75f, 1000f, 0f);
            Physics.SyncTransforms();
            bool blocked = (bool)_minion.GetType().GetMethod("IsNavigationStepBlocked", PrivateInstance)
                .Invoke(_minion, new object[] { destination, 0f });
            Assert.That(blocked, Is.EqualTo(expectedBlocked));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SlightFloorOverlapDoesNotBlockEveryDirectionButClosedWallStillBlocks(bool meshFloor)
        {
            if (meshFloor) MeshWall(new Vector3(0f, 999.9f, 0f), new Vector3(12f, .2f, 8f));
            else
            {
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _objects.Add(floor);
                floor.transform.position = new Vector3(0f, 999.9f, 0f);
                floor.transform.localScale = new Vector3(12f, .2f, 8f);
            }
            _minion.transform.position = new Vector3(-2f, 999.98f, 0f);
            Assert.That(Blocked(new Vector3(-1f, 999.98f, 0f)), Is.False);
            MeshWall(new Vector3(0f, 1001f, 0f), new Vector3(.1f, 2f, 3f));
            Assert.That(Blocked(new Vector3(2f, 999.98f, 0f)), Is.True);
        }

        [Test]
        public void ExistingWallOverlapAllowsEscapeAndSlidingButBlocksDeeperEntry()
        {
            MeshWall(new Vector3(0f, 1001f, 0f), new Vector3(.1f, 2f, 8f));
            _minion.transform.position = new Vector3(-.64f, 1000f, 0f);
            Assert.That(Blocked(new Vector3(-1f, 1000f, 0f)), Is.False);
            Assert.That(Blocked(new Vector3(-.64f, 1000f, 1f)), Is.False);
            Assert.That(Blocked(new Vector3(.2f, 1000f, 0f)), Is.True);
        }

        [Test]
        public void FloorOverlapStillDetectsWallInTheSameMeshCollider()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cubeMesh = cube.GetComponent<MeshFilter>().sharedMesh;
            var mesh = new Mesh();
            mesh.CombineMeshes(new[] {
                new CombineInstance { mesh = cubeMesh, transform = Matrix4x4.TRS(new Vector3(0f, -.1f, 0f), Quaternion.identity, new Vector3(12f, .2f, 8f)) },
                new CombineInstance { mesh = cubeMesh, transform = Matrix4x4.TRS(new Vector3(0f, 1f, 0f), Quaternion.identity, new Vector3(.1f, 2f, 8f)) }
            });
            Object.DestroyImmediate(cube);
            _objects.Add(mesh);
            var go = new GameObject("Combined floor and closed wall");
            _objects.Add(go);
            go.transform.position = Vector3.up * 1000f;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            _minion.transform.position = new Vector3(-2f, 999.98f, 0f);
            Assert.That(Blocked(new Vector3(-3f, 999.98f, 0f)), Is.False);
            Assert.That(Blocked(new Vector3(2f, 999.98f, 0f)), Is.True);
        }

        [TestCase(false, true, true)]
        [TestCase(true, true, false)]
        [TestCase(false, false, false)]
        public void LocalRecoveryCrossesOnlySmallNavigationGapOverClearContinuousFloor(bool wall, bool floor, bool expected)
        {
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = .1f;
            settings.overrideVoxelSize = true;
            settings.voxelSize = .05f;
            settings.minRegionArea = 0f;
            var sources = new List<NavMeshBuildSource>();
            foreach (float x in new[] { -2.6f, 2.6f })
                sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(new Vector3(x, 999.9f, 0f), Quaternion.identity, Vector3.one),
                    size = new Vector3(4.8f, .2f, 6f), area = 0 });
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources,
                new Bounds(Vector3.up * 1000f, new Vector3(12f, 4f, 8f)), Vector3.zero, Quaternion.identity);
            _objects.Add(data);
            var instance = NavMesh.AddNavMeshData(data);
            try
            {
                if (floor) MeshWall(new Vector3(0f, 999.9f, 0f), new Vector3(12f, .2f, 8f));
                else foreach (float x in new[] { -2.6f, 2.6f })
                    MeshWall(new Vector3(x, 999.9f, 0f), new Vector3(4.8f, .2f, 6f));
                if (wall) MeshWall(new Vector3(0f, 1001.5f, 0f), new Vector3(.08f, 3f, 8f));
                var agent = _minion.GetComponent<NavMeshAgent>();
                _minion.transform.position = new Vector3(-.65f, 1000.05f, 0f);
                agent.agentTypeID = 0;
                Physics.SyncTransforms();
                Assert.That(NavMesh.SamplePosition(new Vector3(.55f, 1000.05f, 0f), out var right, .25f, NavMesh.AllAreas), Is.True);
                var checkPath = new NavMeshPath();
                NavMesh.CalculatePath(right.position, new Vector3(2f, 1000.05f, 0f), NavMesh.AllAreas, checkPath);
                Assert.That(checkPath.status, Is.EqualTo(NavMeshPathStatus.PathComplete), right.position.ToString());
                var navigationType = _minion.GetType().GetField("_navigation", PrivateInstance).FieldType;
                var navigation = System.Activator.CreateInstance(navigationType, new object[] { agent });
                var before = _minion.transform.position;
                var arguments = new object[] { new Vector3(2f, 1000.05f, 0f), Vector3.zero };
                bool recovered = (bool)navigationType.GetMethod("TryFindLocalRecoveryPosition", PrivateInstance)
                    .Invoke(navigation, arguments);
                Assert.That(recovered, Is.EqualTo(expected), "start=" + before + ", right=" + right.position
                    + ", bodyBlocked=" + Blocked(right.position));
                if (expected) Assert.That(((Vector3)arguments[1]).x, Is.GreaterThan(0f));
                else Assert.That(_minion.transform.position, Is.EqualTo(before));
            }
            finally { instance.Remove(); }
        }

        [TestCase(-2f, 2f)]
        [TestCase(2f, -2f)]
        public void SingleSidedMeshWallBlocksMovementFromEitherSide(float start, float destination)
        {
            var go = new GameObject("Single sided mesh wall");
            _objects.Add(go);
            var mesh = new Mesh
            {
                vertices = new[] { new Vector3(0, 0, -2), new Vector3(0, 2, -2), new Vector3(0, 2, 2), new Vector3(0, 0, 2) },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            _objects.Add(mesh);
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.transform.position = Vector3.up * 1000f;
            _minion.transform.position = new Vector3(start, 1000f, 0f);
            bool hitBackfaces = Physics.queriesHitBackfaces;
            try
            {
                Physics.queriesHitBackfaces = false;
                Assert.That(Blocked(new Vector3(destination, 1000f, 0f)), Is.True);
                Assert.That(Physics.queriesHitBackfaces, Is.False, "The movement query must restore the shared physics setting.");
            }
            finally { Physics.queriesHitBackfaces = hitBackfaces; }
        }

        [Test]
        public void MeshWallBesideCenterLineStillBlocksTheFullBody()
        {
            MeshWall(new Vector3(0f, 1001f, .55f), new Vector3(.1f, 2f, .1f));
            Assert.That(Blocked(new Vector3(2f, 1000f, 0f)), Is.True,
                "The old 0.3m sphere missed walls intersecting the actual 0.65m body.");
        }

        [Test]
        public void FlyingBodyChecksTheUpperWallAtItsActualHeight()
        {
            SetBody(.4f, 1.2f, 1.6f);
            MeshWall(new Vector3(0f, 1007.6f, 0f), new Vector3(.1f, 2f, 3f));
            var destination = new Vector3(2f, 1000f, 0f);
            Assert.That(Blocked(destination, 6f), Is.True);
            Assert.That(Blocked(destination, .3f), Is.False);
        }

        [Test]
        public void OpenDoorAllowsLowFlightAndClosedDoorBlocksBothHeights()
        {
            SetBody(.4f, 1.2f, 1.6f);
            MeshWall(new Vector3(0f, 1005f, -1.5f), new Vector3(.1f, 10f, 1f));
            MeshWall(new Vector3(0f, 1005f, 1.5f), new Vector3(.1f, 10f, 1f));
            MeshWall(new Vector3(0f, 1006.5f, 0f), new Vector3(.1f, 7f, 2f));
            var destination = new Vector3(2f, 1000f, 0f);
            Physics.SyncTransforms();
            var height = (float)_minion.GetType().GetMethod("ResolveFlightHeight", PrivateInstance)
                .Invoke(_minion, new object[] { destination });
            Assert.That(height, Is.EqualTo(.3f));
            Assert.That(Blocked(destination, height), Is.False);

            MeshWall(new Vector3(0f, 1001.5f, 0f), new Vector3(.1f, 3f, 2f));
            Assert.That(Blocked(destination, .3f), Is.True);
            Assert.That(Blocked(destination, 6f), Is.True);
        }

        private void SetBody(float radius, float height, float centerY)
        {
            _body.radius = radius;
            _body.height = height;
            _body.center = Vector3.up * centerY;
            Field("_bodyColliderBaseCenter", _body.center);
        }

        private void Field(string name, object value) =>
            _minion.GetType().GetField(name, PrivateInstance).SetValue(_minion, value);

        private bool Blocked(Vector3 destination, float height = 0f)
        {
            Physics.SyncTransforms();
            return (bool)_minion.GetType().GetMethod("IsMovementStepBlocked", PrivateInstance)
                .Invoke(_minion, new object[] { destination, height });
        }

        private void MeshWall(Vector3 position, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _objects.Add(go);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(go.GetComponent<BoxCollider>());
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.transform.position = position;
            go.transform.localScale = size;
        }
    }
}
