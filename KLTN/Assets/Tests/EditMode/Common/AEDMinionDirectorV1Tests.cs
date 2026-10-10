using System;
using System.Linq;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;

namespace EchoProtocol.AI.Common.Tests
{
    public sealed class AEDMinionDirectorV1Tests
    {
        private static AEDMinionDirectorRequestV1 Request(bool adaptive = true)
        {
            return new AEDMinionDirectorRequestV1
            {
                MatchId = Guid.NewGuid(), Zone = 2, Difficulty = "Normal",
                ResolutionMode = adaptive ? "Adaptive" : "Fixed", BaselineCap = 2,
                CurrentPopulation = 2, RosterSize = 2, AlivePlayers = 2,
                PressureSourceVerified = true, PressureLevel = AEDPressureLevelV1.Quiet,
                HostAuthority = true, SafeBoundary = true, FullRosterVerified = true,
                FullRosterObserved = true, BackendMetricVerifierSupported = true,
                TeamConfidenceComplete = true, WeakestPlayerSkill = 0.85m,
                SecondsSinceLastDirectorChange = 60, ComparisonContextKey = "NORMAL:CONFIG_V1",
                EvidenceFingerprint = "verified-fingerprint"
            };
        }

        [Test] public void FixedNeverChangesMinionCap()
        { var p = AEDMinionDirectorV1.Evaluate(Request(false)); Assert.That(p.Intent, Is.EqualTo(AEDMinionIntentV1.Hold)); Assert.That(p.TargetCap, Is.EqualTo(2)); }

        [Test] public void StrongVerifiedTeamCanProposeOneExtraMinion()
        { var p = AEDMinionDirectorV1.Evaluate(Request()); Assert.That(p.Intent, Is.EqualTo(AEDMinionIntentV1.IncreasePressure)); Assert.That(p.TargetCap, Is.EqualTo(3)); Assert.That(AEDMinionDirectorV1.Validate(p), Is.True); }

        [Test] public void CriticalPressureRelievesPopulation()
        { var r = Request(); r.PressureLevel = AEDPressureLevelV1.Critical; var p = AEDMinionDirectorV1.Evaluate(r); Assert.That(p.Intent, Is.EqualTo(AEDMinionIntentV1.Relieve)); Assert.That(p.TargetCap, Is.EqualTo(1)); }

        [Test] public void UnverifiedSkillCannotIncreasePressure()
        { var r = Request(); r.BackendMetricVerifierSupported = false; Assert.That(AEDMinionDirectorV1.Evaluate(r).Intent, Is.EqualTo(AEDMinionIntentV1.Hold)); }

        [Test] public void ActiveEncounterBlocksIncrease()
        { var r = Request(); r.ActiveEncounters = 1; Assert.That(AEDMinionDirectorV1.Evaluate(r).Intent, Is.EqualTo(AEDMinionIntentV1.Hold)); }

        [Test] public void CandidateChangesOnlyWhitelistedParameters()
        { var p = AEDMinionDirectorV1.Evaluate(Request()); Assert.That(p.Behavior.VisionRange, Is.EqualTo(22f)); Assert.That(p.Behavior.TrackSpeed, Is.EqualTo(8.5f)); Assert.That(p.CanApplyGameplay, Is.False); }

        private static AEDMinionSpawnCandidateV1 Point(string id, double playerDistance = 25, bool safeRoom = false, bool complete = true)
        { return new AEDMinionSpawnCandidateV1 { PointId = id, Zone = 2, RoomId = 1, HostVerified = true, NavMeshSampled = true, CompletePathFromStalker = complete, SafeRoomClassificationVerified = true, InsideSafeRoom = safeRoom, DistanceFromStalker = 8, DistanceFromNearestAlivePlayer = playerDistance, DistanceFromZoneEntry = 35, DistanceFromNearestExistingMinion = 12 }; }

        [Test] public void SpawnCandidateKeepsMinimumPlayerDistance()
        { var p = AEDMinionDirectorV1.Evaluate(Request()); Assert.That(AEDMinionPlacementV1.TryPlan(p, new[] { Point("near", 10), Point("safe") }, out var receipts), Is.True); Assert.That(receipts.Single().PointId, Is.EqualTo("safe")); }

        [Test] public void SafeRoomCannotBeUsedForMinionSpawn()
        { var p = AEDMinionDirectorV1.Evaluate(Request()); Assert.That(AEDMinionPlacementV1.TryPlan(p, new[] { Point("safe-room", safeRoom: true) }, out var receipts), Is.False); Assert.That(receipts, Is.Empty); }

        [Test] public void PartialNavMeshPathCannotBecomeSpawn()
        { var p = AEDMinionDirectorV1.Evaluate(Request()); Assert.That(AEDMinionPlacementV1.TryPlan(p, new[] { Point("partial", complete: false) }, out var receipts), Is.False); Assert.That(receipts, Is.Empty); }

        [Test] public void ResearchProposalCannotCommitGameplay()
        { var p = AEDMinionDirectorV1.Evaluate(Request()); Assert.That(p.ResearchOnly, Is.True); Assert.That(p.CanApplyGameplay, Is.False); Assert.That(p.AllowForcedDespawn, Is.False); }
    }
}
