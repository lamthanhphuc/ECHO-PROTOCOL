using EchoProtocol.AI.Common.AED;
using NUnit.Framework;
using System.Linq;

public sealed class AEDV02PilotOpportunityTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    public void ConsumerReadbackChecksEachActualParameterRatherThanOnlyThePlanFingerprint(int index)
    {
        var plan = AEDV02Plan.Normal();
        var source = new AEDV02ThreatSource();
        Assert.IsFalse(source.MatchesStalkerConsumer(plan));
        source.ConsumerValues = Enumerable.Range(0, 6).Select(key => plan.Get((AEDV02Key)key)).ToArray();
        Assert.IsTrue(source.MatchesStalkerConsumer(plan));
        source.ConsumerValues[index] = AEDV02Plan.AllowedValues((AEDV02Key)index).First(value => value != plan.Get((AEDV02Key)index));
        Assert.IsFalse(source.MatchesStalkerConsumer(plan));
        source.ConsumerValues[index] = double.NaN;
        Assert.IsFalse(source.MatchesStalkerConsumer(plan));
    }

    [Test]
    public void LongChaseRecoveryUsesAuthoritativeDurationAndResetsWithoutAffectingShortChase()
    {
        var recovery = new AEDV02LongChaseRecovery();
        recovery.Observe(true, 10); recovery.Observe(true, 15); recovery.Observe(false, 16);
        Assert.That(recovery.SeekPausedUntil, Is.Zero);
        recovery.Observe(true, 30); recovery.Observe(true, 50); recovery.Observe(false, 51);
        Assert.That(recovery.SeekPausedUntil, Is.EqualTo(66));
        recovery.Reset(); Assert.That(recovery.SeekPausedUntil, Is.Zero);
        recovery.Observe(true, 100); recovery.Observe(false, 1);
        Assert.That(recovery.SeekPausedUntil, Is.Zero);
    }

    [Test]
    public void TeamPressureProtectsCriticalMemberAndSeparatesOverlapAndLifeConsequences()
    {
        var zero = new AEDV02PressureSnapshot(new double[5], 0, true, "observed");
        var critical = new AEDV02PressureSnapshot(new double[5], .9, true, "observed");
        Assert.That(AEDV02PilotOpportunities.TeamPressure(new[] { critical, zero, zero, zero }, 0, 0).Tier, Is.LessThanOrEqualTo(1));
        Assert.That(AEDV02PilotOpportunities.TeamPressure(new[] { zero, zero }, 1, 0).Tier, Is.EqualTo(2));
        Assert.That(AEDV02PilotOpportunities.TeamPressure(new[] { zero, zero }, 0, 2).Tier, Is.Zero);
        Assert.That(AEDV02PilotOpportunities.TeamPressure(new[] { zero }, 2, 0).Complete, Is.False);
    }
}
