using System;
using System.Linq;
using NUnit.Framework;

namespace EchoProtocol.Player.Tests
{
    public sealed class VoiceChatRulesTests
    {
        private static Type Resolve(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("EchoProtocol.Voice." + name)).First(t => t != null);
        private static bool Capture(params object[] flags) => (bool)Resolve("VoiceTransmissionRules")
            .GetMethod("CanCapture").Invoke(null, flags);
        private static bool Transmit(bool capture, bool open, bool held) => (bool)Resolve("VoiceTransmissionRules")
            .GetMethod("CanTransmit").Invoke(null, new object[] { capture, open, held });

        [TestCase(0)] // room left
        [TestCase(1)] // user disabled mic
        [TestCase(2)] // muted
        [TestCase(3)] // device unplugged
        [TestCase(4)] // local test
        [TestCase(5)] // focus lost
        [TestCase(6)] // app paused
        [TestCase(7)] // settings/rebinding
        public void RevokingAnyCaptureConditionStopsBothOpenMicAndHeldPtt(int changed)
        {
            object[] flags = { true, true, false, true, false, true, false, false };
            Assert.That(Capture(flags), Is.True);
            flags[changed] = !(bool)flags[changed];
            bool capture = Capture(flags);
            Assert.That(Transmit(capture, true, true), Is.False);
            Assert.That(Transmit(capture, false, true), Is.False);
        }

        [Test]
        public void PttReleaseStopsTransmissionWhileOpenMicCanStillTransmit()
        {
            Assert.That(Transmit(true, false, true), Is.True);
            Assert.That(Transmit(true, false, false), Is.False);
            Assert.That(Transmit(true, true, false), Is.True);
        }

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
