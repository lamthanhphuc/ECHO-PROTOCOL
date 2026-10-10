using System;

namespace EchoProtocol.Api.Services;

public enum AEDPhase4CandidateV1
{
    Hold,
    RelieveCandidate,
    IncreaseCandidate
}

public sealed record AEDPhase4GateResultV1(
    AEDPhase4CandidateV1 Candidate,
    string Reason,
    string RuleVersion);

public static class AEDPhase4SafetyGateV1
{
    public const string Version = "AED_PHASE4_GATE_V1_RESEARCH";

    public static AEDPhase4GateResultV1 Evaluate(
        AEDTeamSkillSnapshotV1 team,
        string pressureLevel,
        bool pressureSourceVerified,
        bool rosterObserved,
        bool safeBoundary,
        bool backendMetricVerifierSupported)
    {
        AEDPhase4GateResultV1 Hold(string reason) =>
            new(AEDPhase4CandidateV1.Hold, reason, Version);

        if (team == null)
            return Hold("HOLD_TEAM_CONTEXT_MISSING");

        if (!safeBoundary)
            return Hold("HOLD_UNSAFE_BOUNDARY");

        if (!pressureSourceVerified)
            return Hold("HOLD_PRESSURE_SOURCE_UNVERIFIED");

        if (pressureLevel == "Critical")
        {
            return new AEDPhase4GateResultV1(
                AEDPhase4CandidateV1.RelieveCandidate,
                "HOST_VERIFIED_HIGH_PRESSURE",
                Version);
        }

        if (!rosterObserved)
            return Hold("HOLD_ROSTER_OBSERVATION_INCOMPLETE");

        if (pressureLevel != "Quiet")
            return Hold("HOLD_PRESSURE_NOT_LOW");

        if (!team.ConfidenceComplete || team.HasUncertainPlayer)
            return Hold("HOLD_TEAM_CONFIDENCE_INSUFFICIENT");

        if (!team.Weakest.HasValue || team.Weakest.Value < 0.70m)
            return Hold("HOLD_WEAKEST_PLAYER_PROTECTION");

        if (!backendMetricVerifierSupported)
            return Hold("HOLD_BACKEND_METRIC_VERIFIER_UNSUPPORTED");

        return new AEDPhase4GateResultV1(
            AEDPhase4CandidateV1.IncreaseCandidate,
            "VERIFIED_TEAM_SKILL_CANDIDATE",
            Version);
    }
}
