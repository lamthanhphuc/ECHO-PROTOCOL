using System.Reflection;
using EchoProtocol.Audio;
using EchoProtocol.Networking;
using NUnit.Framework;
using UnityEngine;

public sealed class FloodedMissionParityTests
{
    [TestCase(0, 0f)]
    [TestCase(1, 6.3f)]
    [TestCase(2, 9f)]
    [TestCase(4, 9f)]
    public void EscortDefaultsUseTheSameSoloAndCoopSpeeds(int players, float expected)
        => Assert.That(Zone3ConvoyController.EscortTargetSpeed(players, 1.8f, .7f, false), Is.EqualTo(expected).Within(.001f));

    [Test]
    public void EscortAcceleratesAndDeceleratesRatherThanJumpingToTarget()
    {
        float speed = Zone3ConvoyController.AdvanceEscortSpeed(0, 2, 1.8f, .7f, 2.2f, 3.5f, false, .1f);
        Assert.That(speed, Is.EqualTo(1.1f).Within(.001f));
        Assert.That(Zone3ConvoyController.AdvanceEscortSpeed(9, 0, 1.8f, .7f, 2.2f, 3.5f, false, .1f), Is.EqualTo(7.25f).Within(.001f));
        Assert.That(Zone3ConvoyController.AdvanceEscortSpeed(1, 2, 1.8f, .7f, 2.2f, 3.5f, false, 0), Is.EqualTo(1));
    }

    [Test]
    public void ChargingPhaseRespectsTheDifferentOrderOfTheTwoMaps()
    {
        Assert.That(NetworkMatchStateRules.IsChargePhase(NetworkMatchPhase.Zone3FindFrigate, true), Is.True);
        Assert.That(NetworkMatchStateRules.IsChargePhase(NetworkMatchPhase.Zone3PushFrigate, true), Is.False);
        Assert.That(NetworkMatchStateRules.IsChargePhase(NetworkMatchPhase.Zone3PushFrigate, false), Is.True);
        Assert.That(NetworkMatchStateRules.IsChargePhase(NetworkMatchPhase.Zone3FindFrigate, false), Is.False);
        Assert.That(NetworkMatchStateRules.IsChargePhase(NetworkMatchPhase.MatchEnded, true), Is.False);
    }

    [Test]
    public void FloodedChargeRegistersTheSharedAudioEmitterExactlyOnce()
    {
        var owner = new GameObject("Flooded charge audio test", typeof(BoxCollider), typeof(FloodedChargeInteraction));
        var charge = owner.GetComponent<FloodedChargeInteraction>();
        var awake = typeof(FloodedChargeInteraction).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
        try
        {
            awake.Invoke(charge, null); awake.Invoke(charge, null);
            Assert.That(owner.GetComponents<GameAudioEmitter>().Length, Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(owner); }
    }
}
