using System;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;

public sealed class AEDV02CabinetEncounterTests
{
    private static AEDV02PressureSnapshot Low => new AEDV02PressureSnapshot(new double[5], 0, true, "UNIT_FACTS");
    [Test]
    public void AEDV02_CabinetWarningWaitsForEveryRealRecipientAckAndFullLeadBeforeJump()
    {
        var match = Guid.NewGuid(); var user = Guid.NewGuid(); var teammate = Guid.NewGuid();
        var encounter = new AEDV02CabinetEncounter(); encounter.Reset(match);
        Assert.That(encounter.TryReserve(match, user, 10, 1, 400, 399, Low, AEDV02Plan.Hash("source"), 0, new[] { user, teammate }), Is.True);
        Assert.That(encounter.AcknowledgeCue(Guid.NewGuid(), user, true, true, 401), Is.False);
        Assert.That(encounter.AcknowledgeCue(encounter.EncounterId, user, true, true, 401), Is.True);
        encounter.Tick(402, true, false, AEDV02CabinetInspection.Pending);
        Assert.That(encounter.Phase, Is.EqualTo(AEDV02CabinetPhase.CuePending));
        Assert.That(encounter.AcknowledgeCue(encounter.EncounterId, teammate, true, true, 402), Is.True);
        encounter.Tick(404.49, true, false, AEDV02CabinetInspection.Pending);
        Assert.That(encounter.Phase, Is.EqualTo(AEDV02CabinetPhase.WarningLead));
        encounter.Tick(404.5, true, false, AEDV02CabinetInspection.Pending);
        Assert.That(encounter.Phase, Is.EqualTo(AEDV02CabinetPhase.JumpIn));
        encounter.Tick(407.49, true, false, AEDV02CabinetInspection.Pending);
        Assert.That(encounter.Phase, Is.EqualTo(AEDV02CabinetPhase.JumpIn));
        encounter.Tick(407.5, true, false, AEDV02CabinetInspection.Pending);
        encounter.Tick(410, true, true, AEDV02CabinetInspection.Pending);
        Assert.That(encounter.Phase, Is.EqualTo(AEDV02CabinetPhase.Inspect));
        encounter.Tick(411, true, true, AEDV02CabinetInspection.Pulled);
        Assert.That(encounter.Phase, Is.EqualTo(AEDV02CabinetPhase.Recovery));
        encounter.Tick(436, true, false, AEDV02CabinetInspection.Pending);
        Assert.That(encounter.TryReserve(match, Guid.NewGuid(), 20, 2, 500, 499, Low, AEDV02Plan.Hash("source"), 0, new[] { user }), Is.False);
        var second = Guid.NewGuid();
        Assert.That(encounter.TryReserve(match, second, 20, 2, 900, 899, Low, AEDV02Plan.Hash("source"), 0, new[] { second }), Is.True);
    }
    [TestCase("source")]
    [TestCase("tier")]
    [TestCase("stale-clue")]
    [TestCase("phase")]
    [TestCase("budget")]
    [TestCase("cross-match")]
    public void AEDV02_CabinetReservationFailsClosed(string failure)
    {
        var match = Guid.NewGuid(); var user = Guid.NewGuid(); var encounter = new AEDV02CabinetEncounter(); encounter.Reset(match);
        var pressure = failure == "source" ? new AEDV02PressureSnapshot(new double[5], 0, false, "MISSING") :
            failure == "tier" ? new AEDV02PressureSnapshot(new[] { 1d, 1, 1, 1, 1 }, 1, true, "UNIT_FACTS") : Low;
        Assert.That(encounter.TryReserve(failure == "cross-match" ? Guid.NewGuid() : match, user, 10, failure == "phase" ? 3 : 1,
            400, failure == "stale-clue" ? 380 : 399, pressure, AEDV02Plan.Hash("source"), failure == "budget" ? 1 : 0, new[] { user }), Is.False);
        Assert.That(encounter.IsActive, Is.False);
    }
    [TestCase("cue")]
    [TestCase("revalidation")]
    [TestCase("protected")]
    [TestCase("approach-timeout")]
    public void AEDV02_CabinetAbortCannotPullAndSpecialSeparationSurvivesFailure(string failure)
    {
        var match = Guid.NewGuid(); var user = Guid.NewGuid(); var encounter = new AEDV02CabinetEncounter(); encounter.Reset(match);
        Assert.That(encounter.TryReserve(match, user, 10, 1, 400, 399, Low, AEDV02Plan.Hash("source"), 0, new[] { user }), Is.True);
        encounter.AcknowledgeCue(encounter.EncounterId, user, failure != "cue", true, 401);
        if (failure != "cue")
        {
            encounter.Tick(404, failure != "revalidation", false, AEDV02CabinetInspection.Pending);
            encounter.Tick(407, true, false, AEDV02CabinetInspection.Pending);
            encounter.Tick(419, true, failure == "protected", AEDV02CabinetInspection.Pending);
            encounter.Tick(420, true, false, AEDV02CabinetInspection.Protected);
        }
        Assert.That(encounter.Phase, Is.EqualTo(AEDV02CabinetPhase.Backoff));
        Assert.That(encounter.Reason, Does.StartWith(failure == "cue" ? "HOLD_CUE" : "ABORT"));
        var another = Guid.NewGuid();
        Assert.That(encounter.TryReserve(match, another, 20, 2, 430, 429, Low, AEDV02Plan.Hash("source"), 0, new[] { another }), Is.False);
        encounter.Reset(Guid.NewGuid()); Assert.That(encounter.IsActive, Is.False);
    }
}
