using System;

namespace EchoProtocol.AI.Common.AED
{
    public sealed class AEDPhase8DirectorBundleV1
    {
        public Guid MatchId { get; }
        public uint PhaseOrdinal { get; }
        public string EvidenceFingerprint { get; }
        public string ReasonCode { get; }
        public bool ResearchValid { get; }
        public bool CanApplyGameplay => false;

        private AEDPhase8DirectorBundleV1(Guid matchId, uint ordinal, string fingerprint, bool valid, string reason)
        { MatchId = matchId; PhaseOrdinal = ordinal; EvidenceFingerprint = fingerprint; ResearchValid = valid; ReasonCode = reason; }

        public static AEDPhase8DirectorBundleV1 Validate(AEDv2CurrentMatchEvidence evidence, AEDPhase8GateResultV1 gate, AEDCurrentMatchPressureV1 pressure, AEDResourceSupplySnapshotV1 supply, AEDResourceProposalV1 resource, AEDMinionProposalV1 minion, AEDStalkerProposalV1 stalker)
        {
            AEDPhase8DirectorBundleV1 Hold(string reason) => new AEDPhase8DirectorBundleV1(evidence?.MatchId ?? Guid.Empty, evidence?.PhaseOrdinal ?? 0, evidence?.EvidenceFingerprint, false, reason);
            if (evidence == null || gate?.LocallyEligible != true || !evidence.TelemetryCompleteness || !evidence.HasValidFingerprint()) return Hold("P8_CANONICAL_GATE_FAILED");
            if (pressure == null || !pressure.SourceComplete || !pressure.Score.HasValue || pressure.MatchId != evidence.MatchId || pressure.PhaseOrdinal != evidence.PhaseOrdinal) return Hold("P8_PRESSURE_INCOMPLETE");
            if (supply == null || !supply.CoverageComplete || !supply.SourcesHostVerified || supply.MatchId != evidence.MatchId || supply.PhaseOrdinal != evidence.PhaseOrdinal) return Hold("P8_SUPPLY_INCOMPLETE");
            if (resource == null || minion == null || stalker == null) return Hold("P8_DIRECTOR_MISSING");
            if (resource.MatchId != evidence.MatchId || minion.MatchId != evidence.MatchId || stalker.MatchId != evidence.MatchId) return Hold("P8_MATCH_MISMATCH");
            if (!string.Equals(resource.EvidenceFingerprint, evidence.EvidenceFingerprint, StringComparison.Ordinal) || !string.Equals(minion.EvidenceFingerprint, evidence.EvidenceFingerprint, StringComparison.Ordinal) || !string.Equals(stalker.EvidenceFingerprint, evidence.EvidenceFingerprint, StringComparison.Ordinal)) return Hold("P8_EVIDENCE_MISMATCH");
            if (stalker.CompletedEvidencePhaseOrdinal != evidence.PhaseOrdinal || evidence.PhaseOrdinal == uint.MaxValue || stalker.TargetPhaseOrdinal != evidence.PhaseOrdinal + 1u) return Hold("P8_PHASE_MISMATCH");
            if (!AEDResourceDirectorV1.ValidateFairness(resource) || !AEDMinionDirectorV1.Validate(minion) || !AEDStalkerDirectorV1.Validate(stalker)) return Hold("P8_DIRECTOR_INVALID");
            return new AEDPhase8DirectorBundleV1(evidence.MatchId, evidence.PhaseOrdinal, evidence.EvidenceFingerprint, true, "P8_RESEARCH_BUNDLE_VALID");
        }
    }
}
