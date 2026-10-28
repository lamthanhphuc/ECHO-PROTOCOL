using System;
using EchoProtocol.Networking;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class AlienHuntAudioTests
{
    // Catches replayed snapshot cues, chewing after capture, and drop/stun double cues.
    [Test]
    public void ConfirmedStagesStopChewingAndDoNotReplayInteractionOrBurnCues()
    {
        var type = Type.GetType("EchoProtocol.Networking.AlienHuntAudio, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, "Alien Non needs its own state-driven audio presentation.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AlienHunt/Prefabs/PF_AlienYoung.prefab");
        var owner = Object.Instantiate(prefab);
        owner.hideFlags = HideFlags.HideAndDontSave;
        var camera = Camera.main;
        GameObject cameraOwner = null;
        if (camera == null)
        {
            cameraOwner = new GameObject("Alien audio test listener", typeof(Camera));
            cameraOwner.tag = "MainCamera";
            camera = cameraOwner.GetComponent<Camera>();
        }
        owner.transform.position = camera.transform.position;
        try
        {
            var animator = owner.GetComponentInChildren<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var audio = owner.AddComponent(type);
            type.GetMethod("Initialize").Invoke(audio, new object[] { animator });
            var present = type.GetMethod("Present");
            Action<AlienStage, float> stage = (value, delta) => present.Invoke(audio,
                new object[] { new HuntEntityState { Stage = (int)value }, delta });
            var voice = owner.transform.Find("Alien Voice").GetComponent<AudioSource>();
            var eating = owner.transform.Find("Alien Eating").GetComponent<AudioSource>();
            var contact = owner.transform.Find("Alien Contact").GetComponent<AudioSource>();

            stage(AlienStage.Carried, .02f);
            Assert.That(contact.clip, Is.Null, "Joining while carried must not replay pickup.");
            stage(AlienStage.Eating, .02f);
            animator.Play("Eating", 0, .07f); animator.Update(0f);
            stage(AlienStage.Eating, .02f);
            animator.Play("Eating", 0, .10f); animator.Update(0f);
            stage(AlienStage.Eating, .15f);
            Assert.That(eating.clip, Is.Not.Null, "The eating animation's first bite must produce a chew.");
            Assert.That(eating.clip.name, Does.StartWith("chew_"));

            stage(AlienStage.Carried, .02f);
            Assert.That(eating.clip, Is.Null, "Capture must cancel the current chew immediately.");
            Assert.That(contact.clip.name, Is.EqualTo("pickup"));
            contact.Stop(); contact.clip = null;
            stage(AlienStage.Carried, .02f);
            Assert.That(contact.clip, Is.Null, "A repeated snapshot must not replay pickup.");
            stage(AlienStage.Stunned, .02f);
            Assert.That(contact.clip.name, Is.EqualTo("drop"));
            Assert.That(voice.clip, Is.Null, "Dropping must not also trigger a separate stun voice.");

            stage(AlienStage.OnPan, .02f);
            stage(AlienStage.Burning, .02f);
            Assert.That(voice.clip.name, Is.EqualTo("burn_start"));
            float initialVolume = voice.volume;
            stage(AlienStage.Burning, .8f);
            Assert.That(voice.volume, Is.LessThan(initialVolume * .65f), "The ignition cry must fade with the struggle.");
            contact.Stop(); contact.clip = null;
            stage(AlienStage.Burned, .02f);
            Assert.That(voice.clip, Is.Null);
            Assert.That(contact.clip, Is.Null, "The furnace owns completion; the Alien must not duplicate it.");
            contact.Stop(); contact.clip = null;
            stage(AlienStage.Burned, .02f);
            Assert.That(contact.clip, Is.Null, "Completion must sound exactly once.");

            var silence = type.GetMethod("Silence");
            silence.Invoke(audio, null);
            stage(AlienStage.Burning, .02f);
            Assert.That(voice.clip, Is.Null, "Joining during a burn must not replay the ignition cry.");
            silence.Invoke(audio, null);
            stage(AlienStage.Wandering, .02f);
            stage(AlienStage.Running, .02f);
            Assert.That(voice.clip.name, Is.EqualTo("bait_detect"));
            voice.Stop(); voice.clip = null;
            stage(AlienStage.Running, .02f);
            Assert.That(voice.clip, Is.Null, "Running snapshots must not repeat the bait call.");
            stage(AlienStage.Wandering, .02f);
            stage(AlienStage.Running, .02f);
            Assert.That(voice.clip, Is.Null, "Rapid bait reacquisition must respect its cooldown.");

            silence.Invoke(audio, null);
            stage(AlienStage.Wandering, .02f);
            stage(AlienStage.Wandering, 20f);
            var budget = Object.FindAnyObjectByType<EchoProtocol.Audio.PurgeVoiceBudget>();
            typeof(EchoProtocol.Audio.PurgeVoiceBudget).GetMethod("Flush", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(budget, new object[] { Time.unscaledTime });
            Assert.That(voice.clip.name, Does.StartWith("idle_"));
            silence.Invoke(audio, null);
            owner.transform.position += Vector3.right * 100f;
            stage(AlienStage.Wandering, .02f);
            stage(AlienStage.Running, 4f);
            Assert.That(voice.clip, Is.Null, "Alien calls must not reveal a distant nest.");
            Assert.That(voice.outputAudioMixerGroup, Is.Not.Null, "Alien audio must follow the SFX settings.");
            type.GetMethod("Initialize").Invoke(audio, new object[] { animator });
            Assert.That(owner.GetComponentsInChildren<AudioSource>().Length, Is.EqualTo(3),
                "Repeated initialization must not create duplicate audio sources.");
        }
        finally
        {
            Object.DestroyImmediate(owner);
            var budget = Object.FindAnyObjectByType<EchoProtocol.Audio.PurgeVoiceBudget>();
            if (budget != null) Object.DestroyImmediate(budget.gameObject);
            if (cameraOwner != null) Object.DestroyImmediate(cameraOwner);
        }
    }
}
