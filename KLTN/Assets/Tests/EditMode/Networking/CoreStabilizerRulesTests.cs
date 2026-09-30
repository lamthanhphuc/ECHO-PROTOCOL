using EchoProtocol.Networking;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CoreStabilizerRulesTests
{
    [Test]
    public void StabilizedCoreCarrier_CanSprintAtNormalSpeed()
    {
        Assert.That(CoreStabilizerRules.SupportRadius, Is.EqualTo(5f));
        Assert.That(CoreStabilizerRules.DurationSeconds, Is.EqualTo(15f));
        Assert.That(CoreStabilizerRules.CooldownSeconds, Is.EqualTo(45f));
        Assert.That(CoreStabilizerRules.AllowsSprint(true, false), Is.False);
        Assert.That(CoreStabilizerRules.AllowsSprint(true, true), Is.True);
        Assert.That(CoreStabilizerRules.AllowsCrouch(false, true), Is.False);
    }

    [Test]
    public void CoreStabilizerVfx_WaitsForActivation_AndShowsFiveMeterRadius()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Environment/Teamtoools/Animated/PF_CoreStabilizer_Device_Animated.prefab");
        ParticleSystem status = prefab.transform.Find("Visual/DeviceVFXOrigin/FieldStatusParticles")
            .GetComponent<ParticleSystem>();
        Transform field = prefab.transform.Find("SupportFieldVFX");
        LineRenderer ring = field.GetComponentInChildren<LineRenderer>(true);

        Assert.That(status.main.playOnAwake, Is.False);
        Assert.That(status.transform.localPosition, Is.EqualTo(new Vector3(0f, 0.416f, 0f)));
        Assert.That(ring.GetPosition(0).magnitude, Is.EqualTo(5f).Within(0.001f));
        Assert.That(ring.transform.localPosition, Is.EqualTo(new Vector3(0.165f, -0.46f, -0.78f)));
        Assert.That(field.GetComponentInChildren<ParticleSystem>(true), Is.Null);
    }
}
