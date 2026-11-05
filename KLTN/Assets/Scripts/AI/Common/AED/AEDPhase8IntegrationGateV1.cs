using System;

namespace EchoProtocol.AI.Common.AED
{
    public sealed class AEDPhase8GateResultV1
    {
        public bool LocallyEligible { get; }
        public bool CanApplyGameplay => false;
        public string ReasonCode { get; }

        internal AEDPhase8GateResultV1(bool eligible, string reason)
        { LocallyEligible = eligible; ReasonCode = reason; }
    }

    public static class AEDPhase8IntegrationGateV1
    {
        public static AEDPhase8GateResultV1 Evaluate(Guid matchId, string rosterIdentity, uint networkOrdinalBeforeTransition, AEDv2CurrentMatchEvidence evidence, AEDCurrentMatchPressureV1 pressure, AEDv2RosterSafety roster, bool hostAuthority, bool adaptiveNormal, bool safeBoundary, bool stalkerUnsafe, bool playerUnsafe)
        {
            AEDPhase8GateResultV1 Hold(string reason) => new AEDPhase8GateResultV1(false, reason);
            if (!hostAuthority || !adaptiveNormal) return Hold("P8_AUTHORITY_OR_MODE_INVALID");
            if (matchId == Guid.Empty || evidence == null || evidence.MatchId != matchId || evidence.RosterIdentity != rosterIdentity || !evidence.TelemetryCompleteness || !evidence.HasValidFingerprint()) return Hold("P8_CANONICAL_EVIDENCE_INVALID");
            if (!AEDPhase8OrdinalContractV1.TryCreate(evidence.PhaseOrdinal, networkOrdinalBeforeTransition, out _)) return Hold("P8_PHASE_ORDINAL_MISMATCH");
            if (pressure == null || !pressure.SourceComplete || pressure.MatchId != matchId || pressure.PhaseOrdinal != evidence.PhaseOrdinal || !pressure.Score.HasValue || pressure.Level == AEDPressureLevelV1.Unknown) return Hold("P8_PRESSURE_SOURCE_INVALID");
            if (roster == null || !roster.AllPlayersObserved) return Hold("P8_ROSTER_OBSERVATION_INCOMPLETE");
            if (!safeBoundary || stalkerUnsafe || playerUnsafe) return Hold("P8_UNSAFE_BOUNDARY");
            return new AEDPhase8GateResultV1(true, "P8_LOCAL_RESEARCH_ELIGIBLE");
        }
    }
}
