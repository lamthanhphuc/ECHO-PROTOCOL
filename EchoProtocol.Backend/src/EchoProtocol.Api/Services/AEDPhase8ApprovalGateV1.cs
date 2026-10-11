namespace EchoProtocol.Api.Services;

public sealed record AEDPhase8ApprovalEvidenceV1(bool CanonicalVerified, bool HistoricalSkillVerified, bool PressureVerified, bool SupplyLedgerVerified, bool MinionPopulationVerified, bool RosterCurrent, bool SnapshotCurrent, bool RevisionCurrent, bool SafeBoundary, bool AdaptiveNormal, bool HostAuthorized);
public sealed record AEDPhase8ApprovalGateResultV1(bool ApprovedForResearch, bool GameplayApproved, string ReasonCode);

public static class AEDPhase8ApprovalGateV1
{
    public static AEDPhase8ApprovalGateResultV1 Evaluate(AEDPhase8ApprovalEvidenceV1 evidence, string directorKind)
    {
        AEDPhase8ApprovalGateResultV1 Hold(string reason) => new(false, false, reason);
        if (evidence == null) return Hold("P8_EVIDENCE_MISSING");
        if (!evidence.HostAuthorized || !evidence.AdaptiveNormal || !evidence.RosterCurrent || !evidence.SnapshotCurrent || !evidence.RevisionCurrent) return Hold("P8_AUTHORITY_OR_REVISION_INVALID");
        if (!evidence.CanonicalVerified) return Hold("P8_CANONICAL_UNVERIFIED");
        if (!evidence.SafeBoundary) return Hold("P8_UNSAFE_BOUNDARY");
        if (directorKind is not ("TEAM_TOOL" or "MINION" or "STALKER")) return Hold("P8_DIRECTOR_UNKNOWN");
        if (!evidence.PressureVerified) return Hold("P8_PRESSURE_UNVERIFIED");
        if (!evidence.HistoricalSkillVerified) return Hold("P8_SKILL_UNVERIFIED");
        if (directorKind == "TEAM_TOOL" && !evidence.SupplyLedgerVerified) return Hold("P8_SUPPLY_UNVERIFIED");
        if (directorKind == "MINION" && !evidence.MinionPopulationVerified) return Hold("P8_MINION_STATE_UNVERIFIED");
        return new AEDPhase8ApprovalGateResultV1(true, false, "P8_RESEARCH_APPROVED");
    }
}
