using System;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;

namespace EchoProtocol.AI.Common.Tests
{
    public sealed class AEDPhase8DirectorBundleV1Tests
    {
        [Test] public void MissingEvidenceFailsClosed()
        { var bundle = AEDPhase8DirectorBundleV1.Validate(null, null, null, null, null, null, null); Assert.That(bundle.ResearchValid, Is.False); Assert.That(bundle.CanApplyGameplay, Is.False); Assert.That(bundle.ReasonCode, Is.EqualTo("P8_CANONICAL_GATE_FAILED")); }
        [Test] public void MissingSupplyFailsClosed()
        {
            var now = new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc);
            var evidence = new AEDv2CurrentMatchEvidence(Guid.NewGuid(), "CORE_COLLECTION", 1, "roster", now, now.AddSeconds(60), 2, 0, 0, 0, 0, 0, true, Array.Empty<string>());
            var pursuit = new AEDPursuitEvidenceCollectorV1(); pursuit.ResetMatch(evidence.MatchId); pursuit.StartPhase(evidence.MatchId, 1, "CORE_COLLECTION");
            var minionEvidence = new AEDMinionEvidenceCollectorV1(); minionEvidence.ResetMatch(evidence.MatchId); minionEvidence.StartPhase(evidence.MatchId, 1, "CORE_COLLECTION");
            var survival = new AEDSurvivalEvidenceCollectorV1(); survival.ResetMatch(evidence.MatchId); survival.StartPhase(evidence.MatchId, 1, "CORE_COLLECTION");
            var pressure = AEDCurrentMatchPressureV1.Project(evidence, pursuit.Freeze(), minionEvidence.Freeze(), survival.Freeze(), 100, 10);
            var gate = AEDPhase8IntegrationGateV1.Evaluate(evidence.MatchId, evidence.RosterIdentity, 0, evidence, pressure, new AEDv2RosterSafety(true, false), true, true, true, false, false);
            var bundle = AEDPhase8DirectorBundleV1.Validate(evidence, gate, pressure, null, null, null, null);
            Assert.That(bundle.ResearchValid, Is.False);
            Assert.That(bundle.CanApplyGameplay, Is.False);
            Assert.That(bundle.ReasonCode, Is.EqualTo("P8_SUPPLY_INCOMPLETE"));
        }

        [Test] public void MissingDirectorCanNeverApproveGameplay()
        { var bundle = AEDPhase8DirectorBundleV1.Validate(null, null, null, null, null, null, null); Assert.That(bundle.CanApplyGameplay, Is.False); }
    }
}
