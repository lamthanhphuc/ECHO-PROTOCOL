using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EchoProtocol.Tests.EditMode.Controls
{
    public sealed class CreepMinionPresentationTests
    {
        [Test]
        public void CarriedToolPose_FollowsRightHandAfterMovement()
        {
            var root = new GameObject("CarryTest");
            try
            {
                var minion = root.AddComponent<EchoProtocol.AI.Minions.CreepMinionRuntime>();
                var hand = new GameObject("hand.r").transform;
                hand.SetParent(root.transform, false);
                hand.localPosition = new Vector3(0.5f, 1.2f, 0.4f);
                hand.localRotation = Quaternion.Euler(25f, 40f, 10f);
                minion.GetToolCarryPose(out var position, out var rotation);
                Assert.That(Vector3.Distance(position, hand.position), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(rotation, hand.rotation), Is.LessThan(0.001f));
                root.transform.SetPositionAndRotation(new Vector3(20f, 2f, 5f), Quaternion.Euler(0f, 80f, 0f));
                minion.GetToolCarryPose(out position, out rotation);
                Assert.That(Vector3.Distance(position, hand.position), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(rotation, hand.rotation), Is.LessThan(0.001f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void DropRoomResolution_UsesFloorFootprintAndRejectsUnknownAreas()
        {
            var first = new GameObject("RoomA");
            var second = new GameObject("RoomB");
            try
            {
                var a = first.AddComponent<EchoProtocol.AI.Stalker.Spatial.RegionDefinition>();
                var b = second.AddComponent<EchoProtocol.AI.Stalker.Spatial.RegionDefinition>();
                second.transform.position = new Vector3(20f, 0f, 0f);
                ConfigureRoom(a, 1);
                ConfigureRoom(b, 2);
                var resolve = typeof(EchoProtocol.AI.Minions.CreepMinionRuntime).GetMethod("ResolveDropRoom",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var rooms = new[] { a, b };
                Assert.That(resolve.Invoke(null, new object[] { rooms, new Vector3(0f, 0.15f, 0f) }), Is.SameAs(a));
                Assert.That(resolve.Invoke(null, new object[] { rooms, new Vector3(20f, 0.15f, 0f) }), Is.SameAs(b));
                Assert.That(resolve.Invoke(null, new object[] { rooms, new Vector3(10f, 0.15f, 0f) }), Is.Null);
                Assert.That(resolve.Invoke(null, new object[] { rooms, new Vector3(0f, 5f, 0f) }), Is.Null);
            }
            finally { Object.DestroyImmediate(first); Object.DestroyImmediate(second); }
        }

        private static void ConfigureRoom(EchoProtocol.AI.Stalker.Spatial.RegionDefinition room, int id)
        {
            var serialized = new SerializedObject(room);
            serialized.FindProperty("regionId").intValue = id;
            serialized.FindProperty("localBounds").boundsValue = new Bounds(Vector3.zero, new Vector3(8f, 0.05f, 8f));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

    }
}
