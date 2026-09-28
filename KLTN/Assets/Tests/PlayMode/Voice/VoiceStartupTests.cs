using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EchoProtocol.Voice.Tests
{
    public sealed class VoiceStartupTests
    {
        private Type _managerType;
        private Component _manager;
        private bool _hadMutedPreference;
        private int _previousMutedPreference;
        private static Type Resolve(string fullName) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(fullName)).First(t => t != null);
        private object Read(string property) => _managerType.GetProperty(property).GetValue(_manager);

        [UnitySetUp]
        public IEnumerator Setup()
        {
            _hadMutedPreference = PlayerPrefs.HasKey("Echo.Voice.Muted");
            _previousMutedPreference = PlayerPrefs.GetInt("Echo.Voice.Muted", 0);
            _managerType = Resolve("EchoProtocol.Voice.VoiceManager");
            _managerType.GetMethod("EnsureExists").Invoke(null, null);
            _manager = (Component)_managerType.GetProperty("Instance").GetValue(null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartupIsSingletonWithMicrophoneOffAndDedicatedMixer()
        {
            _managerType.GetMethod("EnsureExists").Invoke(null, null);
            yield return null;
            Assert.That(_managerType.GetProperty("Instance").GetValue(null), Is.SameAs(_manager));
            Assert.That(UnityEngine.Object.FindObjectsByType(_managerType, FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Read("MicrophoneEnabled"), Is.False);
            Assert.That(Read("Joined"), Is.False);
            Assert.That(Read("Speaking"), Is.False);
            Assert.That(Read("OutputMixer"), Is.Not.Null);
            Assert.That(_manager.GetComponentInChildren<Canvas>(true), Is.Not.Null);
            Assert.That(_manager.GetComponentInChildren<Canvas>(true).name, Is.EqualTo("VoiceSettingsCanvas"));
        }

        [UnityTest]
        public IEnumerator EnablingMicWithoutSessionCannotStartRecordingOrTransmit()
        {
            _managerType.GetMethod("EnableMicrophone").Invoke(_manager, new object[] { true });
            yield return null;
            var recorderType = Resolve("Photon.Voice.Unity.Recorder");
            var recorder = _manager.GetComponent(recorderType);
            Assert.That(recorderType.GetProperty("RecordingEnabled").GetValue(recorder), Is.False);
            Assert.That(recorderType.GetProperty("TransmitEnabled").GetValue(recorder), Is.False);
            _managerType.GetMethod("Retry").Invoke(_manager, null);
            yield return null;
            Assert.That(Read("Joined"), Is.False);
            Assert.That(Read("Speaking"), Is.False);
        }

        [UnityTest]
        public IEnumerator ToggleKeyStatePersistsUntilNextToggleWithoutTransmittingOutsideSession()
        {
            _managerType.GetMethod("SetMuted").Invoke(_manager, new object[] { true });
            _managerType.GetMethod("ToggleMicrophone").Invoke(_manager, null);
            Assert.That(Read("MicrophoneEnabled"), Is.True);
            Assert.That(Read("SelfMuted"), Is.False);
            yield return null;
            Assert.That(Read("MicrophoneEnabled"), Is.True);
            _managerType.GetMethod("ToggleMicrophone").Invoke(_manager, null);
            yield return null;
            Assert.That(Read("MicrophoneEnabled"), Is.False);
            var recorderType = Resolve("Photon.Voice.Unity.Recorder");
            var recorder = _manager.GetComponent(recorderType);
            Assert.That(recorderType.GetProperty("RecordingEnabled").GetValue(recorder), Is.False);
            Assert.That(recorderType.GetProperty("TransmitEnabled").GetValue(recorder), Is.False);
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (_hadMutedPreference) PlayerPrefs.SetInt("Echo.Voice.Muted", _previousMutedPreference);
            else PlayerPrefs.DeleteKey("Echo.Voice.Muted");
            if (_manager != null) UnityEngine.Object.Destroy(_manager.gameObject);
            yield return null;
            Assert.That(_managerType.GetProperty("Instance").GetValue(null), Is.Null);
        }
    }
}
