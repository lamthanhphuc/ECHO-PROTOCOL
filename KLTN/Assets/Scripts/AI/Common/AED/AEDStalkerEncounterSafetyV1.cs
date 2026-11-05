using System;

namespace EchoProtocol.AI.Common.AED
{
    public sealed class AEDStalkerSpecialCandidateV1 { public Guid MatchId { get; set; } public uint PhaseOrdinal { get; set; } public bool HostVerified { get; set; } public bool SafeBoundary { get; set; } public bool HasDownedTrigger { get; set; } public bool CooldownReady { get; set; } public bool ActiveSpecialEncounter { get; set; } public int OtherEligibleAlivePlayers { get; set; } public bool SameZone { get; set; } public bool OutsideSafeRoom { get; set; } public bool NavMeshPathComplete { get; set; } public bool EscapeRouteVerified { get; set; } public double DistanceFromTarget { get; set; } public double RecentTargetPressure01 { get; set; } }
    public sealed class AEDStalkerDoorCandidateV1 { public bool HostVerified { get; set; } public bool DoorClosed { get; set; } public bool DoorBreakable { get; set; } public bool SameZone { get; set; } public bool OutsideSafeRoom { get; set; } public bool NotProtectedSafeRoomDoor { get; set; } public bool LegalNavMeshRoute { get; set; } public bool PlayerEscapeRoutePreserved { get; set; } }
    public sealed class AEDStalkerSafetyResultV1 { public bool EligibleForResearch { get; } public string ReasonCode { get; } public bool CanApplyGameplay => false; internal AEDStalkerSafetyResultV1(bool eligible, string reason) { EligibleForResearch = eligible; ReasonCode = reason; } }
    public static class AEDStalkerEncounterSafetyV1
    {
        public const int MinimumOtherAlivePlayers = 2; public const double MinimumJumpDistance = 7d; public const double MaximumJumpDistance = 10d; public const double MaximumRecentTargetPressure01 = 0.60d;
        public static AEDStalkerSafetyResultV1 EvaluateSpecial(AEDStalkerSpecialCandidateV1 c)
        {
            if (c == null || c.MatchId == Guid.Empty || c.PhaseOrdinal == 0 || !c.HostVerified || !c.SafeBoundary) return new AEDStalkerSafetyResultV1(false, "HOLD_SPECIAL_SOURCE_UNVERIFIED");
            if (!c.HasDownedTrigger || !c.CooldownReady || c.ActiveSpecialEncounter || c.OtherEligibleAlivePlayers < MinimumOtherAlivePlayers) return new AEDStalkerSafetyResultV1(false, "HOLD_SPECIAL_TRIGGER_OR_PACING");
            if (!c.SameZone || !c.OutsideSafeRoom || !c.NavMeshPathComplete || !c.EscapeRouteVerified || double.IsNaN(c.DistanceFromTarget) || double.IsInfinity(c.DistanceFromTarget) || c.DistanceFromTarget < MinimumJumpDistance || c.DistanceFromTarget > MaximumJumpDistance) return new AEDStalkerSafetyResultV1(false, "HOLD_SPECIAL_PLACEMENT_UNSAFE");
            if (double.IsNaN(c.RecentTargetPressure01) || double.IsInfinity(c.RecentTargetPressure01) || c.RecentTargetPressure01 < 0 || c.RecentTargetPressure01 > MaximumRecentTargetPressure01) return new AEDStalkerSafetyResultV1(false, "HOLD_SPECIAL_TARGET_PRESSURE");
            return new AEDStalkerSafetyResultV1(true, "SPECIAL_RESEARCH_CANDIDATE");
        }
        public static AEDStalkerSafetyResultV1 EvaluateDoor(AEDStalkerDoorCandidateV1 c)
        {
            if (c == null || !c.HostVerified) return new AEDStalkerSafetyResultV1(false, "HOLD_DOOR_SOURCE_UNVERIFIED");
            if (!c.DoorClosed || !c.DoorBreakable || !c.SameZone || !c.OutsideSafeRoom || !c.NotProtectedSafeRoomDoor) return new AEDStalkerSafetyResultV1(false, "HOLD_DOOR_PROTECTED_OR_INVALID");
            if (!c.LegalNavMeshRoute || !c.PlayerEscapeRoutePreserved) return new AEDStalkerSafetyResultV1(false, "HOLD_DOOR_ROUTE_UNSAFE");
            return new AEDStalkerSafetyResultV1(true, "DOOR_RESEARCH_CANDIDATE");
        }
    }
}
