using System;
using System.Linq;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;

namespace EchoProtocol.AI.Common.Tests
{
    public sealed class AEDStalkerDirectorV1Tests
    {
        private static AEDStalkerDirectorRequestV1 Request(AEDv2Key key = AEDv2Key.DetectionAcquireSeconds)
        {
            var id = Guid.NewGuid();
            return new AEDStalkerDirectorRequestV1
            {
                MatchId = id, PhaseOrdinal = 2, Difficulty = "Normal", ResolutionMode = "Adaptive",
                HostAuthority = true, SafeBoundary = true, FullRosterVerified = true, FullRosterObserved = true,
                EvidenceMatchId = id, EvidencePhaseOrdinal = 2, EvidenceComplete = true,
                EvidenceFingerprint = "verified", ComparisonContextKey = "NORMAL:CONFIG_V1",
                PressureSourceVerified = true, PressureLevel = AEDPressureLevelV1.Quiet,
                RosterSize = 2, AlivePlayers = 2, BackendMetricVerifierSupported = true,
                TeamConfidenceComplete = true, WeakestPlayerSkill = 0.85m,
                SecondsSinceLastChange = 60, RequestedKey = key
            };
        }

        [Test] public void FixedModeNeverChangesStalker()
        { var p = AEDStalkerDirectorV1.Evaluate(Request()); Assert.That(p.CanApplyGameplay, Is.False); Assert.That(p.Intent, Is.EqualTo(AEDStalkerIntentV1.IncreasePressure)); var r = Request(); r.ResolutionMode = "Fixed"; Assert.That(AEDStalkerDirectorV1.Evaluate(r).Intent, Is.EqualTo(AEDStalkerIntentV1.Hold)); }
        [Test] public void StrongTeamChangesOnlyOneParameter()
        { var p = AEDStalkerDirectorV1.Evaluate(Request()); Assert.That(p.ChangedKey, Is.EqualTo(AEDv2Key.DetectionAcquireSeconds)); Assert.That(p.TargetValue, Is.EqualTo(1d)); Assert.That(AEDStalkerDirectorV1.Validate(p), Is.True); }
        [Test] public void CriticalPressureProducesRelief()
        { var r = Request(AEDv2Key.ChaseSpeed); r.PressureLevel = AEDPressureLevelV1.Critical; var p = AEDStalkerDirectorV1.Evaluate(r); Assert.That(p.Intent, Is.EqualTo(AEDStalkerIntentV1.Relieve)); Assert.That(p.TargetValue, Is.EqualTo(7d)); }
        [Test] public void StalePhaseEvidenceCannotAdapt()
        { var r = Request(); r.EvidencePhaseOrdinal = 1; Assert.That(AEDStalkerDirectorV1.Evaluate(r).Intent, Is.EqualTo(AEDStalkerIntentV1.Hold)); }
        [Test] public void ActiveAttackCannotRetuneStalker()
        { var r = Request(); r.ActiveAttack = true; Assert.That(AEDStalkerDirectorV1.Evaluate(r).Intent, Is.EqualTo(AEDStalkerIntentV1.Hold)); }
        [Test] public void AllWhitelistedKeysStayWithinCatalog()
        { foreach (var key in AEDStalkerDirectorV1.Whitelist) Assert.That(AEDStalkerDirectorV1.Validate(AEDStalkerDirectorV1.Evaluate(Request(key))), Is.True, key.ToString()); }

        private static AEDStalkerTargetCandidateV1 Target(Guid id, double pressure = 0.1, double since = 60)
        { return new AEDStalkerTargetCandidateV1 { UserId = id, HostVerified = true, InActiveSession = true, Connected = true, Alive = true, SameZone = true, OutsideSafeRoom = true, LegalLineOfSight = true, Reachable = true, RecentPressure01 = pressure, SecondsSinceLastTargeted = since }; }
        private static AEDStalkerTargetFairnessRequestV1 TargetRequest(params AEDStalkerTargetCandidateV1[] c) => new AEDStalkerTargetFairnessRequestV1 { MatchId = Guid.NewGuid(), HostAuthority = true, AtTargetSelectionBoundary = true, FullRosterVerified = true, Candidates = c };
        [Test] public void FairnessPrefersLowerRecentPressure()
        { var a = Guid.NewGuid(); var b = Guid.NewGuid(); Assert.That(AEDStalkerTargetFairnessV1.Evaluate(TargetRequest(Target(a, .8), Target(b, .1))).SuggestedUserId, Is.EqualTo(b)); }
        [Test] public void RecentlyTargetedPlayerIsProtected()
        { var a = Guid.NewGuid(); var b = Guid.NewGuid(); Assert.That(AEDStalkerTargetFairnessV1.Evaluate(TargetRequest(Target(a, .1, 2), Target(b))).SuggestedUserId, Is.EqualTo(b)); }
        [Test] public void SafeRoomTargetIsRejected()
        { var p = Target(Guid.NewGuid()); p.OutsideSafeRoom = false; Assert.That(AEDStalkerTargetFairnessV1.Evaluate(TargetRequest(p)).SuggestedUserId, Is.Null); }
        [Test] public void DuplicateTargetIdentityFailsClosed()
        { var id = Guid.NewGuid(); Assert.That(AEDStalkerTargetFairnessV1.Evaluate(TargetRequest(Target(id), Target(id))).SuggestedUserId, Is.Null); }

        private static AEDStalkerSpecialCandidateV1 Special() => new AEDStalkerSpecialCandidateV1 { MatchId = Guid.NewGuid(), PhaseOrdinal = 2, HostVerified = true, SafeBoundary = true, HasDownedTrigger = true, CooldownReady = true, OtherEligibleAlivePlayers = 2, SameZone = true, OutsideSafeRoom = true, NavMeshPathComplete = true, EscapeRouteVerified = true, DistanceFromTarget = 8, RecentTargetPressure01 = .1 };
        [Test] public void SpecialRequiresEscapeRoute()
        { var c = Special(); c.EscapeRouteVerified = false; Assert.That(AEDStalkerEncounterSafetyV1.EvaluateSpecial(c).EligibleForResearch, Is.False); }
        [Test] public void ValidSpecialCannotApplyGameplay()
        { var r = AEDStalkerEncounterSafetyV1.EvaluateSpecial(Special()); Assert.That(r.EligibleForResearch, Is.True); Assert.That(r.CanApplyGameplay, Is.False); }
        [Test] public void SafeRoomDoorCannotBeBroken()
        { var r = AEDStalkerEncounterSafetyV1.EvaluateDoor(new AEDStalkerDoorCandidateV1 { HostVerified = true, DoorClosed = true, DoorBreakable = true, SameZone = true, OutsideSafeRoom = true, NotProtectedSafeRoomDoor = false, LegalNavMeshRoute = true, PlayerEscapeRoutePreserved = true }); Assert.That(r.EligibleForResearch, Is.False); }
    }
}
