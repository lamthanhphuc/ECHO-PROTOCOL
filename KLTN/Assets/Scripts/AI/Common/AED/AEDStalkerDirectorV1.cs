using System;
using System.Collections.Generic;
using System.Linq;

namespace EchoProtocol.AI.Common.AED
{
    public enum AEDStalkerIntentV1 { Hold, Relieve, IncreasePressure }

    public sealed class AEDStalkerDirectorRequestV1
    {
        public Guid MatchId { get; set; } public uint PhaseOrdinal { get; set; }
        public string Difficulty { get; set; } public string ResolutionMode { get; set; }
        public bool HostAuthority { get; set; } public bool SafeBoundary { get; set; }
        public bool FullRosterVerified { get; set; } public bool FullRosterObserved { get; set; }
        public Guid EvidenceMatchId { get; set; } public uint EvidencePhaseOrdinal { get; set; }
        public bool EvidenceComplete { get; set; } public string EvidenceFingerprint { get; set; }
        public string ComparisonContextKey { get; set; } public bool PressureSourceVerified { get; set; }
        public AEDPressureLevelV1 PressureLevel { get; set; } public int RosterSize { get; set; }
        public int AlivePlayers { get; set; } public int DownedPlayers { get; set; }
        public int EliminatedPlayers { get; set; } public bool AnyPlayerStruggling { get; set; }
        public bool ActiveAttack { get; set; } public bool ActiveSpecialEncounter { get; set; }
        public bool BackendMetricVerifierSupported { get; set; } public bool TeamConfidenceComplete { get; set; }
        public decimal? WeakestPlayerSkill { get; set; } public double SecondsSinceLastChange { get; set; }
        public AEDv2Key RequestedKey { get; set; }
    }

    public sealed class AEDStalkerProposalV1
    {
        public const string Version = "AED_STALKER_DIRECTOR_V1_RESEARCH";
        public Guid MatchId { get; } public uint PhaseOrdinal { get; }
        public AEDStalkerIntentV1 Intent { get; } public AEDv2Key? ChangedKey { get; }
        public double? TargetValue { get; } public AEDv2Plan Plan { get; }
        public string ReasonCode { get; } public string EvidenceFingerprint { get; }
        public bool ResearchOnly => true; public bool CanApplyGameplay => false;
        internal AEDStalkerProposalV1(AEDStalkerDirectorRequestV1 r, AEDStalkerIntentV1 intent, AEDv2Key? key, AEDv2Plan plan, string reason)
        { MatchId = r.MatchId; PhaseOrdinal = r.PhaseOrdinal; Intent = intent; ChangedKey = key; Plan = plan; TargetValue = key.HasValue ? (double?)plan.Get(key.Value) : null; ReasonCode = reason; EvidenceFingerprint = r.EvidenceFingerprint; }
    }

    public static class AEDStalkerDirectorV1
    {
        public const decimal MinimumWeakestSkill = 0.80m; public const double IncreaseCooldownSeconds = 45d;
        private static readonly IReadOnlyList<AEDv2Key> Keys = Array.AsReadOnly(new[] {
            AEDv2Key.DetectionAcquireSeconds, AEDv2Key.DetectionForgetSeconds, AEDv2Key.HearingMultiplier,
            AEDv2Key.ChaseSpeed, AEDv2Key.PatrolSpeed, AEDv2Key.SearchSeconds, AEDv2Key.SeekPlayersAfterSeconds,
            AEDv2Key.CoreCarrierAfterSeconds, AEDv2Key.DoorBreakSeconds, AEDv2Key.SpecialCooldownSeconds,
            AEDv2Key.PostChaseCooldownSeconds, AEDv2Key.PostAttackCooldownSeconds, AEDv2Key.SameRoomCooldownSeconds,
            AEDv2Key.JumpEntryReuseSeconds, AEDv2Key.PostSpecialCooldownSeconds });
        public static IReadOnlyList<AEDv2Key> Whitelist => Keys;
        public static bool IsWhitelisted(AEDv2Key key) => Keys.Contains(key);

        public static AEDStalkerProposalV1 Evaluate(AEDStalkerDirectorRequestV1 r)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            AEDStalkerProposalV1 Hold(string reason) => new AEDStalkerProposalV1(r, AEDStalkerIntentV1.Hold, null, AEDv2Plan.Normal(), reason);
            if (r.MatchId == Guid.Empty || r.PhaseOrdinal == 0) return Hold("HOLD_INVALID_MATCH");
            if (r.Difficulty != "Normal" || r.ResolutionMode != "Adaptive") return Hold("HOLD_FIXED_OR_NON_NORMAL");
            if (!r.HostAuthority || !r.SafeBoundary) return Hold("HOLD_UNSAFE_AUTHORITY_OR_BOUNDARY");
            if (r.EvidenceMatchId != r.MatchId || r.EvidencePhaseOrdinal != r.PhaseOrdinal || !r.EvidenceComplete || string.IsNullOrWhiteSpace(r.EvidenceFingerprint)) return Hold("HOLD_STALE_OR_INCOMPLETE_EVIDENCE");
            if (r.RosterSize < 1 || r.RosterSize > 4 || r.AlivePlayers < 0 || r.DownedPlayers < 0 || r.EliminatedPlayers < 0 || r.AlivePlayers + r.DownedPlayers + r.EliminatedPlayers != r.RosterSize) return Hold("HOLD_INVALID_ROSTER");
            if (!r.FullRosterVerified || !r.FullRosterObserved) return Hold("HOLD_ROSTER_NOT_VERIFIED");
            if (!r.PressureSourceVerified || !Enum.IsDefined(typeof(AEDPressureLevelV1), r.PressureLevel) || r.PressureLevel == AEDPressureLevelV1.Unknown) return Hold("HOLD_PRESSURE_UNVERIFIED");
            if (r.ActiveAttack || r.ActiveSpecialEncounter) return Hold("HOLD_ACTIVE_STALKER_ACTION");
            if (!IsWhitelisted(r.RequestedKey)) return Hold("HOLD_KEY_NOT_WHITELISTED");
            bool relief = r.PressureLevel == AEDPressureLevelV1.Critical || r.DownedPlayers > 0 || r.EliminatedPlayers > 0 || r.AlivePlayers <= 1 || r.AnyPlayerStruggling;
            if (!relief)
            {
                if (r.PressureLevel != AEDPressureLevelV1.Quiet) return Hold("HOLD_PRESSURE_NOT_LOW");
                if (double.IsNaN(r.SecondsSinceLastChange) || double.IsInfinity(r.SecondsSinceLastChange) || r.SecondsSinceLastChange < IncreaseCooldownSeconds) return Hold("HOLD_PACING_COOLDOWN");
                if (!r.BackendMetricVerifierSupported || !r.TeamConfidenceComplete || !r.WeakestPlayerSkill.HasValue || r.WeakestPlayerSkill.Value < MinimumWeakestSkill || r.WeakestPlayerSkill.Value > 1m || string.IsNullOrWhiteSpace(r.ComparisonContextKey)) return Hold("HOLD_SKILL_CONFIDENCE_INSUFFICIENT");
            }
            var intent = relief ? AdaptationIntent.Relieve : AdaptationIntent.IncreasePressure;
            var spec = AEDv2Catalog.Find(r.RequestedKey);
            if (spec.PreMatchOnly || !spec.HasTarget(intent)) return Hold("HOLD_KEY_OR_DIRECTION_UNSUPPORTED");
            var target = spec.Target(intent);
            if (r.RequestedKey == AEDv2Key.SpecialCooldownSeconds && target < 300d) return Hold("HOLD_SPECIAL_COOLDOWN_FLOOR");
            var plan = AEDv2Plan.Normal().With(r.RequestedKey, target);
            if (!plan.TrySingleBoundedChange(out var changed) || changed != r.RequestedKey || !plan.IsBounded()) return Hold("HOLD_BOUND_REJECTED");
            return new AEDStalkerProposalV1(r, relief ? AEDStalkerIntentV1.Relieve : AEDStalkerIntentV1.IncreasePressure, changed, plan, relief ? "STALKER_RELIEF_CANDIDATE" : "STALKER_PRESSURE_CANDIDATE");
        }

        public static bool Validate(AEDStalkerProposalV1 p)
        {
            if (p == null || p.MatchId == Guid.Empty || p.PhaseOrdinal == 0 || p.Plan == null || !p.Plan.IsBounded() || !p.Plan.TrySingleBoundedChange(out var changed) || !Enum.IsDefined(typeof(AEDStalkerIntentV1), p.Intent) || p.CanApplyGameplay) return false;
            if (p.Intent == AEDStalkerIntentV1.Hold) return !p.ChangedKey.HasValue && !p.TargetValue.HasValue && p.Plan.Fingerprint() == AEDv2Plan.Normal().Fingerprint();
            if (!p.ChangedKey.HasValue || !p.TargetValue.HasValue || !IsWhitelisted(p.ChangedKey.Value) || changed != p.ChangedKey.Value || string.IsNullOrWhiteSpace(p.EvidenceFingerprint)) return false;
            var spec = AEDv2Catalog.Find(p.ChangedKey.Value); var direction = p.Intent == AEDStalkerIntentV1.Relieve ? AdaptationIntent.Relieve : AdaptationIntent.IncreasePressure;
            return !spec.PreMatchOnly && spec.HasTarget(direction) && p.TargetValue.Value == spec.Target(direction) && p.Plan.Get(p.ChangedKey.Value) == spec.Target(direction);
        }
    }
}
