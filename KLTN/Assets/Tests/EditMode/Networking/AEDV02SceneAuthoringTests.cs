using System;
using System.Linq;
using System.Reflection;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.Networking.Tests
{
    public sealed class AEDV02SceneAuthoringTests
    {
        [Test]
        public void AEDV02_FixedSafeRoomGuardRejectsZone1EntryWithoutAedActivation()
        {
            const string path = "Assets/Scenes/SciFi.unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var source = Type.GetType("EchoProtocol.AI.AED.AEDV02SceneSource, Assembly-CSharp", true);
                var args = new object[] { null, null };
                Assert.That(source.GetMethod("TryLoad").Invoke(null, args), Is.True);
                var manifest = (AEDV02SceneManifest)args[0];
                var entry = manifest.Markers.Single(x => x.Id == "Z1_ENTRY");
                var check = source.GetMethod("IsSciFiSafeRoomClearPath");
                Assert.That(check, Is.Not.Null);
                var crossing = new[] { new Vector3((float)entry.X, (float)entry.Y, (float)entry.Z),
                    new Vector3(35.834446f, 1.300647f, -77.35453f) };
                Assert.That(manifest.TryIsInsideSafeRoom(crossing[1].x, crossing[1].y, crossing[1].z, out var inside), Is.True);
                Assert.That(inside, Is.True, "Fixed Minion spawn candidates inside the start Safe Room must be excluded.");
                Assert.That((bool)check.Invoke(null, new object[] { crossing }), Is.False);
                var clear = new[] { crossing[0], new Vector3(34.5f, 1.06f, -100.8f) };
                Assert.That((bool)check.Invoke(null, new object[] { clear }), Is.True);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        [TestCase(35.834446f, 2.300647f, -77.35453f)]
        [TestCase(34.766804f, 2.205f, -75.776802f)]
        public void AEDV02_Adaptive03PlayerFeetRemainOnMeshAcrossDownAndRevive(float x, float y, float z)
        {
            var source = Type.GetType("EchoProtocol.AI.AED.AEDV02SceneSource, Assembly-CSharp", true);
            var args = new object[] { null, null };
            Assert.That(source.GetMethod("TryLoad").Invoke(null, args), Is.True);
            var manifest = (AEDV02SceneManifest)args[0];
            var player = new GameObject("AED ground evidence player");
            try
            {
                player.transform.position = new Vector3(x, y, z);
                var character = player.AddComponent<CharacterController>();
                character.height = 2f;
                character.radius = .5f;
                character.center = Vector3.zero;
                Physics.SyncTransforms();

                Assert.That(manifest.NavCells.Any(c => c.Contains(new double[] { x, y, z })), Is.False,
                    "The original player root must reproduce the Adaptive 03 rejection.");
                foreach (var height in new[] { 2f, .75f, 2f })
                {
                    character.height = height;
                    character.center = Vector3.up * ((character.height - 2f) * .5f);
                    Physics.SyncTransforms();
                    var feet = GroundPosition(character);
                    Assert.That(feet.y, Is.EqualTo(y - 1f).Within(.02f));
                    Assert.That(manifest.NavCells.Any(c => c.Contains(new double[] { feet.x, feet.y, feet.z })), Is.True,
                        "Standing, downed, and revived feet must remain on the authored surface.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(player); }
        }

        [Test]
        public void AEDV02_GroundEvidenceDoesNotSnapInvalidPositionsIntoMesh()
        {
            var source = Type.GetType("EchoProtocol.AI.AED.AEDV02SceneSource, Assembly-CSharp", true);
            var args = new object[] { null, null };
            Assert.That(source.GetMethod("TryLoad").Invoke(null, args), Is.True);
            var manifest = (AEDV02SceneManifest)args[0];
            var player = new GameObject("AED invalid ground evidence player");
            try
            {
                var character = player.AddComponent<CharacterController>();
                character.height = 2f;
                player.transform.position = new Vector3(1000f, 2.3f, 1000f);
                Physics.SyncTransforms();
                var outside = GroundPosition(character);
                Assert.That(manifest.NavCells.Any(c => c.Contains(new double[] { outside.x, outside.y, outside.z })), Is.False);

                player.transform.position = new Vector3(35.834446f, 7.300647f, -77.35453f);
                Physics.SyncTransforms();
                var otherFloor = GroundPosition(character);
                Assert.That(manifest.NavCells.Any(c => c.Contains(new double[] { otherFloor.x, otherFloor.y, otherFloor.z })), Is.False);

                character.enabled = false;
                Assert.That(TryGroundPosition(character, out _), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(player); }
        }

        private static Vector3 GroundPosition(CharacterController character)
        {
            Assert.That(TryGroundPosition(character, out var position), Is.True);
            return position;
        }

        private static bool TryGroundPosition(CharacterController character, out Vector3 position)
        {
            var authority = Type.GetType("EchoProtocol.Networking.Authority.MatchAuthorityRuntime, Assembly-CSharp", true);
            var method = authority.GetMethod("TryV02GroundPosition", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            var args = new object[] { character, Vector3.zero };
            var result = (bool)method.Invoke(null, args);
            position = (Vector3)args[1];
            return result;
        }

        [Test]
        public void AEDV02_MarkerIdentityIgnoresUnrelatedRootReordering()
        {
            var source = Type.GetType("EchoProtocol.AI.AED.AEDV02SceneSource, Assembly-CSharp", true);
            var markerId = source.GetMethod("MarkerId");
            var root = new GameObject("AEDV02 identity root");
            var child = new GameObject("Door");
            var unrelated = new GameObject("AEDV02 unrelated root");
            var duplicate = new GameObject(root.name);
            var duplicateChild = new GameObject(child.name);
            try
            {
                child.transform.SetParent(root.transform);
                duplicateChild.transform.SetParent(duplicate.transform);
                var expected = markerId.Invoke(null, new object[] { child.transform });
                unrelated.transform.SetAsFirstSibling();
                Assert.That(markerId.Invoke(null, new object[] { child.transform }), Is.EqualTo(expected));
                Assert.That(markerId.Invoke(null, new object[] { duplicateChild.transform }), Is.Not.EqualTo(expected));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(unrelated);
                UnityEngine.Object.DestroyImmediate(duplicate);
            }
        }

        [Test]
        public void AEDV02_SafeRoomRegistryFailsClosedAndRejectsCrossingRoute()
        {
            var manifest = new AEDV02SceneManifest
            {
                SceneVersion = "safe-room-contract",
                Markers = new[]
                {
                    new AEDV02SceneMarker { Id = AEDV02Plan.Hash("safe-room"), Kind = "SAFE_ROOM", Zone = 1,
                        InteractionBounds = new[] { -2d, -1d, -2d, 2d, 3d, 2d } }
                }
            };

            Assert.That(manifest.TryIsInsideSafeRoom(0, 0, 0, out var inside), Is.True);
            Assert.That(inside, Is.True);
            Assert.That(manifest.TryPathIntersectsSafeRoom(new[] { new[] { -4d, 0d, 0d }, new[] { 4d, 0d, 0d } }, out var crossing), Is.True);
            Assert.That(crossing, Is.True);

            manifest.Markers[0].InteractionBounds = null;
            Assert.That(manifest.TryIsInsideSafeRoom(0, 0, 0, out _), Is.False);
        }

        [Test]
        public void AEDV02_SceneEntriesAndZoneGatesMatchExportAndProductionReferences()
        {
            const string path = "Assets/Scenes/SciFi.unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var source = Type.GetType("EchoProtocol.AI.AED.AEDV02SceneSource, Assembly-CSharp", true);
                var args = new object[] { null, null };
                Assert.That(source.GetMethod("TryLoad").Invoke(null, args), Is.True);
                var manifest = (AEDV02SceneManifest)args[0];
                var markers = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<MonoBehaviour>(true))
                    .Where(x => x != null && x.GetType().FullName == "EchoProtocol.AI.AED.AEDZoneEntryMarker").ToArray();
                Assert.That(markers.Length, Is.EqualTo(3));
                foreach (var marker in markers)
                {
                    int zone = (int)marker.GetType().GetProperty("Zone").GetValue(marker);
                    var entry = manifest.Markers.Single(x => x.Id == "Z" + zone + "_ENTRY");
                    Assert.That(new Vector3((float)entry.X, (float)entry.Y, (float)entry.Z), Is.EqualTo(marker.transform.position));
                    Assert.That(manifest.NavCells.Any(x => x.Contains(new[] { entry.X, entry.Y, entry.Z })), Is.True, "Entry outside real mesh: " + zone);
                }
                var gates = (Transform[])source.GetMethod("Zone2ExitGates").Invoke(null, new object[] { scene });
                Assert.That(gates.Length, Is.EqualTo(2));
                var directorType = Type.GetType("EchoProtocol.MatchFlow.Zone2MissionDirector, Assembly-CSharp", true);
                var director = directorType.GetProperty("Instance").GetValue(null);
                Assert.That(director, Is.Not.Null);
                // EditMode does not run the scene director's Awake initialization.
                directorType.GetMethod("ResolveReferences").Invoke(director, null);
                for (int i = 0; i < gates.Length; i++)
                {
                    Assert.That(directorType.GetProperty("DoorBlocker" + (i + 1)).GetValue(director), Is.EqualTo(gates[i].gameObject));
                    string id = (string)source.GetMethod("MarkerId").Invoke(null, new object[] { gates[i] });
                    Assert.That(manifest.DoorIds.Contains(id), Is.True, "Gate source absent: " + id);
                }
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
