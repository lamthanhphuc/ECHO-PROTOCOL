using System;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.Networking.Tests
{
    public sealed class AEDV02SourceSerializationTests
    {
        [TestCase("body", true)]
        [TestCase("disabled-player", true)]
        [TestCase("trigger", false)]
        public void AEDV02_SupplyDoesNotSpawnInsideActualActorBodiesIncludingDisabledDownedCapsule(string kind, bool blocked)
        {
            var actor = new GameObject("AEDV02_SupplyActor");
            try
            {
                if (kind == "disabled-player")
                { var capsule = actor.AddComponent<CharacterController>(); capsule.center = Vector3.zero; capsule.enabled = false; }
                else { var box = actor.AddComponent<BoxCollider>(); box.isTrigger = kind == "trigger"; }
                Physics.SyncTransforms();
                var method = Type.GetType("EchoProtocol.TeamTools.TeamToolWorldSpawn, Assembly-CSharp", true)
                    .GetMethod("IsV02Occupied", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                Assert.That(method.Invoke(null, new object[] { Vector3.zero, new[] { actor.transform } }), Is.EqualTo(blocked));
                Assert.That(method.Invoke(null, new object[] { Vector3.one * 10, new[] { actor.transform } }), Is.EqualTo(false));
            }
            finally { UnityEngine.Object.DestroyImmediate(actor); }
        }
        [Serializable] private sealed class WorldPlayers { public Player[] Players; public Supply Supply; }
        [Serializable] private sealed class Supply { public int InitialOptionalBudget; public int OptionalBudget; public int OptionalMaterialized;
            public int[] SpawnedZones; public Reservation[] Reservations; }
        [Serializable] private sealed class Reservation { public int Zone; public int ToolId; public string MarkerId; }
        [Serializable] private sealed class Player { public string UserId; public string HideSpotId; }
        [Serializable] private sealed class Carrier { public string ToolInstanceId; public string MinionNetworkId; public string Action; public long Tick; }

        [Test]
        public void AEDV02_CustodySerializerPreservesActualToolMinionAndTick()
        {
            var source = new AEDV02ToolCarrierSource { ToolInstanceId = "tool:drop:1", MinionNetworkId = "minion:1", Action = "STEAL", Tick = 1900 };
            var writer = Type.GetType("EchoProtocol.AI.AED.AEDV02SourceJson, Assembly-CSharp", true);
            var json = (string)writer.GetMethod("Carrier").Invoke(null, new object[] { source });
            var parsed = JsonUtility.FromJson<Carrier>(json);
            Assert.That(parsed.ToolInstanceId, Is.EqualTo(source.ToolInstanceId)); Assert.That(parsed.MinionNetworkId, Is.EqualTo(source.MinionNetworkId));
            Assert.That(parsed.Action, Is.EqualTo("STEAL")); Assert.That(parsed.Tick, Is.EqualTo(1900));
            source.Action = "FAKE_RELEASE"; Assert.That(source.IsValid(), Is.False);
        }

        [Test]
        public void AEDV02_WorldSerializerPreservesUnsignedHideIdentityAndEscapesSourceStrings()
        {
            var user = Guid.NewGuid();
            var sample = new AEDV02WorldSample
            {
                ResolutionMode = "Adaptive", Difficulty = "Normal", Tick = 60, TickRate = 60, PhaseOrdinal = 1,
                PhaseName = "CORE_COLLECTION", LogicalPhase = 1, Zone = 1, PlanRevision = 1,
                PlanFingerprint = AEDV02Plan.Normal().Fingerprint(), RosterIdentity = AEDV02Plan.Hash("roster"),
                SceneManifestFingerprint = AEDV02Plan.Hash("scene"),
                Players = new[] { new AEDV02PlayerSource { UserId = user, LifeState = "ALIVE", HideSpotId = ulong.MaxValue.ToString() } },
                Doors = new[] { new AEDV02DoorSource { DoorId = "room\"\\door", State = "OPEN" } }
            };
            sample.Supply = new AEDV02SupplySource { InitialOptionalBudget = 2, OptionalBudget = 1, OptionalMaterialized = 1,
                SpawnedZones = new[] { 1 }, Reservations = new[] { new AEDV02SupplyReservation { Zone = 2, ToolId = 3, MarkerId = "real-marker" } } };
            var writer = Type.GetType("EchoProtocol.AI.AED.AEDV02SourceJson, Assembly-CSharp", true);
            var json = (string)writer.GetMethod("World").Invoke(null, new object[] { sample });
            var parsed = JsonUtility.FromJson<WorldPlayers>(json);
            Assert.That(parsed.Players[0].HideSpotId, Is.EqualTo(ulong.MaxValue.ToString()));
            Assert.That(parsed.Players[0].UserId, Is.EqualTo(user.ToString("D")));
            Assert.That(json, Does.Contain("room\\\"\\\\door"));
            Assert.That(parsed.Supply.InitialOptionalBudget, Is.EqualTo(2));
            Assert.That(parsed.Supply.OptionalBudget, Is.EqualTo(1)); Assert.That(parsed.Supply.OptionalMaterialized, Is.EqualTo(1));
            Assert.That(parsed.Supply.SpawnedZones, Is.EqualTo(new[] { 1 }));
            Assert.That(parsed.Supply.Reservations[0].ToolId, Is.EqualTo(3));
            Assert.That(parsed.Supply.Reservations[0].MarkerId, Is.EqualTo("real-marker"));
        }
    }
}
