using System;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;

namespace EchoProtocol.AI.Common.Tests
{
    public sealed class AEDPhase8IntegrationV1Tests
    {
        private static AEDv2CurrentMatchEvidence Evidence(bool complete = true)
        {
            var start = new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc);
            return new AEDv2CurrentMatchEvidence(Guid.NewGuid(), "CORE_COLLECTION", 1, "roster", start, start.AddSeconds(60), 2, 0, 0, 0, 0, 0, complete, Array.Empty<string>());
        }

        private static AEDCurrentMatchPressureV1 Pressure(AEDv2CurrentMatchEvidence evidence)
        {
            var pursuit = new AEDPursuitEvidenceCollectorV1(); pursuit.ResetMatch(evidence.MatchId); pursuit.StartPhase(evidence.MatchId, 1, "CORE_COLLECTION");
            var minion = new AEDMinionEvidenceCollectorV1(); minion.ResetMatch(evidence.MatchId); minion.StartPhase(evidence.MatchId, 1, "CORE_COLLECTION");
            var survival = new AEDSurvivalEvidenceCollectorV1(); survival.ResetMatch(evidence.MatchId); survival.StartPhase(evidence.MatchId, 1, "CORE_COLLECTION");
            return AEDCurrentMatchPressureV1.Project(evidence, pursuit.Freeze(), minion.Freeze(), survival.Freeze(), 100, 10);
        }

        private static AEDPhase8GateResultV1 Gate(AEDv2CurrentMatchEvidence evidence, bool adaptive = true, bool observed = true, bool unsafeStalker = false, uint previousOrdinal = 0) => AEDPhase8IntegrationGateV1.Evaluate(evidence.MatchId, "roster", previousOrdinal, evidence, Pressure(evidence), new AEDv2RosterSafety(observed, false), true, adaptive, true, unsafeStalker, false);

        [Test] public void FirstBoundaryUsesCompletedOrdinalOne() { Assert.That(AEDPhase8OrdinalContractV1.TryCreate(1, 0, out var context), Is.True); Assert.That(context.BackendDecisionOrdinal, Is.EqualTo(1u)); Assert.That(context.ResearchTargetPhaseOrdinal, Is.EqualTo(2u)); }
        [Test] public void SecondBoundaryUsesCompletedOrdinalTwo() { Assert.That(AEDPhase8OrdinalContractV1.TryCreate(2, 1, out var context), Is.True); Assert.That(context.BackendDecisionOrdinal, Is.EqualTo(2u)); }
        [Test] public void StaleOrdinalFailsClosed() => Assert.That(AEDPhase8OrdinalContractV1.TryCreate(2, 0, out _), Is.False);
        [Test] public void OrdinalOverflowFailsClosed() => Assert.That(AEDPhase8OrdinalContractV1.TryCreate(uint.MaxValue, uint.MaxValue - 1, out _), Is.False);
        [Test] public void ValidEvidenceAllowsResearchOnly() { var result = Gate(Evidence()); Assert.That(result.LocallyEligible, Is.True); Assert.That(result.CanApplyGameplay, Is.False); }
        [Test] public void FixedModeCannotEnterResearchGate() => Assert.That(Gate(Evidence(), adaptive: false).LocallyEligible, Is.False);
        [Test] public void IncompleteEvidenceFailsClosed() => Assert.That(Gate(Evidence(false)).LocallyEligible, Is.False);
        [Test] public void MissingRosterObservationFailsClosed() => Assert.That(Gate(Evidence(), observed: false).LocallyEligible, Is.False);
        [Test] public void StalkerUnsafeStateFailsClosed() => Assert.That(Gate(Evidence(), unsafeStalker: true).LocallyEligible, Is.False);
        [Test] public void IncorrectNetworkOrdinalFailsClosed() => Assert.That(Gate(Evidence(), previousOrdinal: 2).LocallyEligible, Is.False);
    }
}
