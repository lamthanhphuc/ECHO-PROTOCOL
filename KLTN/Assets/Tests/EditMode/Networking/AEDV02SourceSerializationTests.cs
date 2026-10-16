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

        [Serializable] private sealed class ToolWorld { public Scan[] Scans; public Jammer[] Jammers; public Candidate[] Scannables; }
        [Serializable] private sealed class Scan { public string MatchId; public string UserId; public uint Sequence; public bool Presented; public int[] ResultIds; public Candidate[] Candidates; }
        [Serializable] private sealed class Candidate { public string Identity; public double Speed; }
        [Serializable] private sealed class Jammer { public string NetworkId, MatchId, PhaseName; public long DeploymentTick; public bool ColliderBlocking; }

        [TestCase("valid", true)]
        [TestCase("null-candidate", false)]
        [TestCase("nonfinite-speed", false)]
        [TestCase("missing-result-target", false)]
        [TestCase("nonblocking-jammer", false)]
        [TestCase("null-door", false)]
        [TestCase("earlier-phase-jammer", true)]
        public void AEDV02_ToolSourcesValidateAndSerializeActualObservation(string mutation, bool valid)
        {
            var user = Guid.NewGuid(); var match = Guid.NewGuid();
            var sample = new AEDV02WorldSample { ResolutionMode = "Adaptive", Difficulty = "Normal", Tick = 120, TickRate = 60,
                PhaseOrdinal = 1, PhaseName = "CORE_COLLECTION", LogicalPhase = 1, Zone = 1, PlanRevision = 1,
                PlanFingerprint = AEDV02Plan.Normal().Fingerprint(), RosterIdentity = AEDV02Plan.Hash("roster"), SceneManifestFingerprint = AEDV02Plan.Hash("scene"),
                Players = new[] { new AEDV02PlayerSource { UserId = user, LifeState = "ALIVE" } },
                Scans = new[] { new AEDV02ScanSource { MatchId = match, UserId = user, Sequence = 9, Tick = 60, PhaseName = "CORE_COLLECTION",
                    Occurrence = "player:scanner:tool:FIELD_SCANNER:9", Mode = "Core", Range = 100, MaxTargets = 1, Presented = true, PresentedTick = 120,
                    Candidates = new[] { new AEDV02ScanCandidate { Identity = "real-core", LocalId = 7, Kind = "Core", Available = true, Speed = 0 } }, ResultIds = new[] { 7 } } },
                Jammers = new[] { new AEDV02JammerSource { MatchId = match, PhaseName = "CORE_COLLECTION", NetworkId = "real-jammer", UserId = user, Occurrence = "player:jammer:tool:1",
                    DeploymentTick = 60, Active = true, ColliderBlocking = true, BreakSeconds = 3 } } };
            switch (mutation)
            {
                case "null-candidate": sample.Scans[0].Candidates[0] = null; break;
                case "nonfinite-speed": sample.Scans[0].Candidates[0].Speed = double.NaN; break;
                case "missing-result-target": sample.Scans[0].ResultIds[0] = 8; break;
                case "nonblocking-jammer": sample.Jammers[0].ColliderBlocking = false; break;
                case "null-door": sample.Doors = new AEDV02DoorSource[] { null }; break;
                case "earlier-phase-jammer":
                    sample.PhaseName = sample.Scans[0].PhaseName = "ZONE_2_OBJECTIVE";
                    sample.LogicalPhase = sample.Zone = 2; break;
            }
            sample.Scannables = sample.Scans[0].Candidates;
            Assert.That(sample.IsValid(), Is.EqualTo(valid));
            if (!valid) return;
            var writer = Type.GetType("EchoProtocol.AI.AED.AEDV02SourceJson, Assembly-CSharp", true);
            var parsed = JsonUtility.FromJson<ToolWorld>((string)writer.GetMethod("World").Invoke(null, new object[] { sample }));
            Assert.That(parsed.Scans[0].MatchId, Is.EqualTo(match.ToString("D")));
            Assert.That(parsed.Scans[0].Sequence, Is.EqualTo(9)); Assert.That(parsed.Scans[0].Presented, Is.True);
            Assert.That(parsed.Scans[0].Candidates[0].Identity, Is.EqualTo("real-core"));
            Assert.That(parsed.Scannables[0].Identity, Is.EqualTo("real-core"));
            Assert.That(parsed.Scans[0].ResultIds, Is.EqualTo(new[] { 7 }));
            Assert.That(parsed.Jammers[0].NetworkId, Is.EqualTo("real-jammer"));
            Assert.That(parsed.Jammers[0].MatchId, Is.EqualTo(match.ToString("D")));
            Assert.That(parsed.Jammers[0].PhaseName, Is.EqualTo(sample.Jammers[0].PhaseName));
            Assert.That(parsed.Jammers[0].DeploymentTick, Is.EqualTo(60)); Assert.That(parsed.Jammers[0].ColliderBlocking, Is.True);
        }
    }
}
