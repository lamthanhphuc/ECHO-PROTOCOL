using System;
using System.Collections.Generic;
using System.Linq;

namespace EchoProtocol.AI.Common.AED
{
    public sealed class AEDStalkerTargetCandidateV1
    {
        public Guid UserId { get; set; } public bool HostVerified { get; set; } public bool InActiveSession { get; set; } public bool Connected { get; set; } public bool Alive { get; set; } public bool Downed { get; set; } public bool Eliminated { get; set; } public bool SameZone { get; set; } public bool OutsideSafeRoom { get; set; } public bool LegalLineOfSight { get; set; } public bool Reachable { get; set; } public bool RecentlyDownedOrAttacked { get; set; } public double RecentPressure01 { get; set; } public double SecondsSinceLastTargeted { get; set; }
    }
    public sealed class AEDStalkerTargetFairnessRequestV1 { public Guid MatchId { get; set; } public bool HostAuthority { get; set; } public bool AtTargetSelectionBoundary { get; set; } public bool ActiveChase { get; set; } public bool ActiveAttack { get; set; } public bool FullRosterVerified { get; set; } public IReadOnlyList<AEDStalkerTargetCandidateV1> Candidates { get; set; } }
    public sealed class AEDStalkerTargetFairnessResultV1 { public Guid? SuggestedUserId { get; } public string ReasonCode { get; } public bool ResearchOnly => true; public bool CanApplyGameplay => false; internal AEDStalkerTargetFairnessResultV1(Guid? id, string reason) { SuggestedUserId = id; ReasonCode = reason; } }
    public static class AEDStalkerTargetFairnessV1
    {
        public const double RecentTargetCooldownSeconds = 15d;
        public static AEDStalkerTargetFairnessResultV1 Evaluate(AEDStalkerTargetFairnessRequestV1 r)
        {
            AEDStalkerTargetFairnessResultV1 Hold(string reason) => new AEDStalkerTargetFairnessResultV1(null, reason);
            if (r == null || r.MatchId == Guid.Empty || !r.HostAuthority || !r.FullRosterVerified) return Hold("HOLD_UNVERIFIED_TARGET_CONTEXT");
            if (!r.AtTargetSelectionBoundary || r.ActiveChase || r.ActiveAttack) return Hold("HOLD_NOT_AT_TARGET_BOUNDARY");
            if (r.Candidates == null || r.Candidates.Count == 0 || r.Candidates.Count > 4) return Hold("HOLD_TARGET_CANDIDATES_MISSING");
            var source = r.Candidates.ToArray();
            if (source.Any(p => p == null || p.UserId == Guid.Empty || !p.HostVerified || double.IsNaN(p.RecentPressure01) || double.IsInfinity(p.RecentPressure01) || p.RecentPressure01 < 0 || p.RecentPressure01 > 1 || double.IsNaN(p.SecondsSinceLastTargeted) || double.IsInfinity(p.SecondsSinceLastTargeted) || p.SecondsSinceLastTargeted < 0)) return Hold("HOLD_INVALID_TARGET_EVIDENCE");
            if (source.Select(p => p.UserId).Distinct().Count() != source.Length) return Hold("HOLD_DUPLICATE_PLAYER");
            var eligible = source.Where(p => p.InActiveSession && p.Connected && p.Alive && !p.Downed && !p.Eliminated && p.SameZone && p.OutsideSafeRoom && p.LegalLineOfSight && p.Reachable && !p.RecentlyDownedOrAttacked && p.SecondsSinceLastTargeted >= RecentTargetCooldownSeconds).OrderBy(p => p.RecentPressure01).ThenByDescending(p => p.SecondsSinceLastTargeted).ThenBy(p => p.UserId).ToArray();
            return eligible.Length == 0 ? Hold("HOLD_NO_FAIR_TARGET_OPPORTUNITY") : new AEDStalkerTargetFairnessResultV1(eligible[0].UserId, "FAIR_TARGET_RECOMMENDATION");
        }
    }
}
