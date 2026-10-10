using System;

namespace EchoProtocol.AI.Common.AED
{
    public enum AEDMinionIntentV1 { Hold, Relieve, IncreasePressure }

    public sealed class AEDMinionDirectorRequestV1
    {
        public Guid MatchId { get; set; }
        public int Zone { get; set; }
        public string Difficulty { get; set; }
        public string ResolutionMode { get; set; }
        public int BaselineCap { get; set; }
        public int CurrentPopulation { get; set; }
        public int RosterSize { get; set; }
        public int AlivePlayers { get; set; }
        public int DownedPlayers { get; set; }
        public int EliminatedPlayers { get; set; }
        public int ActiveEncounters { get; set; }
        public bool HostAuthority { get; set; }
        public bool SafeBoundary { get; set; }
        public bool FullRosterVerified { get; set; }
        public bool FullRosterObserved { get; set; }
        public bool PressureSourceVerified { get; set; }
        public AEDPressureLevelV1 PressureLevel { get; set; }
        public bool BackendMetricVerifierSupported { get; set; }
        public bool TeamConfidenceComplete { get; set; }
        public decimal? WeakestPlayerSkill { get; set; }
        public bool AnyPlayerStruggling { get; set; }
        public double SecondsSinceLastDirectorChange { get; set; }
        public string ComparisonContextKey { get; set; }
        public string EvidenceFingerprint { get; set; }
    }

    public sealed class AEDMinionBehaviorCandidateV1
    {
        public float VisionRange { get; }
        public float TrackSpeed { get; }
        public float HarassSpeed { get; }
        public float AttackCooldownSeconds { get; }
        public float AlertCooldownSeconds { get; }

        public AEDMinionBehaviorCandidateV1(float vision, float track, float harass,
            float attackCooldown, float alertCooldown)
        {
            VisionRange = vision; TrackSpeed = track; HarassSpeed = harass;
            AttackCooldownSeconds = attackCooldown; AlertCooldownSeconds = alertCooldown;
        }
    }

    public sealed class AEDMinionProposalV1
    {
        public const string Version = "AED_MINION_DIRECTOR_V1_RESEARCH";
        public Guid MatchId { get; }
        public int Zone { get; }
        public int BaselineCap { get; }
        public int CurrentPopulation { get; }
        public int TargetCap { get; }
        public int AdditionalSpawnSlots => Math.Max(0, TargetCap - CurrentPopulation);
        public AEDMinionIntentV1 Intent { get; }
        public AEDMinionBehaviorCandidateV1 Behavior { get; }
        public string ReasonCode { get; }
        public string EvidenceFingerprint { get; }
        public string ComparisonContextKey { get; }
        public bool ResearchOnly => true;
        public bool CanApplyGameplay => false;
        public bool AllowForcedDespawn => false;

        internal AEDMinionProposalV1(AEDMinionDirectorRequestV1 request,
            AEDMinionIntentV1 intent, int targetCap,
            AEDMinionBehaviorCandidateV1 behavior, string reason)
        {
            MatchId = request.MatchId; Zone = request.Zone;
            BaselineCap = request.BaselineCap; CurrentPopulation = request.CurrentPopulation;
            TargetCap = targetCap; Behavior = behavior; Intent = intent;
            ReasonCode = reason; EvidenceFingerprint = request.EvidenceFingerprint;
            ComparisonContextKey = request.ComparisonContextKey;
        }
    }

    public static class AEDMinionDirectorV1
    {
        public const int MaxResearchCap = 3;
        public const int MaxIncreasePerZone = 1;
        public const double IncreaseCooldownSeconds = 45d;
        public const decimal MinimumWeakestSkill = 0.80m;

        public static int NormalBaselineCap(int zone) => zone == 1 ? 1 : zone == 2 ? 2 : zone == 3 ? 1 : -1;

        private static AEDMinionBehaviorCandidateV1 Behavior(AEDMinionIntentV1 intent)
        {
            switch (intent)
            {
                case AEDMinionIntentV1.Relieve: return new AEDMinionBehaviorCandidateV1(18f, 7.5f, 9f, 0.85f, 5f);
                case AEDMinionIntentV1.IncreasePressure: return new AEDMinionBehaviorCandidateV1(22f, 8.5f, 10.5f, 0.60f, 3.5f);
                default: return new AEDMinionBehaviorCandidateV1(20f, 8f, 10f, 0.65f, 4f);
            }
        }

        public static AEDMinionProposalV1 Evaluate(AEDMinionDirectorRequestV1 request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            AEDMinionProposalV1 Create(AEDMinionIntentV1 intent, int cap, string reason) =>
                new AEDMinionProposalV1(request, intent, cap, Behavior(intent), reason);
            AEDMinionProposalV1 Hold(string reason) => Create(AEDMinionIntentV1.Hold, Math.Max(0, request.BaselineCap), reason);

            if (request.MatchId == Guid.Empty || NormalBaselineCap(request.Zone) < 0) return Hold("HOLD_INVALID_MATCH_OR_ZONE");
            if (request.Difficulty != "Normal" || request.ResolutionMode != "Adaptive") return Hold("HOLD_FIXED_OR_NON_NORMAL");
            if (!request.HostAuthority || !request.SafeBoundary) return Hold("HOLD_UNSAFE_AUTHORITY_OR_BOUNDARY");
            if (request.BaselineCap != NormalBaselineCap(request.Zone)) return Hold("HOLD_BASELINE_CAP_MISMATCH");
            if (request.CurrentPopulation < 0 || request.RosterSize < 1 || request.RosterSize > 4 ||
                request.AlivePlayers < 0 || request.DownedPlayers < 0 || request.EliminatedPlayers < 0 ||
                request.ActiveEncounters < 0 || request.AlivePlayers + request.DownedPlayers + request.EliminatedPlayers != request.RosterSize)
                return Hold("HOLD_INVALID_POPULATION_OR_ROSTER");
            if (!request.FullRosterVerified || !request.FullRosterObserved) return Hold("HOLD_ROSTER_UNVERIFIED");
            if (!request.PressureSourceVerified || request.PressureLevel == AEDPressureLevelV1.Unknown) return Hold("HOLD_PRESSURE_UNVERIFIED");

            bool mustRelieve = request.PressureLevel == AEDPressureLevelV1.Critical || request.DownedPlayers > 0 ||
                request.EliminatedPlayers > 0 || request.AlivePlayers <= 1 || request.AnyPlayerStruggling;
            if (mustRelieve) return Create(AEDMinionIntentV1.Relieve, Math.Max(0, request.BaselineCap - 1), "RELIEVE_MINION_PRESSURE_CANDIDATE");
            if (request.PressureLevel != AEDPressureLevelV1.Quiet) return Hold("HOLD_PRESSURE_NOT_LOW");
            if (request.ActiveEncounters > 0) return Hold("HOLD_ACTIVE_ENCOUNTER");
            if (request.CurrentPopulation != request.BaselineCap) return Hold("HOLD_WAIT_BASELINE_POPULATION");
            if (double.IsNaN(request.SecondsSinceLastDirectorChange) || double.IsInfinity(request.SecondsSinceLastDirectorChange) || request.SecondsSinceLastDirectorChange < IncreaseCooldownSeconds)
                return Hold("HOLD_PACING_COOLDOWN");
            if (!request.BackendMetricVerifierSupported || !request.TeamConfidenceComplete || !request.WeakestPlayerSkill.HasValue ||
                request.WeakestPlayerSkill.Value < MinimumWeakestSkill || request.WeakestPlayerSkill.Value > 1m ||
                string.IsNullOrWhiteSpace(request.ComparisonContextKey) || string.IsNullOrWhiteSpace(request.EvidenceFingerprint))
                return Hold("HOLD_SKILL_CONFIDENCE_INSUFFICIENT");
            return Create(AEDMinionIntentV1.IncreasePressure, Math.Min(MaxResearchCap, request.BaselineCap + MaxIncreasePerZone), "VERIFIED_STRONG_TEAM_MINION_CANDIDATE");
        }

        public static bool Validate(AEDMinionProposalV1 proposal)
        {
            if (proposal == null || proposal.MatchId == Guid.Empty || NormalBaselineCap(proposal.Zone) < 0 ||
                proposal.BaselineCap != NormalBaselineCap(proposal.Zone) || proposal.CurrentPopulation < 0 ||
                proposal.TargetCap < 0 || proposal.TargetCap > MaxResearchCap || string.IsNullOrWhiteSpace(proposal.ReasonCode) ||
                proposal.Behavior == null || !Enum.IsDefined(typeof(AEDMinionIntentV1), proposal.Intent)) return false;
            int expectedCap = proposal.BaselineCap;
            if (proposal.Intent == AEDMinionIntentV1.Relieve) expectedCap = Math.Max(0, expectedCap - 1);
            if (proposal.Intent == AEDMinionIntentV1.IncreasePressure) expectedCap = Math.Min(MaxResearchCap, expectedCap + MaxIncreasePerZone);
            var expected = Behavior(proposal.Intent); var actual = proposal.Behavior;
            return proposal.TargetCap == expectedCap && actual.VisionRange == expected.VisionRange && actual.TrackSpeed == expected.TrackSpeed &&
                actual.HarassSpeed == expected.HarassSpeed && actual.AttackCooldownSeconds == expected.AttackCooldownSeconds &&
                actual.AlertCooldownSeconds == expected.AlertCooldownSeconds && !proposal.CanApplyGameplay && !proposal.AllowForcedDespawn;
        }
    }
}
