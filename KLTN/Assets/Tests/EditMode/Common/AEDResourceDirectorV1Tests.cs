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

        [Test]
        public void RestrictedToolPlacementBacktracksToValidSolution()
        {
            var plan = AEDResourceDirectorV1.Evaluate(Request(strong: true));
            var points = new List<AEDResourceSpawnCandidateV1>();
            void Add(int zone, string id, int index, double distance, params int[] allowed)
            { points.Add(new AEDResourceSpawnCandidateV1 { PointId = $"z{zone}-{id}", Zone = zone, RoomId = index + 1, AllowedToolIds = allowed, NavMeshReachable = true, PathDistanceFromZoneEntry = distance, X = index * 10, Y = 0, Z = zone * 100 }); }
            Add(1, "A", 0, 10, AEDResourceToolIdsV1.FirstAid, AEDResourceToolIdsV1.NoiseMaker);
            Add(1, "B", 1, 20, AEDResourceToolIdsV1.FirstAid, AEDResourceToolIdsV1.Scanner);
            Add(1, "C", 2, 30, AEDResourceToolIdsV1.Scanner, AEDResourceToolIdsV1.NoiseMaker);
            Add(1, "D", 3, 40, AEDResourceToolIdsV1.DoorJammer);
            Add(1, "E", 4, 50, AEDResourceToolIdsV1.CoreStabilizer);
            for (int zone = 2; zone <= 3; zone++) { Add(zone, "scanner", 0, 30, AEDResourceToolIdsV1.Scanner); Add(zone, "firstaid", 1, 10, AEDResourceToolIdsV1.FirstAid); Add(zone, "noise", 2, 20, AEDResourceToolIdsV1.NoiseMaker); Add(zone, "door", 3, 40, AEDResourceToolIdsV1.DoorJammer); }
            Assert.That(AEDResourcePlacementV1.TryPlan(plan, points, 6, out var receipts), Is.True);
            Assert.That(receipts.Count, Is.EqualTo(13));
            Assert.That(receipts.Single(r => r.Zone == 1 && r.ToolId == AEDResourceToolIdsV1.FirstAid).PointId, Is.EqualTo("z1-A"));
            Assert.That(receipts.Single(r => r.Zone == 1 && r.ToolId == AEDResourceToolIdsV1.Scanner).PointId, Is.EqualTo("z1-B"));
            Assert.That(receipts.Single(r => r.Zone == 1 && r.ToolId == AEDResourceToolIdsV1.NoiseMaker).PointId, Is.EqualTo("z1-C"));
        }

        [Test]
        public void FailedPlacementReturnsNoPartialReceipts()
        {
            var plan = AEDResourceDirectorV1.Evaluate(Request(strong: true));
            var points = new[] { Point(1, 0), Point(1, 1), Point(1, 2) };
            Assert.That(AEDResourcePlacementV1.TryPlan(plan, points, 6, out var receipts), Is.False);
            Assert.That(receipts, Is.Empty);
        }

        [Test]
        public void SupplySnapshotSeparatesWorldFromLobby()
        {
            Guid match = Guid.NewGuid(); string owner = Guid.NewGuid().ToString("D");
            var items = new[]
            {
                new AEDResourceSupplyItemV1("world-core-1", AEDResourceToolIdsV1.CoreStabilizer, 1, AEDResourceSupplyOriginV1.WorldSpawn, AEDResourceLocationV1.World, null, 1, false, false, true, 10, "spawn-core-1", true),
                new AEDResourceSupplyItemV1("lobby-core-1", AEDResourceToolIdsV1.CoreStabilizer, 0, AEDResourceSupplyOriginV1.LobbyLoadout, AEDResourceLocationV1.Carried, owner, 1, true, true, false, 10, "loadout-core-1", true)
            };
            Assert.That(AEDResourceSupplySnapshotV1.TryCreate(match, 1, items, true, out var snapshot), Is.True);
            Assert.That(snapshot.SourcesHostVerified, Is.True);
            Assert.That(snapshot.CountReachableWorld(1, AEDResourceToolIdsV1.CoreStabilizer), Is.EqualTo(1));
            Assert.That(snapshot.Items.Count, Is.EqualTo(2));
            Assert.That(snapshot.ResearchOnly, Is.True);
            Assert.That(snapshot.CanApplyGameplay, Is.False);
            Assert.That(AEDResourceSupplySnapshotV1.TryCreate(match, 1, items.Concat(items), true, out _), Is.False);
        }

        [Test]
        public void BacktrackingUndoesPreferredFirstAidPlacement()
        {
            var plan = AEDResourceDirectorV1.Evaluate(Request(strong: true));
            var points = new List<AEDResourceSpawnCandidateV1>();
            void Add(int zone, string name, int index, double pathDistance, params int[] tools)
            { points.Add(new AEDResourceSpawnCandidateV1 { PointId = $"z{zone}-{name}", Zone = zone, RoomId = index + 1, AllowedToolIds = tools, NavMeshReachable = true, PathDistanceFromZoneEntry = pathDistance, X = index * 10, Y = 0, Z = zone * 100 }); }
            Add(1, "A", 0, 20, AEDResourceToolIdsV1.FirstAid, AEDResourceToolIdsV1.Scanner, AEDResourceToolIdsV1.NoiseMaker);
            Add(1, "B", 1, 30, AEDResourceToolIdsV1.FirstAid);
            Add(1, "C", 2, 40, AEDResourceToolIdsV1.Scanner, AEDResourceToolIdsV1.NoiseMaker);
            Add(1, "D", 3, 50, AEDResourceToolIdsV1.DoorJammer);
            Add(1, "E", 4, 60, AEDResourceToolIdsV1.CoreStabilizer);
            for (int zone = 2; zone <= 3; zone++) { Add(zone, "S", 0, 30, AEDResourceToolIdsV1.Scanner); Add(zone, "F", 1, 10, AEDResourceToolIdsV1.FirstAid); Add(zone, "N", 2, 20, AEDResourceToolIdsV1.NoiseMaker); Add(zone, "D", 3, 40, AEDResourceToolIdsV1.DoorJammer); }
            Assert.That(AEDResourcePlacementV1.TryPlan(plan, points, 6, out var result), Is.True);
            Assert.That(result.Single(x => x.Zone == 1 && x.ToolId == AEDResourceToolIdsV1.FirstAid).PointId, Is.EqualTo("z1-B"));
            Assert.That(result.Where(x => x.Zone == 1).Select(x => x.PointId).Distinct().Count(), Is.EqualTo(5));
        }
    }
}
