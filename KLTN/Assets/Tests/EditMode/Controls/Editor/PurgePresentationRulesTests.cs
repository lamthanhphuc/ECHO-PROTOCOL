using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class PurgePresentationRulesTests
{
    private static Type Required(string name)
    {
        var type = Type.GetType("EchoProtocol.Audio." + name + ", Assembly-CSharp");
        Assert.That(type, Is.Not.Null, name + " is needed for local multiplayer presentation.");
        return type;
    }

    [Test]
    public void VoiceBudgetPrefersNearbyThreatsAndDistantCallsCannotConsumeSlots()
    {
        var type = Required("PurgeVoiceBudget");
        var kind = Required("PurgeVoiceKind");
        var owner = new GameObject("Voice budget test");
        var clip = AudioClip.Create("budget test", 22050, 1, 44100, false);
        var camera = Camera.main;
        GameObject testCamera = null;
        if (camera == null) { testCamera = new GameObject("Budget camera", typeof(Camera)); testCamera.tag = "MainCamera"; camera = testCamera.GetComponent<Camera>(); }
        Func<float, AudioSource> source = distance =>
        {
            var child = new GameObject("Budget source"); child.transform.SetParent(owner.transform);
            child.transform.position = camera.transform.position + Vector3.right * distance;
            var audio = child.AddComponent<AudioSource>(); audio.maxDistance = 34f; return audio;
        };
        try
        {
            var far = source(100f); var near = source(3f); var threat = source(8f);
            var request = type.GetMethod("Request");
            Action<AudioSource, string, bool> submit = (audio, category, local) => request.Invoke(null,
                new object[] { audio, clip, Enum.Parse(kind, category), .5f, local });
            submit(far, "FishcatAlert", false);
            submit(near, "FishcatAlert", false);
            var manager = Object.FindAnyObjectByType(type);
            var flush = type.GetMethod("Flush", BindingFlags.NonPublic | BindingFlags.Instance);
            float now = Time.unscaledTime;
            flush.Invoke(manager, new object[] { now });
            Assert.That(near.clip, Is.SameAs(clip), "An inaudible call must not take the alert slot.");
            Assert.That(far.clip, Is.Null);
            submit(threat, "FishcatAlert", true);
            flush.Invoke(manager, new object[] { now + .1f });
            Assert.That(threat.clip, Is.Null, "Only one alert can occupy the shared interval.");
            submit(near, "FishcatAlert", false); submit(threat, "FishcatAlert", true);
            near.clip = null;
            flush.Invoke(manager, new object[] { now + 2f });
            Assert.That(threat.clip, Is.SameAs(clip), "A direct local threat has priority among eligible alerts.");
            submit(threat, "FishcatIdle", false); submit(near, "FishcatIdle", false);
            threat.clip = null;
            flush.Invoke(manager, new object[] { now + 3f });
            Assert.That(near.clip, Is.SameAs(clip), "The nearest eligible idle wins.");
            Assert.That(threat.clip, Is.Null);
            submit(near, "AlienIdle", false);
            flush.Invoke(manager, new object[] { now - 4f });
            submit(near, "AlienIdle", false);
            near.clip = null;
            flush.Invoke(manager, new object[] { Time.unscaledTime });
            Assert.That(near.clip, Is.SameAs(clip), "A completed slot must allow the same fallback clip again later.");
        }
        finally
        {
            var manager = Object.FindAnyObjectByType(type) as Component;
            if (manager != null) Object.DestroyImmediate(manager.gameObject);
            Object.DestroyImmediate(owner); Object.DestroyImmediate(clip);
            if (testCamera != null) Object.DestroyImmediate(testCamera);
        }
    }

    [Test]
    public void SimultaneousFishcatAttacksProduceOneNearbyCry()
    {
        var type = Required("PurgeVoiceBudget");
        var kind = Enum.Parse(Required("PurgeVoiceKind"), "FishcatAttack");
        var owner = new GameObject("Attack voice test");
        var clip = AudioClip.Create("attack cry", 22050, 1, 44100, false);
        var camera = Camera.main;
        GameObject testCamera = null;
        if (camera == null) { testCamera = new GameObject("Attack listener", typeof(Camera)); testCamera.tag = "MainCamera"; camera = testCamera.GetComponent<Camera>(); }
        try
        {
            var near = new GameObject("Near attack").AddComponent<AudioSource>();
            var far = new GameObject("Far attack").AddComponent<AudioSource>();
            near.transform.SetParent(owner.transform); far.transform.SetParent(owner.transform);
            near.transform.position = camera.transform.position + Vector3.right * 3f;
            far.transform.position = camera.transform.position + Vector3.right * 20f;
            near.maxDistance = far.maxDistance = 34f;
            var request = type.GetMethod("Request");
            request.Invoke(null, new object[] { far, clip, kind, .36f, false });
            request.Invoke(null, new object[] { near, clip, kind, .36f, false });
            var manager = Object.FindAnyObjectByType(type);
            type.GetMethod("Flush", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(manager, new object[] { Time.unscaledTime });
            Assert.That(near.clip, Is.SameAs(clip));
            Assert.That(far.clip, Is.Null);
        }
        finally
        {
            var manager = Object.FindAnyObjectByType(type) as Component;
            if (manager != null) Object.DestroyImmediate(manager.gameObject);
            Object.DestroyImmediate(owner); Object.DestroyImmediate(clip);
            if (testCamera != null) Object.DestroyImmediate(testCamera);
        }
    }

    [Test]
    public void OcclusionRespectsWallsButIgnoresEmitterGeometryAndTriggers()
    {
        var type = Required("PurgeSpatialAudio");
        var owner = new GameObject("Occlusion emitter"); owner.transform.position = new Vector3(0, 4000, 4);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = new Vector3(0, 4000, 2);
        try
        {
            var audio = owner.AddComponent<AudioSource>(); audio.spatialBlend = 1f;
            var component = type.GetMethod("Attach").Invoke(null, new object[] { audio, owner.transform });
            var probe = type.GetMethod("IsBlocked", BindingFlags.NonPublic | BindingFlags.Instance);
            var listener = new Vector3(0, 4000, 0);
            Physics.SyncTransforms();
            Assert.That(probe.Invoke(component, new object[] { listener }), Is.EqualTo(true));
            wall.GetComponent<Collider>().isTrigger = true; Physics.SyncTransforms();
            Assert.That(probe.Invoke(component, new object[] { listener }), Is.EqualTo(false));
            wall.GetComponent<Collider>().isTrigger = false;
            wall.transform.SetParent(owner.transform, true); Physics.SyncTransforms();
            Assert.That(probe.Invoke(component, new object[] { listener }), Is.EqualTo(false), "The creature/furnace must not muffle itself.");
        }
        finally { Object.DestroyImmediate(wall); Object.DestroyImmediate(owner); }
    }

    [Test]
    public void DistantParticleLoopsStopAndRestoreOnlyTheirCurrentState()
    {
        var type = Required("PurgeVfx");
        var owner = new GameObject("VFX distance test");
        var camera = Camera.main;
        GameObject testCamera = null;
        if (camera == null) { testCamera = new GameObject("VFX camera", typeof(Camera)); testCamera.tag = "MainCamera"; camera = testCamera.GetComponent<Camera>(); }
        try
        {
            var particles = owner.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var method = type.GetMethod("SetLoop");
            owner.transform.position = camera.transform.position + Vector3.right;
            method.Invoke(null, new object[] { particles, true, 25f, 8f });
            Assert.That(particles.isEmitting, Is.True);
            particles.Emit(3);
            owner.transform.position = camera.transform.position + Vector3.right * 100f;
            method.Invoke(null, new object[] { particles, true, 25f, 8f });
            Assert.That(particles.isPlaying, Is.False);
            Assert.That(particles.particleCount, Is.Zero);
            owner.transform.position = camera.transform.position + Vector3.right;
            method.Invoke(null, new object[] { particles, true, 25f, 8f });
            Assert.That(particles.isEmitting, Is.True);
            method.Invoke(null, new object[] { particles, false, 25f, 8f });
            Assert.That(particles.isEmitting, Is.False);
        }
        finally { Object.DestroyImmediate(owner); if (testCamera != null) Object.DestroyImmediate(testCamera); }
    }
}
