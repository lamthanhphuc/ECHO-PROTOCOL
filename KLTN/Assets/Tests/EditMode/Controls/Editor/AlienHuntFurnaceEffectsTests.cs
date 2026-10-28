using System;
using EchoProtocol.Networking;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class AlienHuntFurnaceEffectsTests
{
    [Test]
    public void FurnaceCuesFollowConfirmedChangesAndBurnEndsWithAFade()
    {
        var owner = new GameObject("Furnace feedback test");
        owner.hideFlags = HideFlags.HideAndDontSave;
        var bowl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bowl.name = "LP_Cotl";
        bowl.transform.SetParent(owner.transform, false);
        var camera = Camera.main;
        GameObject testCamera = null;
        if (camera == null)
        {
            testCamera = new GameObject("Furnace test camera", typeof(Camera));
            testCamera.tag = "MainCamera";
            camera = testCamera.GetComponent<Camera>();
        }
        owner.transform.position = camera.transform.position;
        try
        {
            var effects = owner.AddComponent<AlienHuntFurnaceEffects>();
            effects.Initialize(AssetDatabase.LoadAssetAtPath<Material>("Assets/AlienHunt/Materials/AlienHuntFlame.mat"));
            var actionObject = owner.transform.Find("Furnace Action");
            Assert.That(actionObject, Is.Not.Null, "Fuel and placement need confirmed furnace feedback.");
            var action = actionObject.GetComponent<AudioSource>();
            var phase = owner.transform.Find("Furnace Phase").GetComponent<AudioSource>();
            var fire = owner.transform.Find("PanFlames").GetComponent<AudioSource>();
            var flames = owner.transform.Find("PanFlames").GetComponent<ParticleSystem>();
            var light = owner.GetComponentInChildren<Light>();
            var presentMethod = typeof(AlienHuntFurnaceEffects).GetMethod("Present", new[] { typeof(HuntFurnaceState), typeof(float) });
            Action<HuntFurnaceState, float> present = (s, delta) => presentMethod.Invoke(effects, new object[] { s, delta });

            var state = new HuntFurnaceState();
            present(state, .02f);
            Assert.That(action.clip, Is.Null);
            state.Fuel = 1;
            present(state, .02f);
            Assert.That(action.clip.name, Is.EqualTo("fuel_pour"));
            Assert.That(effects.OilVisible, Is.True);
            action.clip = null;
            present(state, .02f);
            Assert.That(action.clip, Is.Null, "Repeated fuel snapshots must not replay pouring.");
            state.AlienSlot = 1;
            present(state, .02f);
            Assert.That(action.clip.name, Is.EqualTo("pan_place"));

            state.BurnRemaining = 6f;
            present(state, .02f);
            Assert.That(phase.clip.name, Is.EqualTo("ignition"));
            Assert.That(light.intensity, Is.GreaterThan(1.8f));
            Assert.That(flames.isEmitting, Is.True);
            Assert.That(owner.transform.Find("PanSmoke").GetComponent<ParticleSystem>().isEmitting, Is.True);
            Assert.That(fire.outputAudioMixerGroup, Is.Not.Null, "Fire loop must obey SFX settings.");
            Assert.That(fire.clip.name, Is.EqualTo("fire_loop"));
            phase.clip = null;
            state.BurnRemaining = 5f;
            present(state, .2f);
            Assert.That(phase.clip, Is.Null, "An ongoing burn must not restart ignition.");

            state = new HuntFurnaceState { BurnedCount = 1 };
            present(state, .02f);
            Assert.That(phase.clip.name, Is.EqualTo("burn_complete"));
            Assert.That(flames.isEmitting, Is.False, "Completion lets existing flames expire without emitting new ones.");
            float initialVolume = fire.volume;
            Assert.That(initialVolume, Is.GreaterThan(0f), "Completion must fade the fire rather than cut it.");
            present(state, .4f);
            Assert.That(fire.volume, Is.LessThan(initialVolume));
            present(state, .5f);
            Assert.That(fire.volume, Is.Zero);
            Assert.That(light.enabled, Is.False);
            phase.clip = null;
            present(state, .02f);
            Assert.That(phase.clip, Is.Null, "The furnace owns exactly one completion cue.");

            typeof(AlienHuntFurnaceEffects).GetMethod("ResetPresentation").Invoke(effects, null);
            present(new HuntFurnaceState { Fuel = 1, AlienSlot = 2, BurnRemaining = 4f }, .02f);
            Assert.That(phase.clip, Is.Null, "Joining mid-burn must restore the loop without replaying ignition.");
            Assert.That(flames.isEmitting, Is.True);
            typeof(AlienHuntFurnaceEffects).GetMethod("ResetPresentation").Invoke(effects, null);
            present(new HuntFurnaceState { BurnedCount = 2 }, .02f);
            Assert.That(phase.clip, Is.Null, "Joining after burns must not replay historical completions.");
        }
        finally
        {
            Object.DestroyImmediate(owner);
            if (testCamera != null) Object.DestroyImmediate(testCamera);
        }
    }
}
