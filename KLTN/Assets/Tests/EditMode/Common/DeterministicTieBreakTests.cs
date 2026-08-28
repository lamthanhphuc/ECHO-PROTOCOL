using EchoProtocol.AI.Common;
using NUnit.Framework;

namespace EchoProtocol.AI.Common.Tests
{
    public sealed class DeterministicTieBreakTests
    {
        [Test]
        public void DeterministicTieBreak_PrimaryComparisonWinsRegardlessOfStableId()
        {
            var left = new PlayerId(10);
            var right = new PlayerId(1);

            Assert.That(
                DeterministicTieBreak.ComparePrimaryThenStableKey(-1, left, right),
                Is.LessThan(0));
            Assert.That(
                DeterministicTieBreak.ComparePrimaryThenStableKey(1, left, right),
                Is.GreaterThan(0));
        }

        [Test]
        public void DeterministicTieBreak_EqualPrimaryUsesStablePlayerId()
        {
            var left = new PlayerId(1);
            var right = new PlayerId(2);

            Assert.That(
                DeterministicTieBreak.ComparePrimaryThenStableKey(0, left, right),
                Is.LessThan(0));
            Assert.That(
                DeterministicTieBreak.ComparePrimaryThenStableKey(0, right, left),
                Is.GreaterThan(0));
            Assert.That(
                DeterministicTieBreak.ComparePrimaryThenStableKey(0, left, left),
                Is.EqualTo(0));
        }

        [Test]
        public void DeterministicTieBreak_EqualPrimaryAlsoSupportsStableMonsterId()
        {
            var left = new MonsterId(2);
            var right = new MonsterId(5);

            Assert.That(
                DeterministicTieBreak.ComparePrimaryThenStableKey(0, left, right),
                Is.LessThan(0));
            Assert.That(
                DeterministicTieBreak.ComparePrimaryThenStableKey(0, right, left),
                Is.GreaterThan(0));
        }
    }
}
