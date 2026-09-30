using System;
using System.Linq;
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
