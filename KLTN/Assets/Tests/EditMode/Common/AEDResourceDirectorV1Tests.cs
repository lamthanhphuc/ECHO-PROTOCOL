using System;
using System.Collections.Generic;
using System.Linq;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;

namespace EchoProtocol.AI.Common.Tests
{
    public sealed class AEDResourceDirectorV1Tests
    {
        private static AEDResourceRequestV1 Request(bool strong = false, bool critical = false,
            bool verified = true, bool adaptive = true, bool coreInLobby = false) => new()
        {
            MatchId = Guid.NewGuid(), Difficulty = "Normal",
            ResolutionMode = adaptive ? "Adaptive" : "Fixed",
            FullRosterVerified = true, FullRosterObserved = true,
            TeamConfidenceComplete = strong, BackendMetricVerifierSupported = verified,
            PressureSourceVerified = true, PressureLevel = critical ? "Critical" : "Quiet",
            WeakestSkill = strong ? 0.85m : null, AnyPlayerStruggling = false,
            LobbyToolIds = coreInLobby ? new[] { AEDResourceToolIdsV1.CoreStabilizer } : Array.Empty<int>(),
            ComparisonContextKey = "NORMAL:CONFIG_V1", EvidenceFingerprint = "verified-source"
        };

        [Test] public void FixedModeKeepsLegacyBaseline()
        { var p = AEDResourceDirectorV1.Evaluate(Request(strong: true, adaptive: false)); Assert.That(p.Intent, Is.EqualTo(AEDResourceIntentV1.Hold)); Assert.That(p.Zone1.Count, Is.EqualTo(5)); Assert.That(p.Zone2.Count, Is.EqualTo(5)); Assert.That(p.Zone3.Count, Is.EqualTo(5)); Assert.That(p.CanApplyGameplay, Is.False); }
        [Test] public void ColdStartDoesNotRemoveCoreStabilizer()
        { var p = AEDResourceDirectorV1.Evaluate(Request()); Assert.That(p.Intent, Is.EqualTo(AEDResourceIntentV1.Hold)); Assert.That(new[] { p.Zone1, p.Zone2, p.Zone3 }.Sum(z => z.Count(id => id == AEDResourceToolIdsV1.CoreStabilizer)), Is.EqualTo(3)); }
        [Test] public void VerifiedStrongTeamCapsWorldCoreStabilizer()
        { var p = AEDResourceDirectorV1.Evaluate(Request(strong: true)); Assert.That(p.Intent, Is.EqualTo(AEDResourceIntentV1.ConstrainSupply)); Assert.That(new[] { p.Zone1, p.Zone2, p.Zone3 }.Sum(z => z.Count(id => id == AEDResourceToolIdsV1.CoreStabilizer)), Is.EqualTo(1)); Assert.That(p.PreferDistantScanner, Is.True); Assert.That(AEDResourceDirectorV1.ValidateFairness(p), Is.True); }
        [Test] public void LobbyCorePreventsAdditionalWorldCoreForStrongTeam()
        { var p = AEDResourceDirectorV1.Evaluate(Request(strong: true, coreInLobby: true)); Assert.That(new[] { p.Zone1, p.Zone2, p.Zone3 }.Sum(z => z.Count(id => id == AEDResourceToolIdsV1.CoreStabilizer)), Is.Zero); }
        [Test] public void UnverifiedProfileCannotConstrainResources()
        { var p = AEDResourceDirectorV1.Evaluate(Request(strong: true, verified: false)); Assert.That(p.Intent, Is.EqualTo(AEDResourceIntentV1.Hold)); Assert.That(p.Zone2, Does.Contain(AEDResourceToolIdsV1.CoreStabilizer)); }
        [Test] public void CriticalPressureProposesSupportWithoutRemovingFloor()
        { var p = AEDResourceDirectorV1.Evaluate(Request(critical: true)); Assert.That(p.Intent, Is.EqualTo(AEDResourceIntentV1.Relieve)); Assert.That(p.Zone1.Count(id => id == AEDResourceToolIdsV1.FirstAid), Is.EqualTo(2)); Assert.That(p.Zone2.Count(id => id == AEDResourceToolIdsV1.FirstAid), Is.EqualTo(2)); Assert.That(AEDResourceDirectorV1.ValidateFairness(p), Is.True); }

        private static AEDResourceSpawnCandidateV1 Point(int zone, int index, bool reachable = true, double distance = -1) => new()
        { PointId = $"z{zone}-p{index}", Zone = zone, RoomId = 1 + index % 3, AllowedToolIds = AEDResourceToolIdsV1.All, NavMeshReachable = reachable, PathDistanceFromZoneEntry = distance < 0 ? 10 + index * 5 : distance, X = index * 10, Y = 0, Z = zone * 100 };
        private static AEDResourceSpawnCandidateV1[] Points() => Enumerable.Range(1, 3).SelectMany(zone => Enumerable.Range(0, 8).Select(index => Point(zone, index))).ToArray();

        [Test] public void StrongTeamScannerUsesDistantReachablePoint()
        { var p = AEDResourceDirectorV1.Evaluate(Request(strong: true)); Assert.That(AEDResourcePlacementV1.TryPlan(p, Points(), 6, out var receipts), Is.True); Assert.That(receipts.Where(r => r.ToolId == AEDResourceToolIdsV1.Scanner).All(r => r.PathDistance >= 20), Is.True); Assert.That(receipts.Select(r => r.PointId).Distinct().Count(), Is.EqualTo(receipts.Count)); }
        [Test] public void UnreachableRequiredPointFailsWholePreview()
        { var p = AEDResourceDirectorV1.Evaluate(Request(strong: true)); var points = Points().Select(x => new AEDResourceSpawnCandidateV1 { PointId = x.PointId, Zone = x.Zone, RoomId = x.RoomId, AllowedToolIds = x.AllowedToolIds, NavMeshReachable = x.Zone != 2, PathDistanceFromZoneEntry = x.PathDistanceFromZoneEntry, X = x.X, Y = x.Y, Z = x.Z }); Assert.That(AEDResourcePlacementV1.TryPlan(p, points, 6, out var receipts), Is.False); Assert.That(receipts, Is.Empty); }
        [Test] public void ResearchProposalCannotCommitGameplay()
        { var p = AEDResourceDirectorV1.Evaluate(Request(strong: true)); Assert.That(p.ResearchOnly, Is.True); Assert.That(p.CanApplyGameplay, Is.False); }
    }
}
