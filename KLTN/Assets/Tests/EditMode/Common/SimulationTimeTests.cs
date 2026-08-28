using System;
using EchoProtocol.AI.Common;
using NUnit.Framework;

namespace EchoProtocol.AI.Common.Tests
{
    public sealed class SimulationTimeTests
    {
        [Test]
        public void AiSimulationTime_Default_IsInvalid()
        {
            Assert.That(default(AiSimulationTime).IsValid, Is.False);
            Assert.That(AiSimulationTime.Invalid.IsValid, Is.False);
        }

        [Test]
        public void AiSimulationTime_ValidConstruction_PreservesTickAndSeconds()
        {
            var time = new AiSimulationTime(42, 12.5d);

            Assert.That(time.IsValid, Is.True);
            Assert.That(time.Tick, Is.EqualTo(42));
            Assert.That(time.Seconds, Is.EqualTo(12.5d));
        }

        [Test]
        public void AiSimulationTime_InvalidConstruction_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AiSimulationTime(-1, 0d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AiSimulationTime(0, -0.1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AiSimulationTime(0, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AiSimulationTime(0, double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AiSimulationTime(0, double.NegativeInfinity));
        }

        [Test]
        public void AiSimulationTime_Ordering_IsTickThenSeconds()
        {
            var tickTenSecondOne = new AiSimulationTime(10, 1d);
            var alsoTickTenSecondOne = new AiSimulationTime(10, 1d);
            var tickTenSecondOnePointOne = new AiSimulationTime(10, 1.1d);
            var tickElevenSecondZero = new AiSimulationTime(11, 0d);

            Assert.That(tickTenSecondOne.CompareTo(tickElevenSecondZero), Is.LessThan(0));
            Assert.That(tickElevenSecondZero.CompareTo(tickTenSecondOne), Is.GreaterThan(0));
            Assert.That(tickTenSecondOne.CompareTo(tickTenSecondOnePointOne), Is.LessThan(0));
            Assert.That(tickTenSecondOnePointOne.CompareTo(tickTenSecondOne), Is.GreaterThan(0));
            Assert.That(tickTenSecondOne.CompareTo(alsoTickTenSecondOne), Is.EqualTo(0));
            Assert.That(tickTenSecondOne == alsoTickTenSecondOne, Is.True);
            Assert.That(tickTenSecondOne.Equals(alsoTickTenSecondOne), Is.True);
            Assert.That(tickTenSecondOne.GetHashCode(), Is.EqualTo(alsoTickTenSecondOne.GetHashCode()));
        }

        [Test]
        public void AiSimulationStep_Validity_RequiresValidTimeAndFiniteNonNegativeDelta()
        {
            var time = new AiSimulationTime(5, 0.25d);
            var zeroDelta = new AiSimulationStep(time, 0f);
            var positiveDelta = new AiSimulationStep(time, 0.016f);

            Assert.That(zeroDelta.IsValid, Is.True);
            Assert.That(zeroDelta.Time, Is.EqualTo(time));
            Assert.That(zeroDelta.DeltaSeconds, Is.EqualTo(0f));
            Assert.That(positiveDelta.IsValid, Is.True);
            Assert.That(positiveDelta.DeltaSeconds, Is.EqualTo(0.016f));
            Assert.That(default(AiSimulationStep).IsValid, Is.False);
            Assert.That(AiSimulationStep.Invalid.IsValid, Is.False);
            Assert.Throws<ArgumentException>(() => new AiSimulationStep(AiSimulationTime.Invalid, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AiSimulationStep(time, -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AiSimulationStep(time, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AiSimulationStep(time, float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AiSimulationStep(time, float.NegativeInfinity));
        }
    }
}
