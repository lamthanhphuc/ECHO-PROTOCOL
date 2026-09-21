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
        private static Type Resolve(string fullName) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(fullName)).First(t => t != null);
        private object Read(string property) => _managerType.GetProperty(property).GetValue(_manager);

        [UnitySetUp]
        public IEnumerator Setup()
        {
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

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (_manager != null) UnityEngine.Object.Destroy(_manager.gameObject);
            yield return null;
            Assert.That(_managerType.GetProperty("Instance").GetValue(null), Is.Null);
        }
    }
}
