using System;
using System.Linq;
using NUnit.Framework;

namespace EchoProtocol.Player.Tests
{
    public sealed class PlayerCaughtRulesTests
    {
        private static Type Resolve(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("EchoProtocol.Networking." + name))
            .First(type => type != null);

        private static object Status(string name) => Enum.Parse(Resolve("NetworkPlayerLifeStatus"), name);
        private static bool Rule(string method, params object[] arguments) =>
            (bool)Resolve("NetworkPlayerLifeStateRules").GetMethod(method).Invoke(null, arguments);

        [TestCase("Caught")]
        [TestCase("Downed")]
        [TestCase("Eliminated")]
        [TestCase("Escaped")]
        public void NonAlivePlayerCannotReceiveAnotherCatchOrInitiateActions(string state)
        {
            Assert.That(Rule("CanReceiveDamage", Status(state), false), Is.False);
            Assert.That(Rule("CanInitiateAction", Status(state)), Is.False);
        }

        [Test]
        public void CaughtCannotMoveBleedOutEscapeOrBeRevivedEarly()
        {
            var caught = Status("Caught");
            Assert.That(Rule("CanMove", caught), Is.False);
            Assert.That(Rule("CanBleedOut", caught), Is.False);
            Assert.That(Rule("CanEscape", caught), Is.False);
            Assert.That(Rule("CanRevive", caught, 0, 1), Is.False);
        }

        [Test]
        public void DownedRetainsTeammateReviveAndAliveRetainsProtection()
        {
            Assert.That(Rule("CanStartRevive", Status("Downed"), Status("Alive"), false, false, 0, 1), Is.True);
            Assert.That(Rule("CanStartRevive", Status("Downed"), Status("Caught"), false, false, 0, 1), Is.False);
            Assert.That(Rule("CanReceiveDamage", Status("Alive"), true), Is.False);
            Assert.That(Rule("CanReceiveDamage", Status("Alive"), false), Is.True);
        }
    }
}
