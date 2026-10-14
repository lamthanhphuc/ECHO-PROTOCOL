using System;
using System.Linq;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;

public sealed class AEDV02PilotPolicyTests
{
    [Test]
    public void UnknownOrUnobservedGapCannotSatisfyTierDeclineDwell()
    {
        var memory = new AEDV02PressureTierMemory();
        var high = new AEDV02PressureSnapshot(new[] { 1d, 1, 1, 1, 1 }, 1, true, "TEST");
        var low = new AEDV02PressureSnapshot(new[] { 0d, 0, 0, 0, 0 }, 0, true, "TEST");
        Assert.That(memory.Update(high, 0), Is.EqualTo(4));
        Assert.That(memory.Update(low, 1), Is.EqualTo(4));
        Assert.That(memory.Update(default, 2), Is.EqualTo(-1));
        Assert.That(memory.Update(low, 14), Is.EqualTo(4));
        Assert.That(memory.Update(low, 20), Is.EqualTo(4));
        Assert.That(memory.Update(low, 26), Is.EqualTo(0));
    }
    private readonly Guid _match = Guid.NewGuid();
    private readonly Guid _user = Guid.NewGuid();
    private static AEDV02PressureSnapshot Pressure(double value, bool complete = true) =>
        new AEDV02PressureSnapshot(new double[5], value, complete, "TEST");

    [TestCase(.24, 0), TestCase(.25, 1), TestCase(.44, 1), TestCase(.45, 2)]
    [TestCase(.64, 2), TestCase(.65, 3), TestCase(.84, 3), TestCase(.85, 4)]
    public void Tier_UsesApprovedBoundaries(double value, int expected) => Assert.That(AEDV02PilotPolicy.Tier(value), Is.EqualTo(expected));

    [Test]
    public void Unknown_HasBaselineSensoryAndNoTargetBonus()
    {
        Assert.That(Pressure(0, false).Tier, Is.EqualTo(-1));
        Assert.That(AEDV02PilotPolicy.Sensory(-1), Is.EqualTo(1));
        Assert.That(AEDV02PilotPolicy.TargetAdjustment(-1), Is.Zero);
    }

    private AEDV02PressureFact Fact(string id, string episode, AEDV02PressureChannel channel,
        double start, double end, string cause = null) => new AEDV02PressureFact
        { MatchId = _match, UserId = _user, EventId = id, EpisodeId = episode, Channel = channel,
            StartedSeconds = start, EndedSeconds = end, CauseEventId = cause };

    [Test]
    public void FiveSourcePressure_BoundedAndDeduplicated()
    {
        var facts = new[] { Fact("s", "chase", AEDV02PressureChannel.Stalker, 60, 120),
            Fact("m", "slow", AEDV02PressureChannel.Minion, 75, 120),
            Fact("d1", "down1", AEDV02PressureChannel.Down, 120, 120),
            Fact("d2", "down2", AEDV02PressureChannel.Down, 120, 120),
            Fact("r1", "need1", AEDV02PressureChannel.Resource, 120, 120),
            Fact("r2", "need2", AEDV02PressureChannel.Resource, 120, 120),
            Fact("o", "relay", AEDV02PressureChannel.Objective, 60, 120, "s") };
        var normal = AEDV02PressureNormalizer.Project(_match, _user, 120, 0, Enumerable.Repeat(120d, 5).ToArray(), facts);
        var repeated = AEDV02PressureNormalizer.Project(_match, _user, 120, 0, Enumerable.Repeat(120d, 5).ToArray(), facts.Concat(facts));
        Assert.That(normal.Complete, Is.True);
        Assert.That(normal.Score, Is.EqualTo(1).Within(.000001));
        Assert.That(normal.Tier, Is.EqualTo(4));
        Assert.That(repeated.Score, Is.EqualTo(normal.Score));
    }

    [Test]
    public void OverlappingExposureInOneEpisode_DoesNotMultiplyPressure()
    {
        var facts = new[] { Fact("s1", "chase", AEDV02PressureChannel.Stalker, 0, 60),
            Fact("s2", "chase", AEDV02PressureChannel.Stalker, 20, 60) };
        var result = AEDV02PressureNormalizer.Project(_match, _user, 60, 0, Enumerable.Repeat(60d, 5).ToArray(), facts);
        Assert.That(result.Components[0], Is.EqualTo(1));
        Assert.That(result.Score, Is.EqualTo(.30).Within(.000001));
    }

    [Test]
    public void MissingStaleShortOrCrossMatchSource_CannotBecomePP0()
    {
        var marks = Enumerable.Repeat(60d, 5).ToArray();
        Assert.That(AEDV02PressureNormalizer.Project(_match, _user, 60, 0, null, Array.Empty<AEDV02PressureFact>()).Tier, Is.EqualTo(-1));
        marks[3] = 49;
        Assert.That(AEDV02PressureNormalizer.Project(_match, _user, 60, 0, marks, Array.Empty<AEDV02PressureFact>()).Tier, Is.EqualTo(-1));
        Assert.That(AEDV02PressureNormalizer.Project(_match, _user, 20, 0, Enumerable.Repeat(20d, 5).ToArray(), Array.Empty<AEDV02PressureFact>()).Tier, Is.EqualTo(-1));
        var fact = Fact("s", "e", AEDV02PressureChannel.Stalker, 0, 60);
        fact.MatchId = Guid.NewGuid();
        Assert.That(AEDV02PressureNormalizer.Project(_match, _user, 60, 0, marks, new[] { fact }).Reason, Is.EqualTo("INVALID_PRESSURE_SOURCE"));
    }

    [Test]
    public void ConflictingEventOrObjectiveWithoutCause_IsInvalid()
    {
        var marks = Enumerable.Repeat(60d, 5).ToArray();
        var conflict = new[] { Fact("same", "one", AEDV02PressureChannel.Down, 60, 60),
            Fact("same", "two", AEDV02PressureChannel.Down, 60, 60) };
        Assert.That(AEDV02PressureNormalizer.Project(_match, _user, 60, 0, marks, conflict).Complete, Is.False);
        Assert.That(AEDV02PressureNormalizer.Project(_match, _user, 60, 0, marks,
            new[] { Fact("o", "relay", AEDV02PressureChannel.Objective, 0, 60) }).Complete, Is.False);
    }

    [Test]
    public void TierDownwardChange_RequiresHysteresisAndDwell()
    {
        var memory = new AEDV02PressureTierMemory();
        Assert.That(memory.Update(Pressure(.86), 0), Is.EqualTo(4));
        Assert.That(memory.Update(Pressure(.82), 1), Is.EqualTo(4));
        Assert.That(memory.Update(Pressure(.79), 2), Is.EqualTo(4));
        Assert.That(memory.Update(Pressure(.79), 7), Is.EqualTo(4));
        Assert.That(memory.Update(Pressure(.79), 13), Is.EqualTo(4));
        Assert.That(memory.Update(Pressure(.79), 14), Is.EqualTo(3));
        Assert.That(memory.Update(Pressure(.79, false), 15), Is.EqualTo(-1));
    }

    [Test]
    public void P4Token_StaysAcrossUnknownScoreDropThenGrace()
    {
        var ledger = new AEDV02HideProtectionLedger(); ledger.Reset(_match);
        Assert.That(ledger.TryGrant(_match, _user, 1, "encounter", 1, 0, true, Pressure(.9), out var token), Is.True);
        Assert.That(ledger.TryGrant(_match, _user, 1, "encounter", 1, 1, true, Pressure(.9), out var replay), Is.True);
        Assert.That(replay.TokenId, Is.EqualTo(token.TokenId));
        Assert.That(ledger.TryGrant(_match, _user, 1, "other", 1, 2, true, Pressure(.9, false), out _), Is.False);
        Assert.That(ledger.BlocksForceExit(_match, _user, 1, 9.99, out var reason), Is.True);
        Assert.That(reason, Is.EqualTo("PROTECT_P4_HIDE_WINDOW"));
        Assert.That(ledger.BlocksForceExit(_match, _user, 1, 10, out reason), Is.True);
        Assert.That(reason, Is.EqualTo("GRACE_AFTER_PROTECTION"));
        Assert.That(ledger.BlocksForceExit(_match, _user, 1, 13, out _), Is.False);
    }

    [TestCase(false, .64, 1d, 0d, 1, false, AEDV02Key.ChaseSpeed)]
    [TestCase(false, .65, 1d, 0d, 1, true, AEDV02Key.ChaseSpeed)]
    [TestCase(true, .2, 1d, 0d, 1, true, AEDV02Key.ChaseSpeed)]
    [TestCase(true, .2, 0d, 0d, 1, false, AEDV02Key.ChaseSpeed)]
    [TestCase(false, .7, 0d, 1d, 1, true, AEDV02Key.Zone1MinionCap)]
    [TestCase(false, .7, 0d, 1d, 2, true, AEDV02Key.Zone2MinionCap)]
    [TestCase(false, .7, 0d, 1d, 3, false, AEDV02Key.Zone3AdaptiveMinionCap)]
    public void ReliefUsesVerifiedCauseAndChangesOneKey(bool down, double score, double stalker, double minion, int zone, bool expected, AEDV02Key key)
    {
        var previous = AEDV02Plan.Normal();
        Assert.That(previous.TryProposeRelief(new[] { new[] { stalker, minion, 0d, 0, 0 } }, new[] { score }, down, zone, out var next), Is.EqualTo(expected));
        if (!expected) return;
        Assert.That(next.TryValidateChange(previous, AEDV02DecisionPoint.SAFE_WINDOW, false, 0, out var changed, out _), Is.True);
        Assert.That(changed, Is.EqualTo(key));
        Assert.That(AEDV02Plan.IsAdverse(key, previous.Get(key), next.Get(key)), Is.False);
        Assert.That(previous.Fingerprint(), Is.EqualTo(AEDV02Plan.Normal().Fingerprint()));
    }

    [Test]
    public void P4Token_QuotaCooldownIdentityExitAndReset()
    {
        var ledger = new AEDV02HideProtectionLedger(); ledger.Reset(_match);
        Assert.That(ledger.TryGrant(_match, _user, 1, "one", 1, 0, true, Pressure(.9), out _), Is.True);
        ledger.Exit(_user, 1, 2);
        Assert.That(ledger.BlocksForceExit(_match, _user, 1, 4, out var reason), Is.True);
        Assert.That(reason, Is.EqualTo("GRACE_AFTER_PROTECTION"));
        Assert.That(ledger.BlocksForceExit(_match, _user, 2, 4, out _), Is.False);
        Assert.That(ledger.TryGrant(_match, _user, 1, "two", 1, 119, true, Pressure(.9), out _), Is.False);
        Assert.That(ledger.TryGrant(_match, _user, 1, "two", 1, 120, true, Pressure(.9), out _), Is.True);
        Assert.That(ledger.TryGrant(_match, _user, 1, "three", 1, 240, true, Pressure(.9), out _), Is.False);
        ledger.Reset(Guid.NewGuid());
        Assert.That(ledger.BlocksForceExit(_match, _user, 1, 121, out _), Is.False);
        Assert.That(ledger.TryGrant(_match, _user, 1, "one", 1, 241, true, Pressure(.9), out _), Is.False);
    }
}
