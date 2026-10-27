using System.Collections.Generic;
using System.Reflection;
using EchoProtocol.Tools.Scanner;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.Tests.EditMode.Tools
{
    public sealed class FieldScannerAudioCadenceTests
    {
        private GameObject _owner;
        private FieldScannerAudio _audio;
        private readonly BindingFlags _flags = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            _owner = new GameObject("ScannerCadenceTest");
            _audio = _owner.AddComponent<FieldScannerAudio>();
            typeof(FieldScannerAudio).GetField("audioSource", _flags).SetValue(_audio, null);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (string name in new[] { "_chirpClip", "_beepClip" })
            {
                var clip = typeof(FieldScannerAudio).GetField(name, _flags).GetValue(_audio) as AudioClip;
                if (clip != null) Object.DestroyImmediate(clip);
            }
            Object.DestroyImmediate(_owner);
        }

        private float Timer => (float)typeof(FieldScannerAudio).GetField("_beepTimer", _flags).GetValue(_audio);

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void RepeatedSameSignal_DoesNotRearmPendingBeep(int intensity)
        {
            _audio.SetFeedbackSignal(intensity);
            typeof(FieldScannerAudio).GetField("_beepTimer", _flags).SetValue(_audio, .017f);
            for (int i = 0; i < 40; i++) _audio.SetFeedbackSignal(intensity);
            Assert.That(Timer, Is.EqualTo(.017f).Within(.00001f));
        }

        [TestCase(1, 1.3f)]
        [TestCase(2, .8f)]
        [TestCase(3, .45f)]
        [TestCase(4, .22f)]
        public void FrequentHUDUpdates_PreserveStableSignalCadence(int intensity, float period)
        {
            var tick = typeof(FieldScannerAudio).GetMethod("UpdateFeedback", _flags);
            Assert.That(tick, Is.Not.Null);
            var beeps = new List<float>();
            for (int frame = 0; frame < 400; frame++)
            {
                if (frame % 5 == 0) _audio.SetFeedbackSignal(intensity);
                float previous = Timer;
                tick.Invoke(_audio, new object[] { .01f });
                if (Timer > previous) beeps.Add((frame + 1) * .01f);
            }
            Assert.That(beeps.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(beeps[0], Is.InRange(.04f, .07f));
            for (int i = 1; i < beeps.Count; i++)
                Assert.That(beeps[i] - beeps[i - 1], Is.EqualTo(period).Within(.021f));
        }

        [Test]
        public void StopThenRestart_AndChangedSignal_RearmQuickFirstBeep()
        {
            _audio.SetFeedbackSignal(1);
            typeof(FieldScannerAudio).GetField("_beepTimer", _flags).SetValue(_audio, .9f);
            _audio.SetFeedbackSignal(4);
            Assert.That(Timer, Is.EqualTo(.05f));
            _audio.StopFeedback();
            Assert.That(Timer, Is.Zero);
            _audio.SetFeedbackSignal(4);
            Assert.That(Timer, Is.EqualTo(.05f));
        }
    }
}
