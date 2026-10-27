using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using NUnit.Framework;

namespace EchoProtocol.Player.Tests
{
    public sealed class VoiceChatRulesTests
    {
        private static Type Resolve(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("EchoProtocol.Voice." + name)).First(t => t != null);
        private static bool Capture(params object[] flags)
        {
            var method = Resolve("VoiceTransmissionRules").GetMethod("CanCapture");
            var arguments = new object[method.GetParameters().Length];
            Array.Copy(flags, arguments, flags.Length);
            for (int i = flags.Length; i < arguments.Length; i++) arguments[i] = Type.Missing;
            return (bool)method.Invoke(null, arguments);
        }

        [TestCase(0)] // room left
        [TestCase(1)] // user disabled mic
        [TestCase(2)] // muted
        [TestCase(3)] // device unplugged
        [TestCase(4)] // local test
        [TestCase(5)] // focus lost
        [TestCase(6)] // app paused
        [TestCase(7)] // settings/rebinding
        public void RevokingAnyCaptureConditionStopsTransmission(int changed)
        {
            object[] flags = { true, true, false, true, false, true, false, false };
            Assert.That(Capture(flags), Is.True);
            flags[changed] = !(bool)flags[changed];
            Assert.That(Capture(flags), Is.False);
        }

        [TestCase(false, false, true)]
        [TestCase(false, true, true)]
        [TestCase(true, false, false)]
        [TestCase(true, true, true)]
        public void PushToTalkRequiresHeldKeyOnlyWhenEnabled(bool pushToTalk, bool held, bool expected)
        {
            Assert.That(Capture(true, true, false, true, false, true, false, false, pushToTalk, held),
                Is.EqualTo(expected));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PushToTalkNeverEnablesDisabledMicrophone(bool held)
        {
            Assert.That(Capture(true, false, false, true, false, true, false, false, true, held), Is.False);
        }

        [Test]
        public void HeldPushToTalkDoesNotOverrideMute()
        {
            Assert.That(Capture(true, true, true, true, false, true, false, false, true, true), Is.False);
        }


        [TestCase(false)]
        [TestCase(true)]
        public void OpeningSettingsStopsRecorderInOpenMicAndPushToTalkModes(bool pushToTalk)
        {
            var owner = new GameObject("Voice settings capture test");
            owner.SetActive(false);
            var panelType = Resolve("VoiceSettingsPanel");
            var open = panelType.GetField("<IsOpen>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            var previousOpen = open.GetValue(null);
            try
            {
                var manager = owner.AddComponent(Resolve("VoiceManager"));
                var devices = owner.AddComponent(Resolve("VoiceDeviceController"));
                SetProperty(devices, "Selected", "Headset");
                SetProperty(devices, "Devices", new[] { "Headset" });
                var recorderType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("Photon.Voice.Unity.Recorder")).First(t => t != null);
                var recorder = owner.AddComponent(recorderType);
                SetProperty(manager, "Devices", devices);
                SetProperty(manager, "MicrophoneEnabled", true);
                SetProperty(manager, "PushToTalk", pushToTalk);
                SetField(manager, "_recorder", recorder);
                SetField(manager, "_focused", true);
                SetField(manager, "_pushToTalkHeld", true);
                recorderType.GetProperty("RecordingEnabled").SetValue(recorder, true);
                recorderType.GetProperty("TransmitEnabled").SetValue(recorder, true);
                SetField(manager, "_captureStarted", Time.unscaledTime);
                open.SetValue(null, true);

                manager.GetType().GetMethod("UpdateCapture", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(manager, new object[] { true });

                Assert.That(recorderType.GetProperty("RecordingEnabled").GetValue(recorder), Is.False);
                Assert.That(recorderType.GetProperty("TransmitEnabled").GetValue(recorder), Is.False);
                Assert.That(manager.GetType().GetProperty("MicrophoneEnabled").GetValue(manager), Is.True,
                    "Opening settings must not change the saved microphone preference.");
            }
            finally
            {
                open.SetValue(null, previousOpen);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void MissingPreferredMicrophoneRemainsUnavailableUntilItReturnsOrUserSelectsAnother()
        {
            WithDevices(devices =>
            {
                PlayerPrefs.SetString("Echo.Voice.Device", "Headset");
                ApplyDevices(devices, "Headset", "Laptop");
                AssertDevice(devices, "Headset", true);

                ApplyDevices(devices, "Laptop");
                AssertDevice(devices, "Headset", false);
                StringAssert.Contains("Headset", (string)devices.GetType().GetProperty("Error").GetValue(devices));
                Assert.That(PlayerPrefs.GetString("Echo.Voice.Device"), Is.EqualTo("Headset"));
                ApplyDevices(devices, "Laptop");
                AssertDevice(devices, "Headset", false);

                ApplyDevices(devices, "Headset", "Laptop");
                AssertDevice(devices, "Headset", true);

                ApplyDevices(devices);
                AssertDevice(devices, "Headset", false);
                ApplyDevices(devices, "Laptop");
                AssertDevice(devices, "Headset", false);
                devices.GetType().GetMethod("Select").Invoke(devices, new object[] { "Laptop" });
                AssertDevice(devices, "Laptop", true);
                Assert.That(PlayerPrefs.GetString("Echo.Voice.Device"), Is.EqualTo("Laptop"));
            });
        }

        [Test]
        public void IntentionallySelectedSystemDefaultWaitsForAnyMicrophoneAndRecovers()
        {
            WithDevices(devices =>
            {
                PlayerPrefs.SetString("Echo.Voice.Device", "System default");
                ApplyDevices(devices, "Headset");
                AssertDevice(devices, "System default", true);
                Assert.That(devices.GetType().GetProperty("CaptureDevice").GetValue(devices), Is.Null);
                ApplyDevices(devices);
                AssertDevice(devices, "System default", false);
                ApplyDevices(devices, "Laptop");
                AssertDevice(devices, "System default", true);
            });
        }

        private static void WithDevices(Action<Component> check)
        {
            bool hadPreference = PlayerPrefs.HasKey("Echo.Voice.Device");
            string previous = PlayerPrefs.GetString("Echo.Voice.Device");
            var owner = new GameObject("Voice device availability test");
            owner.SetActive(false);
            try { check(owner.AddComponent(Resolve("VoiceDeviceController"))); }
            finally
            {
                if (hadPreference) PlayerPrefs.SetString("Echo.Voice.Device", previous);
                else PlayerPrefs.DeleteKey("Echo.Voice.Device");
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        private static void ApplyDevices(Component devices, params string[] available) =>
            devices.GetType().GetMethod("ApplyDevices", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(devices, new object[] { available });

        private static void AssertDevice(Component devices, string selected, bool ready)
        {
            Assert.That(devices.GetType().GetProperty("Selected").GetValue(devices), Is.EqualTo(selected));
            Assert.That(devices.GetType().GetProperty("Ready").GetValue(devices), Is.EqualTo(ready));
        }

        private static void SetProperty(Component target, string property, object value) =>
            SetField(target, "<" + property + ">k__BackingField", value);

        private static void SetField(Component target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        [Test]
        public void StreamIdentitySeparatesRoomsAndRespawnedObjects()
        {
            var method = Resolve("VoiceManager").GetMethod("MakeKey");
            string Key(string room, uint id) => (string)method.Invoke(null, new object[] { room, id });
            Assert.That(Key("room:a", 12), Is.Not.EqualTo(Key("room:a", 13)));
            Assert.That(Key("room:a", 12), Is.Not.EqualTo(Key("room:b", 12)));
            Assert.That(Key("room:a", 12), Is.EqualTo(Key("room:a", 12)));
        }
    }
}
