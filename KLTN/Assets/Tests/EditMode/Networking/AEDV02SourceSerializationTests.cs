using System;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.Networking.Tests
{
    public sealed class AEDV02SourceSerializationTests
    {
        [Serializable] private sealed class WorldPlayers { public Player[] Players; }
        [Serializable] private sealed class Player { public string UserId; public string HideSpotId; }

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
            var writer = Type.GetType("EchoProtocol.AI.AED.AEDV02SourceJson, Assembly-CSharp", true);
            var json = (string)writer.GetMethod("World").Invoke(null, new object[] { sample });
            var parsed = JsonUtility.FromJson<WorldPlayers>(json);
            Assert.That(parsed.Players[0].HideSpotId, Is.EqualTo(ulong.MaxValue.ToString()));
            Assert.That(parsed.Players[0].UserId, Is.EqualTo(user.ToString("D")));
            Assert.That(json, Does.Contain("room\\\"\\\\door"));
        }
    }
}
