using EchoProtocol.Networking;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CoreStabilizerRulesTests
{
    [Test]
    public void StabilizedCoreCarrier_CanSprintAtNormalSpeed()
    {
        Assert.That(CoreStabilizerRules.SupportRadius, Is.EqualTo(10f));
        Assert.That(CoreStabilizerRules.DurationSeconds, Is.EqualTo(15f));
        Assert.That(CoreStabilizerRules.CooldownSeconds, Is.EqualTo(45f));
        Assert.That(CoreStabilizerRules.AllowsSprint(true), Is.False);
        Assert.That(CoreStabilizerRules.AllowsSprint(false), Is.True);
        Assert.That(CoreStabilizerRules.AllowsCrouch(false, true), Is.False);
    }

    [Test]
    public void CoreStabilizerVfx_WaitsForActivation_AndShowsTenMeterRadius()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Environment/Teamtoools/Animated/PF_CoreStabilizer_Device_Animated.prefab");
        ParticleSystem status = prefab.transform.Find("Visual/DeviceVFXOrigin/FieldStatusParticles")
            .GetComponent<ParticleSystem>();
        Transform field = prefab.transform.Find("SupportFieldVFX");
        LineRenderer ring = field.GetComponentInChildren<LineRenderer>(true);
        Transform mesh = prefab.transform.Find(
            "Visual/GripPivot/DeviceModel/RandomSciFiDevice/default");

        Assert.That(status.main.playOnAwake, Is.False);
        Assert.That(status.transform.localPosition, Is.EqualTo(new Vector3(0f, 0.416f, 0f)));
        Assert.That(ring.GetPosition(0).magnitude, Is.EqualTo(10f).Within(0.001f));
        Assert.That(ring.transform.localPosition, Is.EqualTo(new Vector3(0.165f, -0.46f, -0.78f)));
        Assert.That(field.GetComponentInChildren<ParticleSystem>(true), Is.Null);
        Assert.That(mesh.localPosition, Is.EqualTo(new Vector3(7.7f, -10.7f, -27f)));
        Assert.That(mesh.localScale, Is.EqualTo(Vector3.one * 3.7f));
        Assert.That(
            Quaternion.Angle(
                mesh.localRotation,
                Quaternion.Euler(90.43701f, -89.996f, -177.121f)),
            Is.LessThan(0.01f));
    }
}
