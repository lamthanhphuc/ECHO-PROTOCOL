using System;
using EchoProtocol.AI.Common;
using NUnit.Framework;

namespace EchoProtocol.AI.Common.Tests
{
    public sealed class StableIdentityTests
    {
        [Test]
        public void PlayerId_Default_IsInvalid()
        {
            Assert.That(default(PlayerId).IsValid, Is.False);
            Assert.That(PlayerId.Invalid.IsValid, Is.False);
            Assert.That(default(PlayerId).Value, Is.EqualTo(0));
        }

        [Test]
        public void PlayerId_ValidValues_HaveDeterministicEqualityAndOrdering()
        {
            var one = new PlayerId(1);
            var alsoOne = new PlayerId(1);
            var two = new PlayerId(2);

            Assert.That(one.IsValid, Is.True);
            Assert.That(one.Value, Is.EqualTo(1));
            Assert.That(one == alsoOne, Is.True);
            Assert.That(one != two, Is.True);
            Assert.That(one.Equals(alsoOne), Is.True);
            Assert.That(one.CompareTo(two), Is.LessThan(0));
            Assert.That(two.CompareTo(one), Is.GreaterThan(0));
            Assert.That(one.GetHashCode(), Is.EqualTo(alsoOne.GetHashCode()));
        }

        [Test]
        public void PlayerId_NonPositiveConstruction_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerId(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerId(-1));
        }

        [Test]
        public void MonsterId_DefaultAndValidOrdering_MatchStableIdentityContract()
        {
            var one = new MonsterId(1);
            var alsoOne = new MonsterId(1);
            var two = new MonsterId(2);

            Assert.That(default(MonsterId).IsValid, Is.False);
            Assert.That(MonsterId.Invalid.IsValid, Is.False);
            Assert.That(one.IsValid, Is.True);
            Assert.That(one.Value, Is.EqualTo(1));
            Assert.That(one == alsoOne, Is.True);
            Assert.That(one != two, Is.True);
            Assert.That(one.Equals(alsoOne), Is.True);
            Assert.That(one.CompareTo(two), Is.LessThan(0));
            Assert.That(two.CompareTo(one), Is.GreaterThan(0));
            Assert.That(one.GetHashCode(), Is.EqualTo(alsoOne.GetHashCode()));
        }
    }
}
