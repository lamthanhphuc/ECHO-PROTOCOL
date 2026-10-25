using System;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;

public sealed class AEDV02PlanTests
{
    [Test]
    public void HistoricalResourceIncreaseRequiresDedicatedAxis()
    {
        Assert.That(AEDV02PilotOpportunities.HistoricalIncreaseAxes, Does.Contain(AEDV02PilotOpportunities.ResourceManagementAxis));
        Assert.That(AEDV02PilotOpportunities.HasResourceManagementHistory(new[] { "Tool" }), Is.False);
        Assert.That(AEDV02PilotOpportunities.HasResourceManagementHistory(new[] { AEDV02PilotOpportunities.ResourceManagementAxis }), Is.True);
    }

    [Test]
    public void HandoffWaitsForRealZone3DespawnBeforeAllocatingAnotherPositiveAxis()
    {
        var normal = AEDV02Plan.Normal();
        Assert.That(normal.HasWorldPressureSlot(0, 1), Is.True, "Lowering a cap must not force despawn.");
        var stalker = normal.With(AEDV02Key.ChaseSpeed, 8);
        Assert.That(stalker.HasWorldPressureSlot(0, 1), Is.False, "A revised cap does not prove the previous Minion pressure disappeared.");
        Assert.That(stalker.HasWorldPressureSlot(0, 0), Is.True);
        var minion = normal.With(AEDV02Key.Zone3AdaptiveMinionCap, 1);
        Assert.That(minion.HasWorldPressureSlot(0, 0), Is.True);
        Assert.That(minion.HasWorldPressureSlot(0, 1), Is.True);
        Assert.That(minion.HasWorldPressureSlot(0, 2), Is.False);
        Assert.That(minion.With(AEDV02Key.ChaseSpeed, 8).HasWorldPressureSlot(0, 0), Is.False);
        Assert.That(normal.HasWorldPressureSlot(0, -1), Is.False);
    }
    [Test]
    public void Normal_KeepsZone3ZeroAndOptionalZero()
    {
        var plan = AEDV02Plan.Normal();
        Assert.That(plan.Get(AEDV02Key.Zone3AdaptiveMinionCap), Is.Zero);
        Assert.That(plan.Get(AEDV02Key.OptionalToolBudget), Is.Zero);
        Assert.That(plan.PositiveTokens(0), Is.Zero);
    }
    [Test]
    public void GlobalBudget_RejectsStackingAndMultipleStalkerBuffs()
    {
        var normal = AEDV02Plan.Normal();
        var stalker = normal.With(AEDV02Key.DetectionAcquireSeconds, 1);
        Assert.That(stalker.PositiveTokens(0), Is.EqualTo(1));
        var stacked = stalker.With(AEDV02Key.Zone3AdaptiveMinionCap, 1);
        Assert.That(stacked.TryValidateChange(stalker, AEDV02DecisionPoint.SAFE_WINDOW, false, 0, out _, out var reason), Is.False);
        Assert.That(reason, Is.EqualTo("HOLD_GLOBAL_PRESSURE_BUDGET"));
        Assert.That(stalker.With(AEDV02Key.ChaseSpeed, 8).PositiveTokens(0), Is.EqualTo(2));
    }
    [Test]
    public void FinalHunt_RejectsAdverseChangesAllowsRelief()
    {
        var normal = AEDV02Plan.Normal();
        Assert.That(normal.With(AEDV02Key.Zone3AdaptiveMinionCap, 1).TryValidateChange(normal,
            AEDV02DecisionPoint.SAFE_WINDOW, true, 0, out _, out _), Is.False);
        Assert.That(normal.With(AEDV02Key.DetectionAcquireSeconds, 1.5).TryValidateChange(normal,
            AEDV02DecisionPoint.PHASE_BOUNDARY, true, 0, out _, out _), Is.True);
    }
    [Test]
    public void PreMatch_AtomicCompositeButNoZone3Increase()
    {
        var normal = AEDV02Plan.Normal();
        var initial = normal.With(AEDV02Key.OptionalToolBudget, 2).With(AEDV02Key.ResourceGrade, 1)
            .With(AEDV02Key.DetectionAcquireSeconds, 1.5);
        Assert.That(initial.TryValidateChange(normal, AEDV02DecisionPoint.PRE_MATCH, false, 2, out _, out _), Is.True);
        Assert.That(initial.With(AEDV02Key.Zone3AdaptiveMinionCap, 1).TryValidateChange(normal,
            AEDV02DecisionPoint.PRE_MATCH, false, 2, out _, out _), Is.False);
    }
    [Test]
    public void WindowIdentity_DistinguishesTwoWindowsInSamePhase()
    {
        var match = Guid.NewGuid();
        var first = AEDV02Plan.WindowId(match, 1, AEDV02DecisionPoint.SAFE_WINDOW, 1, 10, 20, "source");
        Assert.That(first, Is.EqualTo(AEDV02Plan.WindowId(match, 1, AEDV02DecisionPoint.SAFE_WINDOW, 1, 10, 20, "source")));
        Assert.That(first, Is.Not.EqualTo(AEDV02Plan.WindowId(match, 1, AEDV02DecisionPoint.SAFE_WINDOW, 2, 20, 30, "source")));
    }
    [Test]
    public void InMatchCannotSkipTierAndReliefReturnsPressureToBaselineFirst()
    {
        var pressured = AEDV02Plan.Normal().With(AEDV02Key.ChaseSpeed, 8);
        Assert.That(pressured.With(AEDV02Key.ChaseSpeed, 7).TryValidateChange(pressured, AEDV02DecisionPoint.SAFE_WINDOW, false, 0,
            out _, out var reason), Is.False);
        Assert.That(reason, Is.EqualTo("HOLD_SINGLE_TIER_REQUIRED"));
        Assert.That(pressured.TryProposeRelief(new[] { new[] { 1d, 0, 0, 0, 0 } }, new[] { .7 }, false, 1, out var relief), Is.True);
        Assert.That(relief.Get(AEDV02Key.ChaseSpeed), Is.EqualTo(7.5));
        Assert.That(relief.TryValidateChange(pressured, AEDV02DecisionPoint.SAFE_WINDOW, false, 0, out _, out _), Is.True);
        Assert.That(relief.PositiveTokens(0), Is.Zero);
    }
    [Test]
    public void Plan_RejectsOutOfWhitelistAndProtectsValues()
    {
        Assert.Throws<ArgumentException>(() => AEDV02Plan.Normal().With(AEDV02Key.Zone3AdaptiveMinionCap, 2));
        var plan = AEDV02Plan.Normal(); var copy = plan.ToArray(); copy[8] = 1;
        Assert.That(plan.Get(AEDV02Key.Zone3AdaptiveMinionCap), Is.Zero);
        Assert.That(plan.Fingerprint(), Is.EqualTo(AEDV02Plan.Normal().Fingerprint()));
    }
}
