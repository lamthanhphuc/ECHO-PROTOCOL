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
                MatchId = id, CompletedEvidencePhaseOrdinal = 2, TargetPhaseOrdinal = 3, Difficulty = "Normal", ResolutionMode = "Adaptive",
                HostAuthority = true, SafeBoundary = true, FullRosterVerified = true, FullRosterObserved = true,
                EvidenceMatchId = id, EvidenceComplete = true,
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
        { var r = Request(); r.CompletedEvidencePhaseOrdinal = 1; var result = AEDStalkerDirectorV1.Evaluate(r); Assert.That(result.Intent, Is.EqualTo(AEDStalkerIntentV1.Hold)); Assert.That(result.ReasonCode, Is.EqualTo("HOLD_PHASE_TRANSITION_MISMATCH")); }
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
        [Test] public void ValidCompletedToNextPhaseTransitionIsAccepted()
        { var p = AEDStalkerDirectorV1.Evaluate(Request()); Assert.That(p.Intent, Is.EqualTo(AEDStalkerIntentV1.IncreasePressure)); Assert.That(p.CompletedEvidencePhaseOrdinal, Is.EqualTo(2u)); Assert.That(p.TargetPhaseOrdinal, Is.EqualTo(3u)); Assert.That(AEDStalkerDirectorV1.Validate(p), Is.True); }
        [Test] public void SameCompletedAndTargetPhaseIsRejected()
        { var r = Request(); r.TargetPhaseOrdinal = 2; var p = AEDStalkerDirectorV1.Evaluate(r); Assert.That(p.Intent, Is.EqualTo(AEDStalkerIntentV1.Hold)); Assert.That(p.ReasonCode, Is.EqualTo("HOLD_PHASE_TRANSITION_MISMATCH")); }
        [Test] public void SkippedPhaseTransitionIsRejected()
        { var r = Request(); r.TargetPhaseOrdinal = 4; Assert.That(AEDStalkerDirectorV1.Evaluate(r).Intent, Is.EqualTo(AEDStalkerIntentV1.Hold)); }
        [Test] public void ZeroCompletedPhaseIsRejected()
        { var r = Request(); r.CompletedEvidencePhaseOrdinal = 0; Assert.That(AEDStalkerDirectorV1.Evaluate(r).Intent, Is.EqualTo(AEDStalkerIntentV1.Hold)); }
        [Test] public void PhaseOrdinalOverflowIsRejected()
        { var r = Request(); r.CompletedEvidencePhaseOrdinal = uint.MaxValue; r.TargetPhaseOrdinal = 1; Assert.That(AEDStalkerDirectorV1.Evaluate(r).Intent, Is.EqualTo(AEDStalkerIntentV1.Hold)); }
        [Test] public void CrossMatchEvidenceCannotAdapt()
        { var r = Request(); r.EvidenceMatchId = Guid.NewGuid(); var p = AEDStalkerDirectorV1.Evaluate(r); Assert.That(p.Intent, Is.EqualTo(AEDStalkerIntentV1.Hold)); Assert.That(p.ReasonCode, Is.EqualTo("HOLD_STALE_OR_INCOMPLETE_EVIDENCE")); }
        [Test] public void ActiveChaseCannotRetuneStalker()
        { var r = Request(); r.ActiveChase = true; var p = AEDStalkerDirectorV1.Evaluate(r); Assert.That(p.Intent, Is.EqualTo(AEDStalkerIntentV1.Hold)); Assert.That(p.ReasonCode, Is.EqualTo("HOLD_ACTIVE_STALKER_ACTION")); }
        [Test] public void ActiveRecoverCannotRetuneStalker()
        { var r = Request(); r.ActiveRecover = true; Assert.That(AEDStalkerDirectorV1.Evaluate(r).Intent, Is.EqualTo(AEDStalkerIntentV1.Hold)); }
        [Test] public void ActiveSpecialCannotRetuneStalker()
        { var r = Request(); r.ActiveSpecialEncounter = true; Assert.That(AEDStalkerDirectorV1.Evaluate(r).Intent, Is.EqualTo(AEDStalkerIntentV1.Hold)); }
        [Test] public void UnsafeBoundaryCannotRetuneStalker()
        { var r = Request(); r.SafeBoundary = false; Assert.That(AEDStalkerDirectorV1.Evaluate(r).Intent, Is.EqualTo(AEDStalkerIntentV1.Hold)); }
        [Test] public void NonHostCannotRetuneStalker()
        { var r = Request(); r.HostAuthority = false; Assert.That(AEDStalkerDirectorV1.Evaluate(r).Intent, Is.EqualTo(AEDStalkerIntentV1.Hold)); }
        [Test] public void BackendUnverifiedSkillCannotIncreaseStalkerPressure()
        { var r = Request(); r.BackendMetricVerifierSupported = false; Assert.That(AEDStalkerDirectorV1.Evaluate(r).Intent, Is.EqualTo(AEDStalkerIntentV1.Hold)); }
        [Test] public void WeakestPlayerProtectionBlocksIncrease()
        { var r = Request(); r.WeakestPlayerSkill = 0.5m; Assert.That(AEDStalkerDirectorV1.Evaluate(r).Intent, Is.EqualTo(AEDStalkerIntentV1.Hold)); }
        [Test] public void UnknownCatalogKeyCannotAdapt()
        { var r = Request(); r.RequestedKey = (AEDv2Key)999; Assert.That(AEDStalkerDirectorV1.Evaluate(r).Intent, Is.EqualTo(AEDStalkerIntentV1.Hold)); }
        [Test] public void FairnessCannotOverrideActiveRecover()
        { var r = TargetRequest(Target(Guid.NewGuid())); r.ActiveRecover = true; Assert.That(AEDStalkerTargetFairnessV1.Evaluate(r).SuggestedUserId, Is.Null); }
        [Test] public void FairnessCannotOverrideActiveSpecial()
        { var r = TargetRequest(Target(Guid.NewGuid())); r.ActiveSpecialEncounter = true; Assert.That(AEDStalkerTargetFairnessV1.Evaluate(r).SuggestedUserId, Is.Null); }
        [Test] public void FairnessRejectsIllegalLineOfSight()
        { var target = Target(Guid.NewGuid()); target.LegalLineOfSight = false; Assert.That(AEDStalkerTargetFairnessV1.Evaluate(TargetRequest(target)).SuggestedUserId, Is.Null); }
        [Test] public void FairnessRejectsDownedAndEliminatedPlayers()
        { var downed = Target(Guid.NewGuid()); downed.Alive = false; downed.Downed = true; var eliminated = Target(Guid.NewGuid()); eliminated.Alive = false; eliminated.Eliminated = true; Assert.That(AEDStalkerTargetFairnessV1.Evaluate(TargetRequest(downed, eliminated)).SuggestedUserId, Is.Null); }
        [Test] public void FairnessRejectsUnverifiedTarget()
        { var target = Target(Guid.NewGuid()); target.HostVerified = false; Assert.That(AEDStalkerTargetFairnessV1.Evaluate(TargetRequest(target)).SuggestedUserId, Is.Null); }
        [Test] public void FairnessRejectsInvalidPressure()
        { var target = Target(Guid.NewGuid()); target.RecentPressure01 = double.NaN; Assert.That(AEDStalkerTargetFairnessV1.Evaluate(TargetRequest(target)).SuggestedUserId, Is.Null); }
        [Test] public void SpecialRejectsHighRecentTargetPressure()
        { var c = Special(); c.RecentTargetPressure01 = .9d; var p = AEDStalkerEncounterSafetyV1.EvaluateSpecial(c); Assert.That(p.EligibleForResearch, Is.False); Assert.That(p.CanApplyGameplay, Is.False); }
        [Test] public void SpecialRejectsLastStandingPlayer()
        { var c = Special(); c.OtherEligibleAlivePlayers = 0; Assert.That(AEDStalkerEncounterSafetyV1.EvaluateSpecial(c).EligibleForResearch, Is.False); }
        [Test] public void SpecialRejectsInvalidJumpDistance()
        { var c = Special(); c.DistanceFromTarget = 3d; Assert.That(AEDStalkerEncounterSafetyV1.EvaluateSpecial(c).EligibleForResearch, Is.False); }
        [Test] public void DoorRejectsMissingEscapeRoute()
        { var r = AEDStalkerEncounterSafetyV1.EvaluateDoor(new AEDStalkerDoorCandidateV1 { HostVerified = true, DoorClosed = true, DoorBreakable = true, SameZone = true, OutsideSafeRoom = true, NotProtectedSafeRoomDoor = true, LegalNavMeshRoute = true, PlayerEscapeRoutePreserved = false }); Assert.That(r.EligibleForResearch, Is.False); Assert.That(r.CanApplyGameplay, Is.False); }
    }
}
